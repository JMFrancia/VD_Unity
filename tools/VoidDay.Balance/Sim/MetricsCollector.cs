using VoidDay.Core.Events;

namespace VoidDay.Balance.Sim;

/// Subscribes to the REAL EventBus and accumulates the counters the per-level report reads. It never drives
/// anything — it only listens, exactly as a View would (spec: subscribes to LevelUp, MoneyChanged,
/// OrderFulfilled, JobCollected, UpgradePurchased, StationBuilt, StorageFull, StationBlocked). Reading Core
/// state directly is not a rule, but the report's running totals ride the same events the game emits, so the
/// simulator measures the game rather than a parallel bookkeeping.
public sealed class MetricsCollector
{
    public int Money;            // current balance (from MoneyChanged.Total)
    public int MoneyEarned, MoneySpent;
    public int Gems;             // current balance
    public int GemsEarned, GemsSpent;
    public int OrdersFulfilled, OrdersSkipped, JobsCollected, StationsBuilt;

    /// Levels crossed since the last drain — one entry per LevelUp (a fat XP grant may raise several).
    public readonly Queue<int> LevelUps = new();

    public MetricsCollector(EventBus bus)
    {
        bus.Subscribe<MoneyChanged>(e =>
        {
            Money = e.Total;
            if (e.Delta > 0) MoneyEarned += e.Delta; else MoneySpent += -e.Delta;
        });
        bus.Subscribe<GemsChanged>(e =>
        {
            Gems = e.Total;
            if (e.Delta > 0) GemsEarned += e.Delta; else GemsSpent += -e.Delta;
        });
        bus.Subscribe<OrderFulfilled>(_ => OrdersFulfilled++);
        bus.Subscribe<OrderSkipped>(_ => OrdersSkipped++);
        bus.Subscribe<JobCollected>(_ => JobsCollected++);
        bus.Subscribe<StationBuilt>(_ => StationsBuilt++);
        bus.Subscribe<LevelUp>(e => LevelUps.Enqueue(e.Level));
    }
}
