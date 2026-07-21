using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.World;
using VoidDay.Data;
using VoidDay.Systems;

namespace VoidDay.View
{
    /// The placement/move ghost (§12.2, overlay.placementGhost / overlay.moveGhost, mockup 35:7). Drives a
    /// translucent instance of the station mesh that follows the pointer, snapped to the nearest cell, tinted
    /// green on a valid cell and red on an occupied / off-grid one. On drop it publishes the input intent
    /// (place / move) ONLY for a valid cell — the StationRegistry translates that to a Core BuildSystem call.
    /// This is pure View: it reads grid state to preview validity and captures the pointer; it holds no rule.
    ///
    /// Two entry points: BeginPlacement (called by a build-menu entry when its drag starts) and a
    /// StationPickedUp subscription (a long-pressed station, from InputRouter). Both run the same drag loop.
    public sealed class PlacementController : MonoBehaviour
    {
        static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

        [SerializeField] Material ghostValidMaterial;
        [SerializeField] Material ghostInvalidMaterial;

        enum Mode { None, Placing, Moving }

        EventBus _bus;
        StationGrid _grid;
        GridProjection _projection;
        Camera _camera;
        readonly Dictionary<string, StationSO> _byType = new();

        Mode _mode;
        string _stationType;   // Placing: the type being built
        string _movingId;      // Moving: the instance being relocated
        GridCoord _originCell; // Moving: its current cell (a drop here is a valid no-op)
        GameObject _ghost;
        GridCoord _cell;
        bool _valid;

        public void Init(EventBus bus, StationGrid grid, GridProjection projection,
            Camera camera, IReadOnlyList<StationSO> roster)
        {
            _bus = bus;
            _grid = grid;
            _projection = projection;
            _camera = camera;
            foreach (var so in roster) _byType[so.stationType] = so;

            _bus.Subscribe<StationPickedUp>(OnStationPickedUp);
        }

        void OnDestroy()
        {
            _bus?.Unsubscribe<StationPickedUp>(OnStationPickedUp);
        }

        /// Called by a build-menu entry when the player drags an available station off the tray.
        public void BeginPlacement(string stationType)
        {
            if (_mode != Mode.None) return;
            _mode = Mode.Placing;
            _stationType = stationType;
            SpawnGhost(_byType[stationType].prefab);
            _bus.Publish(new PlacementActiveChanged(true));
        }

        void OnStationPickedUp(StationPickedUp e)
        {
            if (_mode != Mode.None) return;
            _mode = Mode.Moving;
            _movingId = e.StationId;
            _originCell = _projection.WorldToCell(RootOf(e.StationId).position);
            SpawnGhost(TypeOf(e.StationId).prefab);
            _bus.Publish(new PlacementActiveChanged(true));
        }

        void Update()
        {
            if (_mode == Mode.None) return;
            var pointer = Pointer.current;
            if (pointer == null) return;

            if (TryPlanePoint(pointer.position.ReadValue(), out Vector3 world))
            {
                _cell = _projection.WorldToCell(world);
                _valid = _grid.InBounds(_cell) &&
                         (!_grid.IsOccupied(_cell) || (_mode == Mode.Moving && _cell.Equals(_originCell)));
                _ghost.transform.position = _projection.CellToWorld(_cell);
                Tint(_valid);
            }

            if (pointer.press.wasReleasedThisFrame)
                Finish();
        }

        void Finish()
        {
            if (_valid)
            {
                if (_mode == Mode.Placing) _bus.Publish(new PlaceRequested(_stationType, _cell));
                else _bus.Publish(new MoveRequested(_movingId, _cell));
            }
            else
            {
                string type = _mode == Mode.Placing ? _stationType : TypeOf(_movingId).stationType;
                _bus.Publish(new PlaceRejected(type, _grid.InBounds(_cell) ? "occupied" : "outOfBounds"));
            }
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _mode = Mode.None;
            _stationType = null;
            _movingId = null;
            _bus.Publish(new PlacementActiveChanged(false));
        }

        // ---- Ghost mesh ----

        void SpawnGhost(GameObject prefab)
        {
            _ghost = Instantiate(prefab);
            _ghost.name = "PlacementGhost";
            // A ghost is preview only: no tap target, no core binding.
            foreach (var col in _ghost.GetComponentsInChildren<Collider>()) col.enabled = false;
            var view = _ghost.GetComponent<StationView>();
            if (view != null) Destroy(view);
            _valid = true;
            Tint(true);
        }

        void Tint(bool valid)
        {
            var mat = valid ? ghostValidMaterial : ghostInvalidMaterial;
            foreach (var r in _ghost.GetComponentsInChildren<Renderer>())
                r.sharedMaterial = mat;
        }

        // ---- Lookups (the picked-up station's SO, via its scene StationView) ----

        Transform RootOf(string stationId)
        {
            var view = FindView(stationId);
            return view.transform;
        }

        StationSO TypeOf(string stationId) => FindView(stationId).Station;

        StationView FindView(string stationId)
        {
            foreach (var v in FindObjectsByType<StationView>(FindObjectsSortMode.None))
                if (v.Id == stationId) return v;
            throw new System.InvalidOperationException($"No StationView for picked-up station '{stationId}'");
        }

        bool TryPlanePoint(Vector2 screen, out Vector3 world)
        {
            Ray ray = _camera.ScreenPointToRay(screen);
            if (GroundPlane.Raycast(ray, out float enter)) { world = ray.GetPoint(enter); return true; }
            world = default;
            return false;
        }
    }
}
