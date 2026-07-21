namespace VoidDay.Core.Model
{
    /// A station-upgrade track projected from UpgradeSO (§8, §14). Tiered, with the cost of each tier explicit
    /// (no formula, §8). Buying a tier ADDS that tier's effects to the station's active set — so a track of
    /// three "+25% speed" tiers stacks to +75% via the resolver's additive Pct rule (§3.5), which is exactly
    /// what the "two +25% = +50%, not ×1.5625" requirement wants.
    ///
    /// Effects cross the boundary unprojected (§14): the model holds the pure-C# Effect[] straight off the SO.
    public sealed class UpgradeTrackModel
    {
        public readonly string Id;
        public readonly string DisplayName;
        /// Player level at which the track becomes purchasable (§9 "upgrades become purchasable"). This is the
        /// track's ONE home for its gate — a level asset never restates it.
        public readonly int UnlockLevel;
        public readonly UpgradeTierModel[] Tiers;

        public UpgradeTrackModel(string id, string displayName, int unlockLevel, UpgradeTierModel[] tiers)
        {
            Id = id;
            DisplayName = displayName;
            UnlockLevel = unlockLevel;
            Tiers = tiers;
        }

        public int MaxTier => Tiers.Length;
    }

    public sealed class UpgradeTierModel
    {
        public readonly int Cost;
        public readonly Effect[] Effects;

        public UpgradeTierModel(int cost, Effect[] effects)
        {
            Cost = cost;
            Effects = effects;
        }
    }
}
