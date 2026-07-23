using VoidDay.Balance.Schema;
using VoidDay.Core.Model;

namespace VoidDay.Balance.Unity;

/// Traverses the economy from GameConfig.asset and projects it into a BalanceConfig. Enums are
/// mapped through the real Core enum types (the tool compiles Core), so the JSON carries names and
/// reordering an enum can never silently reassign a value. Everything reachable from the root the
/// game's GameBoot starts from is reachable here — which is what keeps the config honest.
public sealed class EconomyReader
{
    // The economy root. Structural entry point, not a tunable.
    public const string GameConfigPath = "Assets/Data/SO/GameConfig.asset";
    public const string ScenePath = "Assets/Scenes/Farm.unity";

    private readonly AssetReader _reader;

    // Read caches keyed by guid — every referenced asset is read exactly once.
    private readonly Dictionary<string, RecipeRaw> _recipes = new();
    private readonly Dictionary<string, UpgradeRaw> _upgrades = new();
    private readonly Dictionary<string, ResourceRaw> _resources = new();

    public EconomyReader(AssetReader reader) => _reader = reader;

    public BalanceConfig Read()
    {
        var gc = _reader.Read<GameConfigRaw>(GameConfigPath);
        var config = new BalanceConfig();

        ReadGlobal(gc, config);
        ReadGems(gc, config);
        ReadXp(gc, config);
        ReadOrders(gc, config);
        ReadStationsAndDependencies(gc, config);   // fills Stations, Recipes, Upgrades, Resources caches
        ReadLevels(gc, config);

        // Recipes and upgrades must project first — resolving their ingredient/effect references is
        // what populates the resource cache. ProjectResources then captures the full set.
        ProjectRecipes(config);
        ProjectUpgrades(config);
        ProjectResources(config);

        return config;
    }

    private void ReadGlobal(GameConfigRaw gc, BalanceConfig config)
    {
        config.Global.GridCols = gc.gridCols;
        config.Global.GridRows = gc.gridRows;
        config.Global.CellSize = gc.cellSize;
        config.Global.RefundPercent = gc.refundPercent;
        config.Global.StartingStorageCapacity = gc.startingStorageCapacity;
        config.Global.StartingResources = gc.startingResources
            .Select(sr => new ResourceQuantity
            {
                Resource = ResolveResource(sr.resource, "GameConfig.startingResources").id,
                Amount = sr.amount
            })
            .ToList();
    }

    private void ReadGems(GameConfigRaw gc, BalanceConfig config)
    {
        config.Gems.StartingGems = gc.startingGems;
        config.Gems.SecondsPerGem = gc.secondsPerGem;
        config.Gems.MinGemCost = gc.minGemCost;
    }

    private void ReadXp(GameConfigRaw gc, BalanceConfig config)
    {
        var xp = ReadRef<XpConfigRaw>(gc.xpConfig, "GameConfig.xpConfig");
        config.Xp.PerJobCollected = xp.perJobCollected;
        config.Xp.PerStationBuilt = xp.perStationBuilt;
    }

    private void ReadOrders(GameConfigRaw gc, BalanceConfig config)
    {
        var oc = ReadRef<OrderConfigRaw>(gc.orderConfig, "GameConfig.orderConfig");
        config.Orders.SlotCount = oc.slotCount;
        config.Orders.RefillSeconds = oc.refillSeconds;
        config.Orders.MinRequestKinds = oc.minRequestKinds;
        config.Orders.MaxRequestKinds = oc.maxRequestKinds;
        config.Orders.MaxQuantityAtLevel1 = oc.maxQuantityAtLevel1;
        config.Orders.MaxQuantityPerLevel = oc.maxQuantityPerLevel;
        config.Orders.TierWeightBase = oc.tierWeightBase;
        config.Orders.TierWeightPerLevel = oc.tierWeightPerLevel;
        config.Orders.CashMultiplier = oc.cashMultiplier;
        config.Orders.XpMultiplier = oc.xpMultiplier;
    }

    private void ReadStationsAndDependencies(GameConfigRaw gc, BalanceConfig config)
    {
        var prefabGuidToStationType = new Dictionary<string, string>();

        // Roster order is the authored list order — deterministic, so stations keep it.
        foreach (var stationRef in gc.stationRoster)
        {
            var guid = RequireGuid(stationRef, "GameConfig.stationRoster");
            var s = _reader.ReadByGuid<StationRaw>(guid);

            var recipeIds = new List<string>();
            foreach (var r in s.recipes)
            {
                var rg = RequireGuid(r, $"{s.stationType}.recipes");
                recipeIds.Add(CacheRecipe(rg).id);
            }

            var upgradeIds = new List<string>();
            foreach (var u in s.upgrades)
            {
                var ug = RequireGuid(u, $"{s.stationType}.upgrades");
                upgradeIds.Add(CacheUpgrade(ug).id);
            }

            config.Stations.Add(new StationConfig
            {
                StationType = s.stationType,
                DisplayName = s.displayName,
                Buildable = s.buildable != 0,
                BuildCost = s.buildCost,
                Cap = s.cap,
                UnlockLevel = s.unlockLevel,
                QueueDepth = s.queueDepth,
                Width = s.width,
                Height = s.height,
                BuildSeconds = s.buildSeconds,
                RecipeIds = recipeIds,
                UpgradeIds = upgradeIds
            });

            if (s.prefab?.guid != null)
                prefabGuidToStationType[s.prefab.guid] = s.stationType;
        }

        config.Global.StartingStations =
            SceneScanner.Scan(ScenePath, _reader.Guids, prefabGuidToStationType);
    }

