using System.Collections.Generic;
using NUnit.Framework;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

namespace VoidDay.Tests
{
    /// Levelling (§9): threshold crossing — including several thresholds from one grant — and the generic
    /// grant applier, plus the seam reads those grants move. Pure C#, headless: exactly the economy core
    /// CLAUDE.md says to test, and the kind of bug that never crashes, it just makes the game wrong.
    public static class Levels
    {
        /// Thresholds 0/10/25/45, no grants — the plain curve for tests that only care about crossings.
        public static LevelCurve Plain() => Curve(
            (0, null), (10, null), (25, null), (45, null));

        public static LevelCurve Curve(params (int threshold, LevelGrantModel[] grants)[] rows)
        {
            var levels = new List<LevelModel>(rows.Length);
            for (int i = 0; i < rows.Length; i++)
                levels.Add(new LevelModel(i + 1, rows[i].threshold,
                    rows[i].grants ?? System.Array.Empty<LevelGrantModel>()));
            return new LevelCurve(levels);
        }

        public static LevelGrantModel Grant(LevelEntryKind kind, int amount, string target = "",
            string label = "") => new LevelGrantModel(kind, target, label, amount);
    }

    public sealed class LevelTests
    {
        EventBus _bus;
        Wallet _wallet;
        ValueResolver _resolver;
        LevelGrants _grants;
        readonly List<LevelUp> _levelUps = new();
        readonly List<UnlockGranted> _unlocks = new();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _wallet = new Wallet(_bus);
            _resolver = new ValueResolver();
            _grants = new LevelGrants();
            _resolver.SetGrantSource(_grants);
            _levelUps.Clear();
            _unlocks.Clear();
            _bus.Subscribe<LevelUp>(e => _levelUps.Add(e));
            _bus.Subscribe<UnlockGranted>(e => _unlocks.Add(e));
        }

        Progression Make(LevelCurve curve, params LevelUnlockModel[] gated) =>
            new Progression(_bus, _resolver, curve, _grants, _wallet, gated);

        // ---- Threshold crossing ----

        [Test]
        public void StartsAtLevelOneWithNoXp()
        {
            var p = Make(Levels.Plain());
            Assert.AreEqual(1, p.PlayerLevel);
            Assert.AreEqual(0, p.XpTotal);
            Assert.IsEmpty(_levelUps);
        }

        [Test]
        public void XpBelowTheThresholdDoesNotLevel()
        {
            var p = Make(Levels.Plain());
            p.AwardXp(9, "test");
            Assert.AreEqual(1, p.PlayerLevel);
            Assert.IsEmpty(_levelUps);
        }

        [Test]
        public void CrossingTheThresholdLevelsUpOnce()
        {
            var p = Make(Levels.Plain());
            p.AwardXp(10, "test");
            Assert.AreEqual(2, p.PlayerLevel);
            Assert.AreEqual(1, _levelUps.Count);
            Assert.AreEqual(2, _levelUps[0].Level);
        }

