using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoidDay.Data
{
    /// Every moment in the game that can make a sound. One entry per value in SfxLibrarySO, one listener per
    /// value in SfxController — adding a cue means adding it here, mapping an event to it there, and dropping
    /// a clip on it in the inspector.
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

        // Orders
        OrderFulfilled,
        OrderSkipped,
        OrderRefilled,

        // Build & manage
        StationPickedUp,
        StationBuilt,
        StationMoved,
        StationDemolished,
        PlaceRejected,

        // Economy
        UpgradePurchased,
        MoneySpent,
        XpGained,

        // UI
        UiOpen,
        UiClose,
        UiTap,
    }

    /// The one place a clip is assigned to a moment. The controller holds no clip and no volume — everything
    /// tunable about a cue lives on this asset (CLAUDE.md rule 1); the controller only knows which event maps
    /// to which cue.
    ///
    /// A cue with no clip is SILENT, not an error: the set is authored incrementally, and "this moment has no
    /// sound" is a legitimate designer answer. That is the one deliberate exception to fail-loud validation.
    [CreateAssetMenu(menuName = "VoidDay/SFX Library", fileName = "SfxLibrary")]
    public sealed class SfxLibrarySO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("The moment this clip plays on. Rows are kept in sync with the SfxCue enum automatically.")]
            public SfxCue cue;

            public AudioClip clip;

            [Range(0f, 1f)] public float volume = 1f;

            [Tooltip("Random pitch spread per play, ± this fraction. 0 = every play sounds identical.")]
            [Range(0f, 0.5f)] public float pitchJitter = 0.05f;

            [Tooltip("Seconds this cue must wait before it can retrigger. Guards the bursty ones (a collect " +
                     "that yields several goods, an XP tick). 0 = no throttle.")]
            [Min(0f)] public float minInterval;
        }

        public List<Entry> entries = new();

        /// Keeps exactly one row per cue, in enum order, without ever touching an assignment you have made.
        /// The alternative — hand-adding 21 rows and hoping none is duplicated or missed — is a worse
        /// inspector, and this is the asset's whole reason to exist.
        void OnValidate() => SyncRows();

        public void SyncRows()
        {
            var byCue = new Dictionary<SfxCue, Entry>();
            foreach (var entry in entries)
                if (entry != null && !byCue.ContainsKey(entry.cue)) byCue[entry.cue] = entry;

            var synced = new List<Entry>();
            foreach (SfxCue cue in Enum.GetValues(typeof(SfxCue)))
                synced.Add(byCue.TryGetValue(cue, out var existing) ? existing : new Entry { cue = cue });

            entries = synced;
        }
    }
}
