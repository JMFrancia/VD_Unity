using VoidDay.Balance.Schema;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

namespace VoidDay.Balance.Unity;

/// A config-space MIRROR of `Assets/Systems/Boot/BootValidator.cs` — the invariants the GAME enforces at boot
/// that the economy sim does NOT model (UI/presentation rules, cross-asset gates). The writer runs this before
/// touching a single `.asset`, so a config the game would reject at `GameBoot.Start()` is refused in
/// config-space instead of shipping a build that throws the moment you press Play.
///
/// ⚠ This is a HAND-KEPT mirror, not the real validator: the tool must never reference anything under
/// `Assets/` (the one-way side-tool rule). When `BootValidator.cs` changes, update this to match — the
/// `WriterTests.RoundTripPassesBootRules` canary fails if the mirror rejects a live config the game accepts,
/// which catches the mirror drifting stricter. Only rules on fields the `BalanceConfig` actually carries are
/// mirrored; asset-reference checks (icons, prefabs, thumbnails, sprites) the reader already guarantees.
public static class BootRules
{
    public static void Validate(BalanceConfig c)
    {
        ValidateGlobal(c);
        ValidateXp(c);
        ValidateOrders(c.Orders);
        ValidateLevels(c.Levels);
        foreach (var r in c.Resources) ValidateResource(r);
        ValidateStations(c);
        foreach (var u in c.Upgrades) ValidateUpgrade(u);
        ValidateQuests(c);
    }

    static void ValidateGlobal(BalanceConfig c)
    {
        var g = c.Global;
        Require(g.GridCols > 0, "global.gridCols", "must be > 0");
        Require(g.GridRows > 0, "global.gridRows", "must be > 0");
        Require(g.CellSize > 0f, "global.cellSize", "must be > 0");
        Require(g.StartingResources.Count > 0, "global.startingResources", "must have at least one entry");
        foreach (var sr in g.StartingResources)
            Require(sr.Amount >= 0, "global.startingResources", $"'{sr.Resource}' starting amount must be >= 0");
        Require(c.Gems.StartingGems >= 0, "gems.startingGems", "must be >= 0");
        Require(c.Gems.SecondsPerGem > 0f, "gems.secondsPerGem",
            "must be > 0 — it is the divisor of a timer skip's gem price");
        Require(c.Gems.MinGemCost >= 1, "gems.minGemCost", "must be >= 1 — a free skip is not a sink");
        Require(g.RefundPercent >= 0f && g.RefundPercent <= 1f, "global.refundPercent", "must be within [0, 1]");
        Require(g.StartingStorageCapacity > 0, "global.startingStorageCapacity",
            "must be > 0 — a 0 capacity blocks every collection forever");
        Require(c.Stations.Count > 0, "stations", "must list every buildable station type");
    }

    static void ValidateXp(BalanceConfig c)
    {
        Require(c.Xp.PerJobCollected >= 0, "xp.perJobCollected", "must be >= 0");
        Require(c.Xp.PerStationBuilt >= 0, "xp.perStationBuilt", "must be >= 0");
    }

    static void ValidateOrders(OrderConfig o)
    {
        Require(o.SlotCount > 0, "orders.slotCount", "must be > 0");
        Require(o.RefillSeconds > 0f, "orders.refillSeconds", "must be > 0");
        Require(o.MinRequestKinds > 0, "orders.minRequestKinds", "must be > 0");
        Require(o.MaxRequestKinds >= o.MinRequestKinds, "orders.maxRequestKinds",
            $"must be >= minRequestKinds ({o.MinRequestKinds})");
        Require(o.MaxQuantityAtLevel1 >= 1f, "orders.maxQuantityAtLevel1", "must be >= 1");
        Require(o.MaxQuantityPerLevel >= 0f, "orders.maxQuantityPerLevel", "must be >= 0");
        Require(o.TierWeightBase > 0f, "orders.tierWeightBase",
            "must be > 0 — a zero base makes every level-1 weight zero and the pick undefined");
        Require(o.TierWeightPerLevel >= 0f, "orders.tierWeightPerLevel", "must be >= 0");
        Require(o.CashMultiplier > 0f, "orders.cashMultiplier", "must be > 0");
        Require(o.XpMultiplier > 0f, "orders.xpMultiplier", "must be > 0");
    }

