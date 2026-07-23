using Newtonsoft.Json;

namespace VoidDay.Balance.Agent;

/// The structural guardrail on `patch`: bounds.json declares which knobs are movable and their legal range.
/// A patch whose path has no declared bound, or whose value falls outside it, is rejected — the guardrail is
/// enforced by the tool, not by asking an agent nicely (spec: "Guardrails are structural, not advisory").
public sealed class Bounds
{
    public sealed class Bound
    {
        public double Min;
        public double Max;
        public double Step;
    }

    readonly Dictionary<string, Bound> _map;

    Bounds(Dictionary<string, Bound> map) => _map = map;

    public static Bounds Load(string projectRoot)
    {
        var path = Path.Combine(projectRoot, "tools", "VoidDay.Balance", "bounds.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"bounds.json not found at {path} — patch has no guardrail without it.");
        var map = JsonConvert.DeserializeObject<Dictionary<string, Bound>>(File.ReadAllText(path))
                  ?? throw new InvalidOperationException($"{path}: parsed to null.");
        return new Bounds(map);
    }

    /// The most-specific declared bound for a concrete path, or null if the knob is not declared movable.
    public (string pattern, Bound bound)? Lookup(string path)
    {
        foreach (var candidate in ConfigPath.BoundsCandidates(path))
            if (_map.TryGetValue(candidate, out var b))
                return (candidate, b);
        return null;
    }
}
