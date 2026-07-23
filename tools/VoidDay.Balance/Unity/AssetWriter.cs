using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using VoidDay.Balance.Schema;

namespace VoidDay.Balance.Unity;

/// Writes an edited BalanceConfig back into the Unity `.asset` files it came from — surgically.
///
/// The one rule that governs everything here: **never reserialize.** A one-field change must produce
/// a one-line `git diff`. So the writer resolves each changed value to the exact line in the exact
/// asset and replaces only the scalar after the colon; every other byte is left untouched. A
/// YamlDotNet round-trip would reformat whole files and make the diff unreviewable — that is the
/// milestone's whole point (minimal diff is a correctness property, not a nicety).
///
/// It supports two operations: editing line-addressable top-level scalars, and inserting a new
/// RecipeSO (asset + .meta + a reference appended to its owning StationSO). Anything it cannot do
/// surgically — nested-collection edits, deletions, creating resources or stations — it **refuses
/// loudly before writing a single byte**, rather than silently dropping the edit. Fail loud, fail
/// early, fail whole.
public sealed class AssetWriter
{
    private const string RecipeScriptFallbackGuid = "58c7e8d127a744f82866546ee5265bf2"; // RecipeSO
    private static readonly Regex GuidLine = new(@"^guid:\s*([0-9a-fA-F]{32})", RegexOptions.Multiline);

    private readonly string _projectRoot;
    private readonly EconomyReader _reader;
    private readonly BalanceConfig _current;
    private readonly GuidIndex _guids;

    public AssetWriter(string projectRoot, EconomyReader reader, BalanceConfig current)
    {
        _projectRoot = projectRoot;
        _reader = reader;
        _current = current;
        _guids = reader.Guids;
    }

    // ---- Plan: validate everything, diff, and describe the changes. Writes nothing. ----

    public WritePlan Plan(BalanceConfig incoming)
    {
        if (incoming.SchemaVersion != BalanceConfig.CurrentSchemaVersion)
            throw new WriteRefusedException(
                $"schemaVersion mismatch: config declares {incoming.SchemaVersion}, tool supports {BalanceConfig.CurrentSchemaVersion}. Aborting; nothing written.");

        var plan = new WritePlan();

        DiffGlobal(incoming, plan);
        DiffXp(incoming, plan);
        DiffOrders(incoming, plan);
        DiffResources(incoming, plan);
        DiffRecipes(incoming, plan);   // finds insertions
        DiffStations(incoming, plan);  // needs insertions resolved first (recipe wiring)
        DiffUpgrades(incoming, plan);  // unlockLevel only; tiers/effects still refuse loud
        DiffLevels(incoming, plan);    // xpThreshold + grant amounts, addressed positionally

        return plan;
    }

    // ---- Apply: perform the planned edits. Only reached with --apply. ----

    public void Apply(WritePlan plan)
    {
        foreach (var change in plan.Scalars)
            SetScalar(change);

        foreach (var insert in plan.RecipeInsertions)
            InsertRecipe(insert);

        if (plan.LevelEdits.Count > 0)
            ApplyLevelEdits(plan.LevelEdits);
    }

    // ================= Diffing =================

    private void DiffGlobal(BalanceConfig inc, WritePlan plan)
    {
        var cur = _current.Global;
        var g = inc.Global;
        var path = EconomyReader.GameConfigPath;
        Scalar(plan, path, "gridCols", cur.GridCols, g.GridCols);
        Scalar(plan, path, "gridRows", cur.GridRows, g.GridRows);
        Scalar(plan, path, "cellSize", cur.CellSize, g.CellSize);
        Scalar(plan, path, "refundPercent", cur.RefundPercent, g.RefundPercent);
        Scalar(plan, path, "startingStorageCapacity", cur.StartingStorageCapacity, g.StartingStorageCapacity);
        // Gems are authored as top-level fields on GameConfig.asset (the schema groups them separately).
        Scalar(plan, path, "startingGems", _current.Gems.StartingGems, inc.Gems.StartingGems);
        Scalar(plan, path, "secondsPerGem", _current.Gems.SecondsPerGem, inc.Gems.SecondsPerGem);
        Scalar(plan, path, "minGemCost", _current.Gems.MinGemCost, inc.Gems.MinGemCost);

        if (!SameResourceQuantities(cur.StartingResources, g.StartingResources))
            throw Unsupported("Global.StartingResources", "editing the starting-resource list");
        if (!SameStartingStations(cur.StartingStations, g.StartingStations))
            throw Unsupported("Global.StartingStations", "these are scene-owned (Farm.unity), not tool-editable");
    }

