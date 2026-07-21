using System.Collections.Generic;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// What a resolved value is *for* — the seam's discriminator. M5 maps these to EffectType and applies the
    /// stacked upgrade/trait/event effects; before M5 every kind was a passthrough. Kept local (not §3.1's
    /// EffectType) so the M2 call sites do not depend on the effect schema directly — this seam is the one
    /// translation point from "a rule reading a number" to "the effect system".
    public enum ResolveKind
    {
        RecipeDuration,
        OutputQuantity,
        InputCost,
        QueueDepth,

        // M3 sites. M6 gives OrderPayout/OrderSlots teeth (the universal upgrades). XpGain is wired here.
        OrderPayout,
        OrderSlots,
        XpGain,

        // M4 sites. M6 gives BuildCost teeth (the build.cost effect); StationCap is level-granted (M8).
        BuildCost,
        StationCap,

        // M7 site: the per-resource storage cap, raised by the Silo's storage.cap track.
        StorageCap
    }

    /// Context a resolver needs to decide whether a contribution applies (which station instance, which
    /// resource, which station TYPE). StationType is the discriminator level grants are keyed by — a raised
    /// cap belongs to a *type*, not to an instance, and the cap read has no instance at all.
    public readonly struct ResolveContext
    {
        public readonly string StationId;
        public readonly string ResourceId;
        public readonly string StationType;

        public ResolveContext(string stationId, string resourceId = null, string stationType = null)
        {
            StationId = stationId;
            ResourceId = resourceId;
            StationType = stationType;
        }
    }

    /// The value seam (00-summary decision #2). EVERY tunable read into a rule passes through Resolve. M5 gives
    /// it teeth by folding in the Effect resolver; the call sites (JobSystem, Progression, BuildSystem,
    /// OrderBoard) never changed — that is what made M5 pure forward extension instead of surgery on shipped
    /// code. Kinds not yet backed by an effect emitter (order/build/cap) stay passthrough until their milestone.
    public sealed class ValueResolver
    {
        private IEffectSource _effects;
        private LevelGrants _grants;
        private readonly List<Effect> _scratch = new();

        /// Wired at boot after the effect source (UpgradeSystem) is constructed. Two-phase because the resolver
        /// is created first (JobSystem/Progression/BuildSystem need it) and the source needs the wallet/bus.
        /// Null source = passthrough, which is also what the headless economy tests use.
        public void SetEffectSource(IEffectSource effects) => _effects = effects;

        /// The second contributor (M8): what levelling has granted. Kept separate from the effect source
        /// because a level grant is a **flat move of the base**, not an Effect — it lands before the effect
        /// stack so a percentage upgrade still applies on top of it. Null = passthrough (headless tests).
        public void SetGrantSource(LevelGrants grants) => _grants = grants;

        public float Resolve(float baseValue, ResolveKind kind, in ResolveContext ctx)
        {
            switch (kind)
            {
                // Speed is the one inversion: an effect resolver is additive on the *rate*, and a faster rate
                // means a SHORTER timer. Resolve the speed factor against 1.0, then divide. +25%+25% => ×1.5
                // speed => duration/1.5 (not duration×1.5), and it stacks additively per §3.5.
                case ResolveKind.RecipeDuration:
                {
                    float speed = Applied(1f, EffectType.StationSpeed, EffectType.GlobalSpeed, ctx);
                    return speed <= 0f ? baseValue : baseValue / speed;
                }
                case ResolveKind.OutputQuantity: return Applied(baseValue, EffectType.StationYield, EffectType.GlobalYield, ctx);
                case ResolveKind.InputCost: return Applied(baseValue, EffectType.StationCost, EffectType.GlobalCost, ctx);
                case ResolveKind.XpGain: return Applied(baseValue, EffectType.XpGain, ctx);
                case ResolveKind.StorageCap: return Applied(baseValue, EffectType.StorageCap, ctx);

                // Level-granted (§9) stats. The grant moves the base, then the effect stack applies to the
                // moved base — a level-deepened queue and a queue-depth upgrade add up rather than compete.
                case ResolveKind.QueueDepth:
                    return Applied(baseValue + Granted(LevelEntryKind.QueueDepth, ctx.StationType),
                        EffectType.StationQueueDepth, ctx);

                // Cap and slot count take level grants only — their effect emitters are M6's universal
                // upgrades, so the effect half of these two sites stays passthrough until then.
                case ResolveKind.StationCap: return baseValue + Granted(LevelEntryKind.StationCap, ctx.StationType);
                case ResolveKind.OrderSlots: return baseValue + Granted(LevelEntryKind.OrderSlots, null);

                // No emitter for these until M6 — passthrough keeps the read site honest meanwhile.
                default: return baseValue;
            }
        }

        private int Granted(LevelEntryKind kind, string targetId) =>
            _grants == null ? 0 : _grants.Bonus(kind, targetId);

        private float Applied(float baseValue, EffectType type, in ResolveContext ctx)
        {
            if (_effects == null) return baseValue;
            _scratch.Clear();
            _effects.Collect(type, ctx, _scratch);
            return _scratch.Count == 0 ? baseValue : EffectResolver.Apply(baseValue, _scratch);
        }

        /// Two reaches of the SAME stat (station.speed + global.speed) fold into ONE pool, so +25% from an
        /// upgrade and +25% from a world event read as +50%, not ×1.5625 (§3.5). Reach decides which effects
        /// are collected; it never earns a modifier its own separate multiplication. local.* joins these pools
        /// at M10 by the same route.
        private float Applied(float baseValue, EffectType own, EffectType global, in ResolveContext ctx)
        {
            if (_effects == null) return baseValue;
            _scratch.Clear();
            _effects.Collect(own, ctx, _scratch);
            _effects.Collect(global, ctx, _scratch);
            return _scratch.Count == 0 ? baseValue : EffectResolver.Apply(baseValue, _scratch);
        }
    }
}
