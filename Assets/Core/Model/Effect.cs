namespace VoidDay.Core.Model
{
    // §3.1 — the Effect schema, the spine of the game (§3). Plain [System.Serializable] C# in Core: no
    // UnityEngine dependency, so these types resolve headless in Core *and* serialize into the inspector when
    // a Data-layer SO exposes an Effect[] field. This is the one model that crosses the boundary unprojected
    // (§14): a Core rule reads an Effect directly, never a *SO.
    //
    // The FULL enum vocabularies are defined here even though M5 only *resolves* the passive own-station
    // subset (station.speed/cost/yield/queueDepth, xp.gain). Later milestones give the rest teeth without
    // re-authoring the schema: global/order/build (M6), storage.cap (M7), triggers + egg/pet types (M9),
    // local.* / within-range (M10). Defining the whole vocabulary once is the point — adding an EffectType
    // never needs a new resolver (§3, §3.5).

    public enum EffectOp { Flat, Pct, Mult }

    /// PascalCase of the §3.2 dotted design names (`station.speed` → StationSpeed). Dots aren't identifiers.
    public enum EffectType
    {
        StationSpeed, StationCost, StationYield, StationQueueDepth,
        LocalSpeed, LocalCost, LocalYield,
        GlobalSpeed, GlobalCost, GlobalYield,
        BuildCost, OrderPayout, OrderSlots, XpGain, StorageCap,
        EggChance, PetEffectStrength, PetAutoCollectSpeed
    }

    /// How far an EffectType reaches — the §3.2 "Touches" column, expressed once. The effect source asks a
    /// type for its SCOPE rather than switching on the type itself, which is what keeps "add a new effect
    /// type" from meaning "add a new code path". OwnStation resolves from M5, Global from M7; the two range
    /// scopes are declared vocabulary until M10 gives them grid/pet positions to measure against.
    public enum EffectScope { OwnStation, Global, LocalRange, PetRange }

    public static class EffectScopes
    {
        /// Exhaustive by design: a newly-added EffectType throws here rather than silently defaulting to a
        /// scope nobody chose for it.
        public static EffectScope Of(EffectType type)
        {
            switch (type)
            {
                case EffectType.StationSpeed:
                case EffectType.StationCost:
                case EffectType.StationYield:
                case EffectType.StationQueueDepth:
                    return EffectScope.OwnStation;

                case EffectType.LocalSpeed:
                case EffectType.LocalCost:
                case EffectType.LocalYield:
                    return EffectScope.LocalRange;

                case EffectType.PetEffectStrength:
                case EffectType.PetAutoCollectSpeed:
                    return EffectScope.PetRange;

                // Reach the whole map or the economy at large, so the station being resolved is irrelevant:
                // an emitter anywhere applies everywhere (§3.2).
                case EffectType.GlobalSpeed:
                case EffectType.GlobalCost:
                case EffectType.GlobalYield:
                case EffectType.BuildCost:
                case EffectType.OrderPayout:
                case EffectType.OrderSlots:
                case EffectType.XpGain:
                case EffectType.StorageCap:
                case EffectType.EggChance:
                    return EffectScope.Global;

                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(type), type, "EffectType has no declared scope — add it to EffectScopes.Of");
            }
        }
    }

    /// None = passive modifier, always applied (all M5 upgrades). A real trigger fires once on that event,
    /// subject to triggerChance — resolved starting M9.
    public enum TriggerType
    {
        None, JobQueued, JobCompleted, JobCollected,
        OrderFulfilled, StationBuilt, PetHatched, LevelUp
    }

    /// None = always true. The non-None conditions are evaluated incrementally (M9/M10); M5 upgrades use None.
    public enum ConditionType
    {
        None, AssignedTo, WithinRangePet, WithinRangeStation,
        ResourceAbove, PlayerLevelAbove
    }

    [System.Serializable]
    public struct EffectValue
    {
        public EffectOp op;
        public float amount; // +25% => {Pct, 25}; ×3 => {Mult, 3}; +2 => {Flat, 2}
    }

    /// A flat struct with an enum discriminator + generic args (rather than a polymorphic hierarchy) —
    /// inspector-friendly and KISS (§3.1). arg/amount carry per-type data: stationType / petId / resource, n.
    [System.Serializable]
    public struct Condition
    {
        public ConditionType type; // None = always true
        public string arg;         // stationType / petId / resource, per type
        public int amount;         // n, per type
    }

    [System.Serializable]
    public sealed class Effect
    {
        public string id;             // internal, for debugging — never player-facing
        public EffectType type;       // what it touches
        public EffectValue value;
        public string resource;       // "" = all resources; else scopes to one (§3.2)
        public int range;             // grid cells; required by local.* / pet.* types (validated at boot)
        public TriggerType trigger;   // None = passive, always active
        public int triggerChance;     // 0–100; 0 is normalised to 100 ("unset") at boot
        public Condition condition;   // ConditionType.None = always true
    }

    [System.Serializable]
    public sealed class Trait
    {
        public string id;
        public string name;           // player-facing ("Cow Lover")
        public Effect[] effects;
    }
}
