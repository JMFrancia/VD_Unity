using System;
using System.Collections.Generic;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// Order payout (§6): cash and XP are both derived from the requested ingredients' base values × a
    /// multiplier. Pure C#, no randomness — the same request always prices the same, which is what makes it
    /// testable and what lets M6's order.payout effect ride the seam without touching this math.
    public sealed class OrderPricing
    {
        private readonly IReadOnlyDictionary<string, ResourceModel> _resources;
        private readonly OrderConfigModel _config;
        private readonly ValueResolver _resolver;

        public OrderPricing(IReadOnlyDictionary<string, ResourceModel> resources, OrderConfigModel config,
            ValueResolver resolver)
        {
            _resources = resources;
            _config = config;
            _resolver = resolver;
        }

        /// Sum of (quantity × base value) over everything the order asks for. The one number both payouts
        /// scale off, so cash and XP can never drift apart in how they read the request.
        public int RawValue(IReadOnlyList<ResourceAmount> requests)
        {
            int total = 0;
            foreach (var r in requests)
            {
                if (!_resources.TryGetValue(r.ResourceId, out var resource))
                    throw new InvalidOperationException($"Order requests unknown resource '{r.ResourceId}'");
                total += r.Amount * resource.BaseValue;
            }
            return total;
        }

        public int Cash(IReadOnlyList<ResourceAmount> requests) =>
            AtLeastOne(_resolver.Resolve(RawValue(requests) * _config.CashMultiplier,
                ResolveKind.OrderPayout, new ResolveContext(null)));

        public int Xp(IReadOnlyList<ResourceAmount> requests) =>
            AtLeastOne(_resolver.Resolve(RawValue(requests) * _config.XpMultiplier,
                ResolveKind.XpGain, new ResolveContext(null)));

        /// An order that pays nothing is not a reward — floor both payouts at 1 so a cheap request is still
        /// worth filling. This is a rule, not a clamp over a bug: the multipliers are free to be fractional.
        static int AtLeastOne(float value) => Math.Max(1, (int)Math.Round(value));
    }
}
