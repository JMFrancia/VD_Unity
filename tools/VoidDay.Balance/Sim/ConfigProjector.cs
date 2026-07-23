using VoidDay.Balance.Schema;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

namespace VoidDay.Balance.Sim;

/// The BalanceConfig ↔ Core/Model bridge for the simulator, mirroring Assets/Systems/Boot/ModelProjector.cs.
/// GameBoot projects SOs → Core models; the harness has no SOs, so it projects the JSON DTOs to the same
/// Core models by the same rules. Enum strings are parsed through the real Core enum types (never a table),
/// exactly as the reader maps them the other way.
public static class ConfigProjector
{
    public static ResourceModel Resource(ResourceConfig c) =>
        new(c.Id, c.DisplayName, c.BaseValue, c.Sellable, c.Tier);

    public static RecipeModel Recipe(RecipeConfig c) =>
        new(c.Id, c.StationType, Amounts(c.Inputs), Amounts(c.Outputs), c.Duration, c.UnlockLevel);

    public static StationTypeModel StationType(StationConfig c) =>
        new(c.StationType, c.DisplayName, c.BuildCost, c.Cap, c.UnlockLevel, c.QueueDepth, c.Width, c.Height,
            c.BuildSeconds);

    public static OrderConfigModel Orders(OrderConfig c) =>
        new(c.SlotCount, c.RefillSeconds, c.MinRequestKinds, c.MaxRequestKinds, c.MaxQuantityAtLevel1,
            c.MaxQuantityPerLevel, c.CashMultiplier, c.XpMultiplier, c.TierWeightBase, c.TierWeightPerLevel);

    public static XpConfigModel Xp(XpConfig c) => new(c.PerJobCollected, c.PerStationBuilt);

    public static UpgradeTrackModel Upgrade(UpgradeConfig c)
    {
        var tiers = new UpgradeTierModel[c.Tiers.Count];
        for (int i = 0; i < c.Tiers.Count; i++)
        {
            var effects = new Effect[c.Tiers[i].Effects.Count];
            for (int e = 0; e < effects.Length; e++) effects[e] = Effect(c.Tiers[i].Effects[e]);
            tiers[i] = new UpgradeTierModel(c.Tiers[i].Cost, effects);
        }
        return new UpgradeTrackModel(c.Id, c.DisplayName, c.UnlockLevel, tiers);
    }

    /// Mirrors ModelProjector.ProjectLevels. A level's number is its position in the list; a grant's target
    /// is the station type string (or "" = every station type, LevelGrants.AllTargets).
    public static LevelCurve Levels(BalanceConfig config)
    {
        var displayByType = new Dictionary<string, string>();
        foreach (var s in config.Stations) displayByType[s.StationType] = s.DisplayName;

        var levels = new List<LevelModel>(config.Levels.Count);
        for (int i = 0; i < config.Levels.Count; i++)
        {
            var def = config.Levels[i];
            var grants = new List<LevelGrantModel>(def.Grants.Count);
            foreach (var g in def.Grants)
            {
                var kind = System.Enum.Parse<LevelEntryKind>(g.Kind);
                var target = g.TargetStation ?? LevelGrants.AllTargets;
                var label = g.TargetStation != null && displayByType.TryGetValue(g.TargetStation, out var d)
                    ? d : "";
                grants.Add(new LevelGrantModel(kind, target, label, g.Amount));
            }
            levels.Add(new LevelModel(i + 1, def.XpThreshold, grants));
        }
        return new LevelCurve(levels);
    }

    /// Mirrors ModelProjector.ProjectLevelGates: station types and upgrade tracks gated by their own
    /// unlockLevel, iterated in roster (config.Stations) order, tracks deduped by id.
    public static IReadOnlyList<LevelUnlockModel> LevelGates(BalanceConfig config)
    {
        var byId = new Dictionary<string, UpgradeConfig>();
        foreach (var u in config.Upgrades) byId[u.Id] = u;

        var gates = new List<LevelUnlockModel>();
        var seenTracks = new HashSet<string>();
        foreach (var station in config.Stations)
        {
            if (station.UnlockLevel > Progression.StartingLevel)
                gates.Add(new LevelUnlockModel(LevelEntryKind.StationType, station.StationType,
                    station.DisplayName, station.UnlockLevel));

            foreach (var upgradeId in station.UpgradeIds)
                if (byId.TryGetValue(upgradeId, out var u)
                    && u.UnlockLevel > Progression.StartingLevel && seenTracks.Add(u.Id))
                    gates.Add(new LevelUnlockModel(LevelEntryKind.Upgrade, u.Id, u.DisplayName, u.UnlockLevel));
        }
        return gates;
    }

    public static Effect Effect(EffectConfig c) => new()
    {
        id = c.Id,
        type = System.Enum.Parse<EffectType>(c.Type),
        value = new EffectValue { op = System.Enum.Parse<EffectOp>(c.Op), amount = c.Amount },
        resource = c.Resource,
        range = c.Range,
        trigger = System.Enum.Parse<TriggerType>(c.Trigger),
        triggerChance = c.TriggerChance,
        condition = new Condition
        {
            type = System.Enum.Parse<ConditionType>(c.ConditionType),
            arg = c.ConditionArg,
            amount = c.ConditionAmount
        }
    };

    static IReadOnlyList<ResourceAmount> Amounts(List<ResourceQuantity> src)
    {
        var list = new List<ResourceAmount>(src.Count);
        foreach (var q in src) list.Add(new ResourceAmount(q.Resource, q.Amount));
        return list;
    }
}
