using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Events;

namespace VoidDay.View
{
    /// Presses the baked grass flat wherever a station stands. The grass is a single combined mesh, so
    /// there are no per-tuft objects to toggle; instead the current station positions are published to
    /// the grass shader, which sinks any vertex inside a station's radius below the ground plane.
    ///
    /// Visibility is derived rather than stored: move a station and the patch it vacated springs back on
    /// its own, with no bookkeeping to get wrong.
    ///
    /// Pure View — it reads the shared station map and writes shader globals; it holds no rule.
    public sealed class StationFlattenMask : MonoBehaviour
    {
        /// Must match MAX_FLATTEN_STATIONS in VertexColorToon.shader.
        const int MaxStations = 32;

        static readonly int StationsId = Shader.PropertyToID("_StationFlatten");

        [SerializeField] float radius = 0.42f;

        EventBus _bus;
        IReadOnlyDictionary<string, Transform> _roots;
        readonly Vector4[] _packed = new Vector4[MaxStations];

        /// Ordering matters and is guaranteed by construction: EventBus dispatches through a multicast
        /// delegate, so handlers run in subscription order, and GameBoot inits StationRegistry well before
        /// this component. The registry has therefore already created or moved the transform — and seeded
        /// the shared map — by the time the handlers below read it.
        public void Init(EventBus bus, IReadOnlyDictionary<string, Transform> roots)
        {
            _bus = bus;
            _roots = roots;

            _bus.Subscribe<StationBuilt>(OnStationsChanged);
            _bus.Subscribe<StationMoved>(OnStationsChanged);
            _bus.Subscribe<StationDemolished>(OnStationsChanged);
            Publish();
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<StationBuilt>(OnStationsChanged);
            _bus.Unsubscribe<StationMoved>(OnStationsChanged);
            _bus.Unsubscribe<StationDemolished>(OnStationsChanged);
        }

        void OnStationsChanged(StationBuilt _) => Publish();
        void OnStationsChanged(StationMoved _) => Publish();
        void OnStationsChanged(StationDemolished _) => Publish();

        void Publish()
        {
            int n = 0;
            foreach (var root in _roots.Values)
            {
                if (n == MaxStations) break;
                if (root == null) continue;
                var p = root.position;
                _packed[n++] = new Vector4(p.x, p.y, p.z, radius);
            }
            for (int i = n; i < MaxStations; i++) _packed[i] = Vector4.zero;   // radius 0 contributes nothing

            Shader.SetGlobalVectorArray(StationsId, _packed);
        }
    }
}
