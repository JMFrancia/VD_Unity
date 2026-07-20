namespace VoidDay.Core.Model
{
    /// Rule-relevant projection of a ResourceSO (§14). No Unity handles, no icon/mesh refs —
    /// those stay on the SO. Economic fields land when a rule needs them: M3's order generation reads
    /// Sellable (wheat's exclusion, §6.1) and Tier (level weighting), pricing reads BaseValue (§6).
    public sealed class ResourceModel
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly int BaseValue;

        /// False = never sold as-is. This is the ONE home of wheat's exclusion from the order pool (§6.1) —
        /// generation reads this flag; there is deliberately no separate exclusion list anywhere.
        public readonly bool Sellable;

        /// Production depth (raw = 1, processed = 2+). Order generation weights toward higher tiers as the
        /// player level rises (§6.1).
        public readonly int Tier;

        public ResourceModel(string id, string displayName, int baseValue, bool sellable, int tier)
        {
            Id = id;
            DisplayName = displayName;
            BaseValue = baseValue;
            Sellable = sellable;
            Tier = tier;
        }
    }
}
