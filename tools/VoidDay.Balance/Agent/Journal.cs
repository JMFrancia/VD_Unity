using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using VoidDay.Balance.Schema;

namespace VoidDay.Balance.Agent;

/// The flat global eval log: one JSON line per `eval` appended to runs.jsonl. This milestone keeps it flat and
/// global; M7 structures it into per-session directories. Every eval is recorded so a loop leaves a trail.
public static class Journal
{
    public sealed class RunRecord
    {
        public long Ts;
        public string Config = "";
        public string Goal = "";
        public string ConfigHash = "";               // SHA256[..12] of the (possibly patched) config actually simmed
        public List<Patch.PatchOp> Patch = new();    // ops applied before this eval, [] for a bare eval
        public double Loss;
        public Dictionary<string, double> Breakdown = new();  // "metric@scope" ⇒ contribution
    }

    public static string ConfigHash(BalanceConfig config)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(config)));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    static string Path(string projectRoot) =>
        System.IO.Path.Combine(projectRoot, "tools", "VoidDay.Balance", "runs.jsonl");

    public static void Append(string projectRoot, RunRecord record)
    {
        var line = JsonConvert.SerializeObject(record, Formatting.None);
        File.AppendAllText(Path(projectRoot), line + "\n");
    }

    public static List<RunRecord> ReadAll(string projectRoot)
    {
        var path = Path(projectRoot);
        if (!File.Exists(path)) return new();
        var records = new List<RunRecord>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            records.Add(JsonConvert.DeserializeObject<RunRecord>(line)!);
        }
        return records;
    }
}
