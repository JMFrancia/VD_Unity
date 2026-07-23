using Newtonsoft.Json;
using VoidDay.Balance.Schema;
using VoidDay.Balance.Sim;
using VoidDay.Core.Model;
using Xunit;

namespace VoidDay.Balance.Tests;

/// The M03 sim guards (spec: Testing Strategy). The simulator is economy logic driving the real Core, so it
/// falls under CLAUDE.md's pure-C# economy-core testing exception.
public sealed class SimTests
{
    // ---- Determinism: same (config, profile, seed) ⇒ identical result. Everything else is meaningless without it.
    [Fact]
    public void SimIsDeterministic()
    {
        var config = Baseline();
        var a = new SimRunner(config, SimProfile.Typical(), 1).Run();
        var b = new SimRunner(config, SimProfile.Typical(), 1).Run();
        Assert.Equal(JsonConvert.SerializeObject(a), JsonConvert.SerializeObject(b));
    }

    // ---- The sweep is deterministic despite Parallel.For: same config ⇒ byte-identical aggregate on re-run.
    // This is the M06 load-bearing invariant — an A/B delta is only meaningful if the sweep itself is stable.
    [Fact]
    public void SweepIsDeterministic()
    {
        var config = Baseline();
        var a = SimSweep.Run(config, SimProfile.Typical(), 12, true);
        var b = SimSweep.Run(config, SimProfile.Typical(), 12, true);
        Assert.Equal(JsonConvert.SerializeObject(a.Aggregate), JsonConvert.SerializeObject(b.Aggregate));
    }

    // ---- The p10–p90 band is non-degenerate: with 30 seeds, at least one level's duration band has width.
    // A degenerate band would mean the sweep ran the same stream 30 times (spec M06 DoD).
    [Fact]
    public void SweepBandIsNonDegenerate()
    {
        var agg = SimSweep.Run(Baseline(), SimProfile.Typical(), 30, true).Aggregate;
        Assert.Contains(agg.Levels, l => l.Duration.P90 - l.Duration.P10 > 1.0);
    }

    // ---- Opening a seed reproduces the single-seed run exactly: the retained SeedResult for seed N equals
    // a fresh SimRunner(seed N) — proving the drill-in shows real CLI output, not a re-aggregation.
    [Fact]
    public void SweepSeedMatchesSingleRun()
    {
        var config = Baseline();
        var sweep = SimSweep.Run(config, SimProfile.Typical(), 5, true);
        var seed3 = sweep.SeedResults.First(r => r.Seed == 3);
        var direct = new SimRunner(config, SimProfile.Typical(), 3, true).Run();
        Assert.Equal(JsonConvert.SerializeObject(direct), JsonConvert.SerializeObject(seed3));
    }

    // ---- Lower optimality ⇒ never-faster runs (the dial's four mechanisms only ever add delay).
    [Fact]
    public void OptimalityMonotonicity()
    {
        var config = Baseline();
        double t10 = new SimRunner(config, Profile(1.0f), 1).Run().TotalSeconds;
        double t065 = new SimRunner(config, Profile(0.65f), 1).Run().TotalSeconds;
        double t03 = new SimRunner(config, Profile(0.3f), 1).Run().TotalSeconds;
        Assert.True(t10 <= t065, $"optimality 1.0 ({t10}) should not exceed 0.65 ({t065})");
        Assert.True(t065 <= t03, $"optimality 0.65 ({t065}) should not exceed 0.3 ({t03})");
    }

    // ---- A bad recipe graph errors instead of hanging. Two mutually recursive recipes name the cycle.
    [Fact]
    public void RecipeChainTerminatesOnCycle()
    {
        var config = CyclicConfig();
        var harness = new CoreHarness(config, 1);
        harness.EmitStartingState();
        harness.Orders.Tick(0); // the single sellable producible is itemA → the order demands it
        var chain = new RecipeChain(harness, config);
        Assert.Throws<RecipeCycleException>(() => chain.Next());
    }

    // ---- Pressure is GROSS of gem relief; a skip records GemRelief and never reduces Pressure.
    [Fact]
    public void PressureIsGrossOfGemRelief()
    {
        var ledger = new PressureLedger();
        ledger.Accrue(PressureLedger.Throughput, 100);       // actually waited
        ledger.AccrueGemRelief(PressureLedger.Throughput, 50); // skipped — still counts gross

        var pressure = ledger.SnapshotPressure();
        var relief = ledger.SnapshotRelief();

        Assert.Equal(150, pressure[PressureLedger.Throughput], 3); // gross = waited + relieved
        Assert.Equal(50, relief[PressureLedger.Throughput], 3);
        // Net (what the caller derives) is the actually-waited seconds — never stored.
        Assert.Equal(100, pressure[PressureLedger.Throughput] - relief[PressureLedger.Throughput], 3);
    }

