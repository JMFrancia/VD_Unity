using System.Collections.Generic;
using NUnit.Framework;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

namespace VoidDay.Tests
{
    /// The quest engine (§ quest system): grant on conditions, monotonic progress that never drops on a spend,
    /// completion → ready → collect through the real sinks, and QuestCompleted chains. Pure C#, headless —
    /// exactly the economy core CLAUDE.md says to test, and the kind of bug that never crashes, it just makes
    /// the game quietly wrong.
    public sealed class QuestTests
    {
        EventBus _bus;
        ValueResolver _resolver;
        LevelGrants _grants;
        Wallet _wallet;
        GemPurse _gems;
        ResourcePool _pool;
        UpgradeSystem _upgrades;
        Progression _progression;

        readonly List<QuestGranted> _granted = new();
        readonly List<QuestCompleted> _completed = new();
        readonly List<QuestCollected> _collected = new();
        readonly List<QuestProgressed> _progressed = new();

        [SetUp]
        public void SetUp()
        {
            _bus = new EventBus();
            _resolver = new ValueResolver();
            _grants = new LevelGrants();
            _resolver.SetGrantSource(_grants);
            _wallet = new Wallet(_bus);
            _gems = new GemPurse(_bus, 0);
            _pool = new ResourcePool(_bus, _resolver);
            _upgrades = new UpgradeSystem(_bus, _wallet,
                new Dictionary<string, IReadOnlyList<UpgradeTrackModel>>(), () => _progression.PlayerLevel);
            _progression = new Progression(_bus, _resolver, Levels.Plain(), _grants, _wallet, _gems,
                System.Array.Empty<LevelUnlockModel>());

            _granted.Clear();
            _completed.Clear();
            _collected.Clear();
            _progressed.Clear();
            _bus.Subscribe<QuestGranted>(e => _granted.Add(e));
            _bus.Subscribe<QuestCompleted>(e => _completed.Add(e));
            _bus.Subscribe<QuestCollected>(e => _collected.Add(e));
            _bus.Subscribe<QuestProgressed>(e => _progressed.Add(e));
        }

        QuestLog Make(params QuestModel[] quests)
        {
            var log = new QuestLog(_bus, quests, () => _progression.PlayerLevel, _pool, _upgrades,
                _wallet, _gems, _progression, id => id);
            return log;
        }

        static QuestModel Quest(string id, QuestGoalModel goal, QuestRewardModel reward,
            params QuestConditionModel[] conditions) =>
            new QuestModel(id, conditions, goal, reward);

        static QuestGoalModel Goal(GoalKind kind, int amount, string target = "") =>
            new QuestGoalModel(kind, amount, target);

        static QuestRewardModel Reward(int xp = 0, int money = 0, int gems = 0,
            IReadOnlyList<ResourceAmount> resources = null) =>
            new QuestRewardModel(xp, money, gems, resources ?? System.Array.Empty<ResourceAmount>());

        static QuestConditionModel Cond(ConditionKind kind, int amount = 0, string arg = "") =>
            new QuestConditionModel(kind, amount, arg);

        float ProgressOf(QuestLog log, string id)
        {
            foreach (var q in log.ActiveQuests())
                if (q.Id == id) return q.Progress;
            return -1f;
        }

        bool IsReady(QuestLog log, string id)
        {
            foreach (var q in log.ActiveQuests())
                if (q.Id == id) return q.Ready;
            return false;
        }

        // ---- Grant ----

        [Test]
        public void AQuestWithMetConditionsIsGrantedOnGameStarted()
        {
            var log = Make(Quest("q", Goal(GoalKind.EarnMoney, 100), Reward(xp: 10),
                Cond(ConditionKind.MinLevel, 1)));
            _bus.Publish(new GameStarted());

            Assert.AreEqual(1, _granted.Count);
            Assert.AreEqual("q", _granted[0].QuestId);
            Assert.AreEqual(1, log.ActiveQuests().Count);
        }