    private void DiffXp(BalanceConfig inc, WritePlan plan)
    {
        var path = _reader.XpConfigPath;
        Scalar(plan, path, "perJobCollected", _current.Xp.PerJobCollected, inc.Xp.PerJobCollected);
        // perStationBuilt is absent from the asset (SO field initializer); allow appending it.
        Scalar(plan, path, "perStationBuilt", _current.Xp.PerStationBuilt, inc.Xp.PerStationBuilt, absentAllowed: true);
    }

    private void DiffOrders(BalanceConfig inc, WritePlan plan)
    {
        var path = _reader.OrderConfigPath;
        var c = _current.Orders;
        var o = inc.Orders;
        Scalar(plan, path, "slotCount", c.SlotCount, o.SlotCount);
        Scalar(plan, path, "refillSeconds", c.RefillSeconds, o.RefillSeconds);
        Scalar(plan, path, "minRequestKinds", c.MinRequestKinds, o.MinRequestKinds);
        Scalar(plan, path, "maxRequestKinds", c.MaxRequestKinds, o.MaxRequestKinds);
        Scalar(plan, path, "maxQuantityAtLevel1", c.MaxQuantityAtLevel1, o.MaxQuantityAtLevel1);
        Scalar(plan, path, "maxQuantityPerLevel", c.MaxQuantityPerLevel, o.MaxQuantityPerLevel);
        Scalar(plan, path, "tierWeightBase", c.TierWeightBase, o.TierWeightBase);
        Scalar(plan, path, "tierWeightPerLevel", c.TierWeightPerLevel, o.TierWeightPerLevel);
        Scalar(plan, path, "cashMultiplier", c.CashMultiplier, o.CashMultiplier);
        Scalar(plan, path, "xpMultiplier", c.XpMultiplier, o.XpMultiplier);
    }

    private void DiffResources(BalanceConfig inc, WritePlan plan)
    {
        var currentById = _current.Resources.ToDictionary(r => r.Id);
        var incomingIds = new HashSet<string>();

        foreach (var r in inc.Resources)
        {
            incomingIds.Add(r.Id);
            if (!currentById.TryGetValue(r.Id, out var cur))
                throw new WriteRefusedException(
                    $"resource id '{r.Id}' matches no asset. Creating resources is out of scope (they need icons/prefabs authored in Unity). Aborting; nothing written.");
            if (r.DisplayName != cur.DisplayName)
                throw Unsupported($"resource '{r.Id}'", "changing a resource displayName");

            var path = ResourcePath(r.Id);
            Scalar(plan, path, "baseValue", cur.BaseValue, r.BaseValue);
            Scalar(plan, path, "sellable", cur.Sellable, r.Sellable);
            Scalar(plan, path, "tier", cur.Tier, r.Tier);
        }

        foreach (var cur in _current.Resources)
            if (!incomingIds.Contains(cur.Id))
                throw Unsupported($"resource '{cur.Id}'", "deleting a resource");
    }

    private void DiffRecipes(BalanceConfig inc, WritePlan plan)
    {
        var currentById = _current.Recipes.ToDictionary(r => r.Id);
        var stationTypes = _current.Stations.Select(s => s.StationType).ToHashSet();
        var resourceIds = _current.Resources.Select(r => r.Id).ToHashSet();
        var incomingIds = new HashSet<string>();

        foreach (var r in inc.Recipes)
        {
            incomingIds.Add(r.Id);
            if (currentById.TryGetValue(r.Id, out var cur))
            {
                if (r.StationType != cur.StationType)
                    throw Unsupported($"recipe '{r.Id}'", "re-homing a recipe to a different station");
                if (!SameResourceQuantities(cur.Inputs, r.Inputs) || !SameResourceQuantities(cur.Outputs, r.Outputs))
                    throw Unsupported($"recipe '{r.Id}'", "editing recipe inputs/outputs");
                Scalar(plan, RecipePath(r.Id), "duration", cur.Duration, r.Duration);
                // unlockLevel post-dates most recipe assets (added by Feature A) — append it when absent.
                Scalar(plan, RecipePath(r.Id), "unlockLevel", cur.UnlockLevel, r.UnlockLevel, absentAllowed: true);
            }
            else
            {
                // New recipe → structural insertion. Validate its references exist first.
                if (!stationTypes.Contains(r.StationType))
                    throw new WriteRefusedException(
                        $"new recipe '{r.Id}' targets station '{r.StationType}', which matches no asset. Aborting; nothing written.");
                foreach (var q in r.Inputs.Concat(r.Outputs))
                    if (!resourceIds.Contains(q.Resource))
                        throw new WriteRefusedException(
                            $"new recipe '{r.Id}' references resource '{q.Resource}', which matches no asset. Aborting; nothing written.");
                plan.RecipeInsertions.Add(new RecipeInsertion(r));
            }
        }

        foreach (var cur in _current.Recipes)
            if (!incomingIds.Contains(cur.Id))
                throw Unsupported($"recipe '{cur.Id}'", "deleting a recipe");
    }