    /// §9: strictly rising thresholds; level 1 (index 0) is the starting level and is never crossed, so a grant
    /// on it never applies. Money+Gems are one-shot rewards and popup.levelUp renders only rewards[0].
    static void ValidateLevels(List<LevelConfig> levels)
    {
        Require(levels.Count > 1, "levels", "must hold at least two levels — a one-level curve can never level up");
        Require(levels[0].XpThreshold == 0, "levels[0].xpThreshold",
            "must be 0 — entry 0 is level 1, the level every run starts at");
        Require(levels[0].Grants.Count == 0, "levels[0].grants",
            "must be empty — level 1 is never crossed, so its grants would never apply");

        for (int i = 1; i < levels.Count; i++)
        {
            var def = levels[i];
            Require(def.XpThreshold > levels[i - 1].XpThreshold, $"levels[{i}].xpThreshold",
                $"must be greater than level {i}'s ({levels[i - 1].XpThreshold})");

            int rewardGrants = 0;
            foreach (var grant in def.Grants)
            {
                var kind = ParseKind(grant.Kind, i);
                Require(kind != LevelEntryKind.StationType && kind != LevelEntryKind.Upgrade
                        && kind != LevelEntryKind.Recipe, $"levels[{i}].grants",
                    $"may not grant {kind} — that gate lives on the StationSO / UpgradeSO / RecipeSO unlockLevel");
                Require(grant.Amount > 0, $"levels[{i}].grants", $"{kind} amount must be > 0");
                Require(kind != LevelEntryKind.StationCap || grant.TargetStation != null, $"levels[{i}].grants",
                    "a StationCap grant must name its targetStation");
                if (kind == LevelEntryKind.Money || kind == LevelEntryKind.Gems) rewardGrants++;
            }
            Require(rewardGrants <= 1, $"levels[{i}].grants",
                "may hold at most one reward grant (Money or Gems) — popup.levelUp shows a single reward");
        }
    }

    static void ValidateResource(ResourceConfig r)
    {
        Require(!string.IsNullOrWhiteSpace(r.Id), "resources", "an id must not be empty");
        Require(r.BaseValue > 0, $"resources/{r.Id}/baseValue", "must be > 0 — it is the basis of order payout");
        Require(r.Tier > 0, $"resources/{r.Id}/tier", "must be > 0");
    }

    static void ValidateStations(BalanceConfig c)
    {
        var stationByType = c.Stations.ToDictionary(s => s.StationType);
        var recipeById = c.Recipes.ToDictionary(r => r.Id);

        foreach (var s in c.Stations)
        {
            Require(!string.IsNullOrWhiteSpace(s.StationType), "stations", "a stationType must not be empty");
            var at = $"stations/{s.StationType}";
            Require(s.Width > 0, $"{at}/width", "must be > 0");
            Require(s.Height > 0, $"{at}/height", "must be > 0");
            Require(s.QueueDepth > 0, $"{at}/queueDepth", "must be > 0");
            Require(s.BuildCost >= 0, $"{at}/buildCost", "must be >= 0");
            Require(s.Cap > 0, $"{at}/cap", "must be > 0");
            Require(s.BuildSeconds >= 0f, $"{at}/buildSeconds", "must be >= 0");
            Require(s.UnlockLevel >= Progression.StartingLevel, $"{at}/unlockLevel",
                $"must be >= {Progression.StartingLevel} (the starting level)");

            foreach (var recipeId in s.RecipeIds)
                if (recipeById.TryGetValue(recipeId, out var recipe))
                    ValidateRecipe(recipe, s.UnlockLevel);
        }
    }

