using System;
using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Data;

namespace VoidDay.View
{
    /// The game's ear. It listens to the bus and plays the clip the SfxLibrary assigns to each moment —
    /// nothing else. No system ever asks for a sound (CLAUDE.md rule 2): emitters announce what happened and
    /// this decides, alone, whether that is audible.
    ///
    /// Lives on AudioManager beside the pooled SFXManager it plays through. Every tunable (clip, volume,
    /// pitch spread, retrigger throttle) is on the library asset; the only thing that lives here is the
    /// event → cue mapping, which is routing, not data.
    public sealed class SfxController : MonoBehaviour
    {
        [SerializeField] SfxLibrarySO library;
        [SerializeField] SFXManager sfx;

        EventBus _bus;
        readonly Dictionary<SfxCue, SfxLibrarySO.Entry> _byCue = new();
        readonly Dictionary<SfxCue, float> _lastPlayed = new();

        public void Init(EventBus bus)
        {
            if (library == null)
                throw new InvalidOperationException($"SfxController on '{name}': {nameof(library)} is not assigned");
            if (sfx == null)
                throw new InvalidOperationException($"SfxController on '{name}': {nameof(sfx)} is not assigned");

            _bus = bus;
            foreach (var entry in library.entries) _byCue[entry.cue] = entry;
            foreach (SfxCue cue in Enum.GetValues(typeof(SfxCue)))
                if (!_byCue.ContainsKey(cue))
                    throw new InvalidOperationException(
                        $"SfxController: '{library.name}' has no row for cue {cue} — select the asset to resync its rows");

            // Jobs
            _bus.Subscribe<JobQueued>(OnJobQueued);
            _bus.Subscribe<JobStarted>(OnJobStarted);
            _bus.Subscribe<JobCompleted>(OnJobCompleted);
            _bus.Subscribe<JobCollected>(OnJobCollected);
            _bus.Subscribe<JobCancelled>(OnJobCancelled);
            _bus.Subscribe<StationBlocked>(OnStationBlocked);
            _bus.Subscribe<StorageFull>(OnStorageFull);
            _bus.Subscribe<CollectRefused>(OnCollectRefused);

            // Orders
            _bus.Subscribe<OrderFulfilled>(OnOrderFulfilled);
            _bus.Subscribe<OrderSkipped>(OnOrderSkipped);
            _bus.Subscribe<OrderSlotRefilled>(OnOrderSlotRefilled);

            // Build & manage
            _bus.Subscribe<StationPickedUp>(OnStationPickedUp);
            _bus.Subscribe<StationConstructionStarted>(OnConstructionStarted);
            _bus.Subscribe<StationBuilt>(OnStationBuilt);
            _bus.Subscribe<StationMoved>(OnStationMoved);
            _bus.Subscribe<StationDemolished>(OnStationDemolished);
            _bus.Subscribe<PlaceRejected>(OnPlaceRejected);

            // Economy
            _bus.Subscribe<UpgradePurchased>(OnUpgradePurchased);
            _bus.Subscribe<MoneyChanged>(OnMoneyChanged);
            _bus.Subscribe<XpGained>(OnXpGained);
            _bus.Subscribe<LevelUp>(OnLevelUp);
            _bus.Subscribe<EarnParticleArrived>(OnEarnParticleArrived);

            // UI
            _bus.Subscribe<ExclusiveUiOpened>(OnUiOpened);
            _bus.Subscribe<ExclusiveUiClosed>(OnUiClosed);
            _bus.Subscribe<UiTapped>(OnUiTapped);
        }

        void OnDestroy()
        {
            if (_bus == null) return;

            _bus.Unsubscribe<JobQueued>(OnJobQueued);
            _bus.Unsubscribe<JobStarted>(OnJobStarted);
            _bus.Unsubscribe<JobCompleted>(OnJobCompleted);
            _bus.Unsubscribe<JobCollected>(OnJobCollected);
            _bus.Unsubscribe<JobCancelled>(OnJobCancelled);
            _bus.Unsubscribe<StationBlocked>(OnStationBlocked);
            _bus.Unsubscribe<StorageFull>(OnStorageFull);
            _bus.Unsubscribe<CollectRefused>(OnCollectRefused);

            _bus.Unsubscribe<OrderFulfilled>(OnOrderFulfilled);
            _bus.Unsubscribe<OrderSkipped>(OnOrderSkipped);
            _bus.Unsubscribe<OrderSlotRefilled>(OnOrderSlotRefilled);

            _bus.Unsubscribe<StationPickedUp>(OnStationPickedUp);
            _bus.Unsubscribe<StationConstructionStarted>(OnConstructionStarted);
            _bus.Unsubscribe<StationBuilt>(OnStationBuilt);
            _bus.Unsubscribe<StationMoved>(OnStationMoved);
            _bus.Unsubscribe<StationDemolished>(OnStationDemolished);
            _bus.Unsubscribe<PlaceRejected>(OnPlaceRejected);

            _bus.Unsubscribe<UpgradePurchased>(OnUpgradePurchased);
            _bus.Unsubscribe<MoneyChanged>(OnMoneyChanged);
            _bus.Unsubscribe<XpGained>(OnXpGained);
            _bus.Unsubscribe<LevelUp>(OnLevelUp);
            _bus.Unsubscribe<EarnParticleArrived>(OnEarnParticleArrived);

            _bus.Unsubscribe<ExclusiveUiOpened>(OnUiOpened);
            _bus.Unsubscribe<ExclusiveUiClosed>(OnUiClosed);
            _bus.Unsubscribe<UiTapped>(OnUiTapped);
        }

