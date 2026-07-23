using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VoidDay.Balance.Agent;
using VoidDay.Balance.Schema;
using VoidDay.Balance.Sim;
using VoidDay.Balance.Unity;

namespace VoidDay.Balance.Api;

/// The workbench server (M04). The browser is a client of the SAME reader/writer/runner the CLI uses —
/// there is no second economy code path here. Every endpoint is a thin adapter: parse JSON, call an
/// existing service, serialize the result. JSON goes through Newtonsoft (not System.Text.Json) so the
/// wire format is byte-identical to what `read` writes and version files round-trip exactly.
///
/// The one honest subtlety (spec, the writer contract): the workbench edits EVERY field into a version
/// JSON via save/load, but /api/write funnels through the M02 writer, which supports only scalar edits +
/// recipe insertion and REFUSES nested-collection edits. /api/write therefore returns the writer's refusal
/// verbatim rather than silently dropping the edit — the browser surfaces it.
public static class Server
{
    public static int Serve(string projectRoot, int port)
    {
        var toolDir = Path.Combine(projectRoot, "tools", "VoidDay.Balance");
        var versionsDir = Path.Combine(toolDir, "versions");
        Directory.CreateDirectory(versionsDir);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = toolDir,          // so wwwroot/ resolves regardless of the caller's cwd
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var app = builder.Build();
        app.UseDefaultFiles();   // "/" → index.html
        app.UseStaticFiles();    // serves wwwroot/ (app.js, vendor/*)

        // ---- versions: list / save-as / delete ----

        app.MapGet("/api/versions", () =>
        {
            var names = Directory.GetFiles(versionsDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            return Results.Content(JsonConvert.SerializeObject(names), "application/json");
        });

        app.MapPost("/api/versions", async (HttpRequest req) =>
        {
            var body = await ReadBody(req);
            var obj = JObject.Parse(body);
            var name = SanitizeName((string?)obj["name"]);
            var config = obj["config"]
                ?? throw new BadRequest("body must be { name, config }");
            var path = VersionPath(versionsDir, name);
            if (File.Exists(path))
                return Fail(StatusCodes.Status409Conflict, $"version '{name}' already exists — save-as never overwrites.");
            WriteVersion(path, config);
            return Results.Content(JsonConvert.SerializeObject(new { name }), "application/json");
        });

        app.MapDelete("/api/versions", (string name) =>
        {
            var clean = SanitizeName(name);
            if (clean == "baseline")
                return Fail(StatusCodes.Status400BadRequest, "refusing to delete 'baseline' — it is the frozen initial reference every comparison is measured against.");
            var path = VersionPath(versionsDir, clean);
            if (!File.Exists(path))
                return Fail(StatusCodes.Status404NotFound, $"version '{clean}' does not exist.");
            File.Delete(path);
            return Results.Content(JsonConvert.SerializeObject(new { deleted = clean }), "application/json");
        });

        // ---- config: load one version / save in place ----

        app.MapGet("/api/config", (string? name) =>
        {
            var clean = SanitizeName(name ?? "baseline");
            var path = VersionPath(versionsDir, clean);
            if (!File.Exists(path))
                return Fail(StatusCodes.Status404NotFound, $"version '{clean}' does not exist. Run `balance read` first.");
            return Results.Content(File.ReadAllText(path), "application/json");
        });

        app.MapPut("/api/config", async (string name, HttpRequest req) =>
        {
            var clean = SanitizeName(name);
            var body = await ReadBody(req);
            var config = JToken.Parse(body);
            WriteVersion(VersionPath(versionsDir, clean), config);
            return Results.Content(JsonConvert.SerializeObject(new { saved = clean }), "application/json");
        });

        // ---- sim: single seed → { result, table }; or a multi-seed sweep (seeds > 1) → the aggregate plus
        //       every per-seed run's summary+table, so a seed can be opened and reproduces its CLI output. ----

        app.MapPost("/api/sim", async (HttpRequest req) =>
        {
            var body = await ReadBody(req);
            var obj = JObject.Parse(body);
            var config = obj["config"]!.ToObject<BalanceConfig>()
                ?? throw new BadRequest("sim body must carry a 'config'.");
            var profileName = (string?)obj["profile"] ?? "typical";
            var profile = profileName == "perfect" ? SimProfile.Perfect() : SimProfile.Typical();
            profile.Name = profileName;
            if (obj["optimality"] != null) profile.Optimality = (float)obj["optimality"]!;
            bool gemsEnabled = obj["noGems"] == null || !(bool)obj["noGems"]!;

            // seeds > 1 ⇒ multi-seed sweep (M06). Absent/1 ⇒ the original single-seed shape (M03/M04 clients).
            int seedCount = obj["seeds"] != null ? (int)obj["seeds"]! : 0;
            if (seedCount > 1)
            {
                var sweep = SimSweep.Run(config, profile, seedCount, gemsEnabled);
                var seeds = sweep.SeedResults.Select(r => new
                {
                    seed = r.Seed,
                    levelReached = r.LevelReached,
                    totalMinutes = r.TotalSeconds / 60.0,
                    stop = r.Stop.ToString(),
                    table = SimRunner.Render(r),
                }).ToList();
                var sweepPayload = new { sweep = sweep.Aggregate, seeds };
                return Results.Content(JsonConvert.SerializeObject(sweepPayload), "application/json");
            }

            int seed = obj["seed"] != null ? (int)obj["seed"]! : 1;
            var result = new SimRunner(config, profile, seed, gemsEnabled).Run();
            var payload = new { result, table = SimRunner.Render(result) };
            return Results.Content(JsonConvert.SerializeObject(payload), "application/json");
        });

        // ---- write: dry-run change summary, or --apply to touch the Unity assets ----

        app.MapPost("/api/write", async (HttpRequest req) =>
        {
            var body = await ReadBody(req);
            var obj = JObject.Parse(body);
            var incoming = obj["config"]!.ToObject<BalanceConfig>()
                ?? throw new BadRequest("write body must carry a 'config'.");
            bool apply = obj["apply"] != null && (bool)obj["apply"]!;

            var reader = new EconomyReader(new AssetReader(new GuidIndex(projectRoot)));
            var current = reader.Read();
            var writer = new AssetWriter(projectRoot, reader, current);

            WritePlan plan;
            try
            {
                plan = writer.Plan(incoming);
            }
            catch (WriteRefusedException ex)
            {
                // A refusal is not an error — it is the honest answer that this edit can't reach the game.
                var refused = new { refused = ex.Message, changes = Array.Empty<object>(), insertions = Array.Empty<object>(), applied = false };
                return Results.Content(JsonConvert.SerializeObject(refused), "application/json");
            }

            var changes = plan.Scalars
                .Select(c => new { asset = c.AssetPath, field = c.Field, old = c.Old, @new = c.New })
                .ToList();
            var insertions = plan.RecipeInsertions
                .Select(i => new { recipe = i.Recipe.Id, station = i.Recipe.StationType })
                .ToList();

            if (apply) writer.Apply(plan);
            var payload = new { refused = (string?)null, changes, insertions, applied = apply };
            return Results.Content(JsonConvert.SerializeObject(payload), "application/json");
        });

        // ---- sessions: the live session view polls these (M07). The browser holds no economy logic — it reads
        //       the durable session files the CLI writes and re-sims config.current through /api/sim itself. ----

        var sessionsDir = Path.Combine(toolDir, "sessions");

        app.MapGet("/api/sessions", () =>
        {
            var names = Directory.Exists(sessionsDir)
                ? Directory.GetDirectories(sessionsDir)
                    .OrderByDescending(Directory.GetLastWriteTimeUtc)
                    .Select(Path.GetFileName)
                    .ToList()
                : new List<string?>();
            return Results.Content(JsonConvert.SerializeObject(names), "application/json");
        });

        // The active session's live state: goal, working config, and every iteration line so far. The loss
        // curve reads `journal`; the heatmap/time charts re-sim `current` via /api/sim as iterations land.
        app.MapGet("/api/session", (string name) =>
        {
            var clean = SanitizeName(name);
            var dir = Path.Combine(sessionsDir, clean);
            if (!Directory.Exists(dir))
                return Fail(StatusCodes.Status404NotFound, $"session '{clean}' does not exist.");
            var payload = new JObject
            {
                ["name"] = clean,
                ["goal"] = ReadJsonFile(Path.Combine(dir, "goal.json")),
                ["current"] = ReadJsonFile(Path.Combine(dir, "config.current.json")),
                ["journal"] = ReadJournalLines(Path.Combine(dir, "journal.jsonl")),
            };
            return Results.Content(payload.ToString(Formatting.None), "application/json");
        });

        // Turn our two expected fault kinds into clean JSON error bodies; everything else surfaces the stack.
        app.Use(async (ctx, next) =>
        {
            try { await next(); }
            catch (BadRequest bad)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync(JsonConvert.SerializeObject(new { error = bad.Message }));
            }
        });

        var url = $"http://localhost:{port}";
        Console.WriteLine($"VoidDay Balance workbench → {url}  (Ctrl-C to stop)");
        app.Run(url);
        return 0;
    }

