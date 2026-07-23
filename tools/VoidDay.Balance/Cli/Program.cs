using Newtonsoft.Json;
using VoidDay.Balance.Agent;
using VoidDay.Balance.Api;
using VoidDay.Balance.Schema;
using VoidDay.Balance.Sim;
using VoidDay.Balance.Unity;

// VoidDay Balance Tool — CLI entrypoint. Verbs: `read` (M01), `write` (M02), `sim` (M03), `serve` (M04),
// `eval`/`patch`/`suggest`/`sweep`/`report` (M05 — agent primitives).
// The Unity project is never told this tool exists (spec, the agnosticism rule).

// Bare `dotnet run` (no verb, or only --options) launches the workbench — the DoD's "dotnet run … serves
// the app". A leading token that isn't a `--option` is the verb.
var hasVerb = args.Length > 0 && !args[0].StartsWith("--");
var verb = hasVerb ? args[0] : "serve";
var opts = ParseOptions(hasVerb ? args[1..] : args);
var projectRoot = opts.GetValueOrDefault("project") ?? FindProjectRoot();

switch (verb)
{
    case "read": return Read(projectRoot, opts);
    case "write": return Write(projectRoot, opts);
    case "sim": return Sim(projectRoot, opts);
    case "eval": return Eval(projectRoot, opts);
    case "patch": return PatchCmd(projectRoot, opts);
    case "suggest": return SuggestCmd(projectRoot, opts);
    case "sweep": return SweepCmd(projectRoot, opts);
    case "report": return Report(projectRoot, opts);
    case "serve":
        int port = opts.TryGetValue("port", out var portStr) ? int.Parse(portStr) : 5177;
        return Server.Serve(projectRoot, port);
    default:
        Usage();
        return 1;
}

static int Read(string projectRoot, Dictionary<string, string> opts)
{
    var outPath = opts.GetValueOrDefault("out")
                  ?? Path.Combine(projectRoot, "tools", "VoidDay.Balance", "versions", "baseline.json");

    var config = new EconomyReader(new AssetReader(new GuidIndex(projectRoot))).Read();

    var json = JsonConvert.SerializeObject(config, Formatting.Indented);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    File.WriteAllText(outPath, json + "\n");

    Console.WriteLine(
        $"read ok: {config.Stations.Count} stations, {config.Recipes.Count} recipes, " +
        $"{config.Upgrades.Count} upgrades, {config.Resources.Count} resources, {config.Levels.Count} levels " +
        $"→ {outPath}");
    return 0;
}

static int Write(string projectRoot, Dictionary<string, string> opts)
{
    if (!opts.TryGetValue("config", out var configPath))
    {
        Console.Error.WriteLine("write: --config <file> is required (the edited JSON to apply).");
        return 1;
    }
    var apply = opts.ContainsKey("apply");

    var incoming = JsonConvert.DeserializeObject<BalanceConfig>(File.ReadAllText(configPath))
                   ?? throw new InvalidOperationException($"{configPath}: parsed to null — not a BalanceConfig JSON.");

    var reader = new EconomyReader(new AssetReader(new GuidIndex(projectRoot)));
    var current = reader.Read();
    var writer = new AssetWriter(projectRoot, reader, current);

    WritePlan plan;
    try
    {
        // All validation happens here, before a single byte is written. A refusal aborts whole.
        plan = writer.Plan(incoming);
    }
    catch (WriteRefusedException ex)
    {
        Console.Error.WriteLine("refused: " + ex.Message);
        return 1;
    }

    if (plan.IsEmpty)
    {
        Console.WriteLine("no changes.");
        return 0;
    }

    foreach (var c in plan.Scalars)
        Console.WriteLine($"  {c.AssetPath} {c.Field}: {c.Old} → {c.New}");
    foreach (var i in plan.RecipeInsertions)
        Console.WriteLine($"  + create recipe '{i.Recipe.Id}' (station '{i.Recipe.StationType}') + wire into its StationSO");

    if (!apply)
    {
        Console.WriteLine($"dry run: {plan.Scalars.Count} scalar change(s), {plan.RecipeInsertions.Count} insertion(s). Pass --apply to write.");
        return 0;
    }

    writer.Apply(plan);
    Console.WriteLine($"applied: {plan.Scalars.Count} scalar change(s), {plan.RecipeInsertions.Count} insertion(s).");
    return 0;
}

