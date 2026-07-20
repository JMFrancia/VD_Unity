namespace VoidDay.Core.Model
{
    /// Rule-relevant projection of OrderConfigSO (§14). Every number the order board and its generation read
    /// lives here — nothing about orders is a literal in a rule.
    public sealed class OrderConfigModel
    {
        public readonly int SlotCount;
        public readonly float RefillSeconds;

        public readonly int MinRequestKinds;
        public readonly int MaxRequestKinds;

        /// Largest quantity a single good can be requested at, at level 1. Grows by QuantityPerLevel.
        public readonly float MaxQuantityAtLevel1;
        public readonly float MaxQuantityPerLevel;

        public readonly float CashMultiplier;
        public readonly float XpMultiplier;

        /// Selection weight for a candidate good is TierWeightBase + TierWeightPerLevel × (level−1) × tier.
        /// At level 1 every candidate weighs the same; as the level rises the higher tiers pull ahead (§6.1).
        public readonly float TierWeightBase;
        public readonly float TierWeightPerLevel;

        public OrderConfigModel(int slotCount, float refillSeconds, int minRequestKinds, int maxRequestKinds,
            float maxQuantityAtLevel1, float maxQuantityPerLevel, float cashMultiplier, float xpMultiplier,
            float tierWeightBase, float tierWeightPerLevel)
        {
            SlotCount = slotCount;
            RefillSeconds = refillSeconds;
            MinRequestKinds = minRequestKinds;
            MaxRequestKinds = maxRequestKinds;
            MaxQuantityAtLevel1 = maxQuantityAtLevel1;
            MaxQuantityPerLevel = maxQuantityPerLevel;
            CashMultiplier = cashMultiplier;
            XpMultiplier = xpMultiplier;
            TierWeightBase = tierWeightBase;
            TierWeightPerLevel = tierWeightPerLevel;
        }
    }
}
