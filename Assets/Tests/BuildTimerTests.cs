using System.Collections.Generic;
using NUnit.Framework;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
using VoidDay.Core.World;

namespace VoidDay.Tests
{
    /// A placed station is not built — it spends buildSeconds under construction first (§4.3). Pure-C# economy,
    /// so exactly what CLAUDE.md says to cover: charging at the wrong moment, counting the wrong things against
    /// the cap, or registering a job queue on an unfinished station does not crash — it just makes the game
    /// quietly wrong, and "the station worked before it existed" is invisible until someone tries to use it.
    public sealed class BuildTimerTests
    {
        const string Bakery = "bakery";
        const int Cost = 50;
        const int Cap = 2;

        static readonly GridCoord CellA = new GridCoord(1, 1);
        static readonly GridCoord CellB = new GridCoord(2, 1);

        sealed class Rig
        {
            public EventBus Bus;
            public BuildSystem Build;
            public JobSystem Jobs;
            public Wallet Wallet;
            public StationGrid Grid;
            public readonly List<StationConstructionStarted> Started = new();
            public readonly List<StationBuilt> Built = new();
        }

        static Rig Make(float buildSeconds, int startingMoney = 500)
        {
            var bus = new EventBus();
            var resolver = new ValueResolver();
            var pool = new ResourcePool(bus, resolver);
            var catalog = new RecipeCatalog();
            var jobs = new JobSystem(bus, pool, catalog, resolver, () => 1);
            var grid = new StationGrid(10, 10);
            var wallet = new Wallet(bus);
            wallet.Add(startingMoney);

            var types = new Dictionary<string, StationTypeModel>
            {
                [Bakery] = new StationTypeModel(Bakery, "Bakery", Cost, Cap, 1, 3, 1, 1, buildSeconds)
            };

            var rig = new Rig
            {
                Bus = bus,
                Jobs = jobs,
                Wallet = wallet,
                Grid = grid,
                Build = new BuildSystem(bus, grid, jobs, wallet, resolver, types, () => 1, 0.5f)
            };
            bus.Subscribe<StationConstructionStarted>(e => rig.Started.Add(e));
            bus.Subscribe<StationBuilt>(e => rig.Built.Add(e));
            return rig;
        }

        static string OnlyStationId(Rig rig)
        {
            foreach (var kv in rig.Grid.All) return kv.Value.Id;
            return null;
        }

        [Test]
        public void Place_chargesImmediately_butDoesNotBuildYet()
        {
            var rig = Make(buildSeconds: 30f);
            int before = rig.Wallet.Money;

            rig.Build.Place(Bakery, CellA, now: 0d);

            Assert.AreEqual(before - Cost, rig.Wallet.Money, "the build cost is charged at placement");
            Assert.AreEqual(1, rig.Started.Count, "construction:started announces the placement");
            Assert.AreEqual(30f, rig.Started[0].Duration, "the site carries the type's build duration");
            Assert.IsEmpty(rig.Built, "station:built must NOT fire until the timer expires");
        }

        [Test]
        public void SiteHoldsItsCellAndCap_whileUnderConstruction()
        {
            var rig = Make(buildSeconds: 30f);
            rig.Build.Place(Bakery, CellA, now: 0d);

            Assert.IsTrue(rig.Grid.IsOccupied(CellA), "the site occupies its cell from the moment it is placed");
            Assert.AreEqual(1, rig.Build.CountOf(Bakery), "an unfinished station still counts against the cap");

            rig.Grid.TryGet(CellA, out var model);
            Assert.IsTrue(model.UnderConstruction, "the model is flagged so the order pool can skip it");
        }

        [Test]
        public void StationIsNotOperable_untilTheTimerExpires()
        {
            var rig = Make(buildSeconds: 30f);
            rig.Build.Place(Bakery, CellA, now: 0d);
            string id = OnlyStationId(rig);

            // No job queue exists yet — asking about one is how the rest of the game discovers it can't be used.
            Assert.Throws<System.InvalidOperationException>(() => rig.Jobs.GetQueue(id),
                "an unfinished station must not be registered with the producer");

            rig.Build.Tick(29d);
            Assert.IsEmpty(rig.Built, "the timer has not expired yet");

            rig.Build.Tick(30d);
            Assert.AreEqual(1, rig.Built.Count, "station:built fires when the timer expires");
            Assert.AreEqual(id, rig.Built[0].StationId);
            Assert.IsNotNull(rig.Jobs.GetQueue(id), "the finished station gains its job queue");

            rig.Grid.TryGet(CellA, out var model);
            Assert.IsFalse(model.UnderConstruction, "the flag clears once built");
        }

        [Test]
        public void TickIsIdempotent_afterCompletion()
        {
            var rig = Make(buildSeconds: 10f);
            rig.Build.Place(Bakery, CellA, now: 0d);

            rig.Build.Tick(10d);
            rig.Build.Tick(11d);
            rig.Build.Tick(99d);

            Assert.AreEqual(1, rig.Built.Count, "a finished site must not complete twice");
        }

        [Test]
        public void ZeroBuildSeconds_completesOnTheSameFrame()
        {
            var rig = Make(buildSeconds: 0f);
            rig.Build.Place(Bakery, CellA, now: 0d);

            Assert.AreEqual(1, rig.Started.Count, "the instant path still announces construction:started...");
            Assert.AreEqual(1, rig.Built.Count, "...and station:built lands on the same frame");
            Assert.IsNotNull(rig.Jobs.GetQueue(OnlyStationId(rig)), "an instant build is operable immediately");
        }

        [Test]
        public void ProgressCountsDown_andStopsBeingReportedOnceBuilt()
        {
            var rig = Make(buildSeconds: 20f);
            rig.Build.Place(Bakery, CellA, now: 100d); // non-zero start: progress must be relative, not absolute
            string id = OnlyStationId(rig);

            Assert.IsTrue(rig.Build.TryGetSiteProgress(id, 105d, out float fraction, out float remaining));
            Assert.AreEqual(0.25f, fraction, 0.001f);
            Assert.AreEqual(15f, remaining, 0.001f);

            rig.Build.Tick(120d);
            Assert.IsFalse(rig.Build.TryGetSiteProgress(id, 120d, out _, out _),
                "a built station is no longer a site");
        }

        [Test]
        public void ConcurrentSites_completeIndependently()
        {
            var rig = Make(buildSeconds: 10f);
            rig.Build.Place(Bakery, CellA, now: 0d);
            rig.Build.Place(Bakery, CellB, now: 5d);

            rig.Build.Tick(10d);
            Assert.AreEqual(1, rig.Built.Count, "only the first site's timer has expired");

            rig.Build.Tick(15d);
            Assert.AreEqual(2, rig.Built.Count, "the second finishes on its own schedule");
        }

        [Test]
        public void CapCountsSites_soBuildsCannotBeOverQueued()
        {
            var rig = Make(buildSeconds: 30f);
            rig.Build.Place(Bakery, CellA, now: 0d);
            rig.Build.Place(Bakery, CellB, now: 0d); // fills the cap of 2, both still building

            Assert.Throws<System.InvalidOperationException>(
                () => rig.Build.Place(Bakery, new GridCoord(3, 1), now: 0d),
                "a third placement must be refused while two sites hold the cap");
        }
    }
}
