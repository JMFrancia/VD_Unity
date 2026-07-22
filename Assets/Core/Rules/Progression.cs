using System.Collections.Generic;
using VoidDay.Core.Events;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// Player level and lifetime XP (§9). M3 built the *value*; this is the **increment** — crossing a
    /// threshold raises the level and applies whatever that level hands out.
    ///
    /// The applier is one loop over the level's grant list (§9), not a case per grantable: a new kind of
    /// level reward is data on the level asset, not code here. What a level opens up by way of a *gate* —
    /// a station type, an upgrade track — is derived from the gated asset's own unlockLevel rather than
    /// restated on the level, so an unlock has exactly one home.
    public sealed class Progression
    {
        private readonly EventBus _bus;
        private readonly ValueResolver _resolver;
        private readonly LevelCurve _curve;
        private readonly LevelGrants _grants;
        private readonly Wallet _wallet;
        private readonly GemPurse _gems;
        private readonly IReadOnlyList<LevelUnlockModel> _gated;

        /// The level every run starts at — the structural floor, not a tunable. A station type with
        /// unlockLevel == StartingLevel is buildable from the first frame (§9).
        public const int StartingLevel = 1;

        public int PlayerLevel { get; private set; } = StartingLevel;
        public int XpTotal { get; private set; }

        public Progression(EventBus bus, ValueResolver resolver, LevelCurve curve, LevelGrants grants,
            Wallet wallet, GemPurse gems, IReadOnlyList<LevelUnlockModel> gated)
        {
            _bus = bus;
            _resolver = resolver;
            _curve = curve;
            _grants = grants;
            _wallet = wallet;
            _gems = gems;
            _gated = gated;
        }

        // ---- Queries (hud.levelXp renders from these; the threshold rule stays in Core, §12.1) ----

        public bool IsMaxLevel => _curve.IsMaxLevel(PlayerLevel);

        /// XP banked into the current level and the span of that level — the bar's numerator and denominator.
        public int XpIntoLevel => _curve.XpIntoLevel(PlayerLevel, XpTotal);
        public int XpSpanOfLevel => _curve.XpSpanOfLevel(PlayerLevel);

        /// XP still owed to the next threshold. 0 at the cap — which is also what makes the debug cheat a
        /// no-op there instead of a special case.
        public int XpToNextLevel => IsMaxLevel ? 0 : _curve.XpForLevel(PlayerLevel + 1) - XpTotal;

        // ---- Commands ----

        public void AwardXp(int amount, string source)
        {
            int resolved = (int)_resolver.Resolve(amount, ResolveKind.XpGain, new ResolveContext(null));
            if (resolved == 0) return;
            XpTotal += resolved;
            _bus.Publish(new XpGained(resolved, source));
            AdvanceLevels();
        }

        public void Reset()
        {
            PlayerLevel = StartingLevel;
            XpTotal = 0;
            _grants.Clear();
            _bus.Publish(new EffectsRecalculated()); // every resolved cap / depth / slot count drops back
        }

        /// One grant can span several thresholds (§9) — each crossing is applied and announced in turn, so a
        /// jump from 1 to 4 pays out levels 2, 3 and 4 rather than only the one it landed on.
        void AdvanceLevels()
        {
            while (XpTotal >= _curve.XpForLevel(PlayerLevel + 1))
            {
                PlayerLevel++;
                ApplyLevel(PlayerLevel);
            }
        }

        void ApplyLevel(int level)
        {
            var unlocks = new List<LevelEntry>();
            var rewards = new List<LevelEntry>();

            // Gates that open at this level, read off the gated assets themselves.
            foreach (var gate in _gated)
            {
                if (gate.UnlockLevel != level) continue;
                unlocks.Add(new LevelEntry(gate.Kind, gate.Id, gate.Label, 0));
                _bus.Publish(new UnlockGranted(gate.Kind.ToString(), gate.Id));
            }

            foreach (var grant in _curve.At(level).Grants)
            {
                var entry = new LevelEntry(grant.Kind, grant.TargetId, grant.TargetLabel, grant.Amount);
                if (grant.Kind == LevelEntryKind.Money)
                {
                    _wallet.Add(grant.Amount); // a one-shot payout, not a standing bonus
                    rewards.Add(entry);
                    continue;
                }
                if (grant.Kind == LevelEntryKind.Gems)
                {
                    _gems.Add(grant.Amount); // one-shot too — gems are never a standing bonus
                    rewards.Add(entry);
                    continue;
                }
                _grants.Add(grant.Kind, grant.TargetId, grant.Amount);
                unlocks.Add(entry);
                _bus.Publish(new UnlockGranted(grant.Kind.ToString(), grant.TargetId));
            }

            // Caps, queue depth and slot counts are resolved values, so the listeners that re-read on an
            // upgrade purchase are exactly the ones that must re-read now.
            _bus.Publish(new EffectsRecalculated());
            _bus.Publish(new LevelUp(level, unlocks, rewards));
        }
    }
}
