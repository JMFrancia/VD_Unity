using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Model;
using VoidDay.Core.World;
using VoidDay.Data;
using VoidDay.View;

namespace VoidDay.Systems
{
    /// The composition root (§14). Loads + validates data, projects it to Core models, builds the grid,
    /// and renders the starting world: ground, pre-placed stations, camera. Someone has to construct the
    /// object graph — that is this class's job, and why it may reference the View layer. No event bus yet (M2).
    public sealed class GameBoot : MonoBehaviour
    {
        [SerializeField] GameConfigSO config;
        [SerializeField] Camera worldCamera;

        StationGrid _grid;
        GameConfigModel _configModel;
        readonly List<ResourceModel> _resources = new();

        void Start()
        {
            BootValidator.Validate(config); // fail loud at the data boundary; assume well-formed past here

            _configModel = ModelProjector.Project(config);
            foreach (var sr in config.startingResources)
                _resources.Add(ModelProjector.Project(sr.resource));

            _grid = new StationGrid(_configModel.GridCols, _configModel.GridRows);

            BuildGround();
            BuildStations();
            BuildCamera();
        }

        void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            // A Unity Plane is 10x10 units at scale 1; scale it to cover the whole grid.
            ground.transform.localScale = new Vector3(
                _configModel.GridCols * _configModel.CellSize / 10f, 1f,
                _configModel.GridRows * _configModel.CellSize / 10f);
            ground.GetComponent<Renderer>().material = LitMaterial(config.groundColor);
        }

        void BuildStations()
        {
            // Per-type instance counter gives each placed station a stable id (e.g. "field#0") for later events.
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

                var go = new GameObject($"Station_{instanceId}");
                go.transform.SetParent(transform, false);
                go.transform.position = GridProjection.CellToWorld(cell, _configModel);
                go.AddComponent<StationView>().Build(so);
            }
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

        static Material LitMaterial(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            return m;
        }
    }
}