    // ---- The sim prices a skip through the REAL TimeSkip.CostFor, never a copy of the formula.
    [Fact]
    public void SkipCostMatchesCoreRule()
    {
        var config = Baseline();
        var harness = new CoreHarness(config, 1);
        harness.EmitStartingState();
        harness.Pool.Add("corn", 5); // afford the cornGrow input (corn is queueable at level 1; wheat now unlocks at 2)
        harness.Jobs.QueueJob("field@0", "field.cornGrow", 0); // starts a running head at now=0

        var timer = TimerRef.Job("field@0");
        Assert.True(harness.TimeSkip.CanSkip(timer, 0));

        float remaining = harness.Jobs.HeadSecondsRemaining("field@0", 0);
        int expected = Math.Max(config.Gems.MinGemCost,
            (int)Math.Ceiling(remaining / (double)config.Gems.SecondsPerGem));
        Assert.Equal(expected, harness.TimeSkip.CostFor(timer, 0));
    }

    // ---- startingGems: 0 + no gem grants ⇒ results identical whether the gem code path runs or not.
    [Fact]
    public void ZeroGemsMatchesPreGemBaseline()
    {
        var config = Baseline();
        config.Gems.StartingGems = 0;
        foreach (var level in config.Levels)
            level.Grants.RemoveAll(g => g.Kind == "Gems");

        var withGemCode = new SimRunner(config, SimProfile.Typical(), 1, gemsEnabled: true).Run();
        var withoutGemCode = new SimRunner(config, SimProfile.Typical(), 1, gemsEnabled: false).Run();
        Assert.Equal(JsonConvert.SerializeObject(withoutGemCode), JsonConvert.SerializeObject(withGemCode));
    }

    // ---- A genuinely unwinnable config stops on the stall guard with a named reason — it never hangs.
    // (Note: baseline with buildCost 999999 does NOT stall — the preplaced field's corn→sell loop is
    //  self-sustaining, which is a real economy finding, not a stall. Here corn needs an unobtainable input.)
    [Fact]
    public void SimStallGuardFires()
    {
        var c = OneFieldConfig(cap: 1, queueDepth: 3);
        c.Name = "stall";
        // corn is producible (a recipe output) so orders demand it, but its recipe needs "gold" that nothing
        // makes — the player can never fulfil an order, never earns XP, and the stall guard must fire.
        c.Recipes = new()
        {
            new() { Id = "field.cornFromGold", StationType = "field",
                    Inputs = new() { new() { Resource = "gold", Amount = 1 } },
                    Outputs = new() { new() { Resource = "corn", Amount = 1 } }, Duration = 5 }
        };
        c.Stations[0].RecipeIds = new() { "field.cornFromGold" };

        var result = new SimRunner(c, SimProfile.Typical(), 1).Run();
        Assert.Equal(StopReason.Stalled, result.Stop);
        Assert.True(result.TotalSeconds < 40 * 3600, "stall must fire well before the max-hours cap");
    }

    // ---- Each category accrues only under its stated condition; the Yield-vs-Capacity split is the subtle one.
    [Fact]
    public void PressureLedgerAccrual_OnlyNamedCategory()
    {
        var ledger = new PressureLedger();
        ledger.Accrue(PressureLedger.Storage, 10);
        var snap = ledger.SnapshotPressure();
        Assert.Equal(10, snap[PressureLedger.Storage], 3);
        Assert.False(snap.ContainsKey(PressureLedger.Throughput)); // nothing bleeds into a category never accrued
    }

    [Fact]
    public void PressureLedgerAccrual_YieldWhenSaturated_CapacityWhenExpandable()
    {
        // cap 1: the one field cannot be joined by another and its queue is full ⇒ Yield (more per job).
        Assert.Equal(PressureLedger.Yield("field"), BlockedCategory(fieldCap: 1));
        // cap 2: another field can still be added ⇒ Capacity (more jobs).
        Assert.Equal(PressureLedger.Capacity("field"), BlockedCategory(fieldCap: 2));
    }

    /// Drive a one-field harness into "the order wants corn, but the field's only queue slot is full", then
    /// read what the chain calls the block. The field's recipe has no inputs, so affordability is never the
    /// blocker — only the full queue is, which is exactly the Capacity/Yield fork.
    static string BlockedCategory(int fieldCap)
    {
        var config = OneFieldConfig(fieldCap, queueDepth: 1);
        var harness = new CoreHarness(config, 1);
        harness.EmitStartingState();
        harness.Orders.Tick(0);                                   // an order now demands corn
        harness.Jobs.QueueJob("field@0", "field.fallowCorn", 0);  // fill the field's single slot

        var chain = new RecipeChain(harness, config);
        var intent = chain.Next();
        Assert.Equal(RecipeChain.Kind.Want, intent.Kind);
        return intent.Category;
    }

