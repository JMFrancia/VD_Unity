using System;
using UnityEngine;
using VoidDay.Core.Model;

namespace VoidDay.Data
{
    /// One asset per upgrade *track* (§8, §14 `upgrades.json`). A track is tiered, with each tier's money cost
    /// listed explicitly (no formula, §8) and the Effect[] it grants. Buying a tier ADDS its effects to the
    /// station's active set, so authoring three "+25% speed" tiers stacks additively to +75% via the resolver
    /// (§3.5) — do NOT author tier N as the cumulative total.
    ///
    /// The effects are pure-C# Core types (§3.1) authored directly in the inspector — the one model that
    /// crosses into Core unprojected (§14). Referenced from StationSO.upgrades.
    [CreateAssetMenu(menuName = "VoidDay/Upgrade Track", fileName = "Upgrade")]
    public sealed class UpgradeSO : ScriptableObject
    {
        [Tooltip("Track id, unique per station type — e.g. 'field.speed'. Used by input:upgradePurchaseRequested.")]
        public string id;

        [Tooltip("Player-facing track name — e.g. 'Job Speed'. Named in the level-up popup when it unlocks.")]
        public string displayName;

        [Tooltip("Player level at which this track becomes purchasable (§9). 1 = buyable from the start. "
            + "This is the track's only home for its gate — a level asset never restates it.")]
        public int unlockLevel = 1;

        public UpgradeTier[] tiers = Array.Empty<UpgradeTier>();

        [Serializable]
        public struct UpgradeTier
        {
            [Tooltip("Money cost of THIS tier (explicit per-tier, no formula, §8).")]
            public int cost;

            [Tooltip("Effects this tier grants. Additive with earlier tiers (§3.5). Own-station passive for M5.")]
            public Effect[] effects;
        }
    }
}
