using System.Text;
using VoidDay.Balance.Schema;

namespace VoidDay.Balance.Agent;

/// Scores a SimResult against a Goal. The output is one number (the loss) that an external agent minimises,
/// ALWAYS with a per-target breakdown — a loss you can't decompose is a loss you can't act on.
///
/// ★ Reads pressure GROSS of gem relief (the M03 invariant). Shares and ranks are computed on LevelReport.Pressure,
/// which never has relief netted out — so raising the gem drip cannot flatter a pressure target.
public static class GoalEvaluator
{
    // Violation normaliser: for an over-shoot (v>max) it is (v-max)/max(|max|,|v|), for an under-shoot
    // (v<min) it is (min-v)/max(|min|,|v|). Both land in [0,1), are monotonic in v, and are scale-free, so
    // weights compare across metrics of wildly different units (minutes vs shares vs money).
    static double Over(double v, double max) => v <= max ? 0 : (v - max) / Math.Max(Math.Abs(max), Math.Max(Math.Abs(v), 1e-9));
    static double Under(double v, double min) => v >= min ? 0 : (min - v) / Math.Max(Math.Abs(min), Math.Max(Math.Abs(v), 1e-9));

    static double Band(double v, double? min, double? max)
    {
        double s = 0;
        if (max.HasValue) s += Over(v, max.Value);
        if (min.HasValue) s += Under(v, min.Value);
        return s;
    }

    public static LossReport Evaluate(SimResult result, Goal goal)
    {
        var byLevel = new Dictionary<int, LevelReport>();
        foreach (var l in result.Levels) byLevel[l.Level] = l;

        var report = new LossReport { Goal = goal.Name };
        foreach (var target in goal.Targets)
            report.Targets.Add(ScoreTarget(target, result, byLevel));
        report.Loss = report.Targets.Sum(t => t.Contribution);
        return report;
    }

    static TargetResult ScoreTarget(GoalTarget t, SimResult result, Dictionary<int, LevelReport> byLevel)
    {
        var res = new TargetResult
        {
            Metric = t.Metric,
            Scope = t.ScopeLabel(),
            Bound = BoundLabel(t),
            Weight = t.Weight
        };

        double violation = 0;
        var notes = new List<string>();

        if (t.Metric == "total.minutesToLevel")
        {
            int lvl = t.Level ?? throw new ArgumentException("total.minutesToLevel needs 'level'.");
            double minutes = (byLevel.TryGetValue(lvl, out var r) ? r.EnteredAt : result.TotalSeconds) / 60.0;
            violation = Band(minutes, t.Min, t.Max);
            res.Measured = minutes;
            notes.Add($"reachL{lvl}={minutes:0.0}m");
        }
        else
        {
            double measuredSum = 0; int measuredCount = 0;
            foreach (int lvl in t.ScopeLevels())
            {
                if (!byLevel.TryGetValue(lvl, out var r))
                {
                    // Level never reached: a full unit of violation if the target has any bound at all.
                    if (t.Min.HasValue || t.Max.HasValue || t.MaxRank.HasValue) { violation += 1.0; notes.Add($"L{lvl}=unreached"); }
                    continue;
                }
                double v = Measure(t, r, out string note);
                violation += (t.Metric == "pressure.rank") ? RankViolation(t, r) : Band(v, t.Min, t.Max);
                measuredSum += v; measuredCount++;
                notes.Add($"L{lvl}={note}");
            }
            res.Measured = measuredCount > 0 ? measuredSum / measuredCount : 0;
        }

        res.Violation = violation;
        res.Contribution = violation * t.Weight;
        res.Detail = string.Join(" ", notes);
        return res;
    }