// Run one seeded player through the real economy and print the per-level table (M03). Reads a BalanceConfig
// JSON (a versions/*.json, produced by `read`) — never re-reads Unity, and never writes anything.
static int Sim(string projectRoot, Dictionary<string, string> opts)
{
    var name = opts.GetValueOrDefault("config") ?? "baseline";
    var configPath = File.Exists(name)
        ? name
        : Path.Combine(projectRoot, "tools", "VoidDay.Balance", "versions", name + ".json");
    if (!File.Exists(configPath))
    {
        Console.Error.WriteLine($"sim: config '{name}' not found (looked at {configPath}). Run `balance read` first.");
        return 1;
    }

    var config = JsonConvert.DeserializeObject<BalanceConfig>(File.ReadAllText(configPath))
                 ?? throw new InvalidOperationException($"{configPath}: parsed to null — not a BalanceConfig JSON.");

    var profileName = opts.GetValueOrDefault("profile") ?? "typical";
    var profile = profileName == "perfect" ? SimProfile.Perfect() : SimProfile.Typical();
    profile.Name = profileName;
    if (opts.TryGetValue("optimality", out var optStr)) profile.Optimality = float.Parse(optStr);

    int seed = opts.TryGetValue("seed", out var seedStr) ? int.Parse(seedStr) : 1;
    bool gemsEnabled = !opts.ContainsKey("no-gems");

    var result = new SimRunner(config, profile, seed, gemsEnabled).Run();

    if (opts.ContainsKey("json"))
        Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
    else
        Console.Write(SimRunner.Render(result));
    return 0;
}

// ---- M05 agent primitives: eval / patch / suggest / sweep / report ----

// Score a config against a goal → one loss + a per-target breakdown. Optionally applies a --patch first
// (in-memory, guardrailed) so a candidate can be measured without persisting it. Appends one line to runs.jsonl.
static int Eval(string projectRoot, Dictionary<string, string> opts)
{
    if (!opts.TryGetValue("goal", out var goalPath))
    { Console.Error.WriteLine("eval: --goal <file> is required."); return 1; }

    var config = LoadConfig(projectRoot, opts);
    var goal = LoadGoal(goalPath);

    var patchOps = new List<Patch.PatchOp>();
    if (opts.TryGetValue("patch", out var patchFile))
    {
        patchOps = LoadPatch(patchFile);
        try { config = Patch.Apply(config, patchOps, Bounds.Load(projectRoot)); }
        catch (Patch.PatchRejectedException ex) { Console.Error.WriteLine("refused: " + ex.Message); return 1; }
    }

    var (profile, seed, gems) = SimSettings(opts);
    var result = new SimRunner(config, profile, seed, gems).Run();
    var loss = GoalEvaluator.Evaluate(result, goal);

    var breakdown = new Dictionary<string, double>();
    for (int i = 0; i < loss.Targets.Count; i++)
    {
        var t = loss.Targets[i];
        string key = $"{t.Metric}@{t.Scope}";
        if (breakdown.ContainsKey(key)) key += $"#{i}";
        breakdown[key] = t.Contribution;
    }
    Journal.Append(projectRoot, new Journal.RunRecord
    {
        Ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        Config = config.Name,
        Goal = goal.Name,
        ConfigHash = Journal.ConfigHash(config),
        Patch = patchOps,
        Loss = loss.Loss,
        Breakdown = breakdown
    });

    if (opts.ContainsKey("json")) Console.WriteLine(JsonConvert.SerializeObject(loss, Formatting.Indented));
    else Console.Write(GoalEvaluator.Render(loss));
    return 0;
}

