using System.Collections.Generic;
using NUnit.Framework;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

namespace VoidDay.Tests
{
    /// Storage (§7) is ONE shared silo capacity across every good — Hay Day's model — plus the storage-full
    /// block (§4.4). Pure-C# economy, so exactly what CLAUDE.md says to cover: a capacity that is off by one,
    /// or a shared pool that quietly behaves per-resource, does not crash — it just makes the game wrong.
    /// Also pins the M7 scope generalization (EffectScopes), because global reach is what the Silo and every
    /// later universal upgrade ride on.
    public sealed class StorageTests
    {
        const string Corn = "corn";
        const string Wheat = "wheat";

        static Effect E(EffectType type, EffectOp op, float amount, string resource = "") =>
            new Effect
            {
                type = type,
                value = new EffectValue { op = op, amount = amount },
                resource = resource,
                trigger = TriggerType.None
            };

        static UpgradeTrackModel CapTrack(float perTier = 25f) =>
            new UpgradeTrackModel("silo.cap", "silo.cap", 1, new[]
            {
                new UpgradeTierModel(120, new[] { E(EffectType.StorageCap, EffectOp.Flat, perTier) }),
                new UpgradeTierModel(300, new[] { E(EffectType.StorageCap, EffectOp.Flat, perTier) })
            });

        static UpgradeTrackModel GlobalSpeedTrack() =>
            new UpgradeTrackModel("workshop.speed", "workshop.speed", 1, new[]
            {
                new UpgradeTierModel(100, new[] { E(EffectType.GlobalSpeed, EffectOp.Pct, 25f) })
            });

        /// A pool with a shared capacity, plus the upgrade machinery wired through the same seam the game uses.
        /// `tracksByType` keys on station TYPE, matching UpgradeSystem.Register.
        static (ResourcePool pool, UpgradeSystem upgrades, Wallet wallet, ValueResolver resolver, EventBus bus)
            Rig(int capacity = 10, params (string type, UpgradeTrackModel track)[] tracks)
        {
            var bus = new EventBus();
            var wallet = new Wallet(bus);
            var byType = new Dictionary<string, IReadOnlyList<UpgradeTrackModel>>();
            foreach (var (type, track) in tracks) byType[type] = new[] { track };

            var upgrades = new UpgradeSystem(bus, wallet, byType, () => 1);
            var resolver = new ValueResolver();
            resolver.SetEffectSource(upgrades);

            var pool = new ResourcePool(bus, resolver);
            pool.SetBaseCapacity(capacity);
            return (pool, upgrades, wallet, resolver, bus);
        }

        /// A Field wired to the same pool, with one 1-second recipe yielding corn and consuming nothing.
        static JobSystem Producer(ResourcePool pool, ValueResolver resolver, EventBus bus, int outputAmount = 2)
        {
            var catalog = new RecipeCatalog();
            catalog.Add(new RecipeModel("field.corn", "field",
                new List<ResourceAmount>(),
                new List<ResourceAmount> { new ResourceAmount(Corn, outputAmount) },
                duration: 1f, unlockLevel: 1));

            var jobs = new JobSystem(bus, pool, catalog, resolver, () => 1);
            jobs.Register("field#0", "field", queueDepthBase: 3);
            return jobs;
        }

        // ---- The shared pool (§7) ----

        [Test]
        public void TotalStored_SumsEveryGood()
        {
            var (pool, _, _, _, _) = Rig(capacity: 10);
            pool.Add(Corn, 4);
            pool.Add(Wheat, 3);
            Assert.AreEqual(7, pool.TotalStored);
        }

        [Test]
        public void CapacityIsShared_OneGoodSqueezesTheOthers()
        {
            var (pool, _, _, _, _) = Rig(capacity: 10);
            pool.Add(Wheat, 10);

            Assert.IsFalse(pool.HasRoomFor(1),
                "a silo full of wheat leaves no room for corn — this is the Hay Day model, not per-resource caps");
        }

        [Test]
        public void ExactlyFull_StillFits()
        {
            var (pool, _, _, _, _) = Rig(capacity: 10);
            pool.Add(Corn, 8);
            Assert.IsTrue(pool.HasRoomFor(2), "8 + 2 == capacity 10");
            Assert.IsFalse(pool.HasRoomFor(3));
        }

        [Test]
        public void OutputFitsAllOrNothing()
        {
            var (pool, _, _, _, _) = Rig(capacity: 10);
            pool.Add(Corn, 8);
            var outputs = new List<ResourceAmount>
            {
                new ResourceAmount(Corn, 1),
                new ResourceAmount(Wheat, 2)
            };
            Assert.IsFalse(pool.HasRoomForAll(outputs),
                "a 3-unit output into 2 free units is refused whole — output lands together or not at all (§4.4)");
        }

        [Test]
        public void ZeroBaseCapacity_IsUncapped()
        {
            var bus = new EventBus();
            var pool = new ResourcePool(bus, new ValueResolver());
            Assert.IsTrue(pool.HasRoomFor(9999),
                "only reachable in a headless test — boot always sets a capacity");
        }