        [Test]
        public void AQuestGatedAboveTheStartLevelIsNotGrantedUntilThatLevel()
        {
            var log = Make(Quest("q", Goal(GoalKind.EarnMoney, 100), Reward(xp: 10),
                Cond(ConditionKind.MinLevel, 2)));
            _bus.Publish(new GameStarted());
            Assert.IsEmpty(_granted, "level 1 does not meet MinLevel 2");

            _progression.AwardXp(10, "test"); // crosses to level 2 (Plain curve threshold 10)
            Assert.AreEqual(1, _granted.Count, "the MinLevel gate opened on level-up");
        }

        [Test]
        public void AResourceAtLeastGateOpensWhenTheResourceIsHeld()
        {
            var log = Make(Quest("q", Goal(GoalKind.EarnMoney, 100), Reward(xp: 10),
                Cond(ConditionKind.ResourceAtLeast, 5, "wheat")));
            _bus.Publish(new GameStarted());
            Assert.IsEmpty(_granted);

            _pool.Add("wheat", 5); // publishes ResourceChanged → re-evaluate
            Assert.AreEqual(1, _granted.Count);
        }

        // ---- Track ----

        [Test]
        public void EarnMoneyProgressRisesWithIncomeAndNeverDropsOnASpend()
        {
            var log = Make(Quest("q", Goal(GoalKind.EarnMoney, 100), Reward(xp: 10),
                Cond(ConditionKind.MinLevel, 1)));
            _bus.Publish(new GameStarted());

            _bus.Publish(new MoneyChanged(50, 50));
            Assert.AreEqual(0.5f, ProgressOf(log, "q"), 1e-4);

            _bus.Publish(new MoneyChanged(-40, 10)); // a spend
            Assert.AreEqual(0.5f, ProgressOf(log, "q"), 1e-4, "spending must not drop earn progress");

            _bus.Publish(new MoneyChanged(50, 60));
            Assert.AreEqual(1f, ProgressOf(log, "q"), 1e-4);
            Assert.IsTrue(IsReady(log, "q"));
            Assert.AreEqual(1, _completed.Count);
        }

        [Test]
        public void FulfillOrdersProgressCountsEachOrder()
        {
            var log = Make(Quest("q", Goal(GoalKind.FulfillOrders, 2), Reward(xp: 10),
                Cond(ConditionKind.MinLevel, 1)));
            _bus.Publish(new GameStarted());

            _bus.Publish(new OrderFulfilled("o1", 10, 1));
            Assert.AreEqual(0.5f, ProgressOf(log, "q"), 1e-4);
            _bus.Publish(new OrderFulfilled("o2", 10, 1));
            Assert.AreEqual(1f, ProgressOf(log, "q"), 1e-4);
            Assert.IsTrue(IsReady(log, "q"));
        }

        [Test]
        public void HarvestCropsCountsOnlyTheTargetResource()
        {
            var log = Make(Quest("q", Goal(GoalKind.HarvestCrops, 3, "wheat"), Reward(xp: 10),
                Cond(ConditionKind.MinLevel, 1)));
            _bus.Publish(new GameStarted());

            _bus.Publish(new JobCollected("field#0",
                new[] { new ResourceAmount("corn", 5) }, false));
            Assert.AreEqual(0f, ProgressOf(log, "q"), 1e-4, "a non-target crop does not advance the goal");

            _bus.Publish(new JobCollected("field#0",
                new[] { new ResourceAmount("wheat", 3) }, false));
            Assert.AreEqual(1f, ProgressOf(log, "q"), 1e-4);
        }

        [Test]
        public void ReachLevelMeasuresFromTheGrantBaseline()
        {
            var log = Make(Quest("q", Goal(GoalKind.ReachLevel, 3), Reward(xp: 10),
                Cond(ConditionKind.MinLevel, 1)));
            _bus.Publish(new GameStarted()); // granted at level 1, target 3 → needs 2 levels

            Assert.AreEqual(0f, ProgressOf(log, "q"), 1e-4);
            _progression.AwardXp(10, "test"); // level 2
            Assert.AreEqual(0.5f, ProgressOf(log, "q"), 1e-4);
            _progression.AwardXp(15, "test"); // level 3 (threshold 25)
            Assert.AreEqual(1f, ProgressOf(log, "q"), 1e-4);
            Assert.IsTrue(IsReady(log, "q"));
        }

