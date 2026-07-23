using VoidDay.Balance.Schema;
using VoidDay.Core.Model;

namespace VoidDay.Balance.Sim;

/// Thrown when the recipe graph contains a genuine cycle across distinct recipes (A→B→A) with no acyclic way
/// to satisfy demand. A self-referential grow recipe (corn→corn) is NOT a cycle — it falls back to a base
/// producer (fallowCorn). Errors loudly instead of hanging (spec: RecipeChainTerminatesOnCycle).
public sealed class RecipeCycleException : Exception
{
    public RecipeCycleException(string path) : base($"Recipe cycle detected: {path}") { }
}

/// Demand-driven backward chaining from the order board (spec: Recipe choice). Each time the agent has a free
/// queue slot it walks the recipe tree backward from what orders demand, deepest-unsatisfied-first, ties by
/// output value per second, with a memoised cycle guard.
public sealed class RecipeChain
{
    public enum Kind { Queue, Want, Idle }

    public readonly struct Intent
    {
        public readonly Kind Kind;
        public readonly string RecipeId;
        public readonly string StationId;
        public readonly string Category;  // PressureLedger.* when Kind == Want
        public readonly string Subject;   // station type / good the category is about

        Intent(Kind kind, string recipeId, string stationId, string category, string subject)
        { Kind = kind; RecipeId = recipeId; StationId = stationId; Category = category; Subject = subject; }

        public static Intent Queue(string recipeId, string stationId) => new(Kind.Queue, recipeId, stationId, "", "");
        public static Intent Want(string category, string subject) => new(Kind.Want, "", "", category, subject);
        public static readonly Intent Idle = new(Kind.Idle, "", "", "", "");
    }

    readonly CoreHarness _h;
    readonly Dictionary<string, List<RecipeConfig>> _byOutput = new();
    readonly Dictionary<string, RecipeConfig> _byId = new();
    readonly Dictionary<string, int> _baseValue = new();

    public RecipeChain(CoreHarness h, BalanceConfig config)
    {
        _h = h;
        foreach (var r in config.Resources) _baseValue[r.Id] = r.BaseValue;
        foreach (var recipe in config.Recipes)
        {
            _byId[recipe.Id] = recipe;
            foreach (var o in recipe.Outputs)
            {
                if (!_byOutput.TryGetValue(o.Resource, out var list)) _byOutput[o.Resource] = list = new();
                list.Add(recipe);
            }
        }
        foreach (var list in _byOutput.Values) list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    }

    /// The single best thing to do next: queue a recipe, or (if blocked) the dominant production block, or Idle.
    public Intent Next()
    {
        var demanded = Demanded();
        Intent bestQueue = Intent.Idle;
        int bestDepth = -1;
        double bestVps = -1;
        Intent block = Intent.Idle;

        foreach (var good in demanded)
        {
            var path = new List<string>();
            var r = Resolve(good, path, 0);
            if (r.Kind == Kind.Queue)
            {
                double vps = ValuePerSecond(r.RecipeId);
                int depth = _depth;
                if (depth > bestDepth || (depth == bestDepth && vps > bestVps))
                { bestQueue = r; bestDepth = depth; bestVps = vps; }
            }
            else if (r.Kind == Kind.Want && block.Kind == Kind.Idle)
            {
                block = r;
            }
        }

        if (bestQueue.Kind == Kind.Queue) return bestQueue;
        if (block.Kind == Kind.Want) return block;

        // Nothing demanded is queueable and nothing is blocked toward demand: keep raw producers busy so
        // fields don't idle (spec) — the GreedyValuePerSecond fallback over any affordable, free-slot recipe.
        return KeepBusy();
    }

    // ---- Internals ----

    int _depth; // out-param scratch for the depth of the last Queue intent returned by Resolve

    /// Find the deepest queueable recipe that helps produce `good`, or the dominant block preventing it.
    Intent Resolve(string good, List<string> path, int depth)
    {
        if (!_byOutput.TryGetValue(good, out var producers))
        { _depth = depth; return Intent.Want(PressureLedger.Supply(good), good); }  // nothing makes it

        Intent directBest = Intent.Idle;
        double directVps = -1;
        Intent deepest = Intent.Idle;
        int deepestDepth = -1;
        Intent block = Intent.Idle;

        foreach (var R in producers)
        {
            if (path.Contains(R.Id))
            {
                // Back-edge. A self-grow (good is its own input) is legitimate — skip it. A back-edge to a
                // DIFFERENT ancestor recipe is a real cross-recipe cycle — fail loud naming the path.
                if (Produces(R, good) && ConsumesOnly(R, good)) continue;
                throw new RecipeCycleException(string.Join(" → ", path) + " → " + R.Id);
            }

            bool inputsAffordable = Affordable(R);
            if (inputsAffordable)
            {
                var station = FreeSlotStation(R.StationType);
                if (station != null)
                {
                    double vps = ValuePerSecond(R.Id);
                    if (vps > directVps) { directBest = Intent.Queue(R.Id, station); directVps = vps; }
                }
                else if (block.Kind == Kind.Idle)
                {
                    block = BlockForFullStation(R.StationType, good);
                }
                continue;
            }

            // Inputs missing — recurse to produce the deepest missing one.
            path.Add(R.Id);
            foreach (var input in Sorted(R.Inputs))
            {
                if (_h.Pool.Get(input.Resource) >= input.Amount) continue;
                var sub = Resolve(input.Resource, path, depth + 1);
                if (sub.Kind == Kind.Queue && _depth > deepestDepth)
                { deepest = sub; deepestDepth = _depth; }
                else if (sub.Kind == Kind.Want && block.Kind == Kind.Idle)
                { block = MaybeYield(input.Resource, sub); }
            }
            path.Remove(R.Id);
        }

        if (directBest.Kind == Kind.Queue && directVps >= 0 && (deepest.Kind != Kind.Queue || depth >= deepestDepth))
        { _depth = depth; return directBest; }
        if (deepest.Kind == Kind.Queue) { _depth = deepestDepth; return deepest; }
        _depth = depth;
        return block.Kind == Kind.Want ? block : Intent.Want(PressureLedger.Supply(good), good);
    }

