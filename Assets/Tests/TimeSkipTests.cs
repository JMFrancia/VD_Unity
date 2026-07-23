using System.Collections.Generic;
using NUnit.Framework;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
using VoidDay.Core.World;

namespace VoidDay.Tests
{
    /// The gem sink (§13): pricing a running timer, and finishing it early. Exactly the kind of pure-C#
    /// economy CLAUDE.md still says to test — a cost curve that rounds the wrong way, or a skip that
    /// completes a job down a *different* path than a natural finish, never crashes. It just quietly
    /// overcharges the player, or drops the JobCompleted a listener was waiting on.
    public sealed class TimeSkipTests
    {
        const string Bakery = "bakery";
        const string BakeBread = "bake-bread";
        const string Bread = "bread";
        const string Corn = "corn";
        const string Preplaced = "bakery-A";

        const float SecondsPerGem = 30f;
        const int MinGemCost = 1;

        sealed class Rig
        {
            public EventBus Bus;
            public GemPurse Gems;
            public JobSystem Jobs;
            public BuildSystem Build;
            public OrderBoard Orders;
            public TimeSkip Skip;
            public readonly List<TimerSkipped> Skipped = new();
            public readonly List<JobCompleted> JobsDone = new();
            public readonly List<StationBlocked> Blocked = new();
            public readonly List<StationBuilt> Built = new();
            public readonly List<OrderGenerated> Generated = new();
            public readonly List<OrderSlotRefilled> Refilled = new();
        }

        static Rig Make(int startingGems = 100, float buildSeconds = 120f, float recipeSeconds = 90f)
        {
            var bus = new EventBus();
            var resolver = new ValueResolver();
            var pool = new ResourcePool(bus, resolver);
            pool.SetBaseCapacity(100);
            var wallet = new Wallet(bus);
            wallet.Add(1000);
            var gems = new GemPurse(bus, startingGems);

            var catalog = new RecipeCatalog();
            catalog.Add(new RecipeModel(BakeBread, Bakery,
                new ResourceAmount[0], new[] { new ResourceAmount(Bread, 1) }, recipeSeconds, unlockLevel: 1));
            var jobs = new JobSystem(bus, pool, catalog, resolver, () => 1);

            var grid = new StationGrid(10, 10);
            var types = new Dictionary<string, StationTypeModel>
            {
                [Bakery] = new StationTypeModel(Bakery, "Bakery", 50, 5, 1, 3, 1, 1, buildSeconds)
            };
            var build = new BuildSystem(bus, grid, jobs, wallet, resolver, types, () => 1, 0.5f);
            build.RegisterPreplaced(Preplaced, Bakery, new GridCoord(0, 0));

            var orderResources = new Dictionary<string, ResourceModel>
            {
                [Corn] = new ResourceModel(Corn, "Corn", baseValue: 3, sellable: true, tier: 1),
            };
            var orderConfig = new OrderConfigModel(slotCount: 2, refillSeconds: 60f, minRequestKinds: 1,
                maxRequestKinds: 1, maxQuantityAtLevel1: 2f, maxQuantityPerLevel: 0f,
                cashMultiplier: 10f, xpMultiplier: 1f, tierWeightBase: 1f, tierWeightPerLevel: 0f);
            var pricing = new OrderPricing(orderResources, orderConfig, resolver);
            var generation = new OrderGeneration(orderResources, orderConfig, pricing, new System.Random(7));
            var orders = new OrderBoard(bus, pool, wallet, generation, orderConfig, resolver,
                () => new[] { Corn }, () => 1);

            var rig = new Rig
            {
                Bus = bus,
                Gems = gems,
                Jobs = jobs,
                Build = build,
                Orders = orders,
                Skip = new TimeSkip(bus, gems, jobs, build, orders, SecondsPerGem, MinGemCost)
            };
            bus.Subscribe<TimerSkipped>(e => rig.Skipped.Add(e));
            bus.Subscribe<JobCompleted>(e => rig.JobsDone.Add(e));
            bus.Subscribe<StationBlocked>(e => rig.Blocked.Add(e));
            bus.Subscribe<StationBuilt>(e => rig.Built.Add(e));
            bus.Subscribe<OrderGenerated>(e => rig.Generated.Add(e));
            bus.Subscribe<OrderSlotRefilled>(e => rig.Refilled.Add(e));
            return rig;
        }