        // ---- Event → cue. One line each; the interesting decisions are all on the library asset. ----

        void OnJobQueued(JobQueued _) => Play(SfxCue.JobQueued);
        void OnJobStarted(JobStarted _) => Play(SfxCue.JobStarted);
        void OnJobCompleted(JobCompleted _) => Play(SfxCue.JobCompleted);
        void OnJobCollected(JobCollected _) => Play(SfxCue.JobCollected);
        void OnJobCancelled(JobCancelled _) => Play(SfxCue.JobCancelled);
        void OnStationBlocked(StationBlocked _) => Play(SfxCue.StationBlocked);
        void OnStorageFull(StorageFull _) => Play(SfxCue.StorageFull);
        void OnCollectRefused(CollectRefused _) => Play(SfxCue.CollectRefused);

        void OnOrderFulfilled(OrderFulfilled _) => Play(SfxCue.OrderFulfilled);
        void OnOrderSkipped(OrderSkipped _) => Play(SfxCue.OrderSkipped);
        void OnOrderSlotRefilled(OrderSlotRefilled _) => Play(SfxCue.OrderRefilled);

        void OnStationPickedUp(StationPickedUp _) => Play(SfxCue.StationPickedUp);
        void OnConstructionStarted(StationConstructionStarted _) => Play(SfxCue.StationConstructionStarted);

        /// Fires at COMPLETION now that building takes time — which is exactly when a "done!" sound belongs,
        /// so this mapping did not have to move.
        void OnStationBuilt(StationBuilt _) => Play(SfxCue.StationBuilt);
        void OnStationMoved(StationMoved _) => Play(SfxCue.StationMoved);
        void OnStationDemolished(StationDemolished _) => Play(SfxCue.StationDemolished);
        void OnPlaceRejected(PlaceRejected _) => Play(SfxCue.PlaceRejected);

        void OnUpgradePurchased(UpgradePurchased _) => Play(SfxCue.UpgradePurchased);
        void OnXpGained(XpGained _) => Play(SfxCue.XpGained);
        void OnLevelUp(LevelUp _) => Play(SfxCue.LevelUp);

        /// Only the debit half is a cue. Income already has a voice — the order chime, which fires on the
        /// same frame — and doubling it would just muddy the payout.
        void OnMoneyChanged(MoneyChanged e) { if (e.Delta < 0) Play(SfxCue.MoneySpent); }

        /// One tick per particle landing. The umbrella cue for the same moment (the order chime, the XP tick)
        /// deliberately keeps firing — the stream layers on top of it rather than replacing it.
        void OnEarnParticleArrived(EarnParticleArrived e) => Play(e.Kind switch
        {
            EarnKind.Money => SfxCue.EarnParticleMoney,
            EarnKind.Xp => SfxCue.EarnParticleXp,
            EarnKind.Resource => SfxCue.EarnParticleResource,
            _ => throw new InvalidOperationException($"SfxController: unknown earn kind '{e.Kind}'"),
        });

        void OnUiOpened(ExclusiveUiOpened _) => Play(SfxCue.UiOpen);
        void OnUiClosed(ExclusiveUiClosed _) => Play(SfxCue.UiClose);
        void OnUiTapped(UiTapped _) => Play(SfxCue.UiTap);

        void Play(SfxCue cue)
        {
            var entry = _byCue[cue];
            if (entry.clip == null) return; // an unassigned cue is silent by design, not a missing reference

            if (entry.minInterval > 0f && _lastPlayed.TryGetValue(cue, out float last)
                && Time.unscaledTime - last < entry.minInterval) return;
            _lastPlayed[cue] = Time.unscaledTime;

            float pitch = entry.pitchJitter <= 0f
                ? 1f
                : UnityEngine.Random.Range(1f - entry.pitchJitter, 1f + entry.pitchJitter);
            sfx.PlaySFX(entry.clip, entry.volume, pitch);
        }
    }
}