    private void DiffStations(BalanceConfig inc, WritePlan plan)
    {
        var currentByType = _current.Stations.ToDictionary(s => s.StationType);
        var insertedByStation = plan.RecipeInsertions
            .GroupBy(i => i.Recipe.StationType)
            .ToDictionary(g => g.Key, g => g.Select(i => i.Recipe.Id).ToHashSet());

        foreach (var s in inc.Stations)
        {
            if (!currentByType.TryGetValue(s.StationType, out var cur))
                throw new WriteRefusedException(
                    $"station '{s.StationType}' matches no asset. Creating stations is out of scope. Aborting; nothing written.");
            if (s.DisplayName != cur.DisplayName)
                throw Unsupported($"station '{s.StationType}'", "changing a station displayName");

            var path = StationPath(s.StationType);
            Scalar(plan, path, "buildable", cur.Buildable, s.Buildable);
            Scalar(plan, path, "buildCost", cur.BuildCost, s.BuildCost);
            Scalar(plan, path, "cap", cur.Cap, s.Cap);
            Scalar(plan, path, "unlockLevel", cur.UnlockLevel, s.UnlockLevel);
            Scalar(plan, path, "queueDepth", cur.QueueDepth, s.QueueDepth);
            Scalar(plan, path, "width", cur.Width, s.Width);
            Scalar(plan, path, "height", cur.Height, s.Height);
            Scalar(plan, path, "buildSeconds", cur.BuildSeconds, s.BuildSeconds, absentAllowed: true);

            var allowedExtra = insertedByStation.GetValueOrDefault(s.StationType) ?? new HashSet<string>();
            VerifyIdListDelta(s.StationType, "recipes", cur.RecipeIds, s.RecipeIds, allowedExtra);
            VerifyIdListDelta(s.StationType, "upgrades", cur.UpgradeIds, s.UpgradeIds, new HashSet<string>());
        }

        foreach (var cur in _current.Stations)
            if (inc.Stations.All(s => s.StationType != cur.StationType))
                throw Unsupported($"station '{cur.StationType}'", "deleting a station");
    }

    // The only permitted difference between the current and incoming id list is the appearance of a
    // recipe this run is inserting. A removal, or any other addition, is an unsupported rewire.
    private void VerifyIdListDelta(string stationType, string field,
        List<string> current, List<string> incoming, HashSet<string> allowedExtra)
    {
        var cur = current.ToHashSet();
        var inc = incoming.ToHashSet();
        foreach (var removed in cur.Where(id => !inc.Contains(id)))
            throw Unsupported($"station '{stationType}'", $"removing '{removed}' from {field}");
        foreach (var added in inc.Where(id => !cur.Contains(id)))
            if (!allowedExtra.Contains(added))
                throw Unsupported($"station '{stationType}'",
                    $"adding '{added}' to {field} (not a recipe being inserted this run)");
    }

