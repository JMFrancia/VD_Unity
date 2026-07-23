using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using VoidDay.Balance.Schema;

namespace VoidDay.Balance.Agent;

/// The path grammar shared by `patch` and `sweep` — one string addresses one scalar knob in a BalanceConfig.
///
/// Grammar:
///   singleton:   "global.startingStorageCapacity"   → Global object, dot into a field
///   index-root:  "levels[0].xpThreshold"             → a root LIST field, indexed by position, then a member
///                "levels[0].grants[0].amount"         → the member path may itself index nested lists
///   collection:  "stations/field/buildCost"          → collection / id / member
///                "recipes/field.wheatGrow/duration"   → ids may contain dots; the '/' delimits id from member
///                "upgrades/silo.cap/tiers[0].effects[0].amount"  → member path may index lists
///
/// Ids are looked up by their identity field (StationType for stations, Id for the rest). Field names match
/// case-insensitively, so the camelCase of the JSON binds to the PascalCase C# field. The leaf must be a
/// numeric scalar (int/float/double) — that is the contract patch/sweep rely on.
public static class ConfigPath
{
    public sealed class PathException(string message) : Exception(message);

    /// A resolved handle to one scalar field on one object. Get/Set coerce through the field's real type.
    public sealed class Accessor(object owner, FieldInfo field)
    {
        public double Get() => Convert.ToDouble(field.GetValue(owner));

        public void Set(double value)
        {
            var t = field.FieldType;
            object boxed =
                t == typeof(int) ? (object)(int)Math.Round(value)
                : t == typeof(float) ? (object)(float)value
                : t == typeof(double) ? value
                : throw new PathException($"leaf field '{field.Name}' is {t.Name}, not a numeric scalar");
            field.SetValue(owner, boxed);
        }
    }

    public static Accessor Resolve(BalanceConfig config, string path)
    {
        var slash = path.Split('/');
        if (slash.Length == 1)
        {
            // Singleton "<root>.<field>...", or index-root "<root>[i].<field>..." (the level list is addressed
            // by position, not by an id like the slash collections). The root token carries the optional index.
            var dot = slash[0].Split('.', 2);
            if (dot.Length < 2)
                throw new PathException($"'{path}': a singleton path needs a field (e.g. global.startingStorageCapacity)");
            var (rootName, rootIndex) = ParseToken(dot[0]);
            object container = Field(config, rootName).GetValue(config)
                               ?? throw new PathException($"'{path}': '{rootName}' is null");
            if (rootIndex is int ri)
            {
                var list = (IList)container;
                if (ri < 0 || ri >= list.Count)
                    throw new PathException($"index [{ri}] out of range on '{rootName}' (count {list.Count})");
                container = list[ri]!;
            }
            return NavigateToLeaf(container, dot[1]);
        }

        if (slash.Length < 3)
            throw new PathException($"'{path}': a collection path is '<collection>/<id>/<member>'");

        object element = FindById(config, slash[0], slash[1]);
        string member = string.Join('/', slash[2..]);
        return NavigateToLeaf(element, member);
    }

    static object FindById(BalanceConfig config, string collection, string id)
    {
        object? found = collection.ToLowerInvariant() switch
        {
            "resources" => config.Resources.FirstOrDefault(r => r.Id == id),
            "recipes" => config.Recipes.FirstOrDefault(r => r.Id == id),
            "stations" => config.Stations.FirstOrDefault(s => s.StationType == id),
            "upgrades" => config.Upgrades.FirstOrDefault(u => u.Id == id),
            _ => throw new PathException($"unknown collection '{collection}' (resources|recipes|stations|upgrades)")
        };
        return found ?? throw new PathException($"no '{collection}' element with id '{id}'");
    }

    static Accessor NavigateToLeaf(object container, string memberPath)
    {
        var tokens = memberPath.Split('.');
        object cur = container;
        for (int i = 0; i < tokens.Length - 1; i++)
        {
            var (name, index) = ParseToken(tokens[i]);
            object val = Field(cur, name).GetValue(cur)
                         ?? throw new PathException($"'{name}' is null while walking '{memberPath}'");
            if (index is int idx)
            {
                var list = (IList)val;
                if (idx < 0 || idx >= list.Count)
                    throw new PathException($"index [{idx}] out of range on '{name}' (count {list.Count})");
                val = list[idx]!;
            }
            cur = val;
        }

        var (leafName, leafIndex) = ParseToken(tokens[^1]);
        if (leafIndex is not null)
            throw new PathException($"leaf '{tokens[^1]}' must be a scalar field, not an indexed element");
        return new Accessor(cur, Field(cur, leafName));
    }

    static (string name, int? index) ParseToken(string token)
    {
        var m = Regex.Match(token, @"^([A-Za-z0-9_]+)(?:\[(\d+)\])?$");
        if (!m.Success) throw new PathException($"malformed path token '{token}'");
        return (m.Groups[1].Value, m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : null);
    }

    static FieldInfo Field(object o, string name) =>
        o.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
        ?? throw new PathException($"no field '{name}' on {o.GetType().Name}");

    /// Bounds patterns, most-specific first: concrete indices become [*], then the id segment becomes *.
    /// A concrete patch path is matched against these so one declared bound covers a whole knob family.
    public static IEnumerable<string> BoundsCandidates(string path)
    {
        string wildIndex = Regex.Replace(path, @"\[\d+\]", "[*]");
        yield return wildIndex;
        var parts = wildIndex.Split('/');
        if (parts.Length >= 3)
        {
            parts[1] = "*";
            yield return string.Join('/', parts);
        }
    }
}
