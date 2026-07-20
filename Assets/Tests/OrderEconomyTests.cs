using System.Collections.Generic;
using NUnit.Framework;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

namespace VoidDay.Tests
{
    /// The economy core is the one thing CLAUDE.md still says to test: bugs here don't crash, they just make
    /// the game quietly wrong. Pure C#, no Unity, no scene — this is what the Core boundary buys.
    public sealed class OrderEconomyTests
    {
        const string Wheat = "wheat", Corn = "corn", Bread = "bread";

        static Dictionary<string, ResourceModel> Resources() => new()
        {
            // wheat is sellable:false — the ONE expression of its exclusion from the order pool (§6.1)
            [Wheat] = new ResourceModel(Wheat, "Wheat", baseValue: 2, sellable: false, tier: 1),
            [Corn] = new ResourceModel(Corn, "Corn", baseValue: 3, sellable: true, tier: 1),
            [Bread] = new ResourceModel(Bread, "Bread", baseValue: 10, sellable: true, tier: 2),
        };

        static OrderConfigModel Config(int slots = 3, float refill = 60f, int minKinds = 1, int maxKinds = 2,
            float maxQty1 = 3f, float maxQtyPerLevel = 1f, float cashMult = 12f, float xpMult = 1.5f,
            float tierBase = 1f, float tierPerLevel = 0.25f) =>
            new OrderConfigModel(slots, refill, minKinds, maxKinds, maxQty1, maxQtyPerLevel,
                cashMult, xpMult, tierBase, tierPerLevel);

        static OrderPricing Pricing(OrderConfigModel config) =>
            new OrderPricing(Resources(), config, new ValueResolver());

        // ---- Pricing (§6) ----

        [Test]
        public void RawValue_IsQuantityTimesBaseValue_SummedOverRequests()
        {
            var pricing = Pricing(Config());
            var requests = new[] { new ResourceAmount(Corn, 2), new ResourceAmount(Bread, 1) };

            Assert.AreEqual(2 * 3 + 1 * 10, pricing.RawValue(requests));
        }

        [Test]
        public void Cash_AndXp_ScaleTheSameRawValueByTheirOwnMultiplier()
        {
            var config = Config(cashMult: 12f, xpMult: 1.5f);
            var pricing = Pricing(config);
            var requests = new[] { new ResourceAmount(Corn, 2) }; // raw = 6

            Assert.AreEqual(72, pricing.Cash(requests));
            Assert.AreEqual(9, pricing.Xp(requests));
        }

        [Test]
        public void Payout_NeverDropsBelowOne_EvenWithATinyMultiplier()
        {
            var pricing = Pricing(Config(cashMult: 0.001f, xpMult: 0.001f));
            var requests = new[] { new ResourceAmount(Corn, 1) };

            Assert.AreEqual(1, pricing.Cash(requests));
            Assert.AreEqual(1, pricing.Xp(requests));
        }

        // ---- Generation (§6.1) ----

        static OrderGeneration Generation(OrderConfigModel config, int seed)
        {
            var resources = Resources();
            return new OrderGeneration(resources, config, new OrderPricing(resources, config, new ValueResolver()),
                new System.Random(seed));
        }

        [Test]
        public void NeverRequestsAnUnsellableResource_EvenWhenItIsTheOnlyThingProduced()
        {
            var generation = Generation(Config(), seed: 1);

            Assert.Throws<System.InvalidOperationException>(
                () => generation.Generate(new[] { Wheat }, playerLevel: 1));
        }

        [Test]
        public void WheatNeverAppearsAcrossManyGeneratedOrders()
        {
            var generation = Generation(Config(), seed: 7);
            var producible = new[] { Wheat, Corn, Bread };

            for (int i = 0; i < 500; i++)
                foreach (var request in generation.Generate(producible, playerLevel: 3).Requests)
                    Assert.AreNotEqual(Wheat, request.ResourceId);
        }

