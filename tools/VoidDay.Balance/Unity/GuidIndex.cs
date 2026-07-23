using System.Text.RegularExpressions;

namespace VoidDay.Balance.Unity;

/// guid → asset path, built by scanning every Assets/**/*.meta for its `guid:` line. Every
/// cross-asset reference in Unity is {fileID, guid, type}; this is what turns a reference into a
/// file to open. Fail loud: an unresolvable guid throws naming the guid.
public sealed class GuidIndex
{
    private static readonly Regex GuidLine = new(@"^guid:\s*([0-9a-fA-F]{32})\s*$", RegexOptions.Multiline);

    private readonly Dictionary<string, string> _guidToPath = new();
    private readonly string _projectRoot;

    public GuidIndex(string projectRoot)
    {
        _projectRoot = projectRoot;
        var assets = Path.Combine(projectRoot, "Assets");
        foreach (var meta in Directory.EnumerateFiles(assets, "*.meta", SearchOption.AllDirectories))
        {
            var match = GuidLine.Match(File.ReadAllText(meta));
            if (!match.Success) continue;
            var guid = match.Groups[1].Value;
            // The asset the .meta describes is the sibling file without the .meta suffix.
            var assetPath = meta[..^".meta".Length];
            _guidToPath[guid] = Path.GetRelativePath(projectRoot, assetPath).Replace('\\', '/');
        }
    }

    /// Project-relative path (e.g. "Assets/Data/SO/Station_Field.asset") for a guid, or throws.
    public string PathFor(string guid)
    {
        if (!_guidToPath.TryGetValue(guid, out var path))
            throw new InvalidOperationException(
                $"Unresolvable GUID '{guid}' — no .meta under Assets/ declares it. A referenced asset is missing or the reference is stale.");
        return path;
    }

    /// True if the guid resolves at all (used by the scene scanner to keep only station prefabs).
    public bool TryPathFor(string guid, out string path) => _guidToPath.TryGetValue(guid, out path!);

    public string AbsolutePath(string projectRelative) => Path.Combine(_projectRoot, projectRelative);
}