    private void ReadLevels(GameConfigRaw gc, BalanceConfig config)
    {
        var levels = ReadRef<LevelsRaw>(gc.levels, "GameConfig.levels");
        foreach (var def in levels.levels)
        {
            config.Levels.Add(new LevelConfig
            {
                XpThreshold = def.xpThreshold,
                Grants = def.grants.Select(g => new LevelGrantConfig
                {
                    Kind = EnumName<LevelEntryKind>(g.kind, "grant.kind", "Levels"),
                    TargetStation = _reader.StationTypeOfRef(g.targetStation),
                    Amount = g.amount
                }).ToList()
            });
        }
    }

    // --- Projection of the collected dependency caches (sorted for determinism) ---

    private void ProjectResources(BalanceConfig config)
    {
        config.Resources = _resources.Values
            .Select(r => new ResourceConfig
            {
                Id = r.id,
                DisplayName = r.displayName,
                BaseValue = r.baseValue,
                Sellable = r.sellable != 0,
                Tier = r.tier
            })
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
    }

    private void ProjectRecipes(BalanceConfig config)
    {
        config.Recipes = _recipes.Values
            .Select(r => new RecipeConfig
            {
                Id = r.id,
                StationType = r.stationType,
                Inputs = r.inputs.Select(i => Ingredient(i, $"{r.id}.inputs")).ToList(),
                Outputs = r.outputs.Select(i => Ingredient(i, $"{r.id}.outputs")).ToList(),
                Duration = r.duration
            })
            .OrderBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
    }

    private void ProjectUpgrades(BalanceConfig config)
    {
        config.Upgrades = _upgrades.Values
            .Select(u => new UpgradeConfig
            {
                Id = u.id,
                DisplayName = u.displayName,
                UnlockLevel = u.unlockLevel,
                Tiers = u.tiers.Select(t => new UpgradeTierConfig
                {
                    Cost = t.cost,
                    Effects = t.effects.Select(e => ProjectEffect(e, u.id)).ToList()
                }).ToList()
            })
            .OrderBy(u => u.Id, StringComparer.Ordinal)
            .ToList();
    }

    private EffectConfig ProjectEffect(EffectRaw e, string upgradeId) => new()
    {
        Id = e.id ?? "",
        Type = EnumName<EffectType>(e.type, "effect.type", upgradeId),
        Op = EnumName<EffectOp>(e.value.op, "effect.value.op", upgradeId),
        Amount = e.value.amount,
        Resource = e.resource ?? "",
        Range = e.range,
        Trigger = EnumName<TriggerType>(e.trigger, "effect.trigger", upgradeId),
        TriggerChance = e.triggerChance,
        ConditionType = EnumName<ConditionType>(e.condition.type, "effect.condition.type", upgradeId),
        ConditionArg = e.condition.arg ?? "",
        ConditionAmount = e.condition.amount
    };

    private ResourceQuantity Ingredient(IngredientRaw i, string context) => new()
    {
        Resource = ResolveResource(i.resource, context).id,
        Amount = i.amount
    };

    // --- Reference resolution + caches ---

    private RecipeRaw CacheRecipe(string guid)
    {
        if (!_recipes.TryGetValue(guid, out var r))
            _recipes[guid] = r = _reader.ReadByGuid<RecipeRaw>(guid);
        return r;
    }

    private UpgradeRaw CacheUpgrade(string guid)
    {
        if (!_upgrades.TryGetValue(guid, out var u))
            _upgrades[guid] = u = _reader.ReadByGuid<UpgradeRaw>(guid);
        return u;
    }

    private ResourceRaw ResolveResource(RawRef? reference, string context)
    {
        var guid = RequireGuid(reference, context);
        if (!_resources.TryGetValue(guid, out var r))
            _resources[guid] = r = _reader.ReadByGuid<ResourceRaw>(guid);
        return r;
    }

    private T ReadRef<T>(RawRef? reference, string context) =>
        _reader.ReadByGuid<T>(RequireGuid(reference, context));

    private static string RequireGuid(RawRef? reference, string context)
    {
        if (reference?.guid == null)
            throw new InvalidOperationException($"{context}: expected an asset reference but it is null (fileID: 0).");
        return reference.guid;
    }

    private static string EnumName<T>(int raw, string field, string asset) where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), raw))
            throw new InvalidOperationException(
                $"{asset}.{field}: value {raw} is not a defined {typeof(T).Name}. Enum out of range — the asset and the Core enum disagree.");
        return ((T)(object)raw).ToString();
    }
}
