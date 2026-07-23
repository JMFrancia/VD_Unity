using VoidDay.Balance.Schema;
using VoidDay.Balance.Unity;
using VoidDay.Core.Model;
using Xunit;

namespace VoidDay.Balance.Tests;

/// The two M01 guards from the spec's Testing Strategy. The reader is economy logic, so it falls
/// under CLAUDE.md's pure-C# economy-core testing exception.
public sealed class ReaderTests
{
    private static BalanceConfig ReadRealProject()
    {
        var root = FindProjectRoot();
        var reader = new AssetReader(new GuidIndex(root));
        return new EconomyReader(reader).Read();
    }

    private static string FindProjectRoot()
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

    // Parsed values match hand-checked asset content (spec: ReaderMatchesKnownAssets).
    [Fact]
    public void ReaderMatchesKnownAssets()
    {
        var c = ReadRealProject();

        // Global
        Assert.Equal(20, c.Global.GridCols);
        Assert.Equal(30, c.Global.GridRows);
        Assert.Equal(0.5f, c.Global.RefundPercent);
        Assert.Equal(30, c.Global.StartingStorageCapacity);

        // Gems block
        Assert.Equal(5, c.Gems.StartingGems);
        Assert.Equal(30f, c.Gems.SecondsPerGem);
        Assert.Equal(1, c.Gems.MinGemCost);

        // XP — perStationBuilt is absent from the asset; the SO initializer (5) is what the game sees.
        Assert.Equal(2, c.Xp.PerJobCollected);
        Assert.Equal(5, c.Xp.PerStationBuilt);

        // Order config — all ten fields.
        Assert.Equal(3, c.Orders.SlotCount);
        Assert.Equal(60f, c.Orders.RefillSeconds);
        Assert.Equal(1, c.Orders.MinRequestKinds);
        Assert.Equal(2, c.Orders.MaxRequestKinds);
        Assert.Equal(3f, c.Orders.MaxQuantityAtLevel1);
        Assert.Equal(1f, c.Orders.MaxQuantityPerLevel);
        Assert.Equal(1f, c.Orders.TierWeightBase);
        Assert.Equal(0.25f, c.Orders.TierWeightPerLevel);
        Assert.Equal(12f, c.Orders.CashMultiplier);
        Assert.Equal(1.5f, c.Orders.XpMultiplier);

        // Station Field
        var field = c.Stations.Single(s => s.StationType == "field");
        Assert.True(field.Buildable);
        Assert.Equal(50, field.BuildCost);
        Assert.Equal(2, field.Cap);
        Assert.Equal(1, field.UnlockLevel);
        Assert.Equal(3, field.QueueDepth);
        // buildSeconds is absent from every station asset; Unity applies the SO initializer, 15.
        Assert.All(c.Stations, s => Assert.Equal(15f, s.BuildSeconds));

        // Recipe field.wheatGrow
        var wheat = c.Recipes.Single(r => r.Id == "field.wheatGrow");
        Assert.Equal("field", wheat.StationType);
        Assert.Equal(5f, wheat.Duration);
        Assert.Equal("wheat", wheat.Inputs.Single().Resource);
        Assert.Equal(2, wheat.Outputs.Single().Amount);

        // Upgrade silo.cap — tier costs and effect amounts, effect type as a name not an int.
        var siloCap = c.Upgrades.Single(u => u.Id == "silo.cap");
        Assert.Equal(new[] { 120, 300, 700 }, siloCap.Tiers.Select(t => t.Cost).ToArray());
        Assert.All(siloCap.Tiers, t => Assert.Equal("StorageCap", t.Effects.Single().Type));
        Assert.All(siloCap.Tiers, t => Assert.Equal(25f, t.Effects.Single().Amount));

        // Levels — 20 of them, and level 3 pays 2 gems (not $150), per the gem baseline shift.
        Assert.Equal(20, c.Levels.Count);
        Assert.Equal(0, c.Levels[0].XpThreshold);
        Assert.Equal(50, c.Levels[2].XpThreshold);
        var level3Grant = c.Levels[2].Grants.Single();
        Assert.Equal("Gems", level3Grant.Kind);
        Assert.Equal(2, level3Grant.Amount);
        Assert.Null(level3Grant.TargetStation);

        // Starting stations — scanned from the scene, not hardcoded.
        Assert.Equal(1, c.Global.StartingStations.Single(s => s.StationType == "field").Count);
        Assert.Equal(1, c.Global.StartingStations.Single(s => s.StationType == "silo").Count);
        Assert.Equal(1, c.Global.StartingStations.Single(s => s.StationType == "orderBoard").Count);
    }

    // int ↔ name round-trips for every value of every Core enum the reader maps (spec: EnumMappingIsSymmetric).
    [Fact]
    public void EnumMappingIsSymmetric()
    {
        AssertSymmetric<EffectType>();
        AssertSymmetric<EffectOp>();
        AssertSymmetric<TriggerType>();
        AssertSymmetric<ConditionType>();
        AssertSymmetric<LevelEntryKind>();
    }

    private static void AssertSymmetric<T>() where T : struct, Enum
    {
        foreach (var value in Enum.GetValues<T>())
        {
            var raw = Convert.ToInt32(value);
            var name = value.ToString();

            // A name, never a bare int rendered as a string.
            Assert.False(int.TryParse(name, out _), $"{typeof(T).Name} value {raw} stringified to a number '{name}'");

            // int → name → int and int → enum round-trip.
            Assert.Equal(value, Enum.Parse<T>(name));
            Assert.Equal(value, (T)Enum.ToObject(typeof(T), raw));
            Assert.True(Enum.IsDefined(typeof(T), raw));
        }
    }
}