    /// Cross-asset gate: a recipe is either open the moment its station is built (StartingLevel) or gated
    /// at/after the station itself opens — never in the gap between, where the popup would announce a recipe
    /// for a building the player cannot yet make.
    static void ValidateRecipe(RecipeConfig r, int stationUnlockLevel)
    {
        var at = $"recipes/{r.Id}";
        Require(r.UnlockLevel >= Progression.StartingLevel, $"{at}/unlockLevel",
            $"must be >= {Progression.StartingLevel} (the starting level)");
        Require(r.UnlockLevel == Progression.StartingLevel || r.UnlockLevel >= stationUnlockLevel,
            $"{at}/unlockLevel",
            $"must be {Progression.StartingLevel} (open as soon as the station is built) or >= its station's "
            + $"unlockLevel ({stationUnlockLevel}) — not in the gap before the station exists");
        Require(r.Outputs.Count > 0, $"{at}/outputs", "must have at least one output");
        foreach (var q in r.Inputs.Concat(r.Outputs))
            Require(q.Amount > 0, $"{at}", $"ingredient '{q.Resource}' amount must be > 0");
    }

    static void ValidateUpgrade(UpgradeConfig u)
    {
        var at = $"upgrades/{u.Id}";
        Require(!string.IsNullOrWhiteSpace(u.Id), "upgrades", "an id must not be empty");
        Require(u.UnlockLevel >= Progression.StartingLevel, $"{at}/unlockLevel",
            $"must be >= {Progression.StartingLevel} (the starting level)");
        Require(u.Tiers.Count > 0, $"{at}/tiers", "must have at least one tier");
        for (int t = 0; t < u.Tiers.Count; t++)
        {
            Require(u.Tiers[t].Cost >= 0, $"{at}/tiers[{t}].cost", "must be >= 0");
            Require(u.Tiers[t].Effects.Count > 0, $"{at}/tiers[{t}].effects", "must grant at least one effect");
            foreach (var e in u.Tiers[t].Effects) ValidateEffect(e, $"{at}/tiers[{t}]");
        }
    }

    static void ValidateEffect(EffectConfig e, string where)
    {
        // triggerChance 0 is "unset" (the game normalises it to 100), so only out-of-[0,100] is a fault.
        Require(e.TriggerChance >= 0 && e.TriggerChance <= 100, $"{where} effect '{e.Id}'",
            "triggerChance must be within [0, 100]");
        if (ParseOp(e.Op, e.Id) == EffectOp.Mult)
            Require(e.Amount > 0f, $"{where} effect '{e.Id}'",
                "a Mult amount must be > 0 (a zero multiplier would erase the value)");
        if (RequiresRange(ParseType(e.Type, e.Id)))
            Require(e.Range > 0, $"{where} effect '{e.Id}'",
                $"type {e.Type} is range-scoped and must set range > 0");
    }

    static bool RequiresRange(EffectType t) => t switch
    {
        EffectType.LocalSpeed or EffectType.LocalCost or EffectType.LocalYield
            or EffectType.PetEffectStrength or EffectType.PetAutoCollectSpeed => true,
        _ => false
    };