        // ---- The storage-full block (§4.4) ----

        [Test]
        public void CollectionIsRefused_WhenOutputWouldOverfillTheSilo()
        {
            var (pool, _, _, resolver, bus) = Rig(capacity: 10);
            var jobs = Producer(pool, resolver, bus);
            pool.Add(Corn, 9);                       // 9 + 2 > 10

            jobs.QueueJob("field#0", "field.corn", now: 0);
            jobs.Tick(now: 2);                       // head completes

            Assert.IsFalse(jobs.IsCollectionPossible("field#0"), "collection must be refused at capacity");
            Assert.IsTrue(jobs.IsStorageBlocked("field#0"), "and the reason must read as storage-full");
        }

        [Test]
        public void ADifferentGood_CanFillTheSiloAndBlockCollection()
        {
            var (pool, _, _, resolver, bus) = Rig(capacity: 10);
            var jobs = Producer(pool, resolver, bus);
            pool.Add(Wheat, 10);                     // not one grain of corn stored, yet corn cannot land

            jobs.QueueJob("field#0", "field.corn", now: 0);
            jobs.Tick(now: 2);

            Assert.IsTrue(jobs.IsStorageBlocked("field#0"),
                "the shared pool is what blocks — hoarding wheat stops the corn harvest");
        }

        [Test]
        public void RefusedCollection_DestroysNothing()
        {
            var (pool, _, _, resolver, bus) = Rig(capacity: 10);
            var jobs = Producer(pool, resolver, bus);
            pool.Add(Corn, 9);

            jobs.QueueJob("field#0", "field.corn", now: 0);
            jobs.Tick(now: 2);

            Assert.Throws<System.InvalidOperationException>(() => jobs.Collect("field#0", 2, byPet: false));
            Assert.AreEqual(9, pool.Get(Corn), "the pool is untouched");
            Assert.AreEqual(1, jobs.GetQueue("field#0").Count, "the completed job still sits at the station (§4.4)");
        }

        [Test]
        public void StorageFullEventNamesTheGoodThatWasTurnedAway()
        {
            var (pool, _, _, resolver, bus) = Rig(capacity: 10);
            var jobs = Producer(pool, resolver, bus);
            pool.Add(Corn, 9);

            string turnedAway = null;
            bus.Subscribe<StorageFull>(e => turnedAway = e.ResourceId);

            jobs.QueueJob("field#0", "field.corn", now: 0);
            jobs.Tick(now: 2);

            Assert.AreEqual(Corn, turnedAway);
        }

        [Test]
        public void QueuedJobsStillRun_ThenBlockBehindTheFullHead()
        {
            var (pool, _, _, resolver, bus) = Rig(capacity: 10);
            var jobs = Producer(pool, resolver, bus);
            pool.Add(Corn, 9);

            jobs.QueueJob("field#0", "field.corn", now: 0);
            jobs.QueueJob("field#0", "field.corn", now: 0);
            jobs.Tick(now: 5);

            var queue = jobs.GetQueue("field#0");
            Assert.AreEqual(2, queue.Count, "nothing is dropped");
            Assert.AreEqual(JobState.Complete, queue[0].State);
            Assert.AreEqual(JobState.Queued, queue[1].State, "the second job cannot start behind a blocked head");
        }

        [Test]
        public void FreeingSpace_UnblocksCollection()
        {
            var (pool, _, _, resolver, bus) = Rig(capacity: 10);
            var jobs = Producer(pool, resolver, bus);
            pool.Add(Corn, 9);
            jobs.QueueJob("field#0", "field.corn", now: 0);
            jobs.Tick(now: 2);
            Assert.IsFalse(jobs.IsCollectionPossible("field#0"));

            pool.Add(Corn, -5);                       // spend it on an order
            Assert.IsTrue(jobs.IsCollectionPossible("field#0"));
            jobs.Collect("field#0", 2, byPet: false);
            Assert.AreEqual(6, pool.Get(Corn));
        }

        // ---- storage.cap, and the global scope it rides on ----

        [Test]
        public void SiloUpgrade_RaisesTheSharedCapacity()
        {
            var (pool, upgrades, wallet, _, _) = Rig(capacity: 30, ("silo", CapTrack(perTier: 25)));
            upgrades.Register("Silo", "silo");
            wallet.Add(1000);
            Assert.AreEqual(30, pool.Capacity);

            upgrades.Purchase("Silo", "silo.cap");
            Assert.AreEqual(55, pool.Capacity, "+25 flat on top of the authored 30");
        }

        [Test]
        public void CapacityTiersStackAdditively()
        {
            var (pool, upgrades, wallet, _, _) = Rig(capacity: 30, ("silo", CapTrack(perTier: 25)));
            upgrades.Register("Silo", "silo");
            wallet.Add(1000);
            upgrades.Purchase("Silo", "silo.cap");
            upgrades.Purchase("Silo", "silo.cap");

            Assert.AreEqual(80, pool.Capacity, "30 + 25 + 25 — tiers are additive, never cumulative totals");
        }

