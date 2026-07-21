using System.Collections.Generic;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// The standing bonuses levelling has handed out so far (§9): raised station caps, deeper queues, more
    /// order slots. Purely an accumulator — it decides nothing, it only remembers what was granted, so the
    /// applier in Progression stays one loop over a level's grant list rather than a case per grantable.
    ///
    /// These are **flat additions to a base**, not Effects (§3): a level does not modify the economy, it moves
    /// the number the economy starts from. ValueResolver folds them in before the effect stack, so a Silo
    /// upgrade's +25% still applies on top of a level-raised base.
    public sealed class LevelGrants
    {
        /// "every station type" — a grant with no target raises the stat for all of them at once.
        public const string AllTargets = "";

        private readonly Dictionary<(LevelEntryKind, string), int> _bonus = new();

        public void Add(LevelEntryKind kind, string targetId, int amount)
        {
            var key = (kind, targetId ?? AllTargets);
            _bonus.TryGetValue(key, out int current);
            _bonus[key] = current + amount;
        }

        /// The bonus applying to one target: the untargeted (all-types) pool plus the target's own pool.
        /// A null target asks only for the untargeted pool — which is what the station-less reads (order
        /// slots) want.
        public int Bonus(LevelEntryKind kind, string targetId)
        {
            _bonus.TryGetValue((kind, AllTargets), out int total);
            if (!string.IsNullOrEmpty(targetId) && _bonus.TryGetValue((kind, targetId), out int own))
                total += own;
            return total;
        }

        /// Debug reset (§13): the run starts over at level 1, so every granted bonus goes with it.
        public void Clear() => _bonus.Clear();
    }
}
