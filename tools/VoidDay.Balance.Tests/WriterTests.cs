using Newtonsoft.Json;
using VoidDay.Balance.Schema;
using VoidDay.Balance.Unity;
using Xunit;

namespace VoidDay.Balance.Tests;

/// M02 guards for the writer. Every test here uses Plan() only — Plan validates and diffs but writes
/// nothing, so the tests never touch a real asset. Applying to disk is exercised by the CLI How-to-Test
/// steps, which revert with `git checkout`; a unit test must not mutate the tracked project.
public sealed class WriterTests
{
    private static (string root, EconomyReader reader, BalanceConfig current) ReadReal()
    {
        var root = FindProjectRoot();
        var reader = new EconomyReader(new AssetReader(new GuidIndex(root)));
        return (root, reader, reader.Read());
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

    // A JSON round-trip of the just-read economy plans zero changes — the "second write reports zero
    // changes / byte-identical" invariant from the spec, proven without touching disk.
    [Fact]
    public void RoundTripPlansNoChanges()
    {
        var (root, reader, current) = ReadReal();
        var json = JsonConvert.SerializeObject(current);
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(json)!;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.True(plan.IsEmpty, "an unedited round-trip must plan zero changes");
    }

    // A single scalar edit is planned as exactly one change with the right old/new, and nothing else.
    [Fact]
    public void ScalarEditIsOneChange()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Recipes.Single(r => r.Id == "field.wheatGrow").Duration = 2.5f;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.Empty(plan.RecipeInsertions);
        var change = Assert.Single(plan.Scalars);
        Assert.EndsWith("Recipe_Field_WheatGrow.asset", change.AssetPath);
        Assert.Equal("duration", change.Field);
        Assert.Equal("5", change.Old);
        Assert.Equal("2.5", change.New);
    }