        // ---- Collect ----

        [Test]
        public void CollectingAppliesXpAndResourcesThroughTheSinksAndRetiresTheQuest()
        {
            var log = Make(Quest("q", Goal(GoalKind.EarnMoney, 10),
                Reward(xp: 40, money: 25, resources: new[] { new ResourceAmount("wheat", 2) }),
                Cond(ConditionKind.MinLevel, 1)));
            _bus.Publish(new GameStarted());
            _bus.Publish(new MoneyChanged(10, 10)); // reach 100%

            Assert.IsTrue(IsReady(log, "q"));
            int walletBefore = _wallet.Money;

            _bus.Publish(new CollectQuestRequested("q"));

            Assert.AreEqual(40, _progression.XpTotal, "xp awarded through Progression");
            Assert.AreEqual(2, _pool.Get("wheat"), "resource granted through the pool");
            Assert.AreEqual(walletBefore + 25, _wallet.Money, "money granted through the wallet");
            Assert.AreEqual(1, _collected.Count);
            Assert.IsEmpty(log.ActiveQuests(), "a collected quest is retired");
        }

        [Test]
        public void ACollectedQuestNeverReGrants()
        {
            var log = Make(Quest("q", Goal(GoalKind.EarnMoney, 10), Reward(xp: 10),
                Cond(ConditionKind.MinLevel, 1)));
            _bus.Publish(new GameStarted());
            _bus.Publish(new MoneyChanged(10, 10));
            _bus.Publish(new CollectQuestRequested("q"));

            _granted.Clear();
            _pool.Add("wheat", 1);        // fire more state-change events
            _progression.AwardXp(10, "x");
            Assert.IsEmpty(_granted, "a retired quest is out of the candidate pool for good");
        }

        [Test]
        public void CollectingAQuestThatIsNotReadyThrows()
        {
            var log = Make(Quest("q", Goal(GoalKind.EarnMoney, 100), Reward(xp: 10),
                Cond(ConditionKind.MinLevel, 1)));
            _bus.Publish(new GameStarted());

            Assert.Throws<System.InvalidOperationException>(
                () => _bus.Publish(new CollectQuestRequested("q")));
        }

        // ---- Chains ----

        [Test]
        public void AQuestCompletedGateOpensOnlyAfterThePrerequisiteIsCollected()
        {
            var a = Quest("a", Goal(GoalKind.EarnMoney, 10), Reward(xp: 10), Cond(ConditionKind.MinLevel, 1));
            var b = Quest("b", Goal(GoalKind.EarnMoney, 10), Reward(xp: 10),
                Cond(ConditionKind.QuestCompleted, 0, "a"));
            var log = Make(a, b);
            _bus.Publish(new GameStarted());

            Assert.AreEqual(1, _granted.Count, "only 'a' is grantable at the start");
            Assert.AreEqual("a", _granted[0].QuestId);

            _bus.Publish(new MoneyChanged(10, 10)); // 'a' reaches 100%
            Assert.AreEqual(1, _granted.Count, "reaching 100% is not collecting — 'b' still gated");

            _bus.Publish(new CollectQuestRequested("a")); // collect → 'a' enters the completed set
            Assert.AreEqual(2, _granted.Count, "'b' unlocks once 'a' is collected");
            Assert.AreEqual("b", _granted[1].QuestId);
        }

        // ---- Description ----

        [Test]
        public void TheGrantedDescriptionIsGeneratedFromTheGoal()
        {
            var log = Make(Quest("q", Goal(GoalKind.EarnMoney, 250), Reward(xp: 10),
                Cond(ConditionKind.MinLevel, 1)));
            _bus.Publish(new GameStarted());
            Assert.AreEqual("Earn $250", _granted[0].Description);
        }
    }
}