    // Upgrade unlockLevel is a top-level scalar on the UpgradeSO — line-addressable like any other. Tiers and
    // effects have no surgical write path, so any change to them is still refused loud.
    private void DiffUpgrades(BalanceConfig inc, WritePlan plan)
    {
        var currentById = _current.Upgrades.ToDictionary(u => u.Id);
        var incomingIds = new HashSet<string>();

        foreach (var u in inc.Upgrades)
        {
            incomingIds.Add(u.Id);
            if (!currentById.TryGetValue(u.Id, out var cur))
                throw new WriteRefusedException(
                    $"upgrade id '{u.Id}' matches no asset. Creating upgrades is out of scope. Aborting; nothing written.");
            if (u.DisplayName != cur.DisplayName)
                throw Unsupported($"upgrade '{u.Id}'", "changing an upgrade displayName");
            Scalar(plan, UpgradePath(u.Id), "unlockLevel", cur.UnlockLevel, u.UnlockLevel, absentAllowed: true);
            if (!SameTiers(cur.Tiers, u.Tiers))
                throw Unsupported($"upgrade '{u.Id}'", "editing upgrade tiers or effects");
        }

        foreach (var cur in _current.Upgrades)
            if (!incomingIds.Contains(cur.Id))
                throw Unsupported($"upgrade '{cur.Id}'", "deleting an upgrade");
    }

    // The Levels asset is a single SO holding a list of level entries, each with a nested grant list. Only two
    // scalars are movable — a level's xpThreshold and a grant's amount — and they are addressed positionally
    // (Feature B). Any structural change (a level or grant added/removed, a grant's kind or target retargeted)
    // has no surgical path and is refused loud. LevelEdits are applied by ApplyLevelEdits, not SetScalar.
    private void DiffLevels(BalanceConfig inc, WritePlan plan)
    {
        if (inc.Levels.Count != _current.Levels.Count)
            throw Unsupported("Levels", "adding or removing a level");

        for (int i = 0; i < inc.Levels.Count; i++)
        {
            var cur = _current.Levels[i];
            var lv = inc.Levels[i];

            if (cur.XpThreshold != lv.XpThreshold)
                plan.LevelEdits.Add(new LevelEdit(i, null,
                    cur.XpThreshold.ToString(CultureInfo.InvariantCulture),
                    lv.XpThreshold.ToString(CultureInfo.InvariantCulture)));

            if (cur.Grants.Count != lv.Grants.Count)
                throw Unsupported($"level {i + 1}", "adding or removing a grant");

            for (int j = 0; j < cur.Grants.Count; j++)
            {
                var g0 = cur.Grants[j];
                var g1 = lv.Grants[j];
                if (g0.Kind != g1.Kind || g0.TargetStation != g1.TargetStation)
                    throw Unsupported($"level {i + 1} grant {j + 1}", "changing a grant's kind or target station");
                if (g0.Amount != g1.Amount)
                    plan.LevelEdits.Add(new LevelEdit(i, j,
                        g0.Amount.ToString(CultureInfo.InvariantCulture),
                        g1.Amount.ToString(CultureInfo.InvariantCulture)));
            }
        }
    }

    // ================= Structural insertion =================

    private void InsertRecipe(RecipeInsertion insert)
    {
        var r = insert.Recipe;
        var guid = Guid.NewGuid().ToString("N");
        var name = "Recipe_" + r.Id.Replace('.', '_');
        var relPath = $"Assets/Data/SO/{name}.asset";
        var absAsset = _guids.AbsolutePath(relPath);
        if (File.Exists(absAsset))
            throw new WriteRefusedException($"cannot create '{relPath}': a file already exists there.");

        var scriptGuid = ResolveRecipeScriptGuid(r.StationType);
        File.WriteAllText(absAsset, BuildRecipeAsset(name, scriptGuid, r));
        File.WriteAllText(absAsset + ".meta", BuildMeta(guid));

        AppendRecipeReference(StationPath(r.StationType), guid);
    }

    private string ResolveRecipeScriptGuid(string stationType)
    {
        // Steal the RecipeSO script guid from an existing recipe so it can never drift from the
        // project. Prefer one on the same station; fall back to any recipe, then the known guid.
        var station = _current.Stations.First(s => s.StationType == stationType);
        var sampleId = station.RecipeIds.FirstOrDefault() ?? _current.Recipes.FirstOrDefault()?.Id;
        if (sampleId == null) return RecipeScriptFallbackGuid;

        var text = File.ReadAllText(_guids.AbsolutePath(RecipePath(sampleId)));
        var m = Regex.Match(text, @"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-fA-F]{32})");
        return m.Success ? m.Groups[1].Value : RecipeScriptFallbackGuid;
    }