    static double Measure(GoalTarget t, LevelReport r, out string note)
    {
        switch (t.Metric)
        {
            case "level.durationMinutes": { double v = r.DurationSeconds / 60.0; note = $"{v:0.0}m"; return v; }
            case "level.moneyAtEntry": { note = r.MoneyAtEntry.ToString(); return r.MoneyAtEntry; }
            case "level.moneyAtExit": { note = r.MoneyAtExit.ToString(); return r.MoneyAtExit; }
            case "gems.compressionShare": { double v = r.CompressionShare; note = $"{v:0.###}"; return v; }
            case "gems.heldAtExit": { note = r.GemsAtExit.ToString(); return r.GemsAtExit; }
            case "pressure.share":
            {
                var fam = Families(r.Pressure);
                double total = fam.Values.Sum();
                double share = total > 0 ? fam.GetValueOrDefault(Cat(t)) / total : 0;
                note = $"{Cat(t)}={share:0.###}";
                return share;
            }
            case "pressure.rank": { note = $"{Cat(t)}#{Rank(t, r)}"; return Rank(t, r); }
            case "quest.completions": { note = r.QuestsCompleted.ToString(); return r.QuestsCompleted; }
            case "quest.rewardShare":
            {
                double v = r.MoneyEarned > 0 ? (double)r.QuestRewardMoney / r.MoneyEarned : 0;
                note = $"{v:0.###}";
                return v;
            }
            default: throw new ArgumentException($"unknown metric '{t.Metric}'");
        }
    }

    static double RankViolation(GoalTarget t, LevelReport r)
    {
        int rank = Rank(t, r);
        int maxRank = t.MaxRank ?? 1;
        if (rank <= maxRank) return 0;
        int families = Math.Max(1, Families(r.Pressure).Count);
        return (double)(rank - maxRank) / families;
    }

    static int Rank(GoalTarget t, LevelReport r)
    {
        var fam = Families(r.Pressure);
        string cat = Cat(t);
        // Deterministic sort: pressure desc, then family name asc (never let dict order decide a result).
        var ordered = fam.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        for (int i = 0; i < ordered.Count; i++)
            if (ordered[i].Key == cat) return i + 1;
        return ordered.Count + 1; // absent ⇒ worse than every present family
    }

    static string Cat(GoalTarget t) =>
        t.Category ?? throw new ArgumentException($"metric '{t.Metric}' needs a 'category'.");

    /// Aggregate parametrised pressure keys into families. The rule lives on LevelReport so the M06 heatmap
    /// aggregates identically — a family named in a goal and a family drawn in the heatmap must mean the same set.
    static Dictionary<string, double> Families(Dictionary<string, double> pressure)
    {
        var fam = new Dictionary<string, double>();
        foreach (var kv in pressure)
        {
            string f = kv.Key.Split(':')[0];
            fam[f] = fam.GetValueOrDefault(f) + kv.Value;
        }
        return fam;
    }

    static string BoundLabel(GoalTarget t)
    {
        if (t.Metric == "pressure.rank") return $"rank≤{t.MaxRank ?? 1}";
        var parts = new List<string>();
        if (t.Min.HasValue) parts.Add($"min {t.Min}");
        if (t.Max.HasValue) parts.Add($"max {t.Max}");
        return parts.Count > 0 ? string.Join(", ", parts) : "(none)";
    }

    public static string Render(LossReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"loss: {r.Loss:0.####}   goal '{r.Goal}'");
        sb.AppendLine();
        sb.AppendLine("Metric                  Scope   Bound             Measured   Violation  Weight  Contribution");
        sb.AppendLine("----------------------  ------  ----------------  ---------  ---------  ------  ------------");
        foreach (var t in r.Targets)
            sb.AppendLine(
                $"{t.Metric,-22}  {t.Scope,-6}  {t.Bound,-16}  {t.Measured,9:0.###}  {t.Violation,9:0.####}  {t.Weight,6:0.##}  {t.Contribution,12:0.####}");
        return sb.ToString();
    }
}

public sealed class LossReport
{
    public string Goal = "";
    public double Loss;
    public List<TargetResult> Targets = new();
}

public sealed class TargetResult
{
    public string Metric = "";
    public string Scope = "";
    public string Bound = "";
    public double Measured;     // representative value (mean over scope for ranged metrics)
    public double Violation;    // summed normalised violation over scope
    public double Weight;
    public double Contribution; // Violation × Weight — the target's slice of the loss
    public string Detail = "";  // per-level measurements
}
