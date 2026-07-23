using Newtonsoft.Json;
using VoidDay.Balance.Schema;

namespace VoidDay.Balance.Agent;

/// A patch is `[{"op":"set","path":"...","value":N}]` applied config→config, NEVER to Unity. Every op is
/// guardrailed before a single field moves; a violation aborts the whole patch (fail loud, fail whole — the
/// same contract as the M02 writer).
public static class Patch
{
    public sealed class PatchOp
    {
        public string Op = "set";
        public string Path = "";
        public double Value;
    }

    public sealed class PatchRejectedException(string message) : Exception(message);

    /// Apply ops to a deep copy and return it — the input config is never mutated. Atomic: any rejected op
    /// throws before the returned config is used, and nothing is persisted until the caller writes it.
    public static BalanceConfig Apply(BalanceConfig config, IReadOnlyList<PatchOp> ops, Bounds bounds)
    {
        var clone = JsonConvert.DeserializeObject<BalanceConfig>(JsonConvert.SerializeObject(config))!;
        foreach (var op in ops)
            ApplyOne(clone, op, bounds);
        return clone;
    }

    static void ApplyOne(BalanceConfig config, PatchOp op, Bounds bounds)
    {
        if (op.Op != "set")
            throw new PatchRejectedException($"unsupported op '{op.Op}' (only 'set' is supported)");

        // ★ The read-only rule. Reject the ENTIRE profile/* namespace by prefix, never a field allowlist:
        // optimality, gemPolicy, gemReserve, minSkipSeconds all "improve the simulated PLAYER instead of the
        // GAME", and the next profile field would be another instance of the same exploit.
        var norm = op.Path.Replace('\\', '/');
        if (norm == "profile" || norm.StartsWith("profile/") || norm.StartsWith("profile."))
            throw new PatchRejectedException(
                $"read-only: '{op.Path}' is in the profile/* namespace — the SIMULATED PLAYER, not the game. " +
                "patch may only change game balance; lowering loss by tuning the player is the exploit this rule forbids.");

        ConfigPath.Accessor accessor;
        try
        {
            accessor = ConfigPath.Resolve(config, op.Path);
        }
        catch (ConfigPath.PathException ex)
        {
            throw new PatchRejectedException($"unresolvable path '{op.Path}': {ex.Message}");
        }

        var (pattern, bound) = bounds.Lookup(op.Path)
                  ?? throw new PatchRejectedException(
                      $"no bound declared for '{op.Path}' in bounds.json — patch only moves knobs with a declared bound.");

        if (op.Value < bound.Min || op.Value > bound.Max)
            throw new PatchRejectedException(
                $"value {op.Value} out of bounds for '{op.Path}' (bound '{pattern}': min {bound.Min}, max {bound.Max}).");

        accessor.Set(op.Value);
    }
}
