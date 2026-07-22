namespace VoidDay.Core.Model
{
    /// Which of the three timers a skip is aimed at. The three owners stay entirely separate (each keeps its
    /// own absolute end-timestamp, §13); this is only how a *caller* names one of their timers.
    public enum TimerKind { Job, Construction, OrderRefill }

    /// A pointer to one running timer, anywhere in the game. Plain C# — no Unity types (CLAUDE.md rule 3):
    /// a timer is identified by the station whose queue/site it belongs to, or by an order slot index.
    ///
    /// Deliberately one struct rather than three: the pricing rule and the confirm popup are shared, so they
    /// need one type to talk about "a timer" without knowing which owner is behind it.
    public readonly struct TimerRef
    {
        public readonly TimerKind Kind;
        public readonly string StationId; // Job / Construction; null for OrderRefill
        public readonly int Slot;         // OrderRefill; 0 for the station kinds

        private TimerRef(TimerKind kind, string stationId, int slot)
        {
            Kind = kind;
            StationId = stationId;
            Slot = slot;
        }

        public static TimerRef Job(string stationId) => new(TimerKind.Job, stationId, 0);
        public static TimerRef Construction(string stationId) => new(TimerKind.Construction, stationId, 0);
        public static TimerRef OrderRefill(int slot) => new(TimerKind.OrderRefill, null, slot);

        public override string ToString() =>
            Kind == TimerKind.OrderRefill ? $"{Kind}(slot {Slot})" : $"{Kind}('{StationId}')";
    }
}
