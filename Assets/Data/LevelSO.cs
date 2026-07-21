using System;
using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Model;

namespace VoidDay.Data
{
    /// The level curve (§9, §14 `levels.json`) — one ordered set rather than one asset per level, because
    /// the interesting edit is almost always *the shape of the curve*, and 20 separate assets hide it.
    ///
    /// A level's number is its position in the list: entry 0 is level 1, the starting level. Thresholds are
    /// explicit lifetime-XP totals, never a formula (§9).
    ///
    /// A level grants only what it *hands out*. What it *opens up* — a station type, an upgrade track — lives
    /// on the gated asset's own `unlockLevel`, so a gate has exactly one home; boot validation rejects a
    /// StationType/Upgrade grant authored here.
    [CreateAssetMenu(menuName = "VoidDay/Level Curve", fileName = "Levels")]
    public sealed class LevelSO : ScriptableObject
    {
        [Tooltip("Ordered, level 1 first. Level 1 is the starting level: threshold 0, no grants.")]
        public List<LevelDef> levels = new();

        [Serializable]
        public sealed class LevelDef
        {
            [Tooltip("Lifetime XP required to reach this level. Must rise with every entry.")]
            public int xpThreshold;

            [Tooltip("What this level hands out. Empty is fine — the level still fires its popup.")]
            public List<LevelGrant> grants = new();
        }

        [Serializable]
        public sealed class LevelGrant
        {
            [Tooltip("What is being granted. StationType/Upgrade are NOT authorable — those gates live on the "
                + "StationSO / UpgradeSO themselves.")]
            public LevelEntryKind kind = LevelEntryKind.Money;

            [Tooltip("Which station type the grant applies to, for StationCap / QueueDepth. LEAVE EMPTY to "
                + "apply to every station type. Ignored by OrderSlots and Money.")]
            public StationSO targetStation;

            [Tooltip("How much: +N cap / +N queue depth / +N order slots / N money.")]
            public int amount = 1;
        }
    }
}
