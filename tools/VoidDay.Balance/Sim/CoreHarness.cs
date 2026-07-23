using VoidDay.Balance.Schema;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
using VoidDay.Core.World;

namespace VoidDay.Balance.Sim;

/// Wires the pure-C# Core object graph from a BalanceConfig, mirroring Assets/Systems/Boot/GameBoot.Start().
///
/// ── MIRRORED FILE: Assets/Systems/Boot/GameBoot.cs @ commit bde1702 (last commit to touch it) ──
/// ── Reconciled: 2026-07-23. If GameBoot.cs changes, GameBootParityTests fails; re-reconcile here. ──
///
/// The construction ORDER is load-bearing and matches GameBoot exactly: the gem purse is built after the
/// wallet and before progression; the resolver's grant source is set before progression, its effect source
/// after the upgrade system; pre-placed stations are registered after the effect source is wired; the
/// starting pool / EmitCurrent / boot events fire last (EmitStartingState). GameBoot's Init(...) calls wire
/// the View/Systems MonoBehaviours — the harness has none, so it skips them; nothing they do is a Core rule.
///
/// The one thing GameBoot builds that the harness cannot: GridProjection (it lives in Assets/Systems, not
/// Core, and needs scene transforms). The harness places stations at synthetic cells instead — cell position
/// is presentation, not economy.
public sealed class CoreHarness
{
    public readonly BalanceConfig Config;

    public readonly EventBus Bus;
    public readonly ResourcePool Pool;
    public readonly Wallet Wallet;
    public readonly GemPurse Gems;
    public readonly RecipeCatalog Catalog;
    public readonly JobSystem Jobs;
    public readonly StationGrid Grid;
    public readonly Progression Progression;
    public readonly BuildSystem Builds;
    public readonly UpgradeSystem Upgrades;
    public readonly OrderBoard Orders;
    public readonly TimeSkip TimeSkip;
    public readonly XpConfigModel XpConfig;

    readonly IReadOnlyDictionary<string, StationTypeModel> _stationTypes;
    readonly Dictionary<string, int> _startingCounts;
    readonly HashSet<string> _buildable;

    public CoreHarness(BalanceConfig config, int orderSeed)
    {
        Config = config;

        // ── GameBoot.Start() mirror begins ──
        Bus = new EventBus();
        var resolver = new ValueResolver();
        Pool = new ResourcePool(Bus, resolver);
        Wallet = new Wallet(Bus);
        Gems = new GemPurse(Bus, config.Gems.StartingGems);   // after Wallet
        Catalog = new RecipeCatalog();
        Jobs = new JobSystem(Bus, Pool, Catalog, resolver, () => Progression.PlayerLevel);
        Grid = new StationGrid(config.Global.GridCols, config.Global.GridRows);

        // Recipes for every buildable type — the roster covers placed AND not-yet-placed stations.
        var added = new HashSet<string>();
        foreach (var station in config.Stations)
            foreach (var recipeId in station.RecipeIds)
                if (added.Add(recipeId))
                    Catalog.Add(ConfigProjector.Recipe(FindRecipe(config, recipeId)));

        // Per-type build data (cost / cap / unlock level / queue depth / buildSeconds).
        var stationTypes = new Dictionary<string, StationTypeModel>();
        foreach (var station in config.Stations)
            stationTypes[station.StationType] = ConfigProjector.StationType(station);
        _stationTypes = stationTypes;

        var levelCurve = ConfigProjector.Levels(config);
        var levelGrants = new LevelGrants();
        resolver.SetGrantSource(levelGrants);
        Progression = new Progression(Bus, resolver, levelCurve, levelGrants, Wallet, Gems,
            ConfigProjector.LevelGates(config));

        Builds = new BuildSystem(Bus, Grid, Jobs, Wallet, resolver, stationTypes,
            () => Progression.PlayerLevel, config.Global.RefundPercent);

        // Upgrade tracks per station type.
        var upgradeById = new Dictionary<string, UpgradeConfig>();
        foreach (var u in config.Upgrades) upgradeById[u.Id] = u;
        var tracksByType = new Dictionary<string, IReadOnlyList<UpgradeTrackModel>>();
        foreach (var station in config.Stations)
        {
            var tracks = new List<UpgradeTrackModel>(station.UpgradeIds.Count);
            foreach (var upgradeId in station.UpgradeIds) tracks.Add(ConfigProjector.Upgrade(upgradeById[upgradeId]));
            tracksByType[station.StationType] = tracks;
        }
        Upgrades = new UpgradeSystem(Bus, Wallet, tracksByType, () => Progression.PlayerLevel);
        resolver.SetEffectSource(Upgrades);   // after the upgrade system exists

        // Register the pre-placed (scene-authored) stations into grid + producer + upgrades. GameBoot reads
        // these from StationView[] sorted by id; the harness synthesises them from startingStations with
        // stable ids and cells, registered in the same sorted order.
        foreach (var (id, type, cell) in PreplacedInstances(config))
        {
            Builds.RegisterPreplaced(id, type, cell);
            Upgrades.Register(id, type);
        }

        // id → ResourceModel for pricing: roster recipe OUTPUTS + starting resources (GameBoot's set).
        var resourceById = new Dictionary<string, ResourceConfig>();
        foreach (var r in config.Resources) resourceById[r.Id] = r;
        var resourceModels = new Dictionary<string, ResourceModel>();
        foreach (var recipe in config.Recipes)
            foreach (var o in recipe.Outputs)
                resourceModels[o.Resource] = ConfigProjector.Resource(resourceById[o.Resource]);
        foreach (var sr in config.Global.StartingResources)
            resourceModels[sr.Resource] = ConfigProjector.Resource(resourceById[sr.Resource]);

        Pool.SetBaseCapacity(config.Global.StartingStorageCapacity);

        var orderConfig = ConfigProjector.Orders(config.Orders);
        XpConfig = ConfigProjector.Xp(config.Xp);
        var pricing = new OrderPricing(resourceModels, orderConfig, resolver);
        var generation = new OrderGeneration(resourceModels, orderConfig, pricing, new Random(orderSeed));
        Orders = new OrderBoard(Bus, Pool, Wallet, generation, orderConfig, resolver,
            Producible, () => Progression.PlayerLevel);

        TimeSkip = new TimeSkip(Bus, Gems, Jobs, Builds, Orders, config.Gems.SecondsPerGem, config.Gems.MinGemCost);
        // ── GameBoot.Start() mirror ends (Init(...) calls are View/Systems wiring — none in the harness) ──

        // Systems bridge: the two Systems-layer behaviours that DRIVE Core economically (the rest of the
        // Systems layer only pumps Tick or routes input, which the sim loop / agent do directly). Mirrored
        // here because they are essential to leveling and not covered by the GameBoot parity canary:
        //   • ProgressionSystem — turns domain events into XP awards (§9). Without it nothing ever levels.
        //   • UpgradesSystem    — registers a runtime-built station's upgrade tracks on StationBuilt.
        Bus.Subscribe<JobCollected>(_ => Progression.AwardXp(XpConfig.PerJobCollected, "job"));
        Bus.Subscribe<OrderFulfilled>(e => Progression.AwardXp(e.Xp, "order"));
        Bus.Subscribe<StationBuilt>(e => Progression.AwardXp(XpConfig.PerStationBuilt, "build"));
        Bus.Subscribe<StationBuilt>(e => Upgrades.Register(e.StationId, e.StationType));

        _startingCounts = new Dictionary<string, int>();
        foreach (var sr in config.Global.StartingResources) _startingCounts[sr.Resource] = sr.Amount;

        _buildable = new HashSet<string>();
        foreach (var station in config.Stations) if (station.Buildable) _buildable.Add(station.StationType);
    }