        [Test]
        public void RequestsOnlyWhatAStationCanProduce()
        {
            var generation = Generation(Config(), seed: 3);

            for (int i = 0; i < 100; i++)
                foreach (var request in generation.Generate(new[] { Corn }, playerLevel: 5).Requests)
                    Assert.AreEqual(Corn, request.ResourceId);
        }

        [Test]
        public void QuantityCeilingRisesWithLevel()
        {
            var config = Config(maxQty1: 1f, maxQtyPerLevel: 4f);
            var atLevel1 = Generation(config, seed: 11);
            var atLevel5 = Generation(config, seed: 11);

            for (int i = 0; i < 50; i++)
                foreach (var r in atLevel1.Generate(new[] { Corn }, playerLevel: 1).Requests)
                    Assert.AreEqual(1, r.Amount); // ceiling is 1 at level 1 — the only legal quantity

            int max = 0;
            for (int i = 0; i < 200; i++)
                foreach (var r in atLevel5.Generate(new[] { Corn }, playerLevel: 5).Requests)
                    max = System.Math.Max(max, r.Amount);

            Assert.Greater(max, 1, "the level-5 ceiling (1 + 4*4 = 17) should produce quantities above 1");
        }

        [Test]
        public void HigherTiersAreWeightedUpAsLevelRises_AndAreUniformAtLevelOne()
        {
            var config = Config(minKinds: 1, maxKinds: 1, tierBase: 1f, tierPerLevel: 1f);
            var producible = new[] { Corn, Bread }; // tier 1 vs tier 2

            Assert.AreEqual(0.5f, BreadShare(Generation(config, 42), producible, level: 1), 0.06f,
                "at level 1 every candidate weighs tierBase, so the pick is uniform");

            Assert.Greater(BreadShare(Generation(config, 42), producible, level: 10),
                0.6f, "by level 10 the tier-2 good should dominate the pool");
        }

        static float BreadShare(OrderGeneration generation, string[] producible, int level)
        {
            int bread = 0, total = 0;
            for (int i = 0; i < 2000; i++)
                foreach (var r in generation.Generate(producible, level).Requests)
                {
                    if (r.ResourceId == Bread) bread++;
                    total++;
                }
            return (float)bread / total;
        }

        [Test]
        public void NeverRequestsTheSameResourceTwiceOnOneCard()
        {
            var generation = Generation(Config(minKinds: 2, maxKinds: 2), seed: 5);

            for (int i = 0; i < 200; i++)
            {
                var requests = generation.Generate(new[] { Corn, Bread }, playerLevel: 4).Requests;
                Assert.AreEqual(2, requests.Count);
                Assert.AreNotEqual(requests[0].ResourceId, requests[1].ResourceId);
            }
        }

        [Test]
        public void RequestKindsAreCappedByHowManyGoodsAreProducible()
        {
            var generation = Generation(Config(minKinds: 2, maxKinds: 2), seed: 9);

            // only one sellable good is producible — the order must shrink to it rather than repeat it
            Assert.AreEqual(1, generation.Generate(new[] { Corn, Wheat }, playerLevel: 1).Requests.Count);
        }

        [Test]
        public void SameSeedReplaysTheSameOrders()
        {
            var a = Generation(Config(), seed: 2024);
            var b = Generation(Config(), seed: 2024);
            var producible = new[] { Corn, Bread };

            for (int i = 0; i < 50; i++)
            {
                var left = a.Generate(producible, playerLevel: 3);
                var right = b.Generate(producible, playerLevel: 3);
                Assert.AreEqual(left.Id, right.Id);
                Assert.AreEqual(left.Cash, right.Cash);
                Assert.AreEqual(left.Requests.Count, right.Requests.Count);
                for (int j = 0; j < left.Requests.Count; j++)
                {
                    Assert.AreEqual(left.Requests[j].ResourceId, right.Requests[j].ResourceId);
                    Assert.AreEqual(left.Requests[j].Amount, right.Requests[j].Amount);
                }
            }
        }
    }
}
