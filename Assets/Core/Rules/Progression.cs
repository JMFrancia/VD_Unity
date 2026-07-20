using VoidDay.Core.Events;

namespace VoidDay.Core.Rules
{
    /// Player level and lifetime XP (§9). **The level never increments here** — M8 owns the curve, the
    /// thresholds, and the level-up grant. M3 builds the value that M4–M7 read and M8 will finally move, so
    /// those milestones can gate on a level without the leveling machinery existing yet.
    ///
    /// Deliberately invisible infrastructure: XP accrues with no HUD (M8 builds hud.levelXp, when a level
    /// badge would reflect something that actually moves).
    public sealed class Progression
    {
        private readonly EventBus _bus;
        private readonly ValueResolver _resolver;

        public int PlayerLevel { get; private set; } = 1;
        public int XpTotal { get; private set; }

        public Progression(EventBus bus, ValueResolver resolver)
        {
            _bus = bus;
            _resolver = resolver;
        }

        public void AwardXp(int amount, string source)
        {
            int resolved = (int)_resolver.Resolve(amount, ResolveKind.XpGain, new ResolveContext(null));
            if (resolved == 0) return;
            XpTotal += resolved;
            _bus.Publish(new XpGained(resolved, source));
        }

        public void Reset()
        {
            PlayerLevel = 1;
            XpTotal = 0;
        }
    }
}
