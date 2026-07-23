namespace VoidDay.Balance.Agent;

/// A goal file turns "is this balance good?" into a measurement: a set of targets, each a bound on one metric
/// over one scope. `eval` scores a SimResult against it and returns a single loss plus a per-target breakdown.
public sealed class Goal
{
    public string Name = "";
    public List<GoalTarget> Targets = new();
}

/// One bound on one metric family over one scope. `min`/`max` are two-sided where the metric warrants it
/// (pressure.share, gems.compressionShare) — a metric can be too LOW as well as too high.
public sealed class GoalTarget
{
    /// One of: level.durationMinutes, total.minutesToLevel, pressure.share, pressure.rank,
    /// level.moneyAtEntry, level.moneyAtExit, gems.compressionShare, gems.heldAtExit.
    public string Metric = "";

    // Scope — a single level, or an inclusive "a-b" range. total.minutesToLevel uses Level as its target level.
    public int? Level;
    public string? Levels;

    public string? Category;   // pressure.share / pressure.rank: the family (Storage, Capacity, Yield, ...)

    public double? Min;
    public double? Max;
    public int? MaxRank;       // pressure.rank: the family must rank at least this high (1 = must lead)

    public double Weight = 1.0;

    public IEnumerable<int> ScopeLevels()
    {
        if (Level.HasValue) { yield return Level.Value; yield break; }
        if (string.IsNullOrEmpty(Levels))
            throw new ArgumentException($"target '{Metric}' needs a 'level' or a 'levels' range.");
        var dash = Levels.Split('-');
        if (dash.Length == 1) { yield return int.Parse(dash[0].Trim()); yield break; }
        int lo = int.Parse(dash[0].Trim()), hi = int.Parse(dash[1].Trim());
        for (int l = lo; l <= hi; l++) yield return l;
    }

    public string ScopeLabel() =>
        Level.HasValue ? $"L{Level}" : string.IsNullOrEmpty(Levels) ? "total" : $"L{Levels}";
}
