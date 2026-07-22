using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;
using VoidDay.Data;
using VoidDay.Systems;

namespace VoidDay.View
{
    /// The building site a placed station occupies until its timer runs out (§4.3). It renders the station's own
    /// body semi-transparent with a TimerWidget counting down above it — the same widget a running job uses.
    ///
    /// A site is deliberately INERT: the placeholder carries no StationView and no colliders, so InputRouter can
    /// neither tap nor long-press it, and it never enters StationRegistry.Roots, so the camera, the station panel
    /// and the grass-flatten mask all ignore it. There is no cancel and no refund because there is no gesture
    /// that could ask for one.
    ///
    /// Pure View: it listens for the two facts Core announces (construction started, station built) and polls
    /// Core for progress. It decides nothing about when a station exists.
    public sealed class ConstructionSiteView : MonoBehaviour
    {
        [Header("Placeholder")]
        [Tooltip("Semi-transparent material every renderer on the placeholder body is swapped to.")]
        [SerializeField] Material constructionMaterial;
        [SerializeField] TimerWidget timerTemplate;
        [Tooltip("World units above the station's base that the countdown sits at.")]
        [SerializeField] float timerHeight = 1.15f;

        [Header("Completion")]
        [Tooltip("Particle burst instantiated at a station the moment it finishes building. Optional — an " +
                 "unassigned prefab simply means no confetti.")]
        [SerializeField] GameObject confettiPrefab;
        [Tooltip("Seconds before the spawned burst is destroyed.")]
        [SerializeField] float confettiLifetime = 4f;

        sealed class Site
        {
            public string StationId;
            public GameObject Body;
            public TimerWidget Timer;
        }

        EventBus _bus;
        BuildSystem _build;
        GridProjection _projection;
        Transform _parent;
        readonly Dictionary<string, StationSO> _byType = new();
        readonly List<Site> _sites = new();

        public void Init(EventBus bus, BuildSystem build, GridProjection projection, Transform parent,
            IReadOnlyList<StationSO> roster)
        {
            _bus = bus;
            _build = build;
            _projection = projection;
            _parent = parent;
            foreach (var so in roster) _byType[so.stationType] = so;

            _bus.Subscribe<StationConstructionStarted>(OnConstructionStarted);
            _bus.Subscribe<StationBuilt>(OnBuilt);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<StationConstructionStarted>(OnConstructionStarted);
            _bus.Unsubscribe<StationBuilt>(OnBuilt);
        }

        void OnConstructionStarted(StationConstructionStarted e)
        {
            // A zero-second type completes inside Place, so station:built has already been published by the time
            // this runs — spawning a site for it would leave a ghost standing forever.
            if (e.Duration <= 0f) return;

            var position = _projection.CellToWorld(e.Cell);
            var body = SpawnPlaceholder(_byType[e.StationType].prefab, position);
            var timer = Instantiate(timerTemplate, body.transform);
            timer.transform.localPosition = new Vector3(0f, timerHeight, 0f);
            timer.Show(true);

            _sites.Add(new Site { StationId = e.StationId, Body = body, Timer = timer });
        }

        void OnBuilt(StationBuilt e)
        {
            for (int i = _sites.Count - 1; i >= 0; i--)
            {
                if (_sites[i].StationId != e.StationId) continue;
                Destroy(_sites[i].Body);
                _sites.RemoveAt(i);
                Celebrate(_projection.CellToWorld(e.Cell));
                return;
            }
        }

        void Update()
        {
            if (_sites.Count == 0) return;
            double now = Time.timeAsDouble;
            foreach (var site in _sites)
                if (_build.TryGetSiteProgress(site.StationId, now, out float fraction, out float secondsRemaining))
                    site.Timer.SetProgress(fraction, secondsRemaining);
        }

        /// The station's own body, stripped of everything that would make it act like a real station. Same
        /// recipe as the placement ghost (PlacementController.SpawnGhost) — a preview, not a station.
        GameObject SpawnPlaceholder(GameObject prefab, Vector3 position)
        {
            var go = Instantiate(prefab, position, Quaternion.identity, _parent);
            go.name = "ConstructionSite";

            foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;
            var view = go.GetComponent<StationView>();
            if (view != null) Destroy(view);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = constructionMaterial;

            return go;
        }

        void Celebrate(Vector3 position)
        {
            if (confettiPrefab == null) return;
            var burst = Instantiate(confettiPrefab, position, Quaternion.identity, _parent);
            Destroy(burst, confettiLifetime);
        }
    }
}