        [Test]
        public void OneGrantSpanningThreeThresholdsFiresOnePayloadPerLevel()
        {
            var p = Make(Levels.Plain());
            p.AwardXp(45, "test");

            Assert.AreEqual(4, p.PlayerLevel);
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, _levelUps.ConvertAll(e => e.Level));
        }

        [Test]
        public void LevellingStopsAtTheTopOfTheCurve()
        {
            var p = Make(Levels.Plain());
            p.AwardXp(100_000, "test");

            Assert.AreEqual(4, p.PlayerLevel, "the curve holds four levels");
            Assert.IsTrue(p.IsMaxLevel);
            Assert.AreEqual(0, p.XpToNextLevel);
        }

        [Test]
        public void XpToNextLevelIsWhatTheThresholdStillOwes()
        {
            var p = Make(Levels.Plain());
            p.AwardXp(4, "test");
            Assert.AreEqual(6, p.XpToNextLevel);

            p.AwardXp(p.XpToNextLevel, "debug"); // the debug cheat's exact grant
            Assert.AreEqual(2, p.PlayerLevel);
        }

        [Test]
        public void BarProgressReadsAgainstTheCurrentLevelsSpan()
        {
            var p = Make(Levels.Plain());
            p.AwardXp(15, "test"); // level 2 spans 10..25

            Assert.AreEqual(5, p.XpIntoLevel);
            Assert.AreEqual(15, p.XpSpanOfLevel);
        }

        // ---- Grant application ----

        [Test]
        public void AStationCapGrantRaisesThatTypesResolvedCap()
        {
            var p = Make(Levels.Curve(
                (0, null),
                (10, new[] { Levels.Grant(LevelEntryKind.StationCap, 1, "field", "Field") })));
            p.AwardXp(10, "test");

            Assert.AreEqual(3f, _resolver.Resolve(2f, ResolveKind.StationCap,
                new ResolveContext(null, stationType: "field")));
            Assert.AreEqual(1f, _resolver.Resolve(1f, ResolveKind.StationCap,
                new ResolveContext(null, stationType: "henhouse")), "a cap grant is scoped to its own type");
        }

        [Test]
        public void AnUntargetedQueueDepthGrantReachesEveryStationType()
        {
            var p = Make(Levels.Curve(
                (0, null),
                (10, new[] { Levels.Grant(LevelEntryKind.QueueDepth, 1) })));
            p.AwardXp(10, "test");

            Assert.AreEqual(4f, _resolver.Resolve(3f, ResolveKind.QueueDepth,
                new ResolveContext("field#0", stationType: "field")));
            Assert.AreEqual(4f, _resolver.Resolve(3f, ResolveKind.QueueDepth,
                new ResolveContext("bakery#0", stationType: "bakery")));
        }

        [Test]
        public void OrderSlotGrantsAccumulateAcrossLevels()
        {
            var p = Make(Levels.Curve(
                (0, null),
                (10, new[] { Levels.Grant(LevelEntryKind.OrderSlots, 1) }),
                (25, new[] { Levels.Grant(LevelEntryKind.OrderSlots, 1) })));
            p.AwardXp(25, "test");

            Assert.AreEqual(5f, _resolver.Resolve(3f, ResolveKind.OrderSlots, new ResolveContext(null)));
        }

        [Test]
        public void AMoneyGrantIsPaidOutAndReportedAsAReward()
        {
            var p = Make(Levels.Curve(
                (0, null),
                (10, new[] { Levels.Grant(LevelEntryKind.Money, 250) })));
            p.AwardXp(10, "test");

            Assert.AreEqual(250, _wallet.Money);
            Assert.AreEqual(1, _levelUps[0].Rewards.Count);
            Assert.AreEqual(250, _levelUps[0].Rewards[0].Amount);
            Assert.IsEmpty(_levelUps[0].Unlocks, "a payout is a reward, not an unlock");
        }

        [Test]
        public void MoneyIsPaidOnceNotAccumulatedAsAStandingBonus()
        {
            var p = Make(Levels.Curve(
                (0, null),
                (10, new[] { Levels.Grant(LevelEntryKind.Money, 100) }),
                (25, new[] { Levels.Grant(LevelEntryKind.Money, 100) })));
            p.AwardXp(25, "test");

            Assert.AreEqual(200, _wallet.Money);
        }

        // ---- Derived gates ----

        [Test]
        public void AStationTypeGatedAtThisLevelIsAnnouncedAndListed()
        {
            var p = Make(Levels.Plain(),
                new LevelUnlockModel(LevelEntryKind.StationType, "henhouse", "Henhouse", 3),
                new LevelUnlockModel(LevelEntryKind.Upgrade, "field.queue", "Queue Depth", 2));
            p.AwardXp(25, "test"); // crosses to level 3

            Assert.AreEqual("Queue Depth", _levelUps[0].Unlocks[0].Label, "level 2 opened the upgrade track");
            Assert.AreEqual("Henhouse", _levelUps[1].Unlocks[0].Label, "level 3 opened the station type");
            CollectionAssert.AreEqual(new[] { "Upgrade", "StationType" }, _unlocks.ConvertAll(u => u.Kind));
        }

        [Test]
        public void GrantsAndGatesShareOneUnlockList()
        {
            var p = Make(Levels.Curve(
                    (0, null),
                    (10, new[] { Levels.Grant(LevelEntryKind.OrderSlots, 1) })),
                new LevelUnlockModel(LevelEntryKind.StationType, "henhouse", "Henhouse", 2));
            p.AwardXp(10, "test");

            Assert.AreEqual(2, _levelUps[0].Unlocks.Count);
        }

        // ---- Reset ----

        [Test]
        public void ResetDropsTheLevelAndEveryGrantedBonus()
        {
            var p = Make(Levels.Curve(
                (0, null),
                (10, new[] { Levels.Grant(LevelEntryKind.OrderSlots, 2) })));
            p.AwardXp(10, "test");
            p.Reset();

            Assert.AreEqual(1, p.PlayerLevel);
            Assert.AreEqual(0, p.XpTotal);
            Assert.AreEqual(3f, _resolver.Resolve(3f, ResolveKind.OrderSlots, new ResolveContext(null)));
        }

        // ---- Grants and effects stack, they do not compete ----

        [Test]
        public void AQueueUpgradeAppliesOnTopOfTheLevelRaisedBase()
        {
            var p = Make(Levels.Curve(
                (0, null),
                (10, new[] { Levels.Grant(LevelEntryKind.QueueDepth, 1) })));
            p.AwardXp(10, "test");

            var track = new UpgradeTrackModel("field.queue", "Queue Depth", 1, new[]
            {
                new UpgradeTierModel(50, new[]
                {
                    new Effect { type = EffectType.StationQueueDepth, value = new EffectValue
                        { op = EffectOp.Flat, amount = 1 }, triggerChance = 100 }
                })
            });
            var byType = new Dictionary<string, IReadOnlyList<UpgradeTrackModel>>
            {
                ["field"] = new[] { track }
            };
            var upgrades = new UpgradeSystem(_bus, _wallet, byType, () => p.PlayerLevel);
            _resolver.SetEffectSource(upgrades);
            upgrades.Register("field#0", "field");
            _wallet.Add(50);
            upgrades.Purchase("field#0", "field.queue");

            Assert.AreEqual(5f, _resolver.Resolve(3f, ResolveKind.QueueDepth,
                new ResolveContext("field#0", stationType: "field")),
                "base 3 + level grant 1 + upgrade 1");
        }
    }
}
