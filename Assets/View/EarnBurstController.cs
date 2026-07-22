using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// The engine behind collection particles: earning something throws icon particles from the point of
    /// action to that thing's HUD home, and the counter only moves as each particle lands.
    ///
    /// It announces (EarnBurstLaunched / EarnParticleArrived) and never touches a destination. Hud and
    /// SfxController each subscribe and decide for themselves what an arrival means — the controller holds
    /// no reference to either (CLAUDE.md rule 2).
    ///
    /// Nothing spawns inside an event handler. Progression.AwardXp publishes XpGained synchronously from
    /// inside a JobCollected dispatch and may nest a LevelUp inside that, so handlers only RECORD and
    /// LateUpdate flushes once the frame's whole cascade has settled. Spawning in the handler would launch
    /// particles from a half-updated world.
    public sealed class EarnBurstController : MonoBehaviour
    {
        [Header("Particles")]
        [SerializeField] EarnParticle earnParticlePrefab;

        [Tooltip("Ceiling on particles per burst. A payout above this is split into this many fatter chunks.")]
        [Min(1)] [SerializeField] int maxParticles = 10;

        [Tooltip("Seconds between one particle leaving and the next. This is the rhythm of the stream.")]
        [Min(0f)] [SerializeField] float staggerSeconds = 0.06f;

        [SerializeField] FlightSettings flight = new()
        {
            scatterRadius = 90f,
            scatterSeconds = 0.18f,
            scatterEase = Ease.OutQuad,
            flightSeconds = 0.6f,
            flightSecondsJitter = 0.08f,
            flightEase = Ease.InQuad,
        };

        [Header("Money")]
        [SerializeField] Sprite coinSprite;

        [Tooltip("The money pill on HudCanvas. Read live every tick, not baked at launch.")]
        [SerializeField] RectTransform moneyTarget;

        [Header("XP")]
        [SerializeField] Sprite starSprite;

        [Tooltip("The level badge on HudCanvas. Read live every tick, not baked at launch.")]
        [SerializeField] RectTransform xpTarget;

        [Header("Resources")]
        [Tooltip("The transient pill rail on HudCanvas. Used ONLY for its read-only RectFor query — this is " +
                 "an inspector-wired MonoBehaviour reference, not an Init-injected service.")]
        [SerializeField] ResourcePillRail resourcePillRail;

        EventBus _bus;
        RectTransform _fxRect;
        IReadOnlyDictionary<string, Transform> _stationRoots;
        Camera _worldCamera;

        /// Resource icons, by id. The controller has no other use for the catalog.
        readonly Dictionary<string, ResourceSO> _resourcesById = new();

        readonly List<PendingBurst> _bursts = new();

        /// One origin QUEUE per source, not one slot: two earns from the same source in a single frame would
        /// otherwise overwrite each other and both launch from the wrong place.
        readonly Dictionary<string, Queue<Vector2>> _origins = new();

        /// The Source strings are ProgressionSystem's AwardXp source tags, so an XP burst can find the origin
        /// its triggering event recorded. "debug" is the cheat and deliberately has no entry here.
        const string SourceOrder = "order";
        const string SourceJob = "job";
        const string SourceBuild = "build";
        const string SourceDebug = "debug";

        readonly struct PendingBurst
        {
            public readonly string Kind;
            public readonly string ResourceId;
            public readonly int Amount;
            public readonly string Source; // which origin queue this burst draws its launch point from

            /// True when this burst may legitimately find its source queue empty. An order's XP rides the
            /// same event as its money, and the money burst has already taken the one "order" origin — the
            /// pointer is the correct launch point for both, so falling back to it is the design, not a
            /// papering-over. A money burst enqueues its own origin in the same handler that records it and
            /// so is held to the strict contract.
            public readonly bool PointerFallback;

            public PendingBurst(string kind, string resourceId, int amount, string source,
                bool pointerFallback = false)
            {
                Kind = kind; ResourceId = resourceId; Amount = amount; Source = source;
                PointerFallback = pointerFallback;
            }
        }

        public void Init(EventBus bus, IReadOnlyDictionary<string, Transform> stationRoots, Camera worldCamera,
            IReadOnlyList<ResourceSO> resources)
        {
            _bus = bus;
            _stationRoots = stationRoots;
            _worldCamera = worldCamera;
            foreach (var r in resources) _resourcesById[r.id] = r;
            _fxRect = (RectTransform)transform;
            _bus.Subscribe<OrderFulfilled>(OnOrderFulfilled);
            _bus.Subscribe<JobCollected>(OnJobCollected);
            _bus.Subscribe<StationBuilt>(OnStationBuilt);
            _bus.Subscribe<XpGained>(OnXpGained);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<OrderFulfilled>(OnOrderFulfilled);
            _bus.Unsubscribe<JobCollected>(OnJobCollected);
            _bus.Unsubscribe<StationBuilt>(OnStationBuilt);
            _bus.Unsubscribe<XpGained>(OnXpGained);
        }

        /// Records only. The payout burst is the one and only money trigger — hanging it off MoneyChanged
        /// instead would silently start throwing coins for level-up grants, which is the exclusion that
        /// defines this feature.
        void OnOrderFulfilled(OrderFulfilled e)
        {
            Enqueue(SourceOrder, PointerLocal());
            _bursts.Add(new PendingBurst(EarnKind.Money, null, e.Payout, SourceOrder));
        }

        /// The job's yield, one burst per output, plus the origin for the XP that ProgressionSystem is about
        /// to grant for the same collect.
        ///
        /// Every burst enqueues its OWN origin — a resource burst may not share the one recorded for the XP
        /// burst. Both launch from the same station, but OriginFor dequeues exactly one entry per burst, so
        /// sharing would leave a later burst staring at an empty queue.
        void OnJobCollected(JobCollected e)
        {
            var origin = StationScreenLocal(e.StationId);
            Enqueue(SourceJob, origin); // for the XP burst OnXpGained is about to record

            foreach (var output in e.Outputs)
            {
                Enqueue(SourceJob, origin);
                _bursts.Add(new PendingBurst(EarnKind.Resource, output.ResourceId, output.Amount, SourceJob));
            }
        }

        /// Origin only — finishing a build throws nothing of its own. Its XP grant launches from the new
        /// station.
        void OnStationBuilt(StationBuilt e) => Enqueue(SourceBuild, StationScreenLocal(e.StationId));

        /// The amount is the event's, never XpConfigSO's: AwardXp runs the grant through ValueResolver
        /// before publishing, so an XP-boosting upgrade would make the config and the event disagree. A
        /// resolved 0 never publishes at all, so no event means no burst.
        void OnXpGained(XpGained e)
        {
            if (e.Source == SourceDebug) return; // the level-up cheat pays no stars

            // Every recorded burst enqueues its OWN origin, and an order's XP burst is no exception: an
            // order is filled by tapping a button, so its stars launch from the finger, exactly like its
            // coins. Sharing the money burst's single "order" origin instead would be ORDER-DEPENDENT —
            // ProgressionSystem subscribes to OrderFulfilled before this controller does, so this handler
            // runs first and would drain the origin the money burst is about to need.
            //
            // A job's or a build's XP launches from the station, and OnJobCollected / OnStationBuilt have
            // already recorded that origin for it — enqueuing again here would break their pairing.
            if (e.Source == SourceOrder) Enqueue(SourceOrder, PointerLocal());

            _bursts.Add(new PendingBurst(EarnKind.Xp, null, e.Amount, e.Source,
                pointerFallback: true));
        }

        void Enqueue(string source, Vector2 local)
        {
            if (!_origins.TryGetValue(source, out var queue))
                _origins[source] = queue = new Queue<Vector2>();
            queue.Enqueue(local);
        }

        /// The frame's buffer is drained whatever happens. A burst that throws still surfaces its exception,
        /// but it must not survive into the next frame: leaving it queued would re-launch the whole set
        /// every frame forever, burying the one real error under a storm of duplicates.
        void LateUpdate()
        {
            try
            {
                for (int i = 0; i < _bursts.Count; i++)
                {
                    var burst = _bursts[i];
                    var chunks = EarnChunks.Split(burst.Amount, maxParticles);
                    _bus.Publish(new EarnBurstLaunched(burst.Kind, burst.ResourceId, burst.Amount));
                    StartCoroutine(SpawnBurst(burst, chunks, OriginFor(burst)));
                }
            }
            finally
            {
                _bursts.Clear();
                _origins.Clear(); // an origin with no burst behind it is stale by the next frame
            }
        }

        /// One dequeue per burst, in FIFO order. Two jobs collected in the same frame (a pet auto-collect)
        /// therefore pair with their own stations instead of both taking the last one recorded.
        Vector2 OriginFor(PendingBurst burst)
        {
            if (_origins.TryGetValue(burst.Source, out var queue) && queue.Count > 0)
                return queue.Dequeue();

            if (burst.PointerFallback) return PointerLocal();

            throw new System.InvalidOperationException(
                $"EarnBurstController: a '{burst.Source}' burst was recorded with no origin enqueued for it");
        }

        /// The station's world position, projected to this canvas. Overlay canvas, so the camera argument
        /// is null — only the WorldToScreenPoint step uses the world camera.
        Vector2 StationScreenLocal(string stationId)
        {
            if (!_stationRoots.TryGetValue(stationId, out var root))
                throw new System.InvalidOperationException(
                    $"EarnBurstController: no station root for id '{stationId}'");

            Vector2 screen = _worldCamera.WorldToScreenPoint(root.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_fxRect, screen, null, out var local);
            return local;
        }

        IEnumerator SpawnBurst(PendingBurst burst, int[] chunks, Vector2 origin)
        {
            var wait = new WaitForSeconds(staggerSeconds);
            var icon = IconFor(burst);
            var target = TargetFor(burst);

            for (int i = 0; i < chunks.Length; i++)
            {
                int amount = chunks[i]; // captured per particle — each arrival credits its own chunk
                var particle = Instantiate(earnParticlePrefab, _fxRect);
                particle.Launch(icon, origin, target, flight,
                    () => _bus.Publish(new EarnParticleArrived(burst.Kind, burst.ResourceId, amount)));

                if (i < chunks.Length - 1) yield return wait;
            }
        }

        Sprite IconFor(PendingBurst burst) => burst.Kind switch
        {
            EarnKind.Money => coinSprite,
            EarnKind.Xp => starSprite,
            EarnKind.Resource => ResourceFor(burst.ResourceId).icon,
            _ => throw new System.InvalidOperationException(
                $"EarnBurstController: no icon wired for burst kind '{burst.Kind}'"),
        };

        RectTransform TargetFor(PendingBurst burst) => burst.Kind switch
        {
            EarnKind.Money => moneyTarget,
            EarnKind.Xp => xpTarget,
            EarnKind.Resource => resourcePillRail.RectFor(burst.ResourceId),
            _ => throw new System.InvalidOperationException(
                $"EarnBurstController: no target wired for burst kind '{burst.Kind}'"),
        };

        ResourceSO ResourceFor(string resourceId)
        {
            if (!_resourcesById.TryGetValue(resourceId, out var resource))
                throw new System.InvalidOperationException(
                    $"EarnBurstController: no ResourceSO for id '{resourceId}'");
            return resource;
        }

        /// InputRouter ignores presses that start over UI, so it cannot supply this origin — an order is
        /// filled by tapping a button. Read the pointer directly. Overlay canvas, so the camera is null.
        Vector2 PointerLocal()
        {
            Vector2 screen = Pointer.current.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_fxRect, screen, null, out var local);
            return local;
        }
    }
}
