using System;
using System.Collections.Generic;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// Procedural order generation (§6.1). Pure C# on Core/Model objects, with System.Random injected — never
    /// UnityEngine.Random — so a seeded instance replays exactly and the whole thing is testable headless.
    ///
    /// The candidate pool is "resources the player has a station for" ∩ "sellable". Wheat's exclusion is NOT
    /// a list here: it falls out of ResourceModel.Sellable being false, which is the single home of that rule.
    public sealed class OrderGeneration
    {
        private readonly IReadOnlyDictionary<string, ResourceModel> _resources;
        private readonly OrderConfigModel _config;
        private readonly OrderPricing _pricing;
        private readonly Random _random;

        private int _nextOrderNumber;

        public OrderGeneration(IReadOnlyDictionary<string, ResourceModel> resources, OrderConfigModel config,
            OrderPricing pricing, Random random)
        {
            _resources = resources;
            _config = config;
            _pricing = pricing;
            _random = random;
        }

        /// <param name="producibleIds">Resource ids some registered station can currently produce.</param>
        public OrderModel Generate(IReadOnlyCollection<string> producibleIds, int playerLevel)
        {
            var candidates = Candidates(producibleIds);
            if (candidates.Count == 0)
                throw new InvalidOperationException(
                    "Cannot generate an order: no sellable resource is producible by any station");

            int kinds = Math.Min(_random.Next(_config.MinRequestKinds, _config.MaxRequestKinds + 1), candidates.Count);
            var requests = new List<ResourceAmount>(kinds);
            for (int i = 0; i < kinds; i++)
            {
                var picked = PickWeighted(candidates, playerLevel);
                candidates.Remove(picked); // one entry per resource — never "2 corn and 3 corn" on one card
                requests.Add(new ResourceAmount(picked.Id, Quantity(playerLevel)));
            }

            return new OrderModel($"order#{_nextOrderNumber++}", requests,
                _pricing.Cash(requests), _pricing.Xp(requests));
        }

        List<ResourceModel> Candidates(IReadOnlyCollection<string> producibleIds)
        {
            var list = new List<ResourceModel>();
            foreach (var id in producibleIds)
            {
                if (!_resources.TryGetValue(id, out var resource))
                    throw new InvalidOperationException($"Station produces unknown resource '{id}'");
                if (resource.Sellable) list.Add(resource);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id)); // stable order → a seed replays exactly
            return list;
        }

        /// Weight rises with tier as the level rises (§6.1). At level 1 every weight is TierWeightBase, so
        /// the pick is uniform; the higher tiers only pull ahead once there is a level to pull with.
        ResourceModel PickWeighted(List<ResourceModel> candidates, int playerLevel)
        {
            float total = 0f;
            foreach (var c in candidates) total += Weight(c, playerLevel);

            double roll = _random.NextDouble() * total;
            foreach (var c in candidates)
            {
                roll -= Weight(c, playerLevel);
                if (roll <= 0d) return c;
            }
            return candidates[candidates.Count - 1]; // float drift on the last step; the tail is the pick
        }

        float Weight(ResourceModel r, int playerLevel) =>
            _config.TierWeightBase + _config.TierWeightPerLevel * (playerLevel - 1) * r.Tier;

        int Quantity(int playerLevel)
        {
            float max = _config.MaxQuantityAtLevel1 + _config.MaxQuantityPerLevel * (playerLevel - 1);
            return _random.Next(1, Math.Max(1, (int)Math.Round(max)) + 1);
        }
    }
}
