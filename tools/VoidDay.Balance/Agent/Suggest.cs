using System.Text;
using VoidDay.Balance.Schema;

namespace VoidDay.Balance.Agent;

/// The pressure→knob map. Given a SimResult and the config that produced it, name the handful of knobs
/// actually responsible for the dominant bottleneck — possible only because the pressure ledger already knows
/// *why* the player is stuck (spec).
///
/// ★ Where gem relief is large, gem knobs join the shortlist FLAGGED as a different kind of fix: raising the
/// gem drip HIDES a bottleneck; the structural knob REMOVES it. They are presented as distinct choices so an
/// agent cannot quietly take the cheaper-looking gem knob and declare the problem solved.
public static class Suggest
{
    // Relief counts as "large" once a skip-addressable share of the dominant bottleneck is being papered over
    // by gems. A tool heuristic, not game data.
    const double ReliefShareThreshold = 0.15;

    public sealed class KnobHint
    {
        public string Path = "";
        public string Kind = "";   // "structural" | "relief"
        public string Note = "";
    }

    public sealed class SuggestReport
    {
        public string Dominant = "";        // raw top pressure key, e.g. "Capacity:field" or "Storage"
        public string DominantFamily = "";  // "Capacity", "Storage", ...
        public double DominantSeconds;
        public double ReliefSeconds;
        public List<KnobHint> Structural = new();
        public List<KnobHint> Relief = new();
    }

    public static SuggestReport Analyze(SimResult result, BalanceConfig config)
    {
        // Sum raw pressure keys across the whole run — the parametrised suffix (Capacity:field) is what lets
        // the map reach the responsible station, so it is preserved rather than aggregated away.
        var pressure = new Dictionary<string, double>();
        var relief = new Dictionary<string, double>();
        foreach (var l in result.Levels)
        {
            foreach (var kv in l.Pressure) pressure[kv.Key] = pressure.GetValueOrDefault(kv.Key) + kv.Value;
            foreach (var kv in l.GemRelief) relief[kv.Key] = relief.GetValueOrDefault(kv.Key) + kv.Value;
        }

        if (pressure.Count == 0)
            return new SuggestReport { Dominant = "none" };

        // Deterministic top: seconds desc, then key asc.
        var top = pressure.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First();
        string family = top.Key.Split(':')[0];
        string? param = top.Key.Contains(':') ? top.Key.Split(':')[1] : null;

        var report = new SuggestReport
        {
            Dominant = top.Key,
            DominantFamily = family,
            DominantSeconds = top.Value,
            ReliefSeconds = relief.GetValueOrDefault(top.Key)
        };

        report.Structural.AddRange(StructuralKnobs(family, param, config));

        // Gem relief joins when a meaningful share of this bottleneck is being bought away.
        double reliefShare = top.Value > 0 ? report.ReliefSeconds / top.Value : 0;
        if (reliefShare >= ReliefShareThreshold)
            report.Relief.AddRange(GemKnobs());

        return report;
    }

