using System.Collections.Generic;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// The §3.5 stacking math — the single, type-agnostic fold that every resolved stat runs through. For a
    /// set of effects (already filtered to one type + scope by the caller):
    ///   1. sum all Flat amounts and add,
    ///   2. sum all Pct amounts and apply once (+25% and +25% = +50%, NOT ×1.5625),
    ///   3. multiply all Mult amounts in sequence.
    /// Effects never suppress each other; all applicable effects apply simultaneously. Pure C#, headless —
    /// this is exactly the economy core CLAUDE.md says to test.
    public static class EffectResolver
    {
        public static float Apply(float baseValue, IReadOnlyList<Effect> effects)
        {
            float flat = 0f, pct = 0f, mult = 1f;
            for (int i = 0; i < effects.Count; i++)
            {
                var v = effects[i].value;
                switch (v.op)
                {
                    case EffectOp.Flat: flat += v.amount; break;
                    case EffectOp.Pct: pct += v.amount; break;
                    case EffectOp.Mult: mult *= v.amount; break;
                }
            }
            return (baseValue + flat) * (1f + pct / 100f) * mult;
        }
    }

    /// A supplier of the currently-active effects of a given type for a given context (which station, which
    /// resource). M5's only source is station upgrades (UpgradeSystem); the interface exists so M6 can add a
    /// global/universal source without the resolver's call sites changing — the spine stays type-agnostic.
    public interface IEffectSource
    {
        /// Append every active effect of `type` that applies to `ctx` into `into`. Passive (TriggerType.None)
        /// effects only — triggered effects fire on events (M9), they are not folded into a passive resolve.
        void Collect(EffectType type, in ResolveContext ctx, List<Effect> into);
    }
}
