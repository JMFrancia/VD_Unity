namespace VoidDay.Core.Model
{
    /// A quantity of one resource — a recipe input, a recipe output, or a collected yield (§5.2).
    /// Resource is referenced by its string id (the Core currency); the SO/icon stays in the Data/View layer.
    public readonly struct ResourceAmount
    {
        public readonly string ResourceId;
        public readonly int Amount;

        public ResourceAmount(string resourceId, int amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }

        public override string ToString() => $"{Amount} {ResourceId}";
    }
}
