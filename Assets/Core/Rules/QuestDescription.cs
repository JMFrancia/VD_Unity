using System;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// The player-facing quest string, generated from the goal (§ quest system "Description") — never authored
    /// on the SO, so a designer editing a goal's amount cannot leave the wording stale. Display names come from
    /// the projected models (via the resourceName lookup), never hardcoded. Sibling of TraitDescription.
    public static class QuestDescription
    {
        public static string Describe(QuestGoalModel goal, Func<string, string> resourceName) => goal.Kind switch
        {
            GoalKind.EarnMoney => $"Earn ${goal.Amount}",
            GoalKind.FulfillOrders => $"Fulfill {goal.Amount} {Plural(goal.Amount, "order")}",
            GoalKind.HarvestCrops => $"Harvest {goal.Amount} {resourceName(goal.TargetId)}",
            GoalKind.ReachLevel => $"Reach level {goal.Amount}",
            _ => throw new NotImplementedException(
                $"QuestDescription: goal kind {goal.Kind} has no description (no example quest exercises it)")
        };

        static string Plural(int n, string word) => n == 1 ? word : word + "s";
    }
}