    /// Emit the starting state — the tail of GameBoot.Start() (lines that seed the pool, push EmitCurrent for
    /// wallet then gems, and publish DataLoaded / GameStarted). Called AFTER the MetricsCollector subscribes,
    /// exactly as GameBoot's Init(...) calls (the subscribers) precede this block.
    public void EmitStartingState()
    {
        foreach (var kv in Sorted(_startingCounts))
            if (kv.Value != 0) Pool.Add(kv.Key, kv.Value);
        Wallet.EmitCurrent();
        Gems.EmitCurrent();
        Bus.Publish(new DataLoaded());
        Bus.Publish(new GameStarted());
    }

    /// The order pool's candidate set, evaluated LIVE per generation — a literal copy of GameBoot's Producible
    /// closure. A boot-time snapshot would never learn about a runtime-built Henhouse and would offer
    /// cheesecake at level 1; the closure over grid.All × catalog.ForStationType is the trap's only fix.
    public IReadOnlyCollection<string> Producible()
    {
        var ids = new HashSet<string>();
        foreach (var kv in Grid.All)
        {
            if (kv.Value.UnderConstruction) continue;
            foreach (var recipe in Catalog.ForStationType(kv.Value.StationType))
                foreach (var output in recipe.Outputs)
                    ids.Add(output.ResourceId);
        }
        return ids;
    }

    public StationTypeModel TypeOf(string stationType) => _stationTypes[stationType];
    public bool IsBuildable(string stationType) => _buildable.Contains(stationType);

    /// The first unoccupied in-bounds cell, scanned in a fixed (row-major) order so a build is deterministic.
    public GridCoord FindFreeCell()
    {
        for (int row = 0; row < Grid.Rows; row++)
            for (int col = 0; col < Grid.Cols; col++)
            {
                var cell = new GridCoord(col, row);
                if (!Grid.IsOccupied(cell)) return cell;
            }
        throw new InvalidOperationException("No free grid cell — the grid is full");
    }

    static IEnumerable<(string id, string type, GridCoord cell)> PreplacedInstances(BalanceConfig config)
    {
        // Stable id per instance ("{type}@{n}"), '@' so it never collides with BuildSystem's runtime
        // "{type}#{n}". Sorted by id for GameBoot-parity deterministic registration; cells row-major.
        var instances = new List<(string id, string type)>();
        foreach (var s in config.Global.StartingStations)
            for (int n = 0; n < s.Count; n++)
                instances.Add(($"{s.StationType}@{n}", s.StationType));
        instances.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        int index = 0;
        foreach (var (id, type) in instances)
        {
            var cell = new GridCoord(index % config.Global.GridCols, index / config.Global.GridCols);
            index++;
            yield return (id, type, cell);
        }
    }

    static RecipeConfig FindRecipe(BalanceConfig config, string recipeId)
    {
        foreach (var r in config.Recipes) if (r.Id == recipeId) return r;
        throw new InvalidOperationException($"Station references recipe '{recipeId}' absent from config.Recipes");
    }

    static IEnumerable<KeyValuePair<string, int>> Sorted(Dictionary<string, int> d)
    {
        var keys = new List<string>(d.Keys);
        keys.Sort(StringComparer.Ordinal);
        foreach (var k in keys) yield return new KeyValuePair<string, int>(k, d[k]);
    }
}
