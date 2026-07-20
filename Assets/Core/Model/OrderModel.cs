using System.Collections.Generic;

namespace VoidDay.Core.Model
{
    /// One generated order (§6). Immutable once generated: the requested goods and the payout are fixed at
    /// generation time, so a level-up or an effect that lands later never silently re-prices a card the
    /// player is already looking at. Orders never expire — the only removals are fulfill and skip.
    public sealed class OrderModel
    {
        public readonly string Id;
        public readonly IReadOnlyList<ResourceAmount> Requests;
        public readonly int Cash;
        public readonly int Xp;

        public OrderModel(string id, IReadOnlyList<ResourceAmount> requests, int cash, int xp)
        {
            Id = id;
            Requests = requests;
            Cash = cash;
            Xp = xp;
        }
    }
}
