using System.Text;
using Newtonsoft.Json;
using VoidDay.Balance.Schema;
using VoidDay.Balance.Sim;

namespace VoidDay.Balance.Agent;

/// 1-D sensitivity: the loss across a parameter range in N steps. The coordinate-descent primitive an external
/// agent composes — this milestone builds no search loop of its own (spec: Do NOT Build automated search).
///
/// Sweep is exploratory measurement, not a persisted edit: it clones the config per step, sets one knob, sims,
/// and scores. It rejects profile/* (never measure "a smarter player") but does NOT enforce bounds.json — the
/// point of a sweep is to see the curve, including past where a patch would be allowed to commit.
public static class Sweep
{
    public sealed class SweepPoint
    {
        public double Value;
        public double Loss;
        public int LevelReached;
        public double TotalMinutes;
    }

    public sealed class SweepReport
    {
        public string Path = "";
        public double From, To;
        public int Steps;
        public List<SweepPoint> Points = new();
    }

    public static SweepReport Run(
        BalanceConfig config, Goal goal, string path, double from, double to, int steps,
        SimProfile profile, int seed, bool gemsEnabled)
    {
        var norm = path.Replace('\\', '/');
        if (norm == "profile" || norm.StartsWith("profile/") || norm.StartsWith("profile."))
            throw new ArgumentException($"sweep will not vary '{path}': profile/* is the simulated player, not the game.");
        if (steps < 2) throw new ArgumentException("sweep needs at least 2 steps.");

        var report = new SweepReport { Path = path, From = from, To = to, Steps = steps };
        string baseJson = JsonConvert.SerializeObject(config);
        for (int i = 0; i < steps; i++)
        {
            double value = from + (to - from) * i / (steps - 1);
            var clone = JsonConvert.DeserializeObject<BalanceConfig>(baseJson)!;
            ConfigPath.Resolve(clone, path).Set(value);

            var result = new SimRunner(clone, profile, seed, gemsEnabled).Run();
            var loss = GoalEvaluator.Evaluate(result, goal);
            report.Points.Add(new SweepPoint
            {
                Value = value,
                Loss = loss.Loss,
                LevelReached = result.LevelReached,
                TotalMinutes = result.TotalSeconds / 60.0
            });
        }
        return report;
    }

    public static string Render(SweepReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"sweep: {r.Path}  [{r.From} → {r.To}]  {r.Steps} steps");
        sb.AppendLine();
        sb.AppendLine("Value         Loss      Level  Total");
        sb.AppendLine("-----------   -------   -----  ------");
        foreach (var p in r.Points)
            sb.AppendLine($"{p.Value,11:0.###}   {p.Loss,7:0.####}   {p.LevelReached,5}  {p.TotalMinutes,5:0.0}m");
        return sb.ToString();
    }
}
