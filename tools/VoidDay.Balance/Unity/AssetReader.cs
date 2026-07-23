using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace VoidDay.Balance.Unity;

/// Reads one Unity `.asset` (a single MonoBehaviour document) into a raw DTO. Unity's YAML is not
/// standard-conformant — `%TAG` applies only to the first document, so YamlDotNet throws on a raw
/// file. The fix is a small preprocessor: drop `%YAML`/`%TAG`, rewrite the `--- !u!114 &…` header
/// to a bare `---`. The preprocessor is deliberately strict: any asset form it was not built for
/// (multi-document, a non-114 tag) throws naming the file rather than being silently mis-parsed.
public sealed class AssetReader
{
    private readonly GuidIndex _guids;
    private readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public AssetReader(GuidIndex guids) => _guids = guids;

    public GuidIndex Guids => _guids;

    /// Deserialize the MonoBehaviour body of the asset at a guid.
    public T ReadByGuid<T>(string guid) => Read<T>(_guids.PathFor(guid));

    /// Deserialize the MonoBehaviour body of the asset at a project-relative path.
    public T Read<T>(string projectRelativePath)
    {
        var abs = _guids.AbsolutePath(projectRelativePath);
        var raw = File.ReadAllText(abs);
        var body = Preprocess(raw, projectRelativePath);
        var doc = _deserializer.Deserialize<Dictionary<string, T>>(body);
        if (doc == null || !doc.TryGetValue("MonoBehaviour", out var value))
            throw new InvalidOperationException(
                $"{projectRelativePath}: no 'MonoBehaviour' root after preprocessing — unexpected asset form.");
        return value;
    }

    /// The five-line preprocessor. Fails loud on any structure it does not handle.
    public static string Preprocess(string raw, string pathForError)
    {
        var lines = raw.Replace("\r\n", "\n").Split('\n');

        var docCount = lines.Count(l => l.StartsWith("---"));
        if (docCount != 1)
            throw new InvalidOperationException(
                $"{pathForError}: expected exactly one YAML document (one '---'), found {docCount}. " +
                "The preprocessor handles only single MonoBehaviour assets; this needs a real multi-document reader.");

        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            if (line.StartsWith("%YAML")) continue;
            if (line.StartsWith("%TAG")) continue;
            if (line.StartsWith("---"))
            {
                if (!line.Contains("!u!114"))
                    throw new InvalidOperationException(
                        $"{pathForError}: document header '{line.Trim()}' is not a MonoBehaviour (!u!114). Unhandled asset form.");
                sb.Append("---\n");
                continue;
            }
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    /// Resolve an optional reference to a stationType via its StationSO. Null ref (fileID: 0) → null.
    public string? StationTypeOfRef(RawRef? reference)
    {
        if (reference?.guid == null) return null;
        var station = ReadByGuid<StationRaw>(reference.guid);
        return station.stationType;
    }

    /// Resolve an optional resource reference to its id. A present-but-unresolvable guid throws.
    public string ResourceIdOfRef(RawRef? reference, string context)
    {
        if (reference?.guid == null)
            throw new InvalidOperationException($"{context}: expected a resource reference but it is null.");
        var resource = ReadByGuid<ResourceRaw>(reference.guid);
        return resource.id;
    }
}
