using System;
using System.Collections.Generic;
using VoidDay.Core.Events;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// The quest engine (§ quest system) — pure C# in Core, no UnityEngine, so the balance tool compiles it in
    /// and runs quests headless (M4). It listens and announces; it never calls another system. Three moving
    /// parts:
    ///
    ///  • GRANT   — an ungranted quest whose conditions are all true becomes active (QuestGranted).
    ///  • TRACK   — a goal-relevant event advances the active quest's progress bar (QuestProgressed), and a bar
    ///              that fills reports ready-to-collect (QuestCompleted).
    ///  • COLLECT — a CollectQuestRequested intent on a ready quest applies the reward through the existing
    ///              sinks and retires the quest (QuestCollected).
    ///
    /// Progress can never decrease. Each active quest holds a running MAX of units achieved, measured against a
    /// baseline snapshotted at grant time — so spending money (a negative MoneyChanged) cannot drop an
    /// "earn $X" bar, and a re-derived value that dips just fails the max test and is ignored.
    ///
    /// Lifetime: one QuestLog per game, constructed at boot and living for the app — like OrderBoard and
    /// Progression it subscribes in its constructor and never tears down.
    public sealed class QuestLog
    {
        public const string XpSource = "quest"; // AwardXp source tag for a collected quest's XP burst

        readonly EventBus _bus;
        readonly Func<int> _playerLevel;
        readonly ResourcePool _pool;
        readonly UpgradeSystem _upgrades;   // read handle for a future UpgradePurchased condition; unused today
        readonly Wallet _wallet;
        readonly GemPurse _gems;
        readonly Progression _progression;
        readonly Func<string, string> _displayName; // resource / station / upgrade-track id → label, for descriptions

        readonly List<QuestModel> _candidates;              // ungranted, not-yet-completed
        readonly List<ActiveQuest> _active = new();
        readonly HashSet<string> _completed = new();        // collected quests — the QuestCompleted-condition set

        sealed class ActiveQuest
        {
            public readonly QuestModel Quest;
            public readonly string Description;
            public readonly int BaseLevel; // player level at grant — the baseline for a ReachLevel goal
            public readonly long Needed;   // denominator: units of the goal that must be achieved
            public long Progressed;        // running MAX of units achieved, in [0, Needed]
            public bool Ready;

            public ActiveQuest(QuestModel quest, string description, int baseLevel, long needed)
            {
                Quest = quest;
                Description = description;
                BaseLevel = baseLevel;
                Needed = needed;
            }

            public float Fraction => Needed <= 0 ? 1f : Math.Min(1f, (float)Progressed / Needed);
        }

        public QuestLog(EventBus bus, IReadOnlyList<QuestModel> quests, Func<int> playerLevel,
            ResourcePool pool, UpgradeSystem upgrades, Wallet wallet, GemPurse gems, Progression progression,
            Func<string, string> displayName)
        {
            _bus = bus;
            _playerLevel = playerLevel;
            _pool = pool;
            _upgrades = upgrades;
            _wallet = wallet;
            _gems = gems;
            _progression = progression;
            _displayName = displayName;
            _candidates = new List<QuestModel>(quests);

            // State-change events re-check grant conditions; action events advance progress. Initial grants
            // run on GameStarted, not here, so the menu view (subscribed in its own Init) sees QuestGranted.
            _bus.Subscribe<GameStarted>(OnGameStarted);
            _bus.Subscribe<LevelUp>(OnLevelUp);
            _bus.Subscribe<ResourceChanged>(OnResourceChanged);
            _bus.Subscribe<UpgradePurchased>(OnUpgradePurchased);
            _bus.Subscribe<StationBuilt>(OnStationBuilt);
            _bus.Subscribe<MoneyChanged>(OnMoneyChanged);
            _bus.Subscribe<OrderFulfilled>(OnOrderFulfilled);
            _bus.Subscribe<JobCollected>(OnJobCollected);
            _bus.Subscribe<CollectQuestRequested>(OnCollectRequested);
        }

        // ---- Queries the menu renders from ----

        public IReadOnlyList<QuestStatus> ActiveQuests()
        {
            var list = new List<QuestStatus>(_active.Count);
            foreach (var q in _active)
                list.Add(new QuestStatus(q.Quest.Id, q.Description, q.Fraction, q.Ready));
            return list;
        }

        // ---- Grant ----

        void OnGameStarted(GameStarted _) => EvaluateGrants();
        void OnResourceChanged(ResourceChanged _) => EvaluateGrants();

        void OnUpgradePurchased(UpgradePurchased e)
        {
            EvaluateGrants();     // an UpgradePurchased grant condition may have opened
            foreach (var q in _active)
                if (!q.Ready && q.Quest.Goal.Kind == GoalKind.PurchaseUpgrades && e.UpgradeId == q.Quest.Goal.TargetId)
                    Advance(q, q.Progressed + 1); // one purchased tier = one unit
        }

        void OnLevelUp(LevelUp _)
        {
            EvaluateGrants();     // a MinLevel gate may have opened
            AdvanceReachLevel();  // a ReachLevel bar may have risen
        }

        /// Re-check every candidate; grant the ones whose conditions are all met. Backwards scan so a granted
        /// candidate can be removed in place. A grant publishes only QuestGranted (nothing this method
        /// subscribes to), so no re-entrancy — one pass settles it.
        void EvaluateGrants()
        {
            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                if (!ConditionsMet(_candidates[i])) continue;
                Grant(_candidates[i]);
                _candidates.RemoveAt(i);
            }
        }

        bool ConditionsMet(QuestModel quest)
        {
            foreach (var c in quest.Conditions)
                if (!ConditionMet(c)) return false;
            return true;
        }

        bool ConditionMet(QuestConditionModel c) => c.Kind switch
        {
            ConditionKind.MinLevel => _playerLevel() >= c.Amount,
            ConditionKind.ResourceAtLeast => _pool.Get(c.Arg) >= c.Amount,
            ConditionKind.QuestCompleted => _completed.Contains(c.Arg),
            _ => throw new NotImplementedException(
                $"QuestLog: condition kind {c.Kind} is not implemented (no example quest exercises it)")
        };

        void Grant(QuestModel quest)
        {
            int baseLevel = _playerLevel();
            long needed = NeededUnits(quest.Goal, baseLevel);
            string description = QuestDescription.Describe(quest.Goal, _displayName);
            var active = new ActiveQuest(quest, description, baseLevel, needed);
            _active.Add(active);
            _bus.Publish(new QuestGranted(quest.Id, description));

            // A goal already satisfied the instant it is granted (a ReachLevel target already at/below the
            // player's level) completes at once rather than being uncollectable forever.
            if (needed <= 0) MarkComplete(active);
            else if (quest.Goal.Kind == GoalKind.ReachLevel) Advance(active, _playerLevel() - baseLevel);
        }

        /// The denominator for a goal: for a ReachLevel goal it is the number of levels still to gain from the
        /// grant baseline (so a "reach level 5" granted at level 3 needs 2, not 5); every other goal counts its
        /// own units from zero.
        static long NeededUnits(QuestGoalModel goal, int baseLevel) =>
            goal.Kind == GoalKind.ReachLevel ? goal.Amount - baseLevel : goal.Amount;

        // ---- Track ----

        void OnMoneyChanged(MoneyChanged e)
        {
            if (e.Delta <= 0) return; // spending never advances an earn goal — only income does
            foreach (var q in _active)
                if (!q.Ready && q.Quest.Goal.Kind == GoalKind.EarnMoney)
                    Advance(q, q.Progressed + e.Delta);
        }

        void OnOrderFulfilled(OrderFulfilled _)
        {
            foreach (var q in _active)
                if (!q.Ready && q.Quest.Goal.Kind == GoalKind.FulfillOrders)
                    Advance(q, q.Progressed + 1);
        }

        void OnStationBuilt(StationBuilt e)
        {
            foreach (var q in _active)
                if (!q.Ready && q.Quest.Goal.Kind == GoalKind.BuildStations && e.StationType == q.Quest.Goal.TargetId)
                    Advance(q, q.Progressed + 1); // one built station of the target type = one unit
        }

        void OnJobCollected(JobCollected e)
        {
            foreach (var q in _active)
            {
                if (q.Ready || q.Quest.Goal.Kind != GoalKind.HarvestCrops) continue;
                long harvested = 0;
                foreach (var o in e.Outputs)
                    if (o.ResourceId == q.Quest.Goal.TargetId) harvested += o.Amount;
                if (harvested > 0) Advance(q, q.Progressed + harvested);
            }
        }

        void AdvanceReachLevel()
        {
            int level = _playerLevel();
            foreach (var q in _active)
                if (!q.Ready && q.Quest.Goal.Kind == GoalKind.ReachLevel)
                    Advance(q, level - q.BaseLevel);
        }

        /// Raise a quest's progress to `achieved` units — but never lower it (the running max). Publishes
        /// QuestProgressed on a real rise, and QuestCompleted the first time the bar reaches full.
        void Advance(ActiveQuest q, long achieved)
        {
            achieved = Math.Min(achieved, q.Needed);
            if (achieved <= q.Progressed) return;
            q.Progressed = achieved;
            _bus.Publish(new QuestProgressed(q.Quest.Id, q.Fraction));
            if (q.Fraction >= 1f) MarkComplete(q);
        }

        void MarkComplete(ActiveQuest q)
        {
            if (q.Ready) return;
            q.Ready = true;
            _bus.Publish(new QuestCompleted(q.Quest.Id));
        }

        // ---- Collect ----

        void OnCollectRequested(CollectQuestRequested e)
        {
            var q = _active.Find(a => a.Quest.Id == e.QuestId);
            if (q == null || !q.Ready)
                throw new InvalidOperationException(
                    $"QuestLog: collect requested for '{e.QuestId}', which is not ready to collect");

            ApplyReward(q.Quest.Reward); // XP last — its level-up cascade re-checks grants mid-apply
            _active.Remove(q);
            _completed.Add(q.Quest.Id);
            _bus.Publish(new QuestCollected(q.Quest.Id));
            EvaluateGrants(); // a QuestCompleted-gated chain unlocks now that this quest is in the completed set
        }

        void ApplyReward(QuestRewardModel r)
        {
            if (r.Money > 0) _wallet.Add(r.Money);
            if (r.Gems > 0) _gems.Add(r.Gems);
            foreach (var res in r.Resources) _pool.Add(res.ResourceId, res.Amount);
            if (r.Xp > 0) _progression.AwardXp(r.Xp, XpSource);
        }
    }
}