    private string BuildRecipeAsset(string name, string scriptGuid, RecipeConfig r)
    {
        var sb = new StringBuilder();
        sb.Append("%YAML 1.1\n");
        sb.Append("%TAG !u! tag:unity3d.com,2011:\n");
        sb.Append("--- !u!114 &11400000\n");
        sb.Append("MonoBehaviour:\n");
        sb.Append("  m_ObjectHideFlags: 0\n");
        sb.Append("  m_CorrespondingSourceObject: {fileID: 0}\n");
        sb.Append("  m_PrefabInstance: {fileID: 0}\n");
        sb.Append("  m_PrefabAsset: {fileID: 0}\n");
        sb.Append("  m_GameObject: {fileID: 0}\n");
        sb.Append("  m_Enabled: 1\n");
        sb.Append("  m_EditorHideFlags: 0\n");
        sb.Append($"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}\n");
        sb.Append($"  m_Name: {name}\n");
        sb.Append("  m_EditorClassIdentifier: Assembly-CSharp::VoidDay.Data.RecipeSO\n");
        sb.Append($"  id: {r.Id}\n");
        sb.Append($"  stationType: {r.StationType}\n");
        AppendIngredientBlock(sb, "inputs", r.Inputs);
        AppendIngredientBlock(sb, "outputs", r.Outputs);
        sb.Append($"  duration: {FormatFloat(r.Duration)}\n");
        // Omit when it equals the SO initializer (1), exactly as a hand-authored asset does.
        if (r.UnlockLevel != 1) sb.Append($"  unlockLevel: {r.UnlockLevel}\n");
        return sb.ToString();
    }

    private void AppendIngredientBlock(StringBuilder sb, string field, List<ResourceQuantity> items)
    {
        if (items.Count == 0)
        {
            sb.Append($"  {field}: []\n");
            return;
        }
        sb.Append($"  {field}:\n");
        foreach (var q in items)
        {
            var guid = _reader.ResourceGuidById[q.Resource];
            sb.Append($"  - resource: {{fileID: 11400000, guid: {guid}, type: 2}}\n");
            sb.Append($"    amount: {q.Amount}\n");
        }
    }

    private static string BuildMeta(string guid) =>
        "fileFormatVersion: 2\n" +
        $"guid: {guid}\n" +
        "NativeFormatImporter:\n" +
        "  externalObjects: {}\n" +
        "  mainObjectFileID: 11400000\n" +
        "  userData: \n" +
        "  assetBundleName: \n" +
        "  assetBundleVariant: \n";

    // Append `- {fileID: ..., guid: NEW, type: 2}` after the last item of the station's recipes list.
    private void AppendRecipeReference(string stationRelPath, string newGuid)
    {
        var abs = _guids.AbsolutePath(stationRelPath);
        var lines = File.ReadAllText(abs).Replace("\r\n", "\n").Split('\n').ToList();

        var recipesLine = lines.FindIndex(l => l == "  recipes:");
        if (recipesLine < 0)
            throw new WriteRefusedException(
                $"{stationRelPath}: no block-form 'recipes:' list to append to (an empty '[]' list is not supported).");

        // Walk to the end of the contiguous list items that follow.
        var insertAt = recipesLine + 1;
        while (insertAt < lines.Count && lines[insertAt].StartsWith("  - "))
            insertAt++;

        lines.Insert(insertAt, $"  - {{fileID: 11400000, guid: {newGuid}, type: 2}}");
        File.WriteAllText(abs, string.Join('\n', lines));
    }

    // ================= Positional level editing =================

    // The Levels asset has a fixed, machine-authored shape: a level entry opens with a `  - xpThreshold:` line
    // (2-space list item), and each grant opens with a `    - kind:` line (4-space) carrying an `      amount:`
    // line below it. We locate each target by counting those markers in document order and replace only the
    // scalar after the colon — no reserialize, so a one-field edit is a one-line diff, same as SetScalar.
    private void ApplyLevelEdits(List<LevelEdit> edits)
    {
        var abs = _guids.AbsolutePath(_reader.LevelsPath);
        var lines = File.ReadAllText(abs).Replace("\r\n", "\n").Split('\n').ToList();

        foreach (var edit in edits)
        {
            int lineNo = edit.GrantIndex is int gi
                ? FindGrantAmountLine(lines, edit.LevelIndex, gi)
                : FindLevelXpLine(lines, edit.LevelIndex);
            ReplaceScalarValue(lines, lineNo, edit.Old, edit.New);
        }

        File.WriteAllText(abs, string.Join('\n', lines));
    }