    // ---- Helpers ----

    static SimProfile Profile(float optimality)
    {
        var p = SimProfile.Typical();
        p.Optimality = optimality;
        return p;
    }

    static BalanceConfig Baseline()
    {
        var path = Path.Combine(FindProjectRoot(), "tools", "VoidDay.Balance", "versions", "baseline.json");
        return JsonConvert.DeserializeObject<BalanceConfig>(File.ReadAllText(path))!;
    }

    /// A minimal single-field economy: one field producing corn from nothing (fallow), corn sellable, one
    /// order slot. cap/queueDepth are the fork under test.
    static BalanceConfig OneFieldConfig(int cap, int queueDepth)
    {
        var c = new BalanceConfig { Name = "one-field" };
        c.Global = new GlobalConfig
        {
            GridCols = 10, GridRows = 10, CellSize = 1, RefundPercent = 0.5f, StartingStorageCapacity = 100,
            StartingResources = new(),
            StartingStations = new() { new() { StationType = "field", Count = 1 }, new() { StationType = "orderBoard", Count = 1 } }
        };
        c.Xp = new XpConfig { PerJobCollected = 2, PerStationBuilt = 5 };
        c.Gems = new GemConfig { StartingGems = 0, SecondsPerGem = 30, MinGemCost = 1 };
        c.Orders = new OrderConfig
        {
            SlotCount = 1, RefillSeconds = 60, MinRequestKinds = 1, MaxRequestKinds = 1,
            MaxQuantityAtLevel1 = 3, MaxQuantityPerLevel = 0, TierWeightBase = 1, TierWeightPerLevel = 0,
            CashMultiplier = 12, XpMultiplier = 1.5f
        };
        c.Resources = new() { new() { Id = "corn", DisplayName = "Corn", BaseValue = 3, Sellable = true, Tier = 1 } };
        c.Recipes = new()
        {
            new() { Id = "field.fallowCorn", StationType = "field", Inputs = new(),
                    Outputs = new() { new() { Resource = "corn", Amount = 1 } }, Duration = 30 }
        };
        c.Stations = new()
        {
            new() { StationType = "field", DisplayName = "Field", Buildable = true, BuildCost = 50, Cap = cap,
                    UnlockLevel = 1, QueueDepth = queueDepth, Width = 1, Height = 1, BuildSeconds = 15,
                    RecipeIds = new() { "field.fallowCorn" }, UpgradeIds = new() },
            new() { StationType = "orderBoard", DisplayName = "Order Board", Buildable = false, BuildCost = 0,
                    Cap = 1, UnlockLevel = 1, QueueDepth = 3, Width = 1, Height = 1, BuildSeconds = 0,
                    RecipeIds = new(), UpgradeIds = new() }
        };
        c.Upgrades = new();
        c.Levels = new() { new() { XpThreshold = 0, Grants = new() }, new() { XpThreshold = 999999, Grants = new() } };
        return c;
    }

    /// itemA ⇄ itemB with no base producer — the pathological cycle. itemA is the only sellable producible, so
    /// order generation demands it and the chain walks straight into the loop.
    static BalanceConfig CyclicConfig()
    {
        var c = OneFieldConfig(cap: 2, queueDepth: 3);
        c.Name = "cyclic";
        c.Resources = new()
        {
            new() { Id = "itemA", DisplayName = "ItemA", BaseValue = 5, Sellable = true, Tier = 1 },
            new() { Id = "itemB", DisplayName = "ItemB", BaseValue = 5, Sellable = false, Tier = 1 }
        };
        c.Recipes = new()
        {
            new() { Id = "field.aFromB", StationType = "field",
                    Inputs = new() { new() { Resource = "itemB", Amount = 1 } },
                    Outputs = new() { new() { Resource = "itemA", Amount = 1 } }, Duration = 5 },
            new() { Id = "field.bFromA", StationType = "field",
                    Inputs = new() { new() { Resource = "itemA", Amount = 1 } },
                    Outputs = new() { new() { Resource = "itemB", Amount = 1 } }, Duration = 5 }
        };
        c.Stations[0].RecipeIds = new() { "field.aFromB", "field.bFromA" };
        return c;
    }

    static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets"))
                && File.Exists(Path.Combine(dir.FullName, ".gitignore")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate the Unity project root above the test binary.");
    }
}
