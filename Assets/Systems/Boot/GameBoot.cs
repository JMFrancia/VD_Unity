using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
using VoidDay.Core.World;
using VoidDay.Data;
using VoidDay.View;

namespace VoidDay.Systems
{
    /// The composition root (§14). Validates data, projects it to Core, builds the event bus and the economy
    /// core, renders the world, and wires every System + View to the bus. Someone has to construct the object
    /// graph — that is this class's job, and why it alone may reference every layer. Past this point systems
    /// talk only through the bus (CLAUDE.md rule 2).
    public sealed class GameBoot : MonoBehaviour
    {
        [SerializeField] GameConfigSO config;
        [SerializeField] UiThemeSO uiTheme;
        [SerializeField] Camera worldCamera;

        StationGrid _grid;
        GameConfigModel _configModel;

        EventBus _bus;
        ResourcePool _pool;
        Wallet _wallet;
        RecipeCatalog _catalog;
        JobSystem _jobs;

        void Start()
        {
            BootValidator.Validate(config); // fail loud at the data boundary; assume well-formed past here
            if (uiTheme == null)
                throw new System.InvalidOperationException("[Boot] GameBoot.uiTheme (UiThemeSO) is not assigned");
            UiFactory.SetTheme(uiTheme); // every View reads chrome from the theme; set before any UI is built

            _configModel = ModelProjector.Project(config);
            _grid = new StationGrid(_configModel.GridCols, _configModel.GridRows);

            _bus = new EventBus();
            _pool = new ResourcePool(_bus);
            _wallet = new Wallet(_bus);
            _catalog = new RecipeCatalog();
            _jobs = new JobSystem(_bus, _pool, _catalog, new ValueResolver());

            var resourceNames = BuildResourceNames();      // id → display name (View labels)
            var startingCounts = BuildStartingCounts();     // id → starting amount (seed + debug reset)

            BuildRecipeCatalog();
            BuildGround();
            var stationRoots = BuildStations();
            BuildCamera();

            BuildEventSystem();
            WireSystems(startingCounts);
            WireViews(stationRoots, resourceNames);

            SeedResources(startingCounts);                  // emits resource:changed (views already listening)
            _wallet.EmitCurrent();

            _bus.Publish(new DataLoaded());
            _bus.Publish(new GameStarted());
        }

        Dictionary<string, string> BuildResourceNames()
        {
            var names = new Dictionary<string, string>();
            foreach (var sr in config.startingResources)
                names[sr.resource.id] = sr.resource.displayName;
            return names;
        }

        Dictionary<string, int> BuildStartingCounts()
        {
            var counts = new Dictionary<string, int>();
            foreach (var sr in config.startingResources)
                counts[sr.resource.id] = sr.amount;
            return counts;
        }

        void SeedResources(Dictionary<string, int> startingCounts)
        {
            foreach (var kv in startingCounts)
                if (kv.Value != 0) _pool.Add(kv.Key, kv.Value);
        }

        void BuildRecipeCatalog()
        {
            var added = new HashSet<RecipeSO>();
            foreach (var placed in config.prePlacedStations)
                foreach (var recipe in placed.station.recipes)
                    if (added.Add(recipe))
                        _catalog.Add(ModelProjector.Project(recipe));
        }

        void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = new Vector3(
                _configModel.GridCols * _configModel.CellSize / 10f, 1f,
                _configModel.GridRows * _configModel.CellSize / 10f);
            ground.GetComponent<Renderer>().material = LitMaterial(config.groundColor);
        }

        /// Spawn each pre-placed station, register it with the Core Producer, and return its root transform
        /// (so WorldState can hang the progress bar / ready icon above it).
        Dictionary<string, Transform> BuildStations()
        {
            var roots = new Dictionary<string, Transform>();
            var typeCounts = new Dictionary<string, int>();
            foreach (var placed in config.prePlacedStations)
            {
                var so = placed.station;
                typeCounts.TryGetValue(so.stationType, out int n);
                typeCounts[so.stationType] = n + 1;
                string instanceId = $"{so.stationType}#{n}";

                var model = ModelProjector.Project(so, instanceId);
                var cell = new GridCoord(placed.col, placed.row);
                _grid.Add(cell, model);
                _jobs.Register(instanceId, so.stationType, so.queueDepth);

                var go = new GameObject($"Station_{instanceId}");
                go.transform.SetParent(transform, false);
                go.transform.position = GridProjection.CellToWorld(cell, _configModel);
                go.AddComponent<StationView>().Build(so);
                go.AddComponent<StationTag>().StationId = instanceId;
                roots[instanceId] = go.transform;
            }
            return roots;
        }

        void BuildCamera()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = config.backdropColor;

            var controller = worldCamera.GetComponent<CameraController>();
            if (controller == null) controller = worldCamera.gameObject.AddComponent<CameraController>();
            controller.Init(config, Vector3.zero); // the grid is centered on the world origin
        }

        void BuildEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.transform.SetParent(transform, false);
        }

        void WireSystems(Dictionary<string, int> startingCounts)
        {
            var producer = new GameObject("Producer").AddComponent<Producer>();
            producer.transform.SetParent(transform, false);
            producer.Init(_bus, _jobs, _pool, _wallet, startingCounts);
        }

        void WireViews(Dictionary<string, Transform> stationRoots, Dictionary<string, string> resourceNames)
        {
            var input = new GameObject("InputRouter").AddComponent<InputRouter>();
            input.transform.SetParent(transform, false);
            input.Init(_bus, worldCamera);

            var world = new GameObject("WorldStateView").AddComponent<WorldState>();
            world.transform.SetParent(transform, false);
            world.Init(_jobs, stationRoots, worldCamera, uiTheme, _catalog);

            var panel = new GameObject("StationPanelView").AddComponent<StationPanel>();
            panel.transform.SetParent(transform, false);
            panel.Init(_bus, _jobs, _catalog, _pool, resourceNames, stationRoots, uiTheme, worldCamera);

            var hud = new GameObject("HudView").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Init(_bus, _pool, BuildResourceList(), uiTheme);
        }

        // Stable-ordered id→name pairs for the totals popup + debug add buttons (config order).
        List<KeyValuePair<string, string>> BuildResourceList()
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (var sr in config.startingResources)
                list.Add(new KeyValuePair<string, string>(sr.resource.id, sr.resource.displayName));
            return list;
        }

        static Material LitMaterial(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            return m;
        }
    }
}