    /// If a demanded good's producer type is placed-but-all-full, decide Capacity vs Yield vs Supply.
    Intent BlockForFullStation(string type, string good)
    {
        if (PlacedCount(type) == 0) return Intent.Want(PressureLedger.Supply(good), good);
        return CanBuildAnother(type) ? Intent.Want(PressureLedger.Capacity(type), type)
                                     : Intent.Want(PressureLedger.Yield(type), type);
    }

    /// An unsatisfiable input whose producer type is saturated is a Yield finding for that producer (more
    /// per job is the only fix); otherwise the deeper block propagates unchanged.
    Intent MaybeYield(string input, Intent sub)
    {
        if (!_byOutput.TryGetValue(input, out var producers)) return sub;
        foreach (var R in producers)
        {
            var t = R.StationType;
            if (PlacedCount(t) > 0 && !CanBuildAnother(t) && AllQueuesFull(t))
                return Intent.Want(PressureLedger.Yield(t), t);
        }
        return sub;
    }

    Intent KeepBusy()
    {
        RecipeConfig? best = null;
        string? bestStation = null;
        double bestVps = -1;
        foreach (var recipe in _byId.Values)
        {
            if (!Affordable(recipe)) continue;
            var station = FreeSlotStation(recipe.StationType);
            if (station == null) continue;
            double vps = ValuePerSecond(recipe.Id);
            if (vps > bestVps) { best = recipe; bestStation = station; bestVps = vps; }
        }
        return best != null ? Intent.Queue(best.Id, bestStation!) : Intent.Idle;
    }

    /// Resources requested by orders on the board that the pool cannot already cover, sorted for determinism.
    List<string> Demanded()
    {
        var need = new Dictionary<string, int>();
        int slots = _h.Orders.VisibleSlotCount;
        for (int s = 0; s < slots; s++)
        {
            var order = _h.Orders.OrderAt(s);
            if (order == null) continue;
            foreach (var req in order.Requests)
            {
                need.TryGetValue(req.ResourceId, out var n);
                need[req.ResourceId] = n + req.Amount;
            }
        }
        var list = new List<string>();
        foreach (var kv in need) if (_h.Pool.Get(kv.Key) < kv.Value) list.Add(kv.Key);
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    bool Affordable(RecipeConfig r)
    {
        foreach (var i in r.Inputs) if (_h.Pool.Get(i.Resource) < i.Amount) return false;
        return true;
    }

    string? FreeSlotStation(string type)
    {
        string? pick = null;
        foreach (var id in PlacedIds(type))
            if (_h.Jobs.GetQueue(id).Count < _h.Jobs.QueueDepth(id))
                if (pick == null || string.CompareOrdinal(id, pick) < 0) pick = id;
        return pick;
    }

    List<string> PlacedIds(string type)
    {
        var ids = new List<string>();
        foreach (var kv in _h.Grid.All)
            if (kv.Value.StationType == type && !kv.Value.UnderConstruction) ids.Add(kv.Value.Id);
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    int PlacedCount(string type) => PlacedIds(type).Count;

    bool AllQueuesFull(string type)
    {
        var ids = PlacedIds(type);
        if (ids.Count == 0) return false;
        foreach (var id in ids) if (_h.Jobs.GetQueue(id).Count < _h.Jobs.QueueDepth(id)) return false;
        return true;
    }

    bool CanBuildAnother(string type) =>
        _h.IsBuildable(type)
        && _h.TypeOf(type).UnlockLevel <= _h.Progression.PlayerLevel
        && _h.Builds.CountOf(type) < _h.Builds.Cap(type);

    double ValuePerSecond(string recipeId)
    {
        var r = _byId[recipeId];
        int value = 0;
        foreach (var o in r.Outputs) value += o.Amount * (_baseValue.TryGetValue(o.Resource, out var v) ? v : 0);
        return value / Math.Max(1.0, r.Duration);
    }

    static bool Produces(RecipeConfig r, string good)
    {
        foreach (var o in r.Outputs) if (o.Resource == good) return true;
        return false;
    }

    static bool ConsumesOnly(RecipeConfig r, string good)
    {
        foreach (var i in r.Inputs) if (i.Resource != good) return false;
        return r.Inputs.Count > 0;
    }

    static IEnumerable<ResourceQuantity> Sorted(List<ResourceQuantity> src)
    {
        var copy = new List<ResourceQuantity>(src);
        copy.Sort((a, b) => string.CompareOrdinal(a.Resource, b.Resource));
        return copy;
    }
}