    static IEnumerable<KnobHint> StructuralKnobs(string family, string? param, BalanceConfig config)
    {
        switch (family)
        {
            case "Storage":
                yield return K("global.startingStorageCapacity", "raise the base storage cap");
                foreach (var u in UpgradesWithEffect(config, "StorageCap"))
                {
                    yield return K($"upgrades/{u.Id}/tiers[*].cost", "cheaper storage upgrades");
                    yield return K($"upgrades/{u.Id}/tiers[*].effects[*].amount", "more storage per tier");
                }
                break;

            case "Capacity":
                if (param != null)
                {
                    yield return K($"stations/{param}/cap", $"let the player build more {param} stations");
                    yield return K($"stations/{param}/buildCost", $"cheaper {param} stations");
                }
                break;

            case "Yield":
                if (param != null)
                {
                    foreach (var u in StationUpgradesWithEffects(config, param, "StationYield", "StationSpeed"))
                    {
                        yield return K($"upgrades/{u.Id}/tiers[*].cost", $"cheaper {param} yield/speed upgrades");
                        yield return K($"upgrades/{u.Id}/tiers[*].effects[*].amount", $"stronger {param} yield/speed");
                    }
                    foreach (var r in config.Recipes.Where(r => r.StationType == param))
                        yield return K($"recipes/{r.Id}/duration", $"faster {r.Id}");
                }
                break;

            case "Throughput":
                foreach (var u in UpgradesWithEffect(config, "StationSpeed"))
                    yield return K($"upgrades/{u.Id}/tiers[*].effects[*].amount", "faster production");
                foreach (var s in config.Stations.Where(s => s.Buildable))
                    yield return K($"stations/{s.StationType}/queueDepth", $"deeper {s.StationType} queue");
                break;

            case "Supply":
                if (param != null)
                    foreach (var r in config.Recipes.Where(r => r.Outputs.Any(o => o.Resource == param)))
                        yield return K($"recipes/{r.Id}/duration", $"faster supply of {param}");
                break;

            case "Income":
                yield return K("orders.cashMultiplier", "pay more per order");
                foreach (var res in config.Resources.Where(r => r.Sellable))
                    yield return K($"resources/{res.Id}/baseValue", $"{res.Id} worth more");
                break;

            case "OrderRefill":
                yield return K("orders.refillSeconds", "orders refill faster");
                yield return K("orders.slotCount", "more order slots");
                break;

            case "Unlock":
                foreach (var s in config.Stations.Where(s => s.Buildable))
                    yield return K($"stations/{s.StationType}/unlockLevel", $"unlock {s.StationType} sooner");
                break;
        }
    }

    static IEnumerable<KnobHint> GemKnobs()
    {
        yield return Relief("gems.secondsPerGem", "a faster gem drip HIDES the bottleneck — it does not remove it");
        yield return Relief("gems.startingGems", "more starting gems papers over the stall rather than fixing it");
        yield return Relief("gems.minGemCost", "a cheaper minimum skip buys away the wait instead of shortening it");
    }

    static IEnumerable<UpgradeConfig> UpgradesWithEffect(BalanceConfig c, string effectType) =>
        c.Upgrades.Where(u => u.Tiers.Any(t => t.Effects.Any(e => e.Type == effectType)));

    static IEnumerable<UpgradeConfig> StationUpgradesWithEffects(BalanceConfig c, string stationType, params string[] effectTypes)
    {
        var station = c.Stations.FirstOrDefault(s => s.StationType == stationType);
        if (station == null) yield break;
        foreach (var u in c.Upgrades.Where(u => station.UpgradeIds.Contains(u.Id)))
            if (u.Tiers.Any(t => t.Effects.Any(e => effectTypes.Contains(e.Type))))
                yield return u;
    }

    static KnobHint K(string path, string note) => new() { Path = path, Kind = "structural", Note = note };
    static KnobHint Relief(string path, string note) => new() { Path = path, Kind = "relief", Note = note };

    public static string Render(SuggestReport r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"dominant bottleneck: {r.Dominant}  ({r.DominantSeconds / 60.0:0.0}m gross" +
                      (r.ReliefSeconds > 0 ? $", {r.ReliefSeconds / 60.0:0.0}m bought away by gems)" : ")"));
        sb.AppendLine();
        sb.AppendLine("STRUCTURAL — removes the bottleneck:");
        if (r.Structural.Count == 0) sb.AppendLine("  (no mapped knob for this family)");
        foreach (var k in r.Structural) sb.AppendLine($"  {k.Path,-42}  {k.Note}");
        if (r.Relief.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("GEM RELIEF — HIDES the bottleneck (a different kind of fix; prefer the structural knob):");
            foreach (var k in r.Relief) sb.AppendLine($"  {k.Path,-42}  {k.Note}");
        }
        return sb.ToString();
    }
}
