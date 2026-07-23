using Newtonsoft.Json;
using VoidDay.Balance.Agent;
using VoidDay.Balance.Schema;
using Xunit;

namespace VoidDay.Balance.Tests;

/// The M05 agent-primitive guards: the loss is monotonic (so an agent can descend it), and the patch guardrails
/// are enforced by the tool, not advisory (bounds.json + the profile/* rejection).
public sealed class AgentTests
{
    // ---- Halving every recipe duration must lower the loss on a duration-capped goal (DoD).
    [Fact]
    public void GoalLossIsMonotonic()
    {
        var goal = new Goal
        {
            Name = "fast-levels",
            // Cap tight enough that baseline violates; summed over the range the loss falls when durations
            // halve, even though one level can rise on its own (the order stream shifts — an M03 reality).
            Targets = { new GoalTarget { Metric = "level.durationMinutes", Levels = "1-5", Max = 1, Weight = 1 } }
        };

        var baseline = Baseline();
        var faster = Baseline();
        foreach (var r in faster.Recipes) r.Duration /= 2f;

        double lossBaseline = GoalEvaluator.Evaluate(Run(baseline), goal).Loss;
        double lossFaster = GoalEvaluator.Evaluate(Run(faster), goal).Loss;

        Assert.True(lossFaster < lossBaseline,
            $"halving durations should lower a duration-capped loss: faster {lossFaster} vs baseline {lossBaseline}");
    }

    // ---- A value outside a declared bound is rejected, naming the bound (DoD).
    [Fact]
    public void PatchRejectsOutOfBounds()
    {
        var ops = new List<Patch.PatchOp> { new() { Op = "set", Path = "recipes/field.wheatGrow/duration", Value = 9999 } };
        var ex = Assert.Throws<Patch.PatchRejectedException>(() => Patch.Apply(Baseline(), ops, LoadBounds()));
        Assert.Contains("out of bounds", ex.Message);
        Assert.Contains("recipes/field.wheatGrow/duration", ex.Message);
    }

    // ---- The whole profile/* namespace is read-only (DoD: profile/optimality rejected, naming the rule).
    [Fact]
    public void PatchRejectsProfilePaths()
    {
        var ops = new List<Patch.PatchOp> { new() { Op = "set", Path = "profile/optimality", Value = 1.0 } };
        var ex = Assert.Throws<Patch.PatchRejectedException>(() => Patch.Apply(Baseline(), ops, LoadBounds()));
        Assert.Contains("read-only", ex.Message);
        Assert.Contains("profile", ex.Message);
    }

    // ---- gemPolicy / gemReserve / minSkipSeconds are new instances of the same exploit — rejected by namespace,
    //      not by a hand-maintained field list.
    [Theory]
    [InlineData("profile/gemPolicy")]
    [InlineData("profile/gemReserve")]
    [InlineData("profile/minSkipSeconds")]
    public void PatchRejectsGemPolicyPaths(string path)
    {
        var ops = new List<Patch.PatchOp> { new() { Op = "set", Path = path, Value = 0 } };
        var ex = Assert.Throws<Patch.PatchRejectedException>(() => Patch.Apply(Baseline(), ops, LoadBounds()));
        Assert.Contains("read-only", ex.Message);
    }

    // ---- A within-bounds game knob patches cleanly, config→config, without touching the input.
    [Fact]
    public void PatchAppliesInBoundsToClone()
    {
        var baseline = Baseline();
        var ops = new List<Patch.PatchOp> { new() { Op = "set", Path = "stations/field/buildCost", Value = 40 } };
        var patched = Patch.Apply(baseline, ops, LoadBounds());
        Assert.Equal(40, patched.Stations.First(s => s.StationType == "field").BuildCost);
        Assert.NotEqual(40, baseline.Stations.First(s => s.StationType == "field").BuildCost); // input untouched
    }

    // ---- ★ Where gem relief is large, gem knobs join suggest's shortlist, flagged as a different kind of fix.
    //      Tested directly on a synthesised SimResult so the assertion doesn't depend on the player choosing to
    //      spend gems — the branch reads the GemRelief the sim already records per level.
    [Fact]
    public void SuggestFlagsGemReliefWhenLarge()
    {
        var config = StorageConfig();

        // 50% of the Storage bottleneck is being bought away by gems (0.5 ≥ the 0.15 threshold).
        var large = ResultWith(pressure: 1000, relief: 500);
        var largeReport = Suggest.Analyze(large, config);
        Assert.Equal("Storage", largeReport.DominantFamily);
        Assert.Contains(largeReport.Structural, k => k.Path == "global.startingStorageCapacity");
        Assert.NotEmpty(largeReport.Relief);
        Assert.All(largeReport.Relief, k => Assert.Equal("relief", k.Kind));

        // Only 5% bought away — below threshold, so no gem knobs pollute the structural shortlist.
        var small = ResultWith(pressure: 1000, relief: 50);
        Assert.Empty(Suggest.Analyze(small, config).Relief);
    }

    static SimResult ResultWith(double pressure, double relief)
    {
        var r = new SimResult { LevelReached = 2 };
        r.Levels.Add(new LevelReport
        {
            Level = 1,
            Pressure = new() { ["Storage"] = pressure },
            GemRelief = new() { ["Storage"] = relief }
        });
        return r;
    }

    static BalanceConfig StorageConfig()
    {
        var c = new BalanceConfig { Name = "storage" };
        c.Upgrades.Add(new UpgradeConfig
        {
            Id = "silo.cap",
            Tiers = { new UpgradeTierConfig { Cost = 100, Effects = { new EffectConfig { Type = "StorageCap", Amount = 25 } } } }
        });
        return c;
    }

    // ---- Helpers ----

    static SimResult Run(BalanceConfig c) => new VoidDay.Balance.Sim.SimRunner(c, SimProfile.Typical(), 1).Run();

    static Bounds LoadBounds() => Bounds.Load(FindProjectRoot());

    static BalanceConfig Baseline()
    {
        var path = Path.Combine(FindProjectRoot(), "tools", "VoidDay.Balance", "versions", "baseline.json");
        return JsonConvert.DeserializeObject<BalanceConfig>(File.ReadAllText(path))!;
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