    // A new recipe is planned as one structural insertion, not a scalar edit.
    [Fact]
    public void NewRecipeIsAnInsertion()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Recipes.Add(new RecipeConfig
        {
            Id = "field.unitTestGrow",
            StationType = "field",
            Inputs = new() { new ResourceQuantity { Resource = "wheat", Amount = 1 } },
            Outputs = new() { new ResourceQuantity { Resource = "wheat", Amount = 3 } },
            Duration = 4f
        });

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.Empty(plan.Scalars);
        Assert.Equal("field.unitTestGrow", Assert.Single(plan.RecipeInsertions).Recipe.Id);
    }

    [Fact]
    public void SchemaVersionMismatchRefuses()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.SchemaVersion = 999;

        var writer = new AssetWriter(root, reader, current);
        var ex = Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
        Assert.Contains("schemaVersion", ex.Message);
    }

    [Fact]
    public void BogusResourceIdRefuses()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Resources[0].Id = "NONEXISTENT_RESOURCE";

        var writer = new AssetWriter(root, reader, current);
        var ex = Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
        Assert.Contains("NONEXISTENT_RESOURCE", ex.Message);
    }

    // Feature B: a level xpThreshold edit is now a planned, positional level edit — not a refusal, not a scalar.
    [Fact]
    public void LevelXpThresholdEditIsOneLevelEdit()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        int oldValue = incoming.Levels[2].XpThreshold;
        incoming.Levels[2].XpThreshold = oldValue + 1;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.Empty(plan.Scalars);
        var edit = Assert.Single(plan.LevelEdits);
        Assert.Equal(2, edit.LevelIndex);
        Assert.Null(edit.GrantIndex);
        Assert.Equal(oldValue.ToString(), edit.Old);
        Assert.Equal((oldValue + 1).ToString(), edit.New);
    }

    // A grant amount edit is a positional level edit that carries the grant index.
    [Fact]
    public void GrantAmountEditCarriesGrantIndex()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        // Level 2 (index 1) has grants in the baseline; bump the first grant's amount.
        int oldAmount = incoming.Levels[1].Grants[0].Amount;
        incoming.Levels[1].Grants[0].Amount = oldAmount + 5;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        var edit = Assert.Single(plan.LevelEdits);
        Assert.Equal(1, edit.LevelIndex);
        Assert.Equal(0, edit.GrantIndex);
    }

    // A grant's kind change is now surgical — the level's grant block is regenerated, not refused. Flip a
    // non-reward grant (QueueDepth <-> OrderSlots) so the retarget stays boot-valid (no second reward added).
    [Fact]
    public void GrantKindChangeIsAGrantRewrite()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var (lvl, li) = incoming.Levels.Select((l, i) => (l, i))
            .First(t => t.l.Grants.Any(g => g.Kind is "QueueDepth" or "OrderSlots"));
        var g = lvl.Grants.First(x => x.Kind is "QueueDepth" or "OrderSlots");
        g.Kind = g.Kind == "QueueDepth" ? "OrderSlots" : "QueueDepth";

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.Empty(plan.LevelEdits);
        var rewrite = Assert.Single(plan.GrantRewrites);
        Assert.Equal(li, rewrite.LevelIndex);
    }

    // Appending a grant to a level is a structural change — planned as a grant-block rewrite for that level.
    // A non-reward StationCap grant keeps the level boot-valid regardless of any existing reward grant.
    [Fact]
    public void GrantAppendIsAGrantRewrite()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Levels[1].Grants.Add(new LevelGrantConfig { Kind = "StationCap", TargetStation = "field", Amount = 1 });

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        var rewrite = Assert.Single(plan.GrantRewrites);
        Assert.Equal(1, rewrite.LevelIndex);
        // Block regenerates every grant: grants: header + 3 lines each.
        Assert.Equal(1 + 3 * incoming.Levels[1].Grants.Count, rewrite.Block.Count);
    }

    // A grant with a StationCap target the config knows resolves to a station SO reference, not a refusal.
    [Fact]
    public void GrantWithStationTargetResolves()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Levels[1].Grants.Add(new LevelGrantConfig { Kind = "StationCap", TargetStation = "field", Amount = 1 });

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        var rewrite = Assert.Single(plan.GrantRewrites);
        Assert.Contains(rewrite.Block, l => l.Contains("targetStation: {fileID: 11400000"));
    }

    // A grant targeting a station the config does not know is refused loud rather than written as a guess.
    [Fact]
    public void GrantWithUnknownStationRefuses()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Levels[1].Grants.Add(new LevelGrantConfig { Kind = "StationCap", TargetStation = "NOPE", Amount = 1 });

        var writer = new AssetWriter(root, reader, current);
        var ex = Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
        Assert.Contains("NOPE", ex.Message);
    }

    // Canary: the live economy the game boots must pass the config-space boot-rule mirror. If this fails, the
    // mirror in BootRules has drifted stricter than the real BootValidator.
    [Fact]
    public void RoundTripPassesBootRules()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.True(plan.IsEmpty); // reaching here means BootRules.Validate did not throw on a bootable config
    }

    // Boot rule: a second reward grant (Money + Gems on one level) is refused in config-space — the exact
    // violation that reached Unity playmode before this guard existed.
    [Fact]
    public void TwoRewardGrantsOnALevelRefused()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        // Find a level that already carries one reward grant, then add a second of the other kind.
        var lvl = incoming.Levels.First(l => l.Grants.Any(g => g.Kind is "Money" or "Gems"));
        var otherKind = lvl.Grants.Any(g => g.Kind == "Money") ? "Gems" : "Money";
        lvl.Grants.Add(new LevelGrantConfig { Kind = otherKind, TargetStation = null, Amount = 5 });

        var writer = new AssetWriter(root, reader, current);
        var ex = Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
        Assert.Contains("reward grant", ex.Message);
    }

    // Boot rule: a recipe unlocked in the gap between the starting level and its station's unlockLevel is
    // refused — the popup would announce a recipe for a building the player cannot yet make.
    [Fact]
    public void RecipeUnlockInStationGapRefused()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        // Pick a station that unlocks at level >= 3, and a recipe on it; unlock the recipe at level 2 —
        // above the starting level (1, always allowed) but below the station's unlock → the forbidden gap.
        var station = incoming.Stations.First(s => s.UnlockLevel >= 3 && s.RecipeIds.Count > 0);
        var recipe = incoming.Recipes.First(r => r.Id == station.RecipeIds[0]);
        recipe.UnlockLevel = 2;

        var writer = new AssetWriter(root, reader, current);
        var ex = Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
        Assert.Contains("unlockLevel", ex.Message);
    }

    // Boot rule: a zeroed reward amount is refused (the popup would show a +0 reward).
    [Fact]
    public void ZeroGrantAmountRefused()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Levels.First(l => l.Grants.Count > 0).Grants[0].Amount = 0;

        var writer = new AssetWriter(root, reader, current);
        Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
    }

    // An upgrade tier cost edit is a positional upgrade edit (EffectIndex null), not a refusal.
    [Fact]
    public void UpgradeTierCostEditIsAnUpgradeEdit()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var upgrade = incoming.Upgrades.First(u => u.Tiers.Count > 0);
        upgrade.Tiers[0].Cost += 25;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        var edit = Assert.Single(plan.UpgradeEdits);
        Assert.Equal(0, edit.TierIndex);
        Assert.Null(edit.EffectIndex);
    }

    // An upgrade effect value.amount edit is a positional upgrade edit carrying the effect index.
    [Fact]
    public void UpgradeEffectAmountEditCarriesEffectIndex()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var upgrade = incoming.Upgrades.First(u => u.Tiers.Any(t => t.Effects.Count > 0));
        var tier = upgrade.Tiers.First(t => t.Effects.Count > 0);
        tier.Effects[0].Amount += 5f;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        var edit = Assert.Single(plan.UpgradeEdits);
        Assert.Equal(0, edit.EffectIndex);
    }

    // Changing an effect field other than amount has no surgical path — refused loud.
    [Fact]
    public void UpgradeEffectStructuralChangeRefuses()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var upgrade = incoming.Upgrades.First(u => u.Tiers.Any(t => t.Effects.Count > 0));
        var tier = upgrade.Tiers.First(t => t.Effects.Count > 0);
        tier.Effects[0].Op = tier.Effects[0].Op == "Flat" ? "Pct" : "Flat";

        var writer = new AssetWriter(root, reader, current);
        Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
    }

    // Feature B: a recipe unlockLevel edit is a single scalar change on that recipe's asset.
    [Fact]
    public void RecipeUnlockLevelEditIsOneScalar()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var recipe = incoming.Recipes.First();
        recipe.UnlockLevel += 1;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        var change = Assert.Single(plan.Scalars);
        Assert.Equal("unlockLevel", change.Field);
    }

    // Feature B: an upgrade unlockLevel edit is a single scalar change on that upgrade's asset.
    [Fact]
    public void UpgradeUnlockLevelEditIsOneScalar()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var upgrade = incoming.Upgrades.First();
        upgrade.UnlockLevel += 1;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        var change = Assert.Single(plan.Scalars);
        Assert.Equal("unlockLevel", change.Field);
    }

    // ---- M05: quest authoring write-back ----

    // A new quest is planned as one insertion plus a regeneration of the GameConfig.quests reference block —
    // never a scalar or a recipe insertion.
    [Fact]
    public void NewQuestIsAnInsertionPlusBlockRewrite()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Quests.Add(new QuestConfig
        {
            Id = "quest.unitTest",
            Goal = new QuestGoalConfig { Kind = "EarnMoney", Amount = 250 },
            Reward = new QuestRewardConfig { Xp = 15 }
        });

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.Empty(plan.QuestEdits);
        Assert.Empty(plan.QuestDeletions);
        Assert.Equal("quest.unitTest", Assert.Single(plan.QuestInsertions).Quest.Id);
        Assert.NotNull(plan.QuestBlockRewrite);
        // header + one 2-space item per surviving+new quest.
        Assert.Equal(1 + incoming.Quests.Count, plan.QuestBlockRewrite!.Count);
    }

    // Deleting a quest with no dependents is a deletion plus a block rewrite; no assets are touched at plan time.
    [Fact]
    public void DeletingUnreferencedQuestIsADeletionPlusBlockRewrite()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        // quest.chain depends on quest.starter, so delete the LEAF (chain) — nothing references it.
        incoming.Quests.RemoveAll(q => q.Id == "quest.chain");

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.Equal("quest.chain", Assert.Single(plan.QuestDeletions).Id);
        Assert.NotNull(plan.QuestBlockRewrite);
        Assert.Equal(1 + incoming.Quests.Count, plan.QuestBlockRewrite!.Count);
    }

    // Deleting a quest another quest's QuestCompleted condition depends on is refused in config-space — it would
    // fail BootValidator at play. (quest.chain requires quest.starter, so removing the starter is unsafe.)
    [Fact]
    public void DeletingAReferencedQuestRefuses()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Quests.RemoveAll(q => q.Id == "quest.starter");

        var writer = new AssetWriter(root, reader, current);
        var ex = Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
        Assert.Contains("quest.starter", ex.Message);
    }

    // Reordering the quest list is a pure block rewrite — no insertion, deletion, or scalar edit.
    [Fact]
    public void ReorderingQuestsIsOnlyABlockRewrite()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var moved = incoming.Quests[0];
        incoming.Quests.RemoveAt(0);
        incoming.Quests.Add(moved);   // rotate first quest to the end

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.Empty(plan.QuestInsertions);
        Assert.Empty(plan.QuestDeletions);
        Assert.Empty(plan.QuestEdits);
        Assert.NotNull(plan.QuestBlockRewrite);
    }

    // A quest reward scalar edit is a single positional QuestEdit — not a structural change, no block rewrite.
    [Fact]
    public void QuestRewardXpEditIsOneQuestEdit()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var q = incoming.Quests.First(x => x.Id == "quest.starter");
        int old = q.Reward.Xp;
        q.Reward.Xp = old + 25;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        Assert.Null(plan.QuestBlockRewrite);
        Assert.Empty(plan.QuestInsertions);
        var edit = Assert.Single(plan.QuestEdits);
        Assert.Equal(QuestField.RewardXp, edit.Field);
        Assert.Equal(old.ToString(), edit.Old);
        Assert.Equal((old + 25).ToString(), edit.New);
    }

    // A quest condition amount edit carries the condition index (quest.starter's MinLevel condition).
    [Fact]
    public void QuestConditionAmountEditCarriesIndex()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var q = incoming.Quests.First(x => x.Conditions.Count > 0);
        q.Conditions[0].Amount += 1;

        var plan = new AssetWriter(root, reader, current).Plan(incoming);

        var edit = Assert.Single(plan.QuestEdits);
        Assert.Equal(QuestField.ConditionAmount, edit.Field);
        Assert.Equal(0, edit.ConditionIndex);
    }

    // Structurally editing an existing quest (its goal kind) has no surgical path — refused loud.
    [Fact]
    public void QuestGoalKindChangeRefuses()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        var q = incoming.Quests.First(x => x.Id == "quest.starter");
        q.Goal.Kind = q.Goal.Kind == "EarnMoney" ? "FulfillOrders" : "EarnMoney";

        var writer = new AssetWriter(root, reader, current);
        Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
    }

    // A new quest whose QuestCompleted condition references a non-existent quest is refused (boot rule).
    [Fact]
    public void NewQuestWithDanglingPrerequisiteRefuses()
    {
        var (root, reader, current) = ReadReal();
        var incoming = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(current))!;
        incoming.Quests.Add(new QuestConfig
        {
            Id = "quest.dangling",
            Conditions = new() { new QuestConditionConfig { Kind = "QuestCompleted", Arg = "quest.doesNotExist" } },
            Goal = new QuestGoalConfig { Kind = "ReachLevel", Amount = 5 },
            Reward = new QuestRewardConfig { Xp = 10 }
        });

        var writer = new AssetWriter(root, reader, current);
        var ex = Assert.Throws<WriteRefusedException>(() => writer.Plan(incoming));
        Assert.Contains("quest.doesNotExist", ex.Message);
    }
}
