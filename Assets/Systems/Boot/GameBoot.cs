using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;
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
        [SerializeField] Hud hud;

        void Start()
        {
            BootValidator.Validate(config); // fail loud at the data boundary; assume well-formed past here
            RequireWired();
            var stations = FindStations();

            var bus = new EventBus();
            var pool = new ResourcePool(bus);
            var wallet = new Wallet(bus);
            var catalog = new RecipeCatalog();
            var jobs = new JobSystem(bus, pool, catalog, new ValueResolver());

            var added = new HashSet<RecipeSO>();
            foreach (var station in stations)
                foreach (var recipe in station.Station.recipes)
                    if (added.Add(recipe))
                        catalog.Add(ModelProjector.Project(recipe));

            var roots = new Dictionary<string, Transform>();
            foreach (var station in stations)
            {
                jobs.Register(station.Id, station.Station.stationType, station.Station.queueDepth);
                roots[station.Id] = station.transform;
            }

            var resourceNames = new Dictionary<string, string>();
            var startingCounts = new Dictionary<string, int>();
            var resourceList = new List<KeyValuePair<string, string>>(); // stable config order (totals + cheats)
            foreach (var sr in config.startingResources)
            {
                resourceNames[sr.resource.id] = sr.resource.displayName;
                startingCounts[sr.resource.id] = sr.amount;
                resourceList.Add(new KeyValuePair<string, string>(sr.resource.id, sr.resource.displayName));
            }

            // Grid is centered on the world origin; the camera only needs its world-space extents for pan bounds.
            cameraController.Init(Vector3.zero, config.gridCols * config.cellSize, config.gridRows * config.cellSize);
            producer.Init(bus, jobs, pool, wallet, startingCounts);
            inputRouter.Init(bus, worldCamera);
            worldState.Init(jobs, catalog, roots);
            stationPanel.Init(bus, jobs, catalog, pool, resourceNames, roots, worldCamera);
            hud.Init(bus, pool, resourceList);

            foreach (var kv in startingCounts)
                if (kv.Value != 0) pool.Add(kv.Key, kv.Value); // emits resource:changed (views already listening)
            wallet.EmitCurrent();

            bus.Publish(new DataLoaded());
            bus.Publish(new GameStarted());
        }

        void RequireWired()
        {
            Require(worldCamera, nameof(worldCamera));
            Require(cameraController, nameof(cameraController));
            Require(producer, nameof(producer));
            Require(inputRouter, nameof(inputRouter));
            Require(worldState, nameof(worldState));
            Require(stationPanel, nameof(stationPanel));
            Require(hud, nameof(hud));
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
