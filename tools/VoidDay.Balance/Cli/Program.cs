using Newtonsoft.Json;
using VoidDay.Balance.Schema;
using VoidDay.Balance.Sim;
using VoidDay.Balance.Unity;

// VoidDay Balance Tool — CLI entrypoint. Verbs: `read` (M01), `write` (M02), `sim` (M03).
// The Unity project is never told this tool exists (spec, the agnosticism rule).

if (args.Length == 0)
{
    Usage();
    return 1;
}

var verb = args[0];
var opts = ParseOptions(args[1..]);
var projectRoot = opts.GetValueOrDefault("project") ?? FindProjectRoot();

switch (verb)
{
    case "read": return Read(projectRoot, opts);
    case "write": return Write(projectRoot, opts);
    case "sim": return Sim(projectRoot, opts);
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

static void Usage() => Console.Error.WriteLine(
    "usage:\n" +
    "  balance read  [--project <dir>] [--out <file>]\n" +
    "  balance write --config <file> [--project <dir>] [--apply]\n" +
    "  balance sim   [--config <name|file>] [--profile typical|perfect] [--seed N] [--optimality X] [--no-gems] [--json]");

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
