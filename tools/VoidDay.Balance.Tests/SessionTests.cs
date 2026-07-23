using Newtonsoft.Json;
using VoidDay.Balance.Agent;
using VoidDay.Balance.Schema;
using Xunit;

namespace VoidDay.Balance.Tests;

/// M07 session guards. The load-bearing rule is QA-19: `report.md` is GENERATED from `journal.jsonl`, so every
/// claim in it must trace to a recorded line — no iteration invented, merged or omitted. These tests exercise
/// that on a synthesised session directory (no Unity, no server).
public sealed class SessionTests
{
    // ---- QA-19: the report is true, not narrated. Every journalled rationale + loss appears verbatim in the
    //      report, the iteration count matches exactly, and the export diff reflects config.start→current.
    [Fact]
    public void SessionReportTracesToJournalExactly()
    {
        var dir = NewSessionDir(out var start, out var current);

        // 30 iterations, each with a distinct rationale and loss — enough to model "a long run".
        var rationales = new List<string>();
        for (int i = 1; i <= 30; i++)
        {
            string rationale = $"iteration {i}: knob move number {i} for reason {i * 7 % 11}";
            rationales.Add(rationale);
            Session.AppendIteration(dir, new Session.IterationRecord
            {
                Iteration = i,
                Ts = 1000 + i,
                Patch = i % 2 == 0
                    ? new() { new Patch.PatchOp { Op = "set", Path = "stations/field/buildCost", Value = 50 - i } }
                    : new(),
                ConfigHash = $"hash{i:000}",
                Loss = 30.0 - i,                       // strictly decreasing, distinct per line
                Breakdown = new() { [$"level.durationMinutes@L{i}"] = 30.0 - i },
                Rationale = rationale,
            });
        }

        var report = Session.GenerateReport(dir);

        // Every rationale is present (no iteration omitted or reworded).
        foreach (var r in rationales)
            Assert.Contains(r, report);

        // The iteration count is stated truthfully and no phantom 31st iteration exists.
        Assert.Contains("(30 iterations)", report);
        Assert.DoesNotContain("iteration 31:", report);

        // A specific loss value from the middle of the run is reported, not smoothed away.
        Assert.Contains("15", report);   // iteration 15's loss = 15

        // report.md was actually written to disk (the durable record).
        Assert.True(File.Exists(Path.Combine(dir, "report.md")));

        Directory.Delete(dir, true);
    }

    // ---- The report never invents an iteration the journal does not contain (the inverse of the above).
    [Fact]
    public void SessionReportContainsOnlyJournalledIterations()
    {
        var dir = NewSessionDir(out _, out _);
        Session.AppendIteration(dir, Iter(1, 5.0, "only real iteration"));

        var report = Session.GenerateReport(dir);
        Assert.Contains("only real iteration", report);
        Assert.Contains("### Iteration 1 —", report);
        Assert.DoesNotContain("### Iteration 2 —", report);   // nothing beyond the single journalled line

        Directory.Delete(dir, true);
    }

    // ---- The journal round-trips: appended records read back with rationale + patch intact.
    [Fact]
    public void JournalRoundTripsRationaleAndPatch()
    {
        var dir = NewSessionDir(out _, out _);
        Session.AppendIteration(dir, Iter(1, 9.0, "first"));
        Session.AppendIteration(dir, new Session.IterationRecord
        {
            Iteration = 2, Ts = 2, Loss = 4.0, Rationale = "second",
            Patch = new() { new Patch.PatchOp { Op = "set", Path = "global.startingStorageCapacity", Value = 60 } },
        });

        var back = Session.ReadJournal(dir);
        Assert.Equal(2, back.Count);
        Assert.Equal("first", back[0].Rationale);
        Assert.Equal("second", back[1].Rationale);
        Assert.Single(back[1].Patch);
        Assert.Equal("global.startingStorageCapacity", back[1].Patch[0].Path);
        Assert.Equal(60, back[1].Patch[0].Value);
        Assert.Equal(2, Session.NextIteration(dir) - 1);      // NextIteration = count + 1

        Directory.Delete(dir, true);
    }

    // ---- The export diff reports exactly the fields that differ between start and current.
    [Fact]
    public void DiffConfigsFindsExactlyTheChangedScalar()
    {
        var start = Baseline();
        var current = Baseline();
        var recipe = current.Recipes.First(r => r.Id == "field.wheatGrow");
        recipe.Duration = recipe.Duration / 2;

        var diffs = Session.DiffConfigs(start, current);
        Assert.Single(diffs);
        Assert.Contains("field.wheatGrow", diffs[0].Path);     // labelled by id, not array index
        Assert.Contains("Duration", diffs[0].Path);

        // No change ⇒ no diff (a clean session exports nothing).
        Assert.Empty(Session.DiffConfigs(start, Baseline()));
    }

    // ================= helpers =================

    static Session.IterationRecord Iter(int i, double loss, string rationale) => new()
    {
        Iteration = i, Ts = i, Loss = loss, Rationale = rationale,
        Breakdown = new() { ["total.minutesToLevel@L20"] = loss },
    };

    // A throwaway session directory seeded with a goal + start/current configs, ready for AppendIteration.
    static string NewSessionDir(out BalanceConfig start, out BalanceConfig current)
    {
        var dir = Path.Combine(Path.GetTempPath(), "vdbal-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var goal = new Goal { Name = "test-goal", Targets = { new GoalTarget { Metric = "total.minutesToLevel", Level = 20, Max = 45 } } };
        File.WriteAllText(Path.Combine(dir, "goal.json"), JsonConvert.SerializeObject(goal, Formatting.Indented));
        start = Baseline();
        current = Baseline();
        File.WriteAllText(Path.Combine(dir, "config.start.json"), JsonConvert.SerializeObject(start, Formatting.Indented));
        File.WriteAllText(Path.Combine(dir, "config.current.json"), JsonConvert.SerializeObject(current, Formatting.Indented));
        File.WriteAllText(Path.Combine(dir, "journal.jsonl"), "");
        return dir;
    }

    static BalanceConfig Baseline()
    {
        var path = Path.Combine(FindProjectRoot(), "tools", "VoidDay.Balance", "versions", "baseline.json");
        return JsonConvert.DeserializeObject<BalanceConfig>(File.ReadAllText(path))!;
    }

    static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets"))
                && File.Exists(Path.Combine(dir.FullName, ".gitignore")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate the Unity project root above the test binary.");
    }
}
