using System.Collections.Generic;

namespace VoidDay.Core.Model
{
    /// What one line of a level-up is *about* (§9). One vocabulary serves both halves of the feature: the
    /// grant list authored on a level, and the level:up payload the popup renders.
    ///
    /// StationType and Upgrade are **derived, never authored on a level**: a station type's gate lives on its
    /// StationSO.unlockLevel and an upgrade track's on its UpgradeSO.unlockLevel, so putting them on the level
    /// asset too would give one fact two homes. Boot validation rejects them in an authored grant list.
    public enum LevelEntryKind
    {
        StationType,   // derived — a station type became buildable
        Upgrade,       // derived — an upgrade track became purchasable
        StationCap,    // standing bonus, folded into the value seam
        QueueDepth,    // standing bonus, folded into the value seam
        OrderSlots,    // standing bonus, folded into the value seam
        Money,         // one-shot reward, paid the moment the level lands
        Gems           // one-shot reward, paid the moment the level lands — never a standing bonus
    }

    /// One thing a level hands out (§9, §14). TargetId names the station type for the per-type kinds; an
    /// EMPTY TargetId means "every station type" (that is how a level raises queue depth farm-wide).
    public sealed class LevelGrantModel
    {
        public readonly LevelEntryKind Kind;
        public readonly string TargetId;
        public readonly string TargetLabel; // player-facing name of the target, for the popup line
        public readonly int Amount;

        public LevelGrantModel(LevelEntryKind kind, string targetId, string targetLabel, int amount)
        {
            Kind = kind;
            TargetId = targetId ?? "";
            TargetLabel = targetLabel ?? "";
            Amount = amount;
        }
    }

    /// One row of the level table (§9) — an explicit XP threshold, no formula. Level 1 is the starting level
    /// and is never *crossed*, so it carries a 0 threshold and no grants.
    public sealed class LevelModel
    {
        public readonly int Level;
        public readonly int XpThreshold; // lifetime XP required to BE this level
        public readonly IReadOnlyList<LevelGrantModel> Grants;

        public LevelModel(int level, int xpThreshold, IReadOnlyList<LevelGrantModel> grants)
        {
            Level = level;
            XpThreshold = xpThreshold;
            Grants = grants;
        }
    }

    /// A thing gated behind a level by its OWN asset — a station type (StationSO.unlockLevel) or an upgrade
    /// track (UpgradeSO.unlockLevel). Projected at boot so Progression can announce what a level opened up
    /// without the level asset restating the gate.
    public sealed class LevelUnlockModel
    {
        public readonly LevelEntryKind Kind;
        public readonly string Id;
        public readonly string Label;
        public readonly int UnlockLevel;

        public LevelUnlockModel(LevelEntryKind kind, string id, string label, int unlockLevel)
        {
            Kind = kind;
            Id = id;
            Label = label;
            UnlockLevel = unlockLevel;
        }
    }

    /// One line of a level:up payload. Structured, not a sentence — the popup owns the wording (its copy is
    /// serialized on the View), Core owns the facts.
    public readonly struct LevelEntry
    {
        public readonly LevelEntryKind Kind;
        public readonly string Id;     // station type / upgrade track id / "" for the untargeted kinds
        public readonly string Label;  // player-facing name of the subject
        public readonly int Amount;    // 0 for a pure unlock

        public LevelEntry(LevelEntryKind kind, string id, string label, int amount)
        {
            Kind = kind;
            Id = id;
            Label = label;
            Amount = amount;
        }
    }
}
