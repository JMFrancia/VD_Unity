using VoidDay.Core.Model;

namespace VoidDay.Balance.Sim;

/// The single continuous session clock. Steps at 1s while the player has something to do; when they don't, it
/// jumps exactly to the next timer boundary — min(next job end, next construction end, next order refill).
///
/// The jump is exact, not approximate, only because Core stores absolute timestamps and TryStartHead runs
/// solely on collect/queue/cancel (spec: The clock jump must be exact). ★ Construction end belongs in the
/// jump set: the original three-timer list predates build timers, and omitting it makes the clock skip past a
/// station coming online, under-reporting every config where construction is on the critical path.
public sealed class SimClock
{
    public double Now { get; private set; }

    public void Advance(double seconds) => Now += seconds;
    public void JumpTo(double t) { if (t > Now) Now = t; }

    /// The next timer boundary after Now, or null if no timer of any kind is running (a genuine stall — every
    /// station idle, every order slot filled, no construction in flight).
    public double? NextEvent(CoreHarness h)
    {
        double best = double.PositiveInfinity;

        // Snapshot (id, underConstruction) sorted — never iterate the grid dict in its unordered order.
        var stations = new List<(string id, bool underConstruction)>();
        foreach (var kv in h.Grid.All) stations.Add((kv.Value.Id, kv.Value.UnderConstruction));
        stations.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

        foreach (var (id, underConstruction) in stations)
        {
            if (underConstruction)
            {
                float site = h.Builds.SiteSecondsRemaining(id, Now); // construction IS in the jump set
                if (site > 0f) best = Math.Min(best, Now + site);
            }
            else
            {
                float job = h.Jobs.HeadSecondsRemaining(id, Now); // registered once built
                if (job > 0f) best = Math.Min(best, Now + job);
            }
        }

        int slots = h.Orders.VisibleSlotCount;
        for (int s = 0; s < slots; s++)
        {
            if (h.Orders.OrderAt(s) != null) continue; // a filled slot has no refill running
            float refill = h.Orders.RefillRemaining(s, Now);
            if (refill > 0f) best = Math.Min(best, Now + refill);
        }

        return double.IsPositiveInfinity(best) ? null : best;
    }
}
