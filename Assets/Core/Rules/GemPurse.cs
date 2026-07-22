using VoidDay.Core.Events;

namespace VoidDay.Core.Rules
{
    /// The player's gems — the premium currency whose only sink is finishing a running timer early.
    ///
    /// A deliberate sibling of Wallet rather than a shared base class: two currencies is the second
    /// occurrence, not the third. It is a sibling in spirit, not in shape — gems have exactly one spender,
    /// so the affordability check belongs with the purse (Wallet leaves it to its callers), and Reset takes
    /// the starting amount because a run starts with gems in hand where it starts with no money.
    public sealed class GemPurse
    {
        private readonly EventBus _bus;
        public int Gems { get; private set; }

        public GemPurse(EventBus bus, int startingGems)
        {
            _bus = bus;
            Gems = startingGems;
        }

        public void Add(int delta)
        {
            Gems += delta;
            _bus.Publish(new GemsChanged(delta, Gems));
        }

        public bool CanAfford(int cost) => Gems >= cost;

        /// Fails loud rather than clamping: a spend the purse can't cover means the caller skipped the
        /// affordability check, and silently zeroing the balance would hide that forever.
        public void Spend(int cost)
        {
            if (!CanAfford(cost))
                throw new System.InvalidOperationException(
                    $"Cannot spend {cost} gems — the purse holds {Gems}");
            Add(-cost);
        }

        public void EmitCurrent() => _bus.Publish(new GemsChanged(0, Gems));

        public void Reset(int startingGems)
        {
            if (Gems != startingGems) Add(startingGems - Gems);
        }
    }
}