    // ================= helpers =================

    static string VersionPath(string dir, string name) => Path.Combine(dir, name + ".json");

    // Version files match `read`'s output exactly (Indented + trailing newline) so a no-op save of
    // baseline leaves it byte-identical.
    static void WriteVersion(string path, JToken config) =>
        File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented) + "\n");

    static JToken ReadJsonFile(string path) =>
        File.Exists(path) ? JToken.Parse(File.ReadAllText(path)) : JValue.CreateNull();

    static JArray ReadJournalLines(string path)
    {
        var arr = new JArray();
        if (!File.Exists(path)) return arr;
        foreach (var line in File.ReadAllLines(path))
            if (!string.IsNullOrWhiteSpace(line)) arr.Add(JToken.Parse(line));
        return arr;
    }

    static async Task<string> ReadBody(HttpRequest req)
    {
        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    // Names become filenames. Refuse anything but a safe basename so a request can never escape versions/.
    static string SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new BadRequest("version name is required.");
        if (!name.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
            throw new BadRequest($"invalid version name '{name}': only letters, digits, '-' and '_' are allowed.");
        return name;
    }

    static IResult Fail(int status, string message) =>
        Results.Content(JsonConvert.SerializeObject(new { error = message }), "application/json", statusCode: status);

    sealed class BadRequest(string message) : Exception(message);
}
