using System;
using UnityEngine;

namespace VoidDay.Data
{
    /// Every moment in the game that can make a sound. The controller routes on these values; the library
    /// holds one field per value. Adding a cue means adding it here, adding a matching field + Get case in
    /// SfxLibrarySO, and mapping an event to it in SfxController.
    public enum SfxCue
    {
        // Jobs — the core loop
        JobQueued,
        JobStarted,
        JobCompleted,
        JobCollected,   // the hero pop
        JobCancelled,
        StationBlocked,
        StorageFull,
        CollectRefused, // tried to collect with a full silo — the "no" beat

        // Orders
        OrderFulfilled,
        OrderSkipped,
        OrderRefilled,

        // Build & manage
        StationPickedUp,
        StationConstructionStarted, // the thunk of a build site going down; StationBuilt is its completion
        StationBuilt,
        StationMoved,
        StationDemolished,
        PlaceRejected,

        // Economy
        UpgradePurchased,
        MoneySpent,
        XpGained,
        LevelUp,

        // One tick per collection particle landing in its HUD home. These layer ON TOP of the umbrella cue
        // for the same moment (OrderFulfilled / XpGained / JobCollected) — the umbrella keeps its voice and
        // the stream adds the patter.
        EarnParticleMoney,
        EarnParticleXp,
        EarnParticleResource,

        // UI
        UiOpen,
        UiClose,
        UiTap,
    }

    /// The one place a clip is assigned to a moment. Each cue has its own named field, so a cue can never be
    /// duplicated or accidentally missing (the failure modes of the old list). The controller holds no clip
    /// and no volume — everything tunable about a cue lives on this asset (CLAUDE.md rule 1); the controller
    /// only knows which event maps to which cue.
    ///
    /// A cue with no clip is SILENT, not an error: the set is authored incrementally, and "this moment has no
    /// sound" is a legitimate designer answer. That is the one deliberate exception to fail-loud validation.
    [CreateAssetMenu(menuName = "VoidDay/SFX Library", fileName = "SfxLibrary")]
    public sealed class SfxLibrarySO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public AudioClip clip;

            [Range(0f, 1f)] public float volume = 1f;

            [Tooltip("Random pitch spread per play, ± this fraction. 0 = every play sounds identical.")]
            [Range(0f, 0.5f)] public float pitchJitter = 0.05f;

            [Tooltip("Seconds this cue must wait before it can retrigger. Guards the bursty ones (a collect " +
                     "that yields several goods, an XP tick). 0 = no throttle.")]
            [Min(0f)] public float minInterval;
        }

        [Header("Jobs — the core loop")]
        [SerializeField] Entry jobQueued = new();
        [SerializeField] Entry jobStarted = new();
        [SerializeField] Entry jobCompleted = new();
        [SerializeField] Entry jobCollected = new();
        [SerializeField] Entry jobCancelled = new();
        [SerializeField] Entry stationBlocked = new();
        [SerializeField] Entry storageFull = new();
        [SerializeField] Entry collectRefused = new();

        [Header("Orders")]
        [SerializeField] Entry orderFulfilled = new();
        [SerializeField] Entry orderSkipped = new();
        [SerializeField] Entry orderRefilled = new();

        [Header("Build & manage")]
        [SerializeField] Entry stationPickedUp = new();
        [SerializeField] Entry stationConstructionStarted = new();
        [SerializeField] Entry stationBuilt = new();
        [SerializeField] Entry stationMoved = new();
        [SerializeField] Entry stationDemolished = new();
        [SerializeField] Entry placeRejected = new();

        [Header("Economy")]
        [SerializeField] Entry upgradePurchased = new();
        [SerializeField] Entry moneySpent = new();
        [SerializeField] Entry xpGained = new();
        [SerializeField] Entry levelUp = new();

        [Header("Earn particles")]
        [SerializeField] Entry earnParticleMoney = new();
        [SerializeField] Entry earnParticleXp = new();
        [SerializeField] Entry earnParticleResource = new();

        [Header("UI")]
        [SerializeField] Entry uiOpen = new();
        [SerializeField] Entry uiClose = new();
        [SerializeField] Entry uiTap = new();

        /// Maps a cue to its assigned entry. Throws on an unmapped cue so a new enum value without a matching
        /// field fails loud at boot rather than going silently mute.
        public Entry Get(SfxCue cue) => cue switch
        {
            SfxCue.JobQueued => jobQueued,
            SfxCue.JobStarted => jobStarted,
            SfxCue.JobCompleted => jobCompleted,
            SfxCue.JobCollected => jobCollected,
            SfxCue.JobCancelled => jobCancelled,
            SfxCue.StationBlocked => stationBlocked,
            SfxCue.StorageFull => storageFull,
            SfxCue.CollectRefused => collectRefused,

            SfxCue.OrderFulfilled => orderFulfilled,
            SfxCue.OrderSkipped => orderSkipped,
            SfxCue.OrderRefilled => orderRefilled,

            SfxCue.StationPickedUp => stationPickedUp,
            SfxCue.StationConstructionStarted => stationConstructionStarted,
            SfxCue.StationBuilt => stationBuilt,
            SfxCue.StationMoved => stationMoved,
            SfxCue.StationDemolished => stationDemolished,
            SfxCue.PlaceRejected => placeRejected,

            SfxCue.UpgradePurchased => upgradePurchased,
            SfxCue.MoneySpent => moneySpent,
            SfxCue.XpGained => xpGained,
            SfxCue.LevelUp => levelUp,

            SfxCue.EarnParticleMoney => earnParticleMoney,
            SfxCue.EarnParticleXp => earnParticleXp,
            SfxCue.EarnParticleResource => earnParticleResource,

            SfxCue.UiOpen => uiOpen,
            SfxCue.UiClose => uiClose,
            SfxCue.UiTap => uiTap,

            _ => throw new InvalidOperationException($"SfxLibrary '{name}': no entry field for cue {cue}"),
        };
    }
}
