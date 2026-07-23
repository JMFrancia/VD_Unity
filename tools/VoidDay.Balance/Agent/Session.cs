using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using VoidDay.Balance.Schema;

namespace VoidDay.Balance.Agent;

/// A balancing session is a directory holding one tuning run's durable state (spec: "Sessions in the tool"):
///
///   sessions/&lt;date&gt;-&lt;slug&gt;/
///     goal.json            the agreed goal (step 1 of the workflow)
///     config.start.json    the config as found — never mutated after `session start`
///     config.current.json  the working config — patched freely, never touches Unity
///     journal.jsonl        one line per iteration: patch, loss, breakdown, RATIONALE
///     report.md            generated FROM journal.jsonl, never narrated from memory
///
/// ★ The report is generated, not narrated. A long run exhausts a context window; an agent summarising from a
/// compacted context produces a tidier story than what happened. Every claim in the generated report traces
/// to a journal line or to config.start/config.current — there is no path here that invents a number.
public static class Session
{
    public static string SessionsRoot(string projectRoot) =>
        Path.Combine(projectRoot, "tools", "VoidDay.Balance", "sessions");

    public static string Dir(string projectRoot, string name) =>
        Path.Combine(SessionsRoot(projectRoot), name);

    static string GoalPath(string dir) => Path.Combine(dir, "goal.json");
    static string StartPath(string dir) => Path.Combine(dir, "config.start.json");
    static string CurrentPath(string dir) => Path.Combine(dir, "config.current.json");
    static string JournalPath(string dir) => Path.Combine(dir, "journal.jsonl");
    static string ReportPath(string dir) => Path.Combine(dir, "report.md");

    /// One iteration of the tuning loop. `Rationale` is the one field the agent MUST supply — it is what lets
    /// the report explain *why* each change was made, not only what changed.
    public sealed class IterationRecord
    {
        public int Iteration;
        public long Ts;
        public List<Patch.PatchOp> Patch = new();   // ops applied to config.current this iteration; [] for a bare re-eval
        public string ConfigHash = "";              // SHA256[..12] of config.current AS SIMMED
        public double Loss;
        public Dictionary<string, double> Breakdown = new();  // "metric@scope" ⇒ contribution
        public string Rationale = "";
    }

    /// Resolve a session by its exact dir name, or by a slug (newest dir ending in "-&lt;slug&gt;"), or — when
    /// `name` is null/empty — the most recently modified session. Fails loud when nothing matches.
    public static string Resolve(string projectRoot, string? name)
    {
        var root = SessionsRoot(projectRoot);
        if (!Directory.Exists(root))
            throw new InvalidOperationException($"no sessions yet (looked in {root}). Run `session start` first.");

        var dirs = Directory.GetDirectories(root)
            .OrderByDescending(d => Directory.GetLastWriteTimeUtc(d))
            .ToList();
        if (dirs.Count == 0)
            throw new InvalidOperationException($"no sessions yet (looked in {root}). Run `session start` first.");

        if (string.IsNullOrEmpty(name))
            return dirs[0];

        var exact = dirs.FirstOrDefault(d => Path.GetFileName(d) == name);
        if (exact != null) return exact;

        var bySlug = dirs.FirstOrDefault(d => Path.GetFileName(d).EndsWith("-" + name, StringComparison.Ordinal));
        if (bySlug != null) return bySlug;

        throw new InvalidOperationException(
            $"no session matches '{name}' (checked exact name and '-{name}' suffix). Existing: " +
            string.Join(", ", dirs.Select(Path.GetFileName)));
    }

    /// Create a session directory and seed it: goal.json, config.start.json, config.current.json (a copy of
    /// start), and an empty journal.jsonl. Returns the created directory. Refuses to clobber an existing session.
    public static string Start(string projectRoot, string slug, string goalPath, BalanceConfig startConfig)
    {
        var goal = JsonConvert.DeserializeObject<Goal>(File.ReadAllText(goalPath))
                   ?? throw new InvalidOperationException($"{goalPath}: parsed to null — not a Goal JSON.");

        var dirName = $"{DateTime.Now:yyyy-MM-dd}-{slug}";
        var dir = Dir(projectRoot, dirName);
        if (Directory.Exists(dir))
            throw new InvalidOperationException($"session '{dirName}' already exists at {dir} — pick another --name.");
        Directory.CreateDirectory(dir);

        // goal.json is the canonicalised goal (so the report and every eval read the same object).
        File.WriteAllText(GoalPath(dir), JsonConvert.SerializeObject(goal, Formatting.Indented) + "\n");
        var startJson = JsonConvert.SerializeObject(startConfig, Formatting.Indented) + "\n";
        File.WriteAllText(StartPath(dir), startJson);
        File.WriteAllText(CurrentPath(dir), startJson);
        File.WriteAllText(JournalPath(dir), "");
        return dir;
    }

