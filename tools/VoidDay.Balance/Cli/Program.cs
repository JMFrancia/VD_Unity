using Newtonsoft.Json;
using VoidDay.Balance.Unity;

// VoidDay Balance Tool — CLI entrypoint. M01 ships one verb: `read`.
// The Unity project is never told this tool exists (spec, the agnosticism rule).

var args2 = Environment.GetCommandLineArgs()[1..];   // drop the exe path
if (args2.Length == 0 || args2[0] != "read")
{
    Console.Error.WriteLine("usage: balance read [--project <dir>] [--out <file>]");
    return 1;
}

var opts = ParseOptions(args2[1..]);
var projectRoot = opts.GetValueOrDefault("project") ?? FindProjectRoot();
var outPath = opts.GetValueOrDefault("out")
              ?? Path.Combine(projectRoot, "tools", "VoidDay.Balance", "versions", "baseline.json");

var guids = new GuidIndex(projectRoot);
var reader = new AssetReader(guids);
var config = new EconomyReader(reader).Read();

var json = JsonConvert.SerializeObject(config, Formatting.Indented);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
File.WriteAllText(outPath, json + "\n");

Console.WriteLine(
    $"read ok: {config.Stations.Count} stations, {config.Recipes.Count} recipes, " +
    $"{config.Upgrades.Count} upgrades, {config.Resources.Count} resources, {config.Levels.Count} levels " +
    $"→ {outPath}");
return 0;

static Dictionary<string, string> ParseOptions(string[] tokens)
{
    var opts = new Dictionary<string, string>();
    for (var i = 0; i < tokens.Length; i++)
    {
        if (!tokens[i].StartsWith("--"))
            throw new ArgumentException($"unexpected argument '{tokens[i]}'");
        var key = tokens[i][2..];
        if (i + 1 >= tokens.Length)
            throw new ArgumentException($"option --{key} needs a value");
        opts[key] = tokens[++i];
    }
    return opts;
}

// Discover the repo root by walking up from cwd to the folder holding both Assets/ and .gitignore.
// Keeps `dotnet run --project tools/VoidDay.Balance -- read` working from the repo root or the tool dir.
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
