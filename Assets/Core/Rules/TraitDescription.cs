using System.Collections.Generic;
using System.Globalization;
using System.Text;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// §3.6 — one function turns any Trait's (or upgrade tier's) effects into a player-facing sentence. Serves
    /// upgrade rows now; VoidPet details, relationship popups and world-event popups reuse it. The three §3.6
    /// reference cases are the acceptance bar (covered by EditMode tests):
    ///   Cow Lover:   "When assigned to a pasture, 20% chance of ×3 yield on job completion."
    ///   Hard Worker: "+25% speed at its station."
    ///   Thrifty:     "-15% recipe cost at its station."
    ///
    /// Anatomy of a clause: [condition prefix][chance] magnitude stat [reach | trigger]. A passive effect ends
    /// in its reach ("at its station"); a triggered effect ends in the trigger clause ("on job completion").
    public static class TraitDescription
    {
        public static string Describe(Trait trait) => Describe(trait.effects);

        public static string Describe(IReadOnlyList<Effect> effects)
        {
            if (effects == null || effects.Count == 0) return "";
            var parts = new List<string>(effects.Count);
            for (int i = 0; i < effects.Count; i++)
                parts.Add(DescribeOne(effects[i]));
            return string.Join("; ", parts) + ".";
        }

        static string DescribeOne(Effect e)
        {
            var sb = new StringBuilder();

            string condition = ConditionClause(e.condition);
            if (condition.Length > 0) sb.Append(condition).Append(", ");

            bool triggered = e.trigger != TriggerType.None;
            int chance = e.triggerChance == 0 ? 100 : e.triggerChance;
            if (triggered && chance < 100)
                sb.Append(chance).Append("% chance of ");

            sb.Append(Magnitude(e.value)).Append(' ').Append(Stat(e.type, e.resource));

            string tail = triggered ? TriggerClause(e.trigger) : ReachClause(e.type);
            if (tail.Length > 0) sb.Append(' ').Append(tail);

            return Capitalize(sb.ToString());
        }

        static string Magnitude(EffectValue v)
        {
            string n = v.amount.ToString("0.###", CultureInfo.InvariantCulture);
            return v.op switch
            {
                EffectOp.Pct => (v.amount >= 0 ? "+" : "") + n + "%",
                EffectOp.Flat => (v.amount >= 0 ? "+" : "") + n,
                EffectOp.Mult => "×" + n,
                _ => n
            };
        }

        static string Stat(EffectType type, string resource)
        {
            string word = type switch
            {
                EffectType.StationSpeed or EffectType.LocalSpeed or EffectType.GlobalSpeed => "speed",
                EffectType.StationCost or EffectType.LocalCost or EffectType.GlobalCost => "recipe cost",
                EffectType.StationYield or EffectType.LocalYield or EffectType.GlobalYield => "yield",
                EffectType.StationQueueDepth => "queue depth",
                EffectType.BuildCost => "build cost",
                EffectType.OrderPayout => "order payout",
                EffectType.OrderSlots => "order slots",
                EffectType.XpGain => "XP",
                EffectType.StorageCap => "storage cap",
                EffectType.EggChance => "egg drop chance",
                EffectType.PetEffectStrength => "pet effect strength",
                EffectType.PetAutoCollectSpeed => "auto-collect speed",
                _ => type.ToString()
            };
            // The `resource` scope narrows a *.cost / *.yield to one good (§3.2): "wheat recipe cost".
            return string.IsNullOrEmpty(resource) ? word : resource + " " + word;
        }

        /// Passive reach: where a None-trigger modifier applies. station.* is own-station; local.* is nearby;
        /// the map-wide and non-spatial types carry no locative tail.
        static string ReachClause(EffectType type) => type switch
        {
            EffectType.StationSpeed or EffectType.StationCost or EffectType.StationYield
                or EffectType.StationQueueDepth => "at its station",
            EffectType.LocalSpeed or EffectType.LocalCost or EffectType.LocalYield => "at nearby stations",
            EffectType.GlobalSpeed or EffectType.GlobalCost or EffectType.GlobalYield => "on all stations",
            _ => ""
        };

        static string TriggerClause(TriggerType t) => t switch
        {
            TriggerType.JobQueued => "when a job is queued",
            TriggerType.JobCompleted => "on job completion",
            TriggerType.JobCollected => "on collecting a job",
            TriggerType.OrderFulfilled => "on fulfilling an order",
            TriggerType.StationBuilt => "when a station is built",
            TriggerType.PetHatched => "when an egg hatches",
            TriggerType.LevelUp => "on level up",
            _ => ""
        };

        static string ConditionClause(Condition c) => c.type switch
        {
            ConditionType.AssignedTo => $"When assigned to a {c.arg}",
            ConditionType.WithinRangePet => "When near an assigned pet",
            ConditionType.WithinRangeStation => $"When near a {c.arg}",
            ConditionType.ResourceAbove => $"While {c.arg} is above {c.amount}",
            ConditionType.PlayerLevelAbove => $"At level {c.amount}+",
            _ => ""
        };

        static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
    }
}
