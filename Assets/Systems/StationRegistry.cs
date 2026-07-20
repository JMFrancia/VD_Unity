using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.Systems
{
    /// The station-lifecycle System (§12.2). It bridges both directions across the bus: it translates the
    /// placement View's input intents (place / move / demolish) into Core BuildSystem calls, and it reacts to
    /// the facts BuildSystem announces (station:built/moved/demolished) by making the GameObject real —
    /// instantiating the type's authored prefab, moving its transform, or destroying it. It owns the shared
    /// stationId→Transform map that CameraController, StationPanel and WorldState all read live; mutating that
    /// one dictionary is how a runtime-placed station becomes visible everywhere without re-wiring anything.
    public sealed class StationRegistry : MonoBehaviour
    {
        EventBus _bus;
        BuildSystem _build;
        Transform _parent;
        GridProjection _projection;
        readonly Dictionary<string, StationSO> _byType = new();
        readonly Dictionary<string, Transform> _roots = new();

        /// The shared map GameBoot injects into the station-consuming views. Same reference for all of them,
        /// so an add/remove here is instantly visible everywhere.
        public IReadOnlyDictionary<string, Transform> Roots => _roots;

        public void Init(EventBus bus, BuildSystem build, Transform parent, GridProjection projection,
            IReadOnlyList<StationSO> roster, IReadOnlyDictionary<string, Transform> preplaced)
        {
            _bus = bus;
            _build = build;
            _parent = parent;
            _projection = projection;

            foreach (var so in roster) _byType[so.stationType] = so;
            foreach (var kv in preplaced) _roots[kv.Key] = kv.Value; // scene-authored stations seed the map

            _bus.Subscribe<PlaceRequested>(OnPlaceRequested);
            _bus.Subscribe<MoveRequested>(OnMoveRequested);
            _bus.Subscribe<DebugDemolishLastRequested>(OnDemolishLastRequested);

            _bus.Subscribe<StationBuilt>(OnBuilt);
            _bus.Subscribe<StationMoved>(OnMoved);
            _bus.Subscribe<StationDemolished>(OnDemolished);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<PlaceRequested>(OnPlaceRequested);
            _bus.Unsubscribe<MoveRequested>(OnMoveRequested);
            _bus.Unsubscribe<DebugDemolishLastRequested>(OnDemolishLastRequested);

            _bus.Unsubscribe<StationBuilt>(OnBuilt);
            _bus.Unsubscribe<StationMoved>(OnMoved);
            _bus.Unsubscribe<StationDemolished>(OnDemolished);
        }

        // ---- Input intents → Core (Systems translate, per CLAUDE.md rule 3) ----
        void OnPlaceRequested(PlaceRequested e) => _build.Place(e.StationType, e.Cell);
        void OnMoveRequested(MoveRequested e) => _build.Move(e.StationId, e.Cell);
        void OnDemolishLastRequested(DebugDemolishLastRequested _) => _build.DemolishLast();

        void OnBuilt(StationBuilt e)
        {
            var so = _byType[e.StationType];
            if (so.prefab == null)
                throw new System.InvalidOperationException(
                    $"StationSO '{so.name}' ({e.StationType}) has no prefab — cannot instantiate the placed station");

            var go = Instantiate(so.prefab, _projection.CellToWorld(e.Cell), Quaternion.identity, _parent);
            go.name = e.StationId; // the GameObject name IS the Core instance id (StationView.Id reads it)
            _roots[e.StationId] = go.transform;
        }

        void OnMoved(StationMoved e)
        {
            if (_roots.TryGetValue(e.StationId, out var root))
                root.position = _projection.CellToWorld(e.Cell);
        }

        void OnDemolished(StationDemolished e)
        {
            if (_roots.TryGetValue(e.StationId, out var root))
            {
                Destroy(root.gameObject);
                _roots.Remove(e.StationId);
            }
        }
    }
}