    public static Goal LoadGoal(string dir) =>
        JsonConvert.DeserializeObject<Goal>(File.ReadAllText(GoalPath(dir)))
        ?? throw new InvalidOperationException($"{GoalPath(dir)}: parsed to null — not a Goal JSON.");

    public static BalanceConfig LoadStart(string dir) =>
        JsonConvert.DeserializeObject<BalanceConfig>(File.ReadAllText(StartPath(dir)))
        ?? throw new InvalidOperationException($"{StartPath(dir)}: parsed to null — not a BalanceConfig JSON.");

    public static BalanceConfig LoadCurrent(string dir) =>
        JsonConvert.DeserializeObject<BalanceConfig>(File.ReadAllText(CurrentPath(dir)))
        ?? throw new InvalidOperationException($"{CurrentPath(dir)}: parsed to null — not a BalanceConfig JSON.");

    public static void SaveCurrent(string dir, BalanceConfig config) =>
        File.WriteAllText(CurrentPath(dir), JsonConvert.SerializeObject(config, Formatting.Indented) + "\n");

    public static void AppendIteration(string dir, IterationRecord rec) =>
        File.AppendAllText(JournalPath(dir), JsonConvert.SerializeObject(rec, Formatting.None) + "\n");

    public static List<IterationRecord> ReadJournal(string dir)
    {
        var path = JournalPath(dir);
        var records = new List<IterationRecord>();
        if (!File.Exists(path)) return records;
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            records.Add(JsonConvert.DeserializeObject<IterationRecord>(line)!);
        }
        return records;
    }

    public static int NextIteration(string dir) => ReadJournal(dir).Count + 1;

    // ---- report.md, generated from the journal ----

    /// Build report.md purely from durable files: goal.json, config.start.json, config.current.json, and every
    /// journal.jsonl line. Nothing is read from the caller's memory — that is the whole point (QA-19).
    public static string GenerateReport(string dir)
    {
        var goal = LoadGoal(dir);
        var journal = ReadJournal(dir);
        var start = LoadStart(dir);
        var current = LoadCurrent(dir);
        var name = Path.GetFileName(dir);

        var sb = new StringBuilder();
        sb.AppendLine($"# Balancing session — {name}");
        sb.AppendLine();
        sb.AppendLine($"_Generated from `journal.jsonl` ({journal.Count} iterations). Every claim below traces to a journal line or to `config.start.json` / `config.current.json` — nothing is narrated._");
        sb.AppendLine();

        // --- Goal ---
        sb.AppendLine($"## Goal — `{goal.Name}`");
        sb.AppendLine();
        sb.AppendLine("| Metric | Scope | Bound | Weight |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var t in goal.Targets)
            sb.AppendLine($"| {t.Metric} | {t.ScopeLabel()} | {BoundText(t)} | {t.Weight:0.##} |");
        sb.AppendLine();

        // --- Trajectory ---
        if (journal.Count > 0)
        {
            double first = journal[0].Loss, last = journal[^1].Loss;
            sb.AppendLine("## Loss trajectory");
            sb.AppendLine();
            sb.AppendLine($"Starting loss **{first:0.####}** → final loss **{last:0.####}** over {journal.Count} iteration(s) " +
                          $"({(last < first ? $"down {first - last:0.####}" : last > first ? $"up {last - first:0.####}" : "unchanged")}).");
            sb.AppendLine();
            sb.AppendLine("| # | Loss | Change | Rationale |");
            sb.AppendLine("|---|---|---|---|");
            double prev = double.NaN;
            foreach (var r in journal)
            {
                string change = double.IsNaN(prev) ? "—" : (r.Loss < prev ? $"▼ {prev - r.Loss:0.###}" : r.Loss > prev ? $"▲ {r.Loss - prev:0.###}" : "—");
                sb.AppendLine($"| {r.Iteration} | {r.Loss:0.####} | {change} | {Escape(r.Rationale)} |");
                prev = r.Loss;
            }
            sb.AppendLine();

            // --- Each iteration in detail: patch + rationale ---
            sb.AppendLine("## Iterations");
            sb.AppendLine();
            foreach (var r in journal)
            {
                sb.AppendLine($"### Iteration {r.Iteration} — loss {r.Loss:0.####}");
                sb.AppendLine();
                sb.AppendLine($"- **Rationale:** {r.Rationale}");
                if (r.Patch.Count > 0)
                    sb.AppendLine($"- **Patch:** {string.Join(", ", r.Patch.Select(p => $"`{p.Path}` = {p.Value}"))}");
                else
                    sb.AppendLine("- **Patch:** (none — re-evaluation)");
                sb.AppendLine($"- **Config hash:** `{r.ConfigHash}`");
                var top = r.Breakdown.OrderByDescending(kv => kv.Value).Take(4).ToList();
                if (top.Count > 0)
                    sb.AppendLine($"- **Top contributors:** {string.Join(", ", top.Select(kv => $"{kv.Key} {kv.Value:0.###}"))}");
                sb.AppendLine();
            }

            // --- Final loss breakdown (from the last journal line) ---
            sb.AppendLine("## Final loss breakdown");
            sb.AppendLine();
            sb.AppendLine("| Target | Contribution |");
            sb.AppendLine("|---|---|");
            foreach (var kv in journal[^1].Breakdown.OrderByDescending(kv => kv.Value))
                sb.AppendLine($"| {kv.Key} | {kv.Value:0.####} |");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("## Loss trajectory");
            sb.AppendLine();
            sb.AppendLine("_No iterations recorded yet._");
            sb.AppendLine();
        }

        // --- The exact diff that would be exported to Unity (config.current vs config.start) ---
        var diffs = DiffConfigs(start, current);
        sb.AppendLine("## Diff to export to Unity");
        sb.AppendLine();
        if (diffs.Count == 0)
        {
            sb.AppendLine("_No change from the config as found — nothing to export._");
        }
        else
        {
            sb.AppendLine($"{diffs.Count} field(s) differ between `config.start.json` and `config.current.json`. " +
                          "This is what `write --apply` (gated) would push into `Assets/`:");
            sb.AppendLine();
            sb.AppendLine("| Path | Start | Current |");
            sb.AppendLine("|---|---|---|");
            foreach (var d in diffs)
                sb.AppendLine($"| `{d.Path}` | {d.Old} | {d.New} |");
        }
        sb.AppendLine();

        var report = sb.ToString();
        File.WriteAllText(ReportPath(dir), report);
        return report;
    }

    static string BoundText(GoalTarget t)
    {
        if (t.Metric == "pressure.rank") return $"rank ≤ {t.MaxRank ?? 1}";
        var parts = new List<string>();
        if (t.Category != null) parts.Add(t.Category);
        if (t.Min.HasValue) parts.Add($"min {t.Min}");
        if (t.Max.HasValue) parts.Add($"max {t.Max}");
        return parts.Count > 0 ? string.Join(", ", parts) : "(none)";
    }

    static string Escape(string s) => s.Replace("|", "\\|").Replace("\n", " ");

    public readonly record struct ScalarDiff(string Path, string Old, string New);

    /// A generic deep diff of two BalanceConfig JSON trees → the leaf scalars that changed. Truthful about
    /// *what* differs regardless of how it got there — it reads the two files, not the patch history.
    public static List<ScalarDiff> DiffConfigs(BalanceConfig start, BalanceConfig current)
    {
        var a = JObject.FromObject(start);
        var b = JObject.FromObject(current);
        var diffs = new List<ScalarDiff>();
        DiffTokens("", a, b, diffs);
        return diffs;
    }

    static void DiffTokens(string path, JToken a, JToken b, List<ScalarDiff> diffs)
    {
        if (a.Type != b.Type)
        {
            diffs.Add(new ScalarDiff(path, a.ToString(), b.ToString()));
            return;
        }
        switch (a.Type)
        {
            case JTokenType.Object:
                var oa = (JObject)a; var ob = (JObject)b;
                foreach (var prop in oa.Properties())
                {
                    var bv = ob[prop.Name];
                    if (bv == null) { diffs.Add(new ScalarDiff(Join(path, prop.Name), prop.Value.ToString(), "(absent)")); continue; }
                    DiffTokens(Join(path, prop.Name), prop.Value, bv, diffs);
                }
                foreach (var prop in ob.Properties())
                    if (oa[prop.Name] == null) diffs.Add(new ScalarDiff(Join(path, prop.Name), "(absent)", prop.Value.ToString()));
                break;
            case JTokenType.Array:
                var aa = (JArray)a; var ba = (JArray)b;
                int n = Math.Max(aa.Count, ba.Count);
                for (int i = 0; i < n; i++)
                {
                    // Label an element by its id (Id / StationType / Type) when it has one, so the diff reads
                    // `Recipes[field.wheatGrow].Duration` rather than the opaque `Recipes[9].Duration`.
                    string idx = i < aa.Count ? ElementLabel(aa[i], i) : ElementLabel(ba[i], i);
                    if (i >= aa.Count) { diffs.Add(new ScalarDiff($"{path}[{idx}]", "(absent)", ba[i].ToString())); continue; }
                    if (i >= ba.Count) { diffs.Add(new ScalarDiff($"{path}[{idx}]", aa[i].ToString(), "(absent)")); continue; }
                    DiffTokens($"{path}[{idx}]", aa[i], ba[i], diffs);
                }
                break;
            default:
                if (!JToken.DeepEquals(a, b))
                    diffs.Add(new ScalarDiff(path, a.ToString(), b.ToString()));
                break;
        }
    }

    static string Join(string path, string field) => path.Length == 0 ? field : $"{path}.{field}";

    static string ElementLabel(JToken el, int i)
    {
        if (el is JObject o)
            foreach (var key in new[] { "Id", "StationType", "Type" })
                if (o[key] is JValue { Type: JTokenType.String } v && !string.IsNullOrEmpty((string?)v))
                    return (string)v!;
        return i.ToString();
    }
}
