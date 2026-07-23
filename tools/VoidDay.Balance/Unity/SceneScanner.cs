using System.Text.RegularExpressions;
using VoidDay.Balance.Schema;

namespace VoidDay.Balance.Unity;

/// Counts pre-placed stations in Farm.unity. A 16k-line multi-document scene gets no YAML parser —
/// a line-scan for `m_SourcePrefab` guids is enough and far more robust. Each prefab-instance's
/// source guid, if it resolves under Assets/Prefabs/Stations/, is mapped to its stationType through
/// the roster (StationSO.prefab points back at the prefab asset) and counted. Non-station prefab
/// instances (UI, decor) are simply skipped. Scanning rather than hardcoding means the count can't
/// go stale when someone places a fourth station in the scene.
public static class SceneScanner
{
    private static readonly Regex SourcePrefab =
        new(@"m_SourcePrefab:\s*\{fileID:\s*[0-9-]+,\s*guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);

    private const string StationPrefabDir = "Assets/Prefabs/Stations/";

    public static List<StartingStation> Scan(
        string scenePath, GuidIndex guids, Dictionary<string, string> prefabGuidToStationType)
    {
        var text = File.ReadAllText(guids.AbsolutePath(scenePath));
        var counts = new Dictionary<string, int>();

        foreach (Match m in SourcePrefab.Matches(text))
        {
            var guid = m.Groups[1].Value;
            if (!guids.TryPathFor(guid, out var path)) continue;                 // unresolved → not ours
            if (!path.Replace('\\', '/').StartsWith(StationPrefabDir)) continue;  // not a station prefab

            if (!prefabGuidToStationType.TryGetValue(guid, out var stationType))
                throw new InvalidOperationException(
                    $"{scenePath}: placed prefab '{path}' is under {StationPrefabDir} but no StationSO in the roster " +
                    $"references it (guid {guid}). Cannot identify its station type.");

            counts[stationType] = counts.GetValueOrDefault(stationType) + 1;
        }

        // Deterministic order — dictionary iteration order must never affect output.
        return counts
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new StartingStation { StationType = kv.Key, Count = kv.Value })
            .ToList();
    }
}