    // The line index of the (levelIndex)-th `  - xpThreshold:` marker.
    private int FindLevelXpLine(List<string> lines, int levelIndex)
    {
        int seen = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith("  - xpThreshold:")) continue;
            if (seen == levelIndex) return i;
            seen++;
        }
        throw new WriteRefusedException(
            $"{_reader.LevelsPath}: could not locate level {levelIndex + 1}'s xpThreshold line. Refusing to guess.");
    }

    // The `      amount:` line of the (grantIndex)-th grant inside the (levelIndex)-th level.
    private int FindGrantAmountLine(List<string> lines, int levelIndex, int grantIndex)
    {
        int start = FindLevelXpLine(lines, levelIndex) + 1;
        int grantSeen = 0;
        for (int i = start; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("  - xpThreshold:")) break; // next level — grant not found
            if (!lines[i].StartsWith("    - kind:")) continue;
            if (grantSeen == grantIndex)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (lines[j].StartsWith("      amount:")) return j;
                    if (lines[j].StartsWith("    - ") || lines[j].StartsWith("  - ")) break; // ran past the grant
                }
                break;
            }
            grantSeen++;
        }
        throw new WriteRefusedException(
            $"{_reader.LevelsPath}: could not locate level {levelIndex + 1} grant {grantIndex + 1}'s amount line. Refusing to guess.");
    }

    // Swap the value after `key:` on one line, asserting it currently reads `expectedOld` — a drift guard so a
    // stale plan never silently rewrites the wrong number.
    private void ReplaceScalarValue(List<string> lines, int lineNo, string expectedOld, string newValue)
    {
        var line = lines[lineNo];
        int colon = line.IndexOf(':');
        string current = line[(colon + 1)..].Trim();
        if (current != expectedOld)
            throw new WriteRefusedException(
                $"{_reader.LevelsPath}: line {lineNo + 1} reads '{current}', expected '{expectedOld}'. The asset changed under the plan; refusing to write.");
        lines[lineNo] = line[..(colon + 1)] + " " + newValue;
    }

    // ================= Scalar line editing =================

    private void Scalar(WritePlan plan, string assetPath, string field, int oldV, int newV)
    {
        if (oldV != newV)
            plan.Scalars.Add(new ScalarChange(assetPath, field, oldV.ToString(CultureInfo.InvariantCulture),
                newV.ToString(CultureInfo.InvariantCulture), false));
    }

    private void Scalar(WritePlan plan, string assetPath, string field, bool oldV, bool newV)
    {
        if (oldV != newV)
            plan.Scalars.Add(new ScalarChange(assetPath, field, oldV ? "1" : "0", newV ? "1" : "0", false));
    }

    private void Scalar(WritePlan plan, string assetPath, string field, float oldV, float newV, bool absentAllowed = false)
    {
        if (oldV != newV)
            plan.Scalars.Add(new ScalarChange(assetPath, field, FormatFloat(oldV), FormatFloat(newV), absentAllowed));
    }

    private void Scalar(WritePlan plan, string assetPath, string field, int oldV, int newV, bool absentAllowed)
    {
        if (oldV != newV)
            plan.Scalars.Add(new ScalarChange(assetPath, field, oldV.ToString(CultureInfo.InvariantCulture),
                newV.ToString(CultureInfo.InvariantCulture), absentAllowed));
    }

    private void SetScalar(ScalarChange change)
    {
        var abs = _guids.AbsolutePath(change.AssetPath);
        var lines = File.ReadAllText(abs).Replace("\r\n", "\n").Split('\n').ToList();

        // Top-level MonoBehaviour fields are indented exactly two spaces. Match on that to avoid
        // colliding with a nested field of the same name.
        var prefix = "  " + change.Field + ":";
        var matches = lines.Select((l, i) => (l, i)).Where(t => t.l.StartsWith(prefix)).ToList();

        if (matches.Count == 1)
        {
            lines[matches[0].i] = $"  {change.Field}: {change.New}";
        }
        else if (matches.Count == 0 && change.AbsentAllowed)
        {
            // Field absent from the asset (it post-dates it; the game applies the SO field
            // initializer). Unity reads SO fields by name, order-independent, so appending one line
            // at the end of the document body is a valid, minimal, one-line diff.
            InsertBeforeTrailingBlank(lines, $"  {change.Field}: {change.New}");
        }
        else
        {
            throw new WriteRefusedException(
                $"{change.AssetPath}: expected exactly one top-level '{change.Field}:' line, found {matches.Count}. Refusing to guess.");
        }

        File.WriteAllText(abs, string.Join('\n', lines));
    }

    private static void InsertBeforeTrailingBlank(List<string> lines, string newLine)
    {
        // Files end with a trailing newline, so Split leaves a final empty element. Insert before it.
        var at = lines.Count;
        while (at > 0 && lines[at - 1].Length == 0) at--;
        lines.Insert(at, newLine);
    }

    // ================= Path resolution =================

    private string RecipePath(string id) => _guids.PathFor(_reader.RecipeGuidById[id]);
    private string ResourcePath(string id) => _guids.PathFor(_reader.ResourceGuidById[id]);
    private string StationPath(string type) => _guids.PathFor(_reader.StationGuidByType[type]);
    private string UpgradePath(string id) => _guids.PathFor(_reader.UpgradeGuidById[id]);

    // ================= Equality helpers (order-sensitive for lists, as authored) =================

    private static bool SameResourceQuantities(List<ResourceQuantity> a, List<ResourceQuantity> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (a[i].Resource != b[i].Resource || a[i].Amount != b[i].Amount) return false;
        return true;
    }

    private static bool SameStartingStations(List<StartingStation> a, List<StartingStation> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (a[i].StationType != b[i].StationType || a[i].Count != b[i].Count) return false;
        return true;
    }

    // Tier structure (cost + effects) only — unlockLevel is diffed separately as a movable scalar, so it is
    // deliberately NOT compared here.
    private static bool SameTiers(List<UpgradeTierConfig> a, List<UpgradeTierConfig> b)
    {
        if (a.Count != b.Count) return false;
        for (var t = 0; t < a.Count; t++)
        {
            if (a[t].Cost != b[t].Cost) return false;
            if (!SameEffects(a[t].Effects, b[t].Effects)) return false;
        }
        return true;
    }

    private static bool SameEffects(List<EffectConfig> a, List<EffectConfig> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            var e1 = a[i];
            var e2 = b[i];
            if (e1.Type != e2.Type || e1.Op != e2.Op || e1.Amount != e2.Amount || e1.Resource != e2.Resource ||
                e1.Range != e2.Range || e1.Trigger != e2.Trigger || e1.TriggerChance != e2.TriggerChance ||
                e1.ConditionType != e2.ConditionType || e1.ConditionArg != e2.ConditionArg ||
                e1.ConditionAmount != e2.ConditionAmount)
                return false;
        }
        return true;
    }

    // Unity writes whole floats without a decimal (`cellSize: 1`, `secondsPerGem: 30`) and others as
    // their shortest round-trippable form (`0.25`, `1.5`). Match that so an edited value reads back
    // identically and a diff shows exactly what changed.
    private static string FormatFloat(float f)
    {
        if (float.IsFinite(f) && MathF.Floor(f) == f)
            return ((long)f).ToString(CultureInfo.InvariantCulture);
        return f.ToString("R", CultureInfo.InvariantCulture);
    }

    private static WriteRefusedException Unsupported(string where, string what) =>
        new($"{where}: {what} is not supported by the M2 writer (no surgical write path). Aborting; nothing written.");
}

public sealed class WritePlan
{
    public List<ScalarChange> Scalars { get; } = new();
    public List<RecipeInsertion> RecipeInsertions { get; } = new();
    public List<LevelEdit> LevelEdits { get; } = new();
    public bool IsEmpty => Scalars.Count == 0 && RecipeInsertions.Count == 0 && LevelEdits.Count == 0;
}

public readonly record struct ScalarChange(string AssetPath, string Field, string Old, string New, bool AbsentAllowed);

/// One positional edit inside the Levels asset: a level's xpThreshold (GrantIndex == null) or a grant's amount.
public readonly record struct LevelEdit(int LevelIndex, int? GrantIndex, string Old, string New);

public sealed class RecipeInsertion
{
    public RecipeConfig Recipe { get; }
    public RecipeInsertion(RecipeConfig recipe) => Recipe = recipe;
}

/// A validation refusal — a clean, named abort raised before any file is touched. Program prints its
/// message and exits non-zero; it is not a bug, so it does not warrant a stack trace.
public sealed class WriteRefusedException : Exception
{
    public WriteRefusedException(string message) : base(message) { }
}