// Apply a patch config→config (NEVER to Unity), guardrailed by bounds.json and the profile/* rejection.
// The resulting config JSON goes to --out (or stdout). A rejected op aborts the whole patch, loud.
static int PatchCmd(string projectRoot, Dictionary<string, string> opts)
{
    var config = LoadConfig(projectRoot, opts);

    List<Patch.PatchOp> ops;
    if (opts.TryGetValue("patch", out var patchFile)) ops = LoadPatch(patchFile);
    else if (opts.TryGetValue("path", out var path) && opts.TryGetValue("value", out var valueStr))
        ops = new() { new() { Op = "set", Path = path, Value = double.Parse(valueStr) } };
    else { Console.Error.WriteLine("patch: pass --patch <file>, or --path <p> --value <v>."); return 1; }

    BalanceConfig patched;
    try { patched = Patch.Apply(config, ops, Bounds.Load(projectRoot)); }
    catch (Patch.PatchRejectedException ex) { Console.Error.WriteLine("refused: " + ex.Message); return 1; }

    var json = JsonConvert.SerializeObject(patched, Formatting.Indented);
    if (opts.TryGetValue("out", out var outPath)) { File.WriteAllText(outPath, json + "\n"); Console.Error.WriteLine($"patched: {ops.Count} op(s) → {outPath}"); }
    else Console.WriteLine(json);
    return 0;
}

// Name the knobs responsible for the dominant bottleneck (structural), and — where gems are papering it over —
// the gem knobs, flagged as a different kind of fix.
static int SuggestCmd(string projectRoot, Dictionary<string, string> opts)
{
    var config = LoadConfig(projectRoot, opts);
    var (profile, seed, gems) = SimSettings(opts);
    var result = new SimRunner(config, profile, seed, gems).Run();
    var report = Suggest.Analyze(result, config);

    if (opts.ContainsKey("json")) Console.WriteLine(JsonConvert.SerializeObject(report, Formatting.Indented));
    else Console.Write(Suggest.Render(report));
    return 0;
}

// 1-D sensitivity: loss across a knob's range, N steps. The coordinate-descent primitive — no search loop here.
static int SweepCmd(string projectRoot, Dictionary<string, string> opts)
{
    if (!opts.TryGetValue("goal", out var goalPath)) { Console.Error.WriteLine("sweep: --goal <file> is required."); return 1; }
    if (!opts.TryGetValue("path", out var path)) { Console.Error.WriteLine("sweep: --path <knob> is required."); return 1; }
    if (!opts.TryGetValue("from", out var fromStr) || !opts.TryGetValue("to", out var toStr))
    { Console.Error.WriteLine("sweep: --from and --to are required."); return 1; }
    int steps = opts.TryGetValue("steps", out var stepsStr) ? int.Parse(stepsStr) : 5;

    var config = LoadConfig(projectRoot, opts);
    var goal = LoadGoal(goalPath);
    var (profile, seed, gems) = SimSettings(opts);
    var report = Sweep.Run(config, goal, path, double.Parse(fromStr), double.Parse(toStr), steps, profile, seed, gems);

    if (opts.ContainsKey("json")) Console.WriteLine(JsonConvert.SerializeObject(report, Formatting.Indented));
    else Console.Write(Sweep.Render(report));
    return 0;
}

// The flat global eval log (runs.jsonl). M7 turns this into per-session reports; here it is a plain listing.
static int Report(string projectRoot, Dictionary<string, string> opts)
{
    var runs = Journal.ReadAll(projectRoot);
    if (opts.ContainsKey("json")) { Console.WriteLine(JsonConvert.SerializeObject(runs, Formatting.Indented)); return 0; }

    Console.WriteLine($"runs.jsonl: {runs.Count} eval(s)");
    Console.WriteLine("Loss       Config          Goal            Hash          Patch");
    Console.WriteLine("-------    --------------  --------------  ------------  -----");
    foreach (var r in runs)
    {
        string patch = r.Patch.Count == 0 ? "-" : string.Join(", ", r.Patch.Select(p => $"{p.Path}={p.Value}"));
        Console.WriteLine($"{r.Loss,7:0.####}    {r.Config,-14}  {r.Goal,-14}  {r.ConfigHash,-12}  {patch}");
    }
    return 0;
}

// ---- shared M05 helpers ----

