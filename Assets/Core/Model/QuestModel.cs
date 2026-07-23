using System.Collections.Generic;

namespace VoidDay.Core.Model
{
    /// Rule-relevant projection of a QuestSO (§14) — pure Core, no Unity handles. QuestLog reads a QuestModel,
    /// never a QuestSO, exactly as the rest of the economy reads *Model not *SO. The SO/icon refs are dropped
    /// in projection; Core speaks resource ids and quest ids.
    public sealed class QuestModel
    {
        public readonly string Id;
        public readonly IReadOnlyList<QuestConditionModel> Conditions;
        public readonly QuestGoalModel Goal;
        public readonly QuestRewardModel Reward;

        public QuestModel(string id, IReadOnlyList<QuestConditionModel> conditions, QuestGoalModel goal,
            QuestRewardModel reward)
        {
            Id = id;
            Conditions = conditions;
            Goal = goal;
            Reward = reward;
        }
    }

    /// One grant gate. Arg carries per-kind data (resource id / prerequisite quest id / track id); Amount
    /// carries the threshold (required level / count).
    public sealed class QuestConditionModel
    {
        public readonly ConditionKind Kind;
        public readonly int Amount;
        public readonly string Arg;

        public QuestConditionModel(ConditionKind kind, int amount, string arg)
        {
            Kind = kind;
            Amount = amount;
            Arg = arg ?? "";
        }
    }

    /// The action the quest tracks. TargetId names the crop/recipe/station id the goal is about; kinds that
    /// have no target (EarnMoney, ReachLevel, FulfillOrders) leave it empty.
    public sealed class QuestGoalModel
    {
        public readonly GoalKind Kind;
        public readonly int Amount;
        public readonly string TargetId;

        public QuestGoalModel(GoalKind kind, int amount, string targetId)
        {
            Kind = kind;
            Amount = amount;
            TargetId = targetId ?? "";
        }
    }

    /// What collecting the quest pays out. Xp is the always-present reward; money/gems/resources are optional
    /// (0 / empty when unused). Applied through the same sinks the rest of the game uses (Wallet, GemPurse,
    /// ResourcePool, Progression), never a bespoke grant path.
    public sealed class QuestRewardModel
    {
        public readonly int Xp;
        public readonly int Money;
        public readonly int Gems;
        public readonly IReadOnlyList<ResourceAmount> Resources;

        public QuestRewardModel(int xp, int money, int gems, IReadOnlyList<ResourceAmount> resources)
        {
            Xp = xp;
            Money = money;
            Gems = gems;
            Resources = resources;
        }
    }

    /// A read-only snapshot of one active quest, for the menu to render. The menu never reaches into QuestLog's
    /// internal state — it renders these facts (description already generated, progress already clamped).
    public readonly struct QuestStatus
    {
        public readonly string Id;
        public readonly string Description;
        public readonly float Progress; // [0,1], monotonic
        public readonly bool Ready;      // reached 100%, awaiting collect

        public QuestStatus(string id, string description, float progress, bool ready)
        {
            Id = id;
            Description = description;
            Progress = progress;
            Ready = ready;
        }
    }
}