        [Test]
        public void RaisingCapacity_UnblocksAFullStation()
        {
            var (pool, upgrades, wallet, resolver, bus) = Rig(capacity: 10, ("silo", CapTrack(perTier: 25)));
            upgrades.Register("Silo", "silo");
            wallet.Add(1000);
            var jobs = Producer(pool, resolver, bus);

            pool.Add(Corn, 9);
            jobs.QueueJob("field#0", "field.corn", now: 0);
            jobs.Tick(now: 2);
            Assert.IsFalse(jobs.IsCollectionPossible("field#0"));

            upgrades.Purchase("Silo", "silo.cap");    // capacity 10 -> 35
            Assert.IsTrue(jobs.IsCollectionPossible("field#0"), "the same station is now collectable");
        }

        // ---- EffectScopes: the generalization M7 introduces and M11 depends on ----

        [Test]
        public void GlobalEffect_ReachesAStationlessResolve()
        {
            var (pool, upgrades, wallet, _, _) = Rig(capacity: 30, ("silo", CapTrack(perTier: 25)));
            upgrades.Register("Silo", "silo");
            wallet.Add(1000);
            upgrades.Purchase("Silo", "silo.cap");

            // The capacity read has no station in context at all — an own-station source would contribute nothing.
            Assert.AreEqual(55, pool.Capacity);
        }

        [Test]
        public void GlobalEffect_ReachesEveryStationsResolve()
        {
            var (_, upgrades, wallet, resolver, _) = Rig(capacity: 10, ("workshop", GlobalSpeedTrack()));
            upgrades.Register("workshop#0", "workshop");
            upgrades.Register("field#0", "field");
            upgrades.Register("field#1", "field");
            wallet.Add(1000);
            upgrades.Purchase("workshop#0", "workshop.speed"); // global.speed +25%

            // Bought at the Workshop, felt at both Fields — the M11 (Dopamine Rain) reach, proven early.
            foreach (var station in new[] { "field#0", "field#1" })
                Assert.AreEqual(4f, resolver.Resolve(5f, ResolveKind.RecipeDuration, new ResolveContext(station)),
                    1e-4f, $"global.speed must apply at {station} regardless of which building sold it");
        }

        [Test]
        public void OwnStationEffect_StillDoesNotLeak()
        {
            var stationTrack = new UpgradeTrackModel("field.speed", "field.speed", 1, new[]
            {
                new UpgradeTierModel(50, new[] { E(EffectType.StationSpeed, EffectOp.Pct, 25f) })
            });
            var (_, upgrades, wallet, resolver, _) = Rig(capacity: 10, ("field", stationTrack));
            upgrades.Register("field#0", "field");
            upgrades.Register("field#1", "field");
            wallet.Add(1000);
            upgrades.Purchase("field#0", "field.speed");

            Assert.AreEqual(4f, resolver.Resolve(5f, ResolveKind.RecipeDuration, new ResolveContext("field#0")), 1e-4f);
            Assert.AreEqual(5f, resolver.Resolve(5f, ResolveKind.RecipeDuration, new ResolveContext("field#1")), 1e-4f,
                "widening scope for global types must not have widened station.* too");
        }

        [Test]
        public void StationAndGlobalSpeed_FoldIntoOneAdditivePool()
        {
            var bus = new EventBus();
            var wallet = new Wallet(bus);
            var byType = new Dictionary<string, IReadOnlyList<UpgradeTrackModel>>
            {
                ["field"] = new[] { new UpgradeTrackModel("field.speed", "field.speed", 1, new[]
                    { new UpgradeTierModel(50, new[] { E(EffectType.StationSpeed, EffectOp.Pct, 25f) }) }) },
                ["workshop"] = new[] { GlobalSpeedTrack() }
            };
            var upgrades = new UpgradeSystem(bus, wallet, byType, () => 1);
            var resolver = new ValueResolver();
            resolver.SetEffectSource(upgrades);

            upgrades.Register("field#0", "field");
            upgrades.Register("workshop#0", "workshop");
            wallet.Add(1000);
            upgrades.Purchase("field#0", "field.speed");        // station.speed +25%
            upgrades.Purchase("workshop#0", "workshop.speed");  // global.speed  +25%

            // One stat, two reaches: +50% speed => 6s / 1.5 = 4s. NOT 6 / 1.25 / 1.25 = 3.84 (§3.5).
            Assert.AreEqual(4f, resolver.Resolve(6f, ResolveKind.RecipeDuration, new ResolveContext("field#0")), 1e-4f,
                "reach must not earn a modifier its own separate multiplication");
        }

        [Test]
        public void EveryEffectType_HasADeclaredScope()
        {
            foreach (EffectType type in System.Enum.GetValues(typeof(EffectType)))
                Assert.DoesNotThrow(() => EffectScopes.Of(type), $"{type} has no scope declared");
        }
    }
}