static BalanceConfig LoadConfig(string projectRoot, Dictionary<string, string> opts)
{
    var name = opts.GetValueOrDefault("config") ?? "baseline";
    var path = File.Exists(name) ? name : Path.Combine(projectRoot, "tools", "VoidDay.Balance", "versions", name + ".json");
    if (!File.Exists(path))
        throw new FileNotFoundException($"config '{name}' not found (looked at {path}). Run `balance read` first.");
    return JsonConvert.DeserializeObject<BalanceConfig>(File.ReadAllText(path))
           ?? throw new InvalidOperationException($"{path}: parsed to null — not a BalanceConfig JSON.");
}

static Goal LoadGoal(string goalPath)
{
    if (!File.Exists(goalPath)) throw new FileNotFoundException($"goal file '{goalPath}' not found.");
    return JsonConvert.DeserializeObject<Goal>(File.ReadAllText(goalPath))
           ?? throw new InvalidOperationException($"{goalPath}: parsed to null — not a Goal JSON.");
}

static List<Patch.PatchOp> LoadPatch(string patchPath)
{
    if (!File.Exists(patchPath)) throw new FileNotFoundException($"patch file '{patchPath}' not found.");
    return JsonConvert.DeserializeObject<List<Patch.PatchOp>>(File.ReadAllText(patchPath))
           ?? throw new InvalidOperationException($"{patchPath}: parsed to null — expected a JSON array of ops.");
}

static (SimProfile profile, int seed, bool gems) SimSettings(Dictionary<string, string> opts)
{
    var profileName = opts.GetValueOrDefault("profile") ?? "typical";
    var profile = profileName == "perfect" ? SimProfile.Perfect() : SimProfile.Typical();
    profile.Name = profileName;
    if (opts.TryGetValue("optimality", out var optStr)) profile.Optimality = float.Parse(optStr);
    int seed = opts.TryGetValue("seed", out var seedStr) ? int.Parse(seedStr) : 1;
    return (profile, seed, !opts.ContainsKey("no-gems"));
}

static void Usage() => Console.Error.WriteLine(
    "usage:\n" +
    "  balance serve   [--project <dir>] [--port N]   (default when no verb given)\n" +
    "  balance read    [--project <dir>] [--out <file>]\n" +
    "  balance write   --config <file> [--project <dir>] [--apply]\n" +
    "  balance sim     [--config <name|file>] [--profile typical|perfect] [--seed N] [--optimality X] [--no-gems] [--json]\n" +
    "  balance eval    --goal <file> [--config <name|file>] [--patch <file>] [--seed N] [--profile P] [--optimality X] [--no-gems] [--json]\n" +
    "  balance patch   (--patch <file> | --path <p> --value <v>) [--config <name|file>] [--out <file>]\n" +
    "  balance suggest [--config <name|file>] [--seed N] [--profile P] [--optimality X] [--no-gems] [--json]\n" +
    "  balance sweep   --goal <file> --path <knob> --from A --to B [--steps N] [--config <name|file>] [--seed N] [--json]\n" +
    "  balance report  [--json]");

static Dictionary<string, string> ParseOptions(string[] tokens)
{
    var opts = new Dictionary<string, string>();
    for (var i = 0; i < tokens.Length; i++)
    {
        if (!tokens[i].StartsWith("--"))
            throw new ArgumentException($"unexpected argument '{tokens[i]}'");
        var key = tokens[i][2..];
        // Flags (no value): --apply, --json, --no-gems. Everything else takes the next token as its value.
        if (key is "apply" or "json" or "no-gems") { opts[key] = "true"; continue; }
        if (i + 1 >= tokens.Length)
            throw new ArgumentException($"option --{key} needs a value");
        opts[key] = tokens[++i];
    }
    return opts;
}

// Discover the repo root by walking up from cwd to the folder holding both Assets/ and .gitignore.
static string FindProjectRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, "Assets"))
            && File.Exists(Path.Combine(dir.FullName, ".gitignore")))
            return dir.FullName;
        dir = dir.Parent;
    }
    throw new InvalidOperationException(
        "Could not find the Unity project root (a folder with Assets/ and .gitignore) above the current directory. Pass --project.");
}
