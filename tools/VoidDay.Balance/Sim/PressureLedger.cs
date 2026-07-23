namespace VoidDay.Balance.Sim;

/// The eight pressure categories (spec: The pressure ledger). Seconds-lost is the common unit, so causes are
/// directly comparable and the player buys the affordable remedy for whichever is worst.
///
/// ★ Pressure is recorded GROSS of gem relief. A gem skip does NOT reduce a pressure number; the seconds it
/// removed are recorded separately in GemRelief, and net is derived (Pressure − GemRelief), never stored.
/// Netting at accrual time is the obvious implementation and it silently corrupts the tool: a config with
/// absurd timers would report healthy pressure because the simulated player bought their way out.
public sealed class PressureLedger
{
    // Actionable (a remedy can be bought).
    public const string Storage = "Storage";
    public const string Throughput = "Throughput";
    public static string Capacity(string type) => "Capacity:" + type;
    public static string Supply(string good) => "Supply:" + good;
    public static string Yield(string type) => "Yield:" + type;

    // Diagnostic (reported only, no purchasable remedy).
    public const string Income = "Income";
    public const string OrderRefill = "OrderRefill";
    public const string Unlock = "Unlock";

    readonly Dictionary<string, double> _pressure = new();
    readonly Dictionary<string, double> _relief = new();

    /// The player was stalled by `category` for `seconds`. Gross accrual — this is called whether or not gems
    /// later relieved the same seconds.
    public void Accrue(string category, double seconds)
    {
        if (seconds <= 0) return;
        _pressure.TryGetValue(category, out var p);
        _pressure[category] = p + seconds;
    }

    /// A gem skip removed `seconds` of wait that WOULD have accrued to `category`. Records the relief AND the
    /// gross pressure (the seconds still count as though gems did not exist). Gross = actual-idle + relief;
    /// net = actual-idle is what the caller gets from Pressure − GemRelief.
    public void AccrueGemRelief(string category, double seconds)
    {
        if (seconds <= 0) return;
        Accrue(category, seconds);
        _relief.TryGetValue(category, out var r);
        _relief[category] = r + seconds;
    }

    /// Gross seconds accrued to a category so far this level — the agent's decision input (threshold + rank).
    public double Get(string category) => _pressure.TryGetValue(category, out var p) ? p : 0;

    /// Snapshot of gross pressure this level, keyed by category. Ordered for deterministic report output.
    public Dictionary<string, double> SnapshotPressure() => Ordered(_pressure);
    public Dictionary<string, double> SnapshotRelief() => Ordered(_relief);

    /// Reset at a level boundary — the report is per level.
    public void Clear()
    {
        _pressure.Clear();
        _relief.Clear();
    }

    static Dictionary<string, double> Ordered(Dictionary<string, double> src)
    {
        var keys = new List<string>(src.Keys);
        keys.Sort(StringComparer.Ordinal);
        var outp = new Dictionary<string, double>();
        foreach (var k in keys) outp[k] = src[k];
        return outp;
    }
}
