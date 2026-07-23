using System.Collections.Concurrent;
using VoidDay.Balance.Schema;

namespace VoidDay.Balance.Sim;

/// Multi-seed aggregation (M06). Runs the SAME single-seed M03 runner across a fixed seed set in parallel,
/// then reduces per-level metrics to median / p10 / p90. This adds NO simulation behaviour — it only
/// aggregates what SimRunner already produces (spec M06: "visualises M3's output").
///
/// ★ Determinism: the seed set is a fixed 1..N, each seed's run is independent and self-deterministic, and
/// every reduction sorts before it reads. Parallel.For only changes *when* a seed runs, never its result,
/// so two sweeps of the same config produce byte-identical aggregates. Individual seed results are retained
/// (spec: "an aggregate you can't drill into is an aggregate you can't debug").
public static class SimSweep
{
    public const int DefaultSeedCount = 30;

    public sealed class Stat
    {
        public double Median, P10, P90;
        public static Stat From(List<double> sorted) => new()
        {
            Median = Percentile(sorted, 0.5),
            P10 = Percentile(sorted, 0.10),
            P90 = Percentile(sorted, 0.90),
        };
    }

    public sealed class LevelAgg
    {
        public int Level;
        public int SeedsReached;
        public Stat Duration = new(), Acting = new(), Waiting = new();
        public Stat MoneyEntry = new(), MoneyExit = new();
        public Dictionary<string, Stat> Pressure = new(); // family ⇒ gross seconds lost, aggregated across seeds
    }

    public sealed class PurchaseAgg
    {
        public string Kind = "", Target = "", ForPressure = "";
        public int SeedsBought;
        public Stat FirstLevel = new(); // level at which this remedy is first bought, across seeds that bought it
    }

    public sealed class Aggregate
    {
        public string ConfigName = "";
        public int SeedCount;
        public List<int> Seeds = new();
        public Stat TotalMinutes = new(), LevelReached = new();
        public List<LevelAgg> Levels = new();
        public List<PurchaseAgg> Purchases = new();
    }

    public sealed class Result
    {
        public Aggregate Aggregate = new();
        public List<SimResult> SeedResults = new(); // ordered by seed, retained for drill-in
    }

    public static Result Run(BalanceConfig config, SimProfile profile, int seedCount, bool gemsEnabled)
    {
        if (seedCount < 1) throw new ArgumentException("sweep needs at least 1 seed.");

        // Fixed seed set 1..N so an A/B pair (or a re-run) sees the exact same streams — a delta is then a
        // real config effect, not seed noise (spec M06: "Both sides run the same seed set").
        var results = new SimResult[seedCount];
        Parallel.For(0, seedCount, i =>
        {
            int seed = i + 1;
            results[i] = new SimRunner(config, profile, seed, gemsEnabled).Run();
        });

        var ordered = results.OrderBy(r => r.Seed).ToList();
        return new Result { Aggregate = Reduce(config.Name, seedCount, ordered), SeedResults = ordered };
    }

    static Aggregate Reduce(string configName, int seedCount, List<SimResult> runs)
    {
        var agg = new Aggregate
        {
            ConfigName = configName,
            SeedCount = seedCount,
            Seeds = runs.Select(r => r.Seed).ToList(),
            TotalMinutes = Stat.From(Sorted(runs.Select(r => r.TotalSeconds / 60.0))),
            LevelReached = Stat.From(Sorted(runs.Select(r => (double)r.LevelReached))),
        };

        // Union of every level any seed reached; each level aggregated only over the seeds that reached it.
        var levels = runs.SelectMany(r => r.Levels.Select(l => l.Level)).Distinct().OrderBy(l => l).ToList();
        foreach (int level in levels)
        {
            var reports = runs
                .Select(r => r.Levels.FirstOrDefault(l => l.Level == level))
                .Where(l => l != null)
                .Select(l => l!)
                .ToList();

            var la = new LevelAgg
            {
                Level = level,
                SeedsReached = reports.Count,
                Duration = Stat.From(Sorted(reports.Select(r => r.DurationSeconds))),
                Acting = Stat.From(Sorted(reports.Select(r => r.ActingSeconds))),
                Waiting = Stat.From(Sorted(reports.Select(r => r.WaitingSeconds))),
                MoneyEntry = Stat.From(Sorted(reports.Select(r => (double)r.MoneyAtEntry))),
                MoneyExit = Stat.From(Sorted(reports.Select(r => (double)r.MoneyAtExit))),
            };

            // Every family any seed recorded this level; a seed with no entry contributes 0 (it did reach the
            // level, it just had no pressure of that family — a real 0, not a missing sample).
            var families = reports
                .SelectMany(r => r.PressureFamilies().Keys)
                .Distinct().OrderBy(f => f, StringComparer.Ordinal).ToList();
            foreach (string fam in families)
            {
                var vals = Sorted(reports.Select(r => r.PressureFamilies().GetValueOrDefault(fam)));
                la.Pressure[fam] = Stat.From(vals);
            }
            agg.Levels.Add(la);
        }

        agg.Purchases = ReducePurchases(runs);
        return agg;
    }

    /// For each distinct remedy (kind+target), the level at which each seed FIRST bought it, reduced to
    /// median/p10/p90. Seeds that never bought it are excluded (they widen no band — absence is not level 0).
    static List<PurchaseAgg> ReducePurchases(List<SimResult> runs)
    {
        var firstLevelBySeed = new Dictionary<string, (string kind, string target, string forPressure, List<double> levels)>();
        foreach (var run in runs)
        {
            var seenThisSeed = new HashSet<string>();
            foreach (var lvl in run.Levels)
                foreach (var p in lvl.Purchases)
                {
                    string key = p.Kind + "\u0000" + p.Target;
                    if (!seenThisSeed.Add(key)) continue; // first purchase in this seed only
                    if (!firstLevelBySeed.TryGetValue(key, out var entry))
                        entry = (p.Kind, p.Target, p.ForPressure, new List<double>());
                    entry.levels.Add(lvl.Level);
                    firstLevelBySeed[key] = entry;
                }
        }

        return firstLevelBySeed
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new PurchaseAgg
            {
                Kind = kv.Value.kind,
                Target = kv.Value.target,
                ForPressure = kv.Value.forPressure,
                SeedsBought = kv.Value.levels.Count,
                FirstLevel = Stat.From(Sorted(kv.Value.levels)),
            })
            .OrderBy(p => p.FirstLevel.Median).ThenBy(p => p.Target, StringComparer.Ordinal)
            .ToList();
    }

    static List<double> Sorted(IEnumerable<double> values)
    {
        var list = values.ToList();
        list.Sort();
        return list;
    }

    /// Linear-interpolated percentile on an already-sorted list. Empty ⇒ 0.
    static double Percentile(List<double> sorted, double p)
    {
        int n = sorted.Count;
        if (n == 0) return 0;
        if (n == 1) return sorted[0];
        double rank = p * (n - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        double frac = rank - lo;
        return sorted[lo] * (1 - frac) + sorted[hi] * frac;
    }
}
