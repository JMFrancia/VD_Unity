using System;
using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Model;

namespace VoidDay.Data
{
    /// One asset per quest (§ quest system, §14). Conditions gate the grant; the goal drives the progress bar;
    /// the reward is paid on collect. Flat [Serializable] structs with enum discriminators — inspector-friendly
    /// and KISS, mirroring Effect.cs's Condition. Projected to a pure-Core QuestModel at boot (Core reads the
    /// model, never this SO). Listed on GameConfigSO.quests, like the station roster.
    [CreateAssetMenu(menuName = "VoidDay/Quest", fileName = "Quest")]
    public sealed class QuestSO : ScriptableObject
    {
        [Tooltip("Unique quest id — referenced by a QuestCompleted condition to build a chain.")]
        public string id;

        [Tooltip("ALL must be true for the quest to be granted. Empty = granted at the start of the run.")]
        public List<QuestCondition> conditions = new();

        public QuestGoal goal;
        public QuestReward reward;
    }

    /// A grant gate. `amount`/`arg` carry per-kind data — see ConditionKind.
    [Serializable]
    public struct QuestCondition
    {
        public ConditionKind kind;

        [Tooltip("MinLevel: required player level. ResourceAtLeast: required count.")]
        public int amount;

        [Tooltip("ResourceAtLeast: resource id. QuestCompleted: prerequisite quest id. Ignored by MinLevel.")]
        public string arg;
    }

    /// What the player must do to fill the bar. `amount` is the target; `targetId` names the crop for
    /// HarvestCrops and is ignored by the untargeted kinds.
    [Serializable]
    public struct QuestGoal
    {
        public GoalKind kind;
        public int amount;

        [Tooltip("HarvestCrops: crop resource id. Ignored by EarnMoney / FulfillOrders / ReachLevel.")]
        public string targetId;
    }

    /// The payout on collect. `xp` is always paid; money/gems/resources are optional (0 / empty when unused).
    [Serializable]
    public struct QuestReward
    {
        [Tooltip("XP awarded on collect. This is what throws the collect particle burst, so keep it > 0.")]
        public int xp;
        public int money;
        public int gems;
        public List<QuestResourceGrant> resources;
    }

    /// A resource line in a quest reward — the authoring shape (a ResourceSO ref + amount), sibling of
    /// Ingredient / StartingResource. Projected to a Core ResourceAmount (id + amount) at boot.
    [Serializable]
    public struct QuestResourceGrant
    {
        public ResourceSO resource;
        public int amount;
    }
}
