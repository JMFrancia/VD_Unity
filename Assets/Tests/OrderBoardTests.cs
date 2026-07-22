using System.Collections.Generic;
using NUnit.Framework;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

namespace VoidDay.Tests
{
    /// The board's rules: slots fill, fulfilling costs goods and pays cash, skipping is free, and a used slot
    /// stays empty for exactly the refill window. Headless — no scene, no MonoBehaviour, no clock but the one
    /// passed in.
    public sealed class OrderBoardTests
    {
        const string Corn = "corn";

        EventBus _bus;
        ResourcePool _pool;
        Wallet _wallet;
        OrderBoard _board;
        Progression _progression;

        [SetUp]
        public void SetUp()
        {
            var resources = new Dictionary<string, ResourceModel>
            {
                [Corn] = new ResourceModel(Corn, "Corn", baseValue: 3, sellable: true, tier: 1),
            };
            var config = new OrderConfigModel(slotCount: 3, refillSeconds: 60f, minRequestKinds: 1,
                maxRequestKinds: 1, maxQuantityAtLevel1: 2f, maxQuantityPerLevel: 0f,
                cashMultiplier: 10f, xpMultiplier: 1f, tierWeightBase: 1f, tierWeightPerLevel: 0f);

            var resolver = new ValueResolver();
            _bus = new EventBus();
            _pool = new ResourcePool(_bus, resolver);
            _wallet = new Wallet(_bus);
            _progression = new Progression(_bus, resolver, Levels.Plain(), new LevelGrants(), _wallet,
                new GemPurse(_bus, 0), System.Array.Empty<LevelUnlockModel>());

            var pricing = new OrderPricing(resources, config, resolver);
            var generation = new OrderGeneration(resources, config, pricing, new System.Random(99));
            _board = new OrderBoard(_bus, _pool, _wallet, generation, config, resolver,
                () => new[] { Corn }, () => _progression.PlayerLevel);
        }

        [Test]
        public void FirstTickFillsEverySlot()
        {
            _board.Tick(0d);

            Assert.AreEqual(3, _board.VisibleSlotCount);
            for (int i = 0; i < 3; i++)
                Assert.IsNotNull(_board.OrderAt(i), $"slot {i} should hold an order after the first tick");
        }

        [Test]
        public void FulfillingConsumesTheGoodsAndPaysTheCash()
        {
            _board.Tick(0d);
            var order = _board.OrderAt(0);
            int needed = order.Requests[0].Amount;
            _pool.Add(Corn, needed);

            _board.Fulfill(order.Id, 0d);

            Assert.AreEqual(0, _pool.Get(Corn), "the requested goods should have been consumed");
            Assert.AreEqual(order.Cash, _wallet.Money);
            Assert.IsNull(_board.OrderAt(0), "the fulfilled slot should be empty");
        }

        [Test]
        public void FulfillingWithoutTheGoodsThrowsRatherThanSilentlySucceeding()
        {
            _board.Tick(0d);
            var order = _board.OrderAt(0);

            Assert.Throws<System.InvalidOperationException>(() => _board.Fulfill(order.Id, 0d));
            Assert.AreEqual(0, _wallet.Money);
        }

        [Test]
        public void FulfillingPublishesTheXpTheOrderCarries()
        {
            int observed = 0;
            _bus.Subscribe<OrderFulfilled>(e => observed = e.Xp);

            _board.Tick(0d);
            var order = _board.OrderAt(0);
            _pool.Add(Corn, order.Requests[0].Amount);
            _board.Fulfill(order.Id, 0d);

            Assert.AreEqual(order.Xp, observed);
            Assert.Greater(observed, 0);
        }

        [Test]
        public void SkippingIsFree_AndCostsNoGoods()
        {
            _board.Tick(0d);
            var order = _board.OrderAt(0);
            _pool.Add(Corn, 5);

            _board.Skip(order.Id, 0d);

            Assert.AreEqual(5, _pool.Get(Corn), "skipping must not consume anything");
            Assert.AreEqual(0, _wallet.Money, "skipping must not pay");
            Assert.IsNull(_board.OrderAt(0));
        }

        [Test]
        public void AUsedSlotStaysEmptyUntilTheRefillWindowElapses()
        {
            _board.Tick(0d);
            _board.Skip(_board.OrderAt(1).Id, 0d);

            _board.Tick(59d);
            Assert.IsNull(_board.OrderAt(1), "the slot should still be refilling one second short of the window");

            _board.Tick(60d);
            Assert.IsNotNull(_board.OrderAt(1), "the slot should refill once the window has passed");
        }

        [Test]
        public void RefillRemainingCountsDownFromTheFullWindow()
        {
            _board.Tick(0d);
            _board.Skip(_board.OrderAt(2).Id, 0d);

            Assert.AreEqual(60f, _board.RefillRemaining(2, 0d), 0.001f);
            Assert.AreEqual(15f, _board.RefillRemaining(2, 45d), 0.001f);
            Assert.AreEqual(0f, _board.RefillRemaining(2, 90d), 0.001f, "never reports a negative countdown");
        }

        [Test]
        public void OrdersNeverExpireOnTheirOwn()
        {
            _board.Tick(0d);
            var order = _board.OrderAt(0);

            _board.Tick(10_000d); // an hour of ticks with no player action

            Assert.AreSame(order, _board.OrderAt(0), "a filled slot is only ever emptied by fulfill or skip");
        }

        [Test]
        public void FulfillingAnUnknownOrderThrows()
        {
            _board.Tick(0d);
            Assert.Throws<System.InvalidOperationException>(() => _board.Fulfill("order#nope", 0d));
        }
    }
}
