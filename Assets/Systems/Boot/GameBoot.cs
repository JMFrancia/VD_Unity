using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
using VoidDay.Core.World;
using VoidDay.Data;
using VoidDay.View;

namespace VoidDay.Systems
{
    /// The composition root (§14). The scene owns the objects (CLAUDE.md rule 4) — this validates data,
    /// constructs the pure-C# core, and injects core services into the scene-placed Systems and Views via
    /// Init. Someone has to construct the core object graph — that is this class's job, and why it alone may
    /// reference every layer. Past this point systems talk only through the bus (rule 2). It creates no
    /// GameObjects: what exists is authored in Farm.unity and the prefabs.
    public sealed class GameBoot : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] GameConfigSO config;

        [Header("Scene wiring")]
        [SerializeField] Camera worldCamera;
        [SerializeField] CameraController cameraController;
        [SerializeField] Producer producer;
        [SerializeField] InputRouter inputRouter;
        [SerializeField] WorldState worldState;
        [SerializeField] StationPanel stationPanel;
        [SerializeField] OrderBoardPanel orderBoardPanel;
        [SerializeField] SiloPanel siloPanel;
        [SerializeField] OrderBoardSystem orderBoardSystem;
        [SerializeField] ProgressionSystem progressionSystem;
        [SerializeField] UpgradesSystem upgradesSystem;
        [SerializeField] StationRegistry stationRegistry;
        [SerializeField] ConstructionSiteView constructionSiteView;
        [SerializeField] GameObject stationsParent;
        [SerializeField] BuildMenu buildMenu;
        [SerializeField] PlacementController placementController;
        [SerializeField] Hud hud;
        [SerializeField] LevelXpHud levelXpHud;
        [SerializeField] LevelUpPopup levelUpPopup;
        [SerializeField] SfxController sfxController;
        [SerializeField] ToastController toastController;
        [SerializeField] StationFlattenMask stationFlattenMask;
        [SerializeField] MusicManager musicManager;

        [Tooltip("Fixed seed makes a session's orders reproducible; 0 = seed from the clock.")]
        [SerializeField] int orderSeed = 12345;

        void Start()
        {
            BootValidator.Validate(config); // fail loud at the data boundary; assume well-formed past here
            RequireWired();
            var stations = FindStations();

            var bus = new EventBus();
            var resolver = new ValueResolver(); // one seam instance — M5 gives it teeth for every rule at once
            var pool = new ResourcePool(bus, resolver);
            var wallet = new Wallet(bus);
            var catalog = new RecipeCatalog();
            var jobs = new JobSystem(bus, pool, catalog, resolver);
            var grid = new StationGrid(config.gridCols, config.gridRows);
            var projection = new GridProjection(config.gridCols, config.gridRows, config.cellSize);

            // Recipes for every buildable type — the roster covers placed AND not-yet-placed stations, so a
            // station built at runtime already has its recipes registered.
            var added = new HashSet<RecipeSO>();
            foreach (var so in config.stationRoster)
                foreach (var recipe in so.recipes)
                    if (added.Add(recipe))
                        catalog.Add(ModelProjector.Project(recipe));

            // Per-type build data the build rules read (cost / cap / unlock level / queue depth).
            var stationTypes = new Dictionary<string, StationTypeModel>();
            foreach (var so in config.stationRoster)
                stationTypes[so.stationType] = ModelProjector.ProjectType(so);

            // Levelling (§9): the curve is the XP→level table, the grants object accumulates what levels have
            // handed out. The grant source is a second contributor to the value seam, alongside the effect
            // source — a level moves a base, an effect scales it.
            var levelCurve = ModelProjector.ProjectLevels(config.levels);
            var levelGrants = new LevelGrants();
            resolver.SetGrantSource(levelGrants);
            var progression = new Progression(bus, resolver, levelCurve, levelGrants, wallet,
                ModelProjector.ProjectLevelGates(config.stationRoster));

            var buildSystem = new BuildSystem(bus, grid, jobs, wallet, resolver, stationTypes,
                () => progression.PlayerLevel, config.refundPercent);

            // Upgrade tracks per station type (§8), projected from the roster's UpgradeSO refs. The upgrade
            // system is the M5 effect source; wiring it into the seam is what gives resolve() its teeth (§3).
            var tracksByType = new Dictionary<string, IReadOnlyList<UpgradeTrackModel>>();
            foreach (var so in config.stationRoster)
            {
                var tracks = new List<UpgradeTrackModel>(so.upgrades.Count);
                foreach (var upgrade in so.upgrades) tracks.Add(ModelProjector.ProjectUpgrade(upgrade));
                tracksByType[so.stationType] = tracks;
            }
            var upgrades = new UpgradeSystem(bus, wallet, tracksByType, () => progression.PlayerLevel);
            resolver.SetEffectSource(upgrades); // seam now sums real effects; call sites unchanged since M2

            // Register the scene-authored pre-placed stations into grid + producer + upgrades; each cell is
            // derived from its transform (the scene owns placement, CLAUDE.md rule 4).
            var preplaced = new Dictionary<string, Transform>();
            foreach (var station in stations)
            {
                buildSystem.RegisterPreplaced(station.Id, station.Station.stationType,
                    projection.WorldToCell(station.transform.position));
                upgrades.Register(station.Id, station.Station.stationType);
                preplaced[station.Id] = station.transform;
            }

            stationRegistry.Init(bus, buildSystem, stationsParent.transform, projection, config.stationRoster, preplaced);
            constructionSiteView.Init(bus, buildSystem, projection, stationsParent.transform, config.stationRoster);
            var roots = stationRegistry.Roots; // shared live map, mutated by StationRegistry on build/demolish

            // id → ResourceSO: the display lookup the UI reads for both name and icon. One SO per resource, so
            // name and icon can't drift apart (a parallel name/sprite pair would). Seeded from starting
            // resources for stable config order, then widened with every recipe input/output in the roster so
            // ingredient rows and recipe tiles always resolve an icon.
            //
            // Roster, not the placed stations: a type built at runtime must already have its goods known, or
            // its first output renders as a raw id with no icon and never shows in the silo. Same reason the
            // recipe catalog above reads the roster.
            var resourceDisplays = new Dictionary<string, ResourceSO>();
            var startingCounts = new Dictionary<string, int>();
            var resourceList = new List<ResourceSO>(); // stable config order (totals + cheats)
            foreach (var sr in config.startingResources)
            {
                resourceDisplays[sr.resource.id] = sr.resource;
                startingCounts[sr.resource.id] = sr.amount;
                resourceList.Add(sr.resource);
            }
            foreach (var so in config.stationRoster)
                foreach (var recipe in so.recipes)
                {
                    foreach (var input in recipe.inputs) Widen(input.resource);
                    foreach (var output in recipe.outputs) Widen(output.resource);
                }

            void Widen(ResourceSO resource)
            {
                if (resourceDisplays.ContainsKey(resource.id)) return;
                resourceDisplays[resource.id] = resource;
                resourceList.Add(resource); // starting resources keep the head of the list; goods follow
            }

            // One shared silo capacity across every good (§7), set before anything is added so the very first
            // collection is already gated.
            pool.SetBaseCapacity(config.startingStorageCapacity);

            // Pricing lookup for every resource the game can ever order — roster-wide, because an order for a
            // good is priced the moment its station type exists, and OrderGeneration throws on an id it can't
            // resolve. Which of these are *offered* is a separate, live question (Producible below).
            var resourceModels = new Dictionary<string, ResourceModel>();
            foreach (var so in config.stationRoster)
                foreach (var recipe in so.recipes)
                    foreach (var output in recipe.outputs)
                        resourceModels[output.resource.id] = ModelProjector.Project(output.resource);
            foreach (var sr in config.startingResources)
                resourceModels[sr.resource.id] = ModelProjector.Project(sr.resource);

            // The order pool's candidate set (§6.1) — what a *currently placed* station can make, evaluated
            // per generation rather than snapshotted at boot. OrderBoard takes a callback precisely so the
            // pool widens when a Henhouse goes up and narrows when it comes down; a boot-time set would offer
            // cheesecake before a Bakery is buildable and never learn about eggs after one is built.
            IReadOnlyCollection<string> Producible()
            {
                var ids = new HashSet<string>();
                foreach (var kv in grid.All)
                {
                    // A station still under construction does not produce yet, so it must not widen the pool —
                    // otherwise a half-built Bakery starts attracting cake orders nothing can fill (§4.3).
                    if (kv.Value.UnderConstruction) continue;
                    foreach (var recipe in catalog.ForStationType(kv.Value.StationType))
                        foreach (var output in recipe.Outputs)
                            ids.Add(output.ResourceId);
                }
                return ids;
            }

            var orderConfig = ModelProjector.Project(config.orderConfig);
            var xpConfig = ModelProjector.Project(config.xpConfig);
            var pricing = new OrderPricing(resourceModels, orderConfig, resolver);
            var generation = new OrderGeneration(resourceModels, orderConfig, pricing,
                orderSeed == 0 ? new System.Random() : new System.Random(orderSeed));
            var orderBoard = new OrderBoard(bus, pool, wallet, generation, orderConfig, resolver,
                Producible, () => progression.PlayerLevel);

            // Grid is centered on the world origin; the camera only needs its world-space extents for pan bounds.
            cameraController.Init(Vector3.zero, config.gridCols * config.cellSize, config.gridRows * config.cellSize, bus, roots);
            producer.Init(bus, jobs, pool, wallet, startingCounts);
            inputRouter.Init(bus, worldCamera);
            worldState.Init(bus, jobs, catalog, upgrades, resourceDisplays, roots, worldCamera);
            stationPanel.Init(bus, jobs, catalog, pool, wallet, upgrades, resourceDisplays, roots, worldCamera);
            orderBoardPanel.Init(bus, orderBoard, pool, jobs, resourceDisplays);
            siloPanel.Init(bus, pool, jobs, upgrades, wallet, resourceList);
            orderBoardSystem.Init(bus, orderBoard, wallet);
            progressionSystem.Init(bus, progression, xpConfig);
            upgradesSystem.Init(bus, upgrades);
            buildMenu.Init(bus, buildSystem, wallet, () => progression.PlayerLevel, config.stationRoster);
            placementController.Init(bus, grid, projection, worldCamera, config.stationRoster);
            hud.Init(bus, pool, progression, resourceList);
            levelXpHud.Init(bus, progression);
            levelUpPopup.Init(bus, config.stationRoster);
            sfxController.Init(bus);
            toastController.Init(bus);
            stationFlattenMask.Init(bus, roots);
            musicManager.Init(bus);

            foreach (var kv in startingCounts)
                if (kv.Value != 0) pool.Add(kv.Key, kv.Value); // emits resource:changed (views already listening)
            wallet.EmitCurrent();

            bus.Publish(new DataLoaded());
            bus.Publish(new GameStarted());
        }

        void RequireWired()
        {
            Require(worldCamera, nameof(worldCamera));
            Require(toastController, nameof(toastController));
            Require(cameraController, nameof(cameraController));
            Require(producer, nameof(producer));
            Require(inputRouter, nameof(inputRouter));
            Require(worldState, nameof(worldState));
            Require(stationPanel, nameof(stationPanel));
            Require(orderBoardPanel, nameof(orderBoardPanel));
            Require(orderBoardSystem, nameof(orderBoardSystem));
            Require(progressionSystem, nameof(progressionSystem));
            Require(upgradesSystem, nameof(upgradesSystem));
            Require(stationRegistry, nameof(stationRegistry));
            Require(constructionSiteView, nameof(constructionSiteView));
            Require(stationsParent, nameof(stationsParent));
            Require(buildMenu, nameof(buildMenu));
            Require(placementController, nameof(placementController));
            Require(hud, nameof(hud));
            Require(levelXpHud, nameof(levelXpHud));
            Require(levelUpPopup, nameof(levelUpPopup));
            Require(siloPanel, nameof(siloPanel));
            Require(sfxController, nameof(sfxController));
            Require(stationFlattenMask, nameof(stationFlattenMask));
            Require(musicManager, nameof(musicManager));
        }

        static void Require(Object reference, string field)
        {
            if (reference == null)
                throw new System.InvalidOperationException($"[Boot] GameBoot.{field} is not wired in the inspector");
        }

        /// The scene owns which stations exist and where; their GameObject names are the Core instance ids.
        StationView[] FindStations()
        {
            var views = FindObjectsByType<StationView>(FindObjectsSortMode.None);
            if (views.Length == 0)
                throw new System.InvalidOperationException(
                    "[Boot] no StationView in the scene — place station prefabs in Farm.unity");

            System.Array.Sort(views, (a, b) => string.CompareOrdinal(a.Id, b.Id)); // deterministic registration
            var seen = new HashSet<string>();
            foreach (var view in views)
            {
                if (view.Station == null)
                    throw new System.InvalidOperationException(
                        $"[Boot] station '{view.Id}' has no StationSO assigned");
                BootValidator.ValidateStation(view.Station);
                if (!seen.Add(view.Id))
                    throw new System.InvalidOperationException(
                        $"[Boot] duplicate station name '{view.Id}' — names are Core ids and must be unique");
            }
            return views;
        }
    }
}
