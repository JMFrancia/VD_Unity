using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;

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

        EventBus _bus;
        RectTransform _fxRect;

        readonly List<PendingBurst> _bursts = new();

        /// One origin QUEUE per source, not one slot: two earns from the same source in a single frame would
        /// otherwise overwrite each other and both launch from the wrong place.
        readonly Dictionary<string, Queue<Vector2>> _origins = new();

        const string SourceOrder = "order";

        readonly struct PendingBurst
        {
            public readonly string Kind;
            public readonly string ResourceId;
            public readonly int Amount;
            public readonly string Source; // which origin queue this burst draws its launch point from
            public PendingBurst(string kind, string resourceId, int amount, string source)
            { Kind = kind; ResourceId = resourceId; Amount = amount; Source = source; }
        }

        public void Init(EventBus bus)
        {
            _bus = bus;
            _fxRect = (RectTransform)transform;
            _bus.Subscribe<OrderFulfilled>(OnOrderFulfilled);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<OrderFulfilled>(OnOrderFulfilled);
        }

        /// Records only. The payout burst is the one and only money trigger — hanging it off MoneyChanged
        /// instead would silently start throwing coins for level-up grants, which is the exclusion that
        /// defines this feature.
        void OnOrderFulfilled(OrderFulfilled e)
        {
            Enqueue(SourceOrder, PointerLocal());
            _bursts.Add(new PendingBurst(EarnKind.Money, null, e.Payout, SourceOrder));
        }

        void Enqueue(string source, Vector2 local)
        {
            if (!_origins.TryGetValue(source, out var queue))
                _origins[source] = queue = new Queue<Vector2>();
            queue.Enqueue(local);
        }

        void LateUpdate()
        {
            for (int i = 0; i < _bursts.Count; i++)
            {
                var burst = _bursts[i];
                var chunks = EarnChunks.Split(burst.Amount, maxParticles);
                _bus.Publish(new EarnBurstLaunched(burst.Kind, burst.ResourceId, burst.Amount));
                StartCoroutine(SpawnBurst(burst, chunks, Dequeue(burst.Source)));
            }

            _bursts.Clear();
            _origins.Clear(); // an origin with no burst behind it is stale by the next frame
        }

        Vector2 Dequeue(string source)
        {
            if (!_origins.TryGetValue(source, out var queue) || queue.Count == 0)
                throw new System.InvalidOperationException(
                    $"EarnBurstController: a '{source}' burst was recorded with no origin enqueued for it");
            return queue.Dequeue();
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
            _ => throw new System.InvalidOperationException(
                $"EarnBurstController: no icon wired for burst kind '{burst.Kind}'"),
        };

        RectTransform TargetFor(PendingBurst burst) => burst.Kind switch
        {
            EarnKind.Money => moneyTarget,
            _ => throw new System.InvalidOperationException(
                $"EarnBurstController: no target wired for burst kind '{burst.Kind}'"),
        };

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
