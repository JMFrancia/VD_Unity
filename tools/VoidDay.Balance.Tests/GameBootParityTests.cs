using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace VoidDay.Balance.Tests;

/// The staleness canary (spec: GameBootParityCanary). CoreHarness mirrors GameBoot.Start()'s object-graph
/// wiring by hand — the one thing agnosticism costs. This test hashes GameBoot.cs and fails when it moves, so
/// silent drift between the game's boot and the harness becomes a loud, actionable failure.
///
/// ── Reconciled against GameBoot.cs @ commit bde1702 (last commit to touch it), 2026-07-23. ──
///    (bde1702's only wiring change was JobSystem gaining `() => progression.PlayerLevel`, already mirrored
///     in CoreHarness; Feature A left the hash stale — re-stamped here after confirming parity.)
/// When this fails: re-read Assets/Systems/Boot/GameBoot.cs, reconcile Sim/CoreHarness.cs to any changed
/// wiring (order and the Systems bridge — ProgressionSystem XP + UpgradesSystem register-on-build), then
/// update the expected hash below to the new value the failure prints.
public sealed class GameBootParityTests
{
    // Normalized SHA256 (CRLF/CR → LF, UTF-8) of Assets/Systems/Boot/GameBoot.cs at reconciliation time.
    const string ExpectedHash = "b52bb3d23f30a8ea1000b41410a7d4d70975a09d7477f10b832fd638e42aecb2";

    [Fact]
    public void GameBootHasNotChangedSinceCoreHarnessReconciled()
    {
        var path = Path.Combine(FindProjectRoot(), "Assets", "Systems", "Boot", "GameBoot.cs");
        Assert.True(File.Exists(path), $"GameBoot.cs not found at {path}");

        string actual = Hash(File.ReadAllText(path));
        Assert.True(actual == ExpectedHash,
            "GameBoot.cs changed since CoreHarness was last reconciled. Re-reconcile Sim/CoreHarness.cs " +
            $"against Assets/Systems/Boot/GameBoot.cs, then set ExpectedHash to: {actual}");
    }

    static string Hash(string text)
    {
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
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