        // ---- The cost curve ----

        [Test]
        public void ANearlyFinishedTimerStillCostsTheFloor()
        {
            var rig = Make(recipeSeconds: 90f);
            rig.Jobs.QueueJob(Preplaced, BakeBread, 0d);

            // 1 second left of 90 — 1/30th of a gem's worth, so only the floor keeps it from being free.
            Assert.AreEqual(MinGemCost, rig.Skip.CostFor(TimerRef.Job(Preplaced), 89d));
        }

        [Test]
        public void CostRoundsUpNotToNearest()
        {
            var rig = Make(recipeSeconds: 90f);
            rig.Jobs.QueueJob(Preplaced, BakeBread, 0d);

            // 31s remaining is 1.03 gems' worth. Rounding to nearest would price it at 1 and give a second
            // gem-worth of waiting away free.
            Assert.AreEqual(2, rig.Skip.CostFor(TimerRef.Job(Preplaced), 59d));
        }

        [Test]
        public void AnExactMultipleOfSecondsPerGemDoesNotOverchargeByOne()
        {
            var rig = Make(recipeSeconds: 90f);
            rig.Jobs.QueueJob(Preplaced, BakeBread, 0d);

            Assert.AreEqual(2, rig.Skip.CostFor(TimerRef.Job(Preplaced), 30d)); // exactly 60s left
        }

        [Test]
        public void ALongTimerPricesHigh()
        {
            var rig = Make(recipeSeconds: 600f);
            rig.Jobs.QueueJob(Preplaced, BakeBread, 0d);

            Assert.AreEqual(20, rig.Skip.CostFor(TimerRef.Job(Preplaced), 0d)); // 600 / 30
        }

        [Test]
        public void AnExpiredTimerIsNotSkippable()
        {
            var rig = Make(recipeSeconds: 90f);
            rig.Jobs.QueueJob(Preplaced, BakeBread, 0d);

            Assert.IsTrue(rig.Skip.CanSkip(TimerRef.Job(Preplaced), 89d));
            Assert.IsFalse(rig.Skip.CanSkip(TimerRef.Job(Preplaced), 90d),
                "a timer at its end has nothing left to buy — the next Tick finishes it free");
        }

        [Test]
        public void AStationWithNoRunningJobHasNoSkippableTimer()
        {
            var rig = Make();
            Assert.IsFalse(rig.Skip.CanSkip(TimerRef.Job(Preplaced), 0d));
        }

        // ---- Each owner completes down its NORMAL path ----

        [Test]
        public void SkippingAJobCompletesItOnTheNextTick()
        {
            var rig = Make(recipeSeconds: 90f);
            rig.Jobs.QueueJob(Preplaced, BakeBread, 0d);

            rig.Skip.Skip(TimerRef.Job(Preplaced), 10d);
            Assert.AreEqual(0, rig.JobsDone.Count, "the skip only nudges the timestamp; Tick completes");

            rig.Jobs.Tick(10d);

            Assert.AreEqual(1, rig.JobsDone.Count, "a skipped job publishes the same JobCompleted as a natural one");
            Assert.AreEqual(Preplaced, rig.JobsDone[0].StationId);
            Assert.AreEqual(1, rig.Blocked.Count, "and the same StationBlocked pair");
            Assert.AreEqual("output-uncollected", rig.Blocked[0].Reason);
            Assert.IsTrue(rig.Jobs.IsCollectionPossible(Preplaced));
        }

