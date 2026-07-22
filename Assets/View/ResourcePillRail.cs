using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// The resources' HUD home — which they otherwise do not have. Collecting a job slides a pill out from
    /// BEHIND the money pill carrying that resource's icon and running total, catches the flying icons one at
    /// a time, then slides back behind the money pill and disappears.
    ///
    /// It retracts behind the money pill on purpose: that pill is the control the player taps to open the
    /// totals popup, so the motion teaches the affordance.
    ///
    /// Like Hud and LevelXpHud, this holds back what is still in the air: it draws
    /// ResourcePool.Get(id) MINUS the amount still flying, so the number climbs per arrival instead of
    /// jumping at the moment of the collect.
    ///
    /// No VerticalLayoutGroup, deliberately: pills slide in and out independently and a layout group would
    /// fight the slide tween every frame. The rail owns its own slot arithmetic.
    public sealed class ResourcePillRail : MonoBehaviour
    {
        [Header("Authored")]
        [Tooltip("Instantiated once per distinct resource in flight. Count is data; look is the prefab.")]
        [SerializeField] ResourcePill pillTemplate;

        [Tooltip("The money pill. Pills slide out from behind it and retract back into it.")]
        [SerializeField] RectTransform hidePoint;

        [Header("Slots")]
        [Tooltip("anchoredPosition.x of every slot. Matches the money and gem pills' right-edge inset.")]
        [SerializeField] float slotX = -24f;

        [Tooltip("anchoredPosition.y of the first slot. The money pill owns -24 and the gem pill -144, so " +
                 "the rail starts one row below them. Move this down if another permanent pill lands above.")]
        [SerializeField] float firstSlotOffset = -264f;

        [Tooltip("Vertical distance between stacked pills.")]
        [SerializeField] float slotPitch = 120f;

        [Header("Feel")]
        [Tooltip("Seconds a pill takes to slide out of, or back into, the money pill.")]
        [SerializeField] float slideSeconds = 0.28f;
        [SerializeField] Ease slideEase = Ease.OutBack;

        [Tooltip("Scale of a pill while it is still tucked behind the money pill.")]
        [SerializeField] float hiddenScale = 0.85f;

        [Tooltip("Seconds a pill lingers after its LAST icon lands. A fresh collect of the same resource " +
                 "restarts this rather than spawning a second pill.")]
        [SerializeField] float dwellSeconds = 1.4f;

        EventBus _bus;
        ResourcePool _pool;
        readonly Dictionary<string, ResourceSO> _byId = new();

        /// One live pill. Pending is what has launched but not yet landed — withheld from the displayed total.
        sealed class Slot
        {
            public string Id;
            public ResourcePill Pill;
            public int Pending;
            public float DwellRemaining;
            public bool Retracting;
        }

        readonly List<Slot> _slots = new();

        public void Init(EventBus bus, ResourcePool pool, IReadOnlyList<ResourceSO> resources)
        {
            _bus = bus;
            _pool = pool;
            foreach (var r in resources) _byId[r.id] = r;

            _bus.Subscribe<EarnBurstLaunched>(OnEarnBurstLaunched);
            _bus.Subscribe<EarnParticleArrived>(OnEarnParticleArrived);
            _bus.Subscribe<ResourceChanged>(OnResourceChanged);
            _bus.Subscribe<GameReset>(OnGameReset);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<EarnBurstLaunched>(OnEarnBurstLaunched);
            _bus.Unsubscribe<EarnParticleArrived>(OnEarnParticleArrived);
            _bus.Unsubscribe<ResourceChanged>(OnResourceChanged);
            _bus.Unsubscribe<GameReset>(OnGameReset);
        }

        /// The burst controller's aiming query, and its only contact with this view. Read-only — routing a
        /// live RectTransform through a Core event would break the Core boundary to satisfy the event rule.
        public RectTransform RectFor(string resourceId)
        {
            var slot = Find(resourceId);
            if (slot == null)
                throw new System.InvalidOperationException(
                    $"ResourcePillRail: no pill out for resource '{resourceId}' — a burst was aimed at it " +
                    "before EarnBurstLaunched created it");
            return slot.Pill.Rect;
        }

        void OnEarnBurstLaunched(EarnBurstLaunched e)
        {
            if (e.Kind != EarnKind.Resource) return;

            var slot = Find(e.ResourceId) ?? Revive(e.ResourceId) ?? SlideOut(e.ResourceId);
            slot.Pending += e.Amount;
            slot.DwellRemaining = dwellSeconds;
            Refresh(slot);
        }

        void OnEarnParticleArrived(EarnParticleArrived e)
        {
            if (e.Kind != EarnKind.Resource) return;

            var slot = Find(e.ResourceId);
            if (slot == null) return; // the pill was dropped by a reset while its icons were still in the air

            slot.Pending -= e.Amount;
            slot.DwellRemaining = dwellSeconds; // the dwell is measured from the LAST arrival
            Refresh(slot);
            slot.Pill.Pulse();
        }

        /// A spend elsewhere (an order consuming goods) must move the number under a pill that happens to be
        /// out. It never creates one — spends throw no particles.
        void OnResourceChanged(ResourceChanged e)
        {
            var slot = Find(e.ResourceId);
            if (slot != null) Refresh(slot);
        }

        void OnGameReset(GameReset _)
        {
            foreach (var slot in _slots) Destroy(slot.Pill.gameObject);
            _slots.Clear();
        }

        void Update()
        {
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                var slot = _slots[i];
                if (slot.Retracting) continue;
                if (slot.Pending > 0) continue; // icons still in the air — the dwell has not started

                slot.DwellRemaining -= Time.deltaTime;
                if (slot.DwellRemaining <= 0f) Retract(slot);
            }
        }

        Slot Find(string resourceId)
        {
            foreach (var slot in _slots)
                if (slot.Id == resourceId && !slot.Retracting) return slot;
            return null;
        }

        void Refresh(Slot slot) => slot.Pill.SetCount(_pool.Get(slot.Id) - slot.Pending);

        /// A second collect that lands while the pill is already sliding away catches it and pulls it back,
        /// rather than stacking a duplicate pill for the same resource behind the one leaving.
        Slot Revive(string resourceId)
        {
            Slot found = null;
            foreach (var slot in _slots)
                if (slot.Id == resourceId) { found = slot; break; }
            if (found == null) return null;

            found.Retracting = false;
            var rect = found.Pill.Rect;
            rect.DOKill();               // kills without firing the retract's OnComplete, so it is not destroyed
            found.Pill.Group.DOKill();
            rect.DOScale(1f, slideSeconds).SetEase(slideEase);
            found.Pill.Group.DOFade(1f, slideSeconds);
            TweenToSlot(found, _slots.IndexOf(found));
            return found;
        }

        Slot SlideOut(string resourceId)
        {
            if (!_byId.TryGetValue(resourceId, out var resource))
                throw new System.InvalidOperationException(
                    $"ResourcePillRail: no ResourceSO for id '{resourceId}'");

            var pill = Instantiate(pillTemplate, transform);
            pill.gameObject.SetActive(true);
            pill.name = $"ResourcePill_{resourceId}";
            pill.Bind(resource.icon, _pool.Get(resourceId));

            var rect = pill.Rect;
            rect.anchoredPosition = HiddenPosition();
            rect.localScale = Vector3.one * hiddenScale;
            pill.Group.alpha = 0f;

            var slot = new Slot { Id = resourceId, Pill = pill, DwellRemaining = dwellSeconds };
            _slots.Add(slot);

            TweenToSlot(slot, _slots.Count - 1);
            rect.DOScale(1f, slideSeconds).SetEase(slideEase);
            pill.Group.DOFade(1f, slideSeconds);
            return slot;
        }

        void Retract(Slot slot)
        {
            slot.Retracting = true;

            var rect = slot.Pill.Rect;
            rect.DOKill();
            slot.Pill.Group.DOKill();

            rect.DOAnchorPos(HiddenPosition(), slideSeconds).SetEase(slideEase);
            rect.DOScale(hiddenScale, slideSeconds).SetEase(slideEase);
            slot.Pill.Group.DOFade(0f, slideSeconds).OnComplete(() =>
            {
                _slots.Remove(slot);
                Destroy(slot.Pill.gameObject);
                Reflow();
            });
        }

        /// The survivors close the gap left by a retracted pill.
        void Reflow()
        {
            for (int i = 0; i < _slots.Count; i++)
                if (!_slots[i].Retracting) TweenToSlot(_slots[i], i);
        }

        void TweenToSlot(Slot slot, int index)
        {
            var rect = slot.Pill.Rect;
            rect.DOAnchorPos(new Vector2(slotX, firstSlotOffset - index * slotPitch), slideSeconds)
                .SetEase(slideEase);
        }

        /// The money pill's own anchored position, expressed in this rail's rect. Both share HudCanvas and
        /// the same top-right anchor, so the pill's anchoredPosition is directly reusable.
        Vector2 HiddenPosition() => hidePoint.anchoredPosition;
    }
}
