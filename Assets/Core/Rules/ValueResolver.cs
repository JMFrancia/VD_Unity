using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// What a resolved value is *for* — the seam's discriminator. M5 maps these to EffectType and applies
    /// stacked upgrade/trait/event effects; M2 ignores it. Kept local (not §3.1's EffectType) so M2 does
    /// not depend on the effect schema it hasn't built yet.
    public enum ResolveKind
    {
        RecipeDuration,
        OutputQuantity,
        InputCost,
        QueueDepth,

        // M3 sites. M6 gives OrderPayout/OrderSlots teeth (the universal upgrades); M5 gives XpGain teeth.
        OrderPayout,
        OrderSlots,
        XpGain,

        // M4 sites. M6 gives BuildCost teeth (the build.cost effect); M8 gives StationCap teeth (level raises caps).
        BuildCost,
        StationCap
    }

    /// Context a resolver needs to decide whether an effect applies (which station, which resource).
    /// M2 reads none of it; M5's resolver will.
    public readonly struct ResolveContext
    {
        public readonly string StationId;
        public readonly string ResourceId;

        public ResolveContext(string stationId, string resourceId = null)
        {
            StationId = stationId;
            ResourceId = resourceId;
        }
    }

    /// The value seam (00-summary decision #2). EVERY tunable read into a rule — recipe duration, output
    /// quantity, input cost, queue depth — passes through Resolve. In M2 it is a pure passthrough. M5 gives
    /// it teeth by folding in the Effect resolver; call sites never change. This is what makes M5 pure
    /// forward extension instead of surgery on shipped code.
    public sealed class ValueResolver
    {
        public float Resolve(float baseValue, ResolveKind kind, in ResolveContext ctx) => baseValue;
    }
}