    /// § quest system: mirror of BootValidator.ValidateQuest — condition amounts in range, a QuestCompleted
    /// condition names a real (non-self) quest id, goal amount > 0, a HarvestCrops goal names a crop, rewards
    /// non-negative and reward-resource amounts > 0. The writer creating a quest that fails any of these would
    /// ship a build that throws at GameBoot.Start(); refuse it in config-space instead.
    static void ValidateQuests(BalanceConfig c)
    {
        var questIds = c.Quests.Select(q => q.Id).ToHashSet();
        foreach (var q in c.Quests)
        {
            Require(!string.IsNullOrWhiteSpace(q.Id), "quests", "a quest id must not be empty");
            var at = $"quests/{q.Id}";

            foreach (var cond in q.Conditions)
            {
                switch (ParseConditionKind(cond.Kind, q.Id))
                {
                    case ConditionKind.MinLevel:
                        Require(cond.Amount >= Progression.StartingLevel, $"{at}/conditions",
                            $"a MinLevel condition amount must be >= {Progression.StartingLevel}");
                        break;
                    case ConditionKind.ResourceAtLeast:
                        Require(!string.IsNullOrWhiteSpace(cond.Arg), $"{at}/conditions",
                            "a ResourceAtLeast condition must name a resource id in arg");
                        Require(cond.Amount > 0, $"{at}/conditions",
                            "a ResourceAtLeast condition amount must be > 0");
                        break;
                    case ConditionKind.QuestCompleted:
                        Require(!string.IsNullOrWhiteSpace(cond.Arg), $"{at}/conditions",
                            "a QuestCompleted condition must name a prerequisite quest id in arg");
                        Require(cond.Arg != q.Id, $"{at}/conditions",
                            $"a QuestCompleted condition cannot reference its own quest ('{q.Id}')");
                        Require(questIds.Contains(cond.Arg), $"{at}/conditions",
                            $"a QuestCompleted condition references '{cond.Arg}', which is not a quest id");
                        break;
                    case ConditionKind.UpgradePurchased:
                        Require(!string.IsNullOrWhiteSpace(cond.Arg), $"{at}/conditions",
                            "an UpgradePurchased condition must name an upgrade track id in arg");
                        break;
                }
            }

            var goalKind = ParseGoalKind(q.Goal.Kind, q.Id);
            Require(q.Goal.Amount > 0, $"{at}/goal", $"goal amount must be > 0 (is {q.Goal.Amount})");
            if (goalKind == GoalKind.HarvestCrops)
                Require(!string.IsNullOrWhiteSpace(q.Goal.TargetId), $"{at}/goal",
                    "a HarvestCrops goal must name a crop resource id in targetId");

            Require(q.Reward.Xp >= 0, $"{at}/reward", "reward xp must be >= 0");
            Require(q.Reward.Money >= 0, $"{at}/reward", "reward money must be >= 0");
            Require(q.Reward.Gems >= 0, $"{at}/reward", "reward gems must be >= 0");
            foreach (var g in q.Reward.Resources)
                Require(g.Amount > 0, $"{at}/reward", $"a reward grant of '{g.Resource}' must be > 0");
        }
    }

    static ConditionKind ParseConditionKind(string kind, string questId) =>
        Enum.TryParse<ConditionKind>(kind, out var k) ? k
            : throw new WriteRefusedException($"boot rule: quest '{questId}' has unknown condition kind '{kind}'. Aborting; nothing written.");

    static GoalKind ParseGoalKind(string kind, string questId) =>
        Enum.TryParse<GoalKind>(kind, out var g) ? g
            : throw new WriteRefusedException($"boot rule: quest '{questId}' has unknown goal kind '{kind}'. Aborting; nothing written.");

    static LevelEntryKind ParseKind(string kind, int levelIndex) =>
        Enum.TryParse<LevelEntryKind>(kind, out var k) ? k
            : throw new WriteRefusedException($"boot rule: levels[{levelIndex}].grants has unknown kind '{kind}'. Aborting; nothing written.");

    static EffectOp ParseOp(string op, string effectId) =>
        Enum.TryParse<EffectOp>(op, out var o) ? o
            : throw new WriteRefusedException($"boot rule: effect '{effectId}' has unknown op '{op}'. Aborting; nothing written.");

    static EffectType ParseType(string type, string effectId) =>
        Enum.TryParse<EffectType>(type, out var t) ? t
            : throw new WriteRefusedException($"boot rule: effect '{effectId}' has unknown type '{type}'. Aborting; nothing written.");

    static void Require(bool condition, string field, string why)
    {
        if (!condition)
            throw new WriteRefusedException(
                $"boot rule: {field} {why} — GameBoot would reject this config on play. Aborting; nothing written.");
    }
}
