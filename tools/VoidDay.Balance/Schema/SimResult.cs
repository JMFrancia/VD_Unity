namespace VoidDay.Balance.Schema;

/// The whole output of one seed run. The per-level table is the M03 deliverable; later milestones (eval,
/// suggest, sweep) read the same structure.
public sealed class SimResult
{
    public string ConfigName = "";
    public int Seed;
    public float Optimality;
    public StopReason Stop = StopReason.MaxLevel;
    public string StopDetail = "";
    public double TotalSeconds;
    public int LevelReached;
    public List<LevelReport> Levels = new();
}

/// Why the run ended. A stalled config is a finding, not an error (spec: Termination).
public enum StopReason { MaxLevel, MaxHours, Stalled }

/// One row of the per-level report. Pressure is GROSS of gem relief; net is derived (Pressure − GemRelief),
/// never stored (spec: the single most important structural decision gems force on the tool).
public sealed class LevelReport
{
    public int Level;
    public double EnteredAt, ExitedAt, DurationSeconds;
    public double ActingSeconds;   // consumed by taps / queues / fulfills / purchases (+ reaction lag)
    public double WaitingSeconds;  // the remainder — watching timers

    public int MoneyAtEntry, MoneyAtExit, MoneyEarned, MoneySpent;
    public int OrdersFulfilled, OrdersSkipped, JobsCollected;

    public int GemsAtEntry, GemsAtExit, GemsEarned, GemsSpent;
    public double SecondsPurchased;        // wall-clock skipped by gems this level
    public double CompressionShare;        // SecondsPurchased / DurationSeconds
    public double SecondsPerGemRealised;   // SecondsPurchased / GemsSpent — waste detector
    public Dictionary<string, double> GemRelief = new();  // category ⇒ seconds a skip removed

    public Dictionary<string, double> Pressure = new();   // category ⇒ seconds lost, GROSS
    public List<PurchaseRecord> Purchases = new();

    /// Aggregate parametrised pressure keys (Capacity:field, Yield:silo, Supply:corn) into families
    /// (Capacity, Yield, Supply) so a consumer names a family without knowing every station/good suffix.
    /// GROSS of gem relief — this reads Pressure, which is never netted. The single family rule the loss
    /// evaluator and the M06 heatmap must share, so it lives on the data object both read.
    public Dictionary<string, double> PressureFamilies()
    {
        var fam = new Dictionary<string, double>();
        foreach (var kv in Pressure)
        {
            string f = kv.Key.Split(':')[0];
            fam[f] = fam.GetValueOrDefault(f) + kv.Value;
        }
        return fam;
    }

    /// The worst pressure category by gross seconds — the reported bottleneck (deterministic tie-break by name).
    public string TopPressure()
    {
        string top = "";
        double best = -1;
        foreach (var kv in Pressure)
        {
            if (kv.Value > best || (kv.Value == best && string.CompareOrdinal(kv.Key, top) < 0))
            {
                best = kv.Value;
                top = kv.Key;
            }
        }
        return top == "" ? "none" : top;
    }
}

/// One remedy purchase (structural or a gem skip) on the level timeline.
public sealed class PurchaseRecord
{
    public double At;
    public string Kind = "";      // "build" | "upgrade" | "skip"
    public string Target = "";    // station type / track id / timer description
    public int Cost;              // money (build/upgrade) or gems (skip)
    public string ForPressure = ""; // the category it addressed
}