        [Test]
        public void SkippingAConstructionSiteBuildsItOnTheNextTick()
        {
            var rig = Make(buildSeconds: 120f);
            rig.Build.Place(Bakery, new GridCoord(3, 3), 0d);
            const string id = Bakery + "#0"; // BuildSystem's instance ids are deterministic
            Assert.AreEqual(0, rig.Built.Count, "placing starts a site; it does not build");

            rig.Skip.Skip(TimerRef.Construction(id), 5d);
            Assert.AreEqual(0, rig.Built.Count);

            rig.Build.Tick(5d);

            Assert.AreEqual(1, rig.Built.Count, "a skipped build publishes the same StationBuilt as a natural one");
            Assert.AreEqual(id, rig.Built[0].StationId);
        }

        [Test]
        public void SkippingAnOrderRefillFillsTheSlotOnTheNextTick()
        {
            var rig = Make();
            rig.Orders.Tick(0d);
            var order = rig.Orders.OrderAt(0);
            rig.Orders.Skip(order.Id, 0d); // the free "discard this order" skip — slot now refilling for 60s
            rig.Generated.Clear();
            rig.Refilled.Clear();

            Assert.IsTrue(rig.Skip.CanSkip(TimerRef.OrderRefill(0), 10d));
            rig.Skip.Skip(TimerRef.OrderRefill(0), 10d);
            Assert.IsNull(rig.Orders.OrderAt(0), "the skip only nudges the timestamp; Tick fills");

            rig.Orders.Tick(10d);

            Assert.IsNotNull(rig.Orders.OrderAt(0));
            Assert.AreEqual(1, rig.Generated.Count, "a skipped refill publishes the same OrderGenerated");
            Assert.AreEqual(1, rig.Refilled.Count, "…and the same OrderSlotRefilled");
            Assert.AreEqual(0, rig.Refilled[0].Slot);
        }

        [Test]
        public void AFilledOrderSlotHasNoSkippableTimer()
        {
            var rig = Make();
            rig.Orders.Tick(0d);

            Assert.IsFalse(rig.Skip.CanSkip(TimerRef.OrderRefill(0), 0d));
        }

        // ---- Paying for it ----

        [Test]
        public void SkippingChargesTheCostAndAnnouncesIt()
        {
            var rig = Make(startingGems: 10, recipeSeconds: 90f);
            rig.Jobs.QueueJob(Preplaced, BakeBread, 0d);

            rig.Skip.Skip(TimerRef.Job(Preplaced), 0d); // 90s left → 3 gems

            Assert.AreEqual(7, rig.Gems.Gems);
            Assert.AreEqual(1, rig.Skipped.Count);
            Assert.AreEqual(3, rig.Skipped[0].Cost);
            Assert.AreEqual(TimerKind.Job, rig.Skipped[0].Timer.Kind);
        }

        [Test]
        public void AnUnaffordableSkipThrowsAndLeavesTheTimerRunning()
        {
            var rig = Make(startingGems: 1, recipeSeconds: 90f);
            rig.Jobs.QueueJob(Preplaced, BakeBread, 0d);

            Assert.Throws<System.InvalidOperationException>(
                () => rig.Skip.Skip(TimerRef.Job(Preplaced), 0d));

            Assert.AreEqual(1, rig.Gems.Gems, "nothing should have been spent");
            Assert.AreEqual(0, rig.Skipped.Count);

            rig.Jobs.Tick(10d);
            Assert.AreEqual(0, rig.JobsDone.Count, "the timer must still be running");
            Assert.AreEqual(90f, rig.Jobs.HeadSecondsRemaining(Preplaced, 0d), 0.001f);
        }

        [Test]
        public void PricingADeadTimerThrows()
        {
            var rig = Make();
            Assert.Throws<System.InvalidOperationException>(
                () => rig.Skip.CostFor(TimerRef.Job(Preplaced), 0d));
        }
    }
}
