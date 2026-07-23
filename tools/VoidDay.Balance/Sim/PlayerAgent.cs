using VoidDay.Balance.Schema;
using VoidDay.Core.Model;

namespace VoidDay.Balance.Sim;

/// One player action the runner applies. The agent decides WHAT to do; the runner applies it, charges the
/// action-time, and records the metric — the agent never touches the clock or the ledger's write side.
public sealed class AgentDecision
{
    public enum T { Collect, Fulfill, Queue, Build, Upgrade, Skip, Wait }
    public T Type;
    public string StationId = "";
    public string RecipeId = "";
    public string OrderId = "";
    public string StationType = "";
    public string TrackId = "";
    public GridCoord Cell;
    public TimerRef Timer;
    public int Cost;                 // money (Build/Upgrade) or gems (Skip)
    public string ForPressure = "";  // the category a remedy addresses
    public List<string> Active = new(); // categories active during a Wait (gross accrual list; Storage may repeat)
}

/// The bottleneck-seeking player. Productive work first (collect → fulfil → queue via the recipe chain); when
/// nothing is productive it buys the affordable remedy for the worst accrued pressure, and the optimality dial
/// governs the pick, the action threshold and the gem waste floor.
///
/// ★ A remedy in flight (a station UnderConstruction) is excluded from RE-PURCHASE — read straight off the
/// grid, so the agent never buys a second field while the first is still building — but it stays gem-skippable
/// (accelerating an in-flight construction is plausibly the best gem play in the game).
public sealed class PlayerAgent
{
    readonly CoreHarness _h;
    readonly SimProfile _p;
    readonly RecipeChain _chain;
    readonly PressureLedger _ledger;
    readonly Random _random; // the AGENT stream — separate from OrderGeneration's, so behaviour never reshuffles orders
    readonly bool _gemsEnabled;

    public PlayerAgent(CoreHarness h, SimProfile profile, RecipeChain chain, PressureLedger ledger,
        Random agentRandom, bool gemsEnabled)
    {
        _h = h;
        _p = profile;
        _chain = chain;
        _ledger = ledger;
        _random = agentRandom;
        _gemsEnabled = gemsEnabled;
    }

    float Opt => _p.Optimality;

    public AgentDecision Decide(double now)
    {
        // 1. Productive work, in priority order.
        var collect = FirstCollectable();
        if (collect != null) return new AgentDecision { Type = AgentDecision.T.Collect, StationId = collect };

        var order = BestAffordableOrder();
        if (order != null) return new AgentDecision { Type = AgentDecision.T.Fulfill, OrderId = order };

        var chain = _chain.Next();
        if (chain.Kind == RecipeChain.Kind.Queue)
            return new AgentDecision { Type = AgentDecision.T.Queue, StationId = chain.StationId, RecipeId = chain.RecipeId };

        // 2. Nothing productive — evaluate pressure and consider a remedy.
        var active = new List<string>();
        AddStorage(active);
        active.AddRange(BlockAndDiagnostics(chain));
        var remedy = ChooseRemedy(now, active);
        if (remedy != null) return remedy;

        // 3. Wait; the runner accrues the active categories over the idle slice.
        return new AgentDecision { Type = AgentDecision.T.Wait, Active = active };
    }

    /// The pressure the world is under RIGHT NOW, for continuous accrual over the next time slice (spec:
    /// "Because the player is always present, pressure accrues continuously"). Storage is credited per blocked
    /// station whether or not the player is otherwise busy — a full silo hurts while you tap elsewhere. The
    /// stuck-on-production categories (Capacity/Supply/Yield/Throughput) and the diagnostics accrue only when
    /// there is no productive action, i.e. the player really is watching a timer.
    public List<string> ActivePressureNow()
    {
        var active = new List<string>();
        AddStorage(active);
        if (FirstCollectable() != null || BestAffordableOrder() != null) return active;
        var chain = _chain.Next();
        if (chain.Kind == RecipeChain.Kind.Queue) return active;
        active.AddRange(BlockAndDiagnostics(chain));
        return active;
    }

    void AddStorage(List<string> active)
    {
        foreach (var id in PlacedStations())
            if (_h.Jobs.IsStorageBlocked(id)) active.Add(PressureLedger.Storage);
    }

    // ---- Productive helpers ----

    string? FirstCollectable()
    {
        foreach (var id in PlacedStations())
            if (_h.Jobs.IsCollectionPossible(id)) return id;
        return null;
    }

    string? BestAffordableOrder()
    {
        string? best = null;
        int bestCash = -1;
        int slots = _h.Orders.VisibleSlotCount;
        for (int s = 0; s < slots; s++)
        {
            var o = _h.Orders.OrderAt(s);
            if (o == null || !_h.Pool.CanAfford(o.Requests)) continue;
            if (o.Cash > bestCash || (o.Cash == bestCash && (best == null || string.CompareOrdinal(o.Id, best) < 0)))
            { best = o.Id; bestCash = o.Cash; }
        }
        return best;
    }

    // ---- Pressure evaluation ----

    /// The stuck-on-production categories plus diagnostics, when the player has no productive action. Storage
    /// is handled separately (it accrues continuously), so it is NOT added here. Gross — gem relief is never
    /// netted.
    List<string> BlockAndDiagnostics(RecipeChain.Intent chain)
    {
        var active = new List<string>();

        if (chain.Kind == RecipeChain.Kind.Want)
        {
            active.Add(chain.Category);

            // A wanted structural remedy that is gated above the current level is an Unlock finding; one that
            // is affordable-in-principle but not now is an Income finding.
            string type = chain.Subject;
            if (chain.Category.StartsWith("Capacity") || chain.Category.StartsWith("Supply"))
            {
                if (!string.IsNullOrEmpty(type) && _h.IsBuildable(type)
                    && _h.TypeOf(type).UnlockLevel > _h.Progression.PlayerLevel)
                    active.Add(PressureLedger.Unlock);
                else if (!string.IsNullOrEmpty(type) && _h.IsBuildable(type)
                    && _h.Builds.CountOf(type) < _h.Builds.Cap(type)
                    && _h.Wallet.Money < _h.Builds.BuildCost(type))
                    active.Add(PressureLedger.Income);
            }
        }

        if (active.Count == 0 && HasRunningJob()) active.Add(PressureLedger.Throughput);

        if (AllSlotsEmpty() && HoldsSellableGoods()) active.Add(PressureLedger.OrderRefill);

        return active;
    }

    // ---- Remedy choice ----

    AgentDecision? ChooseRemedy(double now, List<string> active)
    {
        // Rank the actionable categories present, weighted by accrued gross pressure; the optimality dial
        // softens argmax into softmax as it drops.
        var candidates = new List<string>();
        foreach (var c in active)
            if (IsActionable(c) && !candidates.Contains(c)) candidates.Add(c);
        candidates.Sort(StringComparer.Ordinal);

        if (candidates.Count == 0)
            return ConsiderDiagnosticGem(now, active);

        string cat = PickCategory(candidates);

        double threshold = (1f - Opt) * 60.0;
        if (_ledger.Get(cat) < threshold)
            return ConsiderConsumable(now, cat); // not pressured enough to buy structurally — a gem may still help

        return RemedyFor(now, cat) ?? ConsiderConsumable(now, cat);
    }

    string PickCategory(List<string> candidates)
    {
        if (candidates.Count == 1) return candidates[0];

        // argmax by accrued pressure (deterministic tie-break) at optimality 1.
        if (Opt >= 1f)
        {
            string best = candidates[0];
            foreach (var c in candidates)
                if (_ledger.Get(c) > _ledger.Get(best)) best = c;
            return best;
        }

        // softmax, temperature (1 − optimality); one draw off the agent stream keeps it deterministic.
        double temp = Math.Max(1e-3, 1f - Opt);
        double maxw = double.NegativeInfinity;
        foreach (var c in candidates) maxw = Math.Max(maxw, _ledger.Get(c));
        double scale = Math.Max(1.0, Math.Abs(maxw));
        double sum = 0;
        var weights = new double[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            weights[i] = Math.Exp((_ledger.Get(candidates[i]) - maxw) / (temp * scale));
            sum += weights[i];
        }
        double roll = _random.NextDouble() * sum;
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0) return candidates[i];
        }
        return candidates[^1];
    }

    AgentDecision? RemedyFor(double now, string cat)
    {
        if (cat == PressureLedger.Storage)
            return BuyUpgradeForEffect(EffectType.StorageCap, null, cat);

        if (cat == PressureLedger.Throughput)
            return BuyUpgradeForEffect(EffectType.StationSpeed, null, cat) ?? ConsiderSkipJob(now, cat);

        var (baseCat, subject) = Split(cat);
        if (baseCat == "Capacity")
        {
            if (AnyUnderConstruction(subject)) return null; // remedy in flight — no re-purchase (gem handled by ConsiderConsumable)
            if (CanBuildAnother(subject) && CanAffordBuild(subject)) return BuildDecision(subject, cat);
            if (CanBuildAnother(subject)) return null;       // affordable-later → Income
            return BuyUpgradeForEffect(EffectType.StationQueueDepth, subject, cat); // saturated cap → deepen queue
        }
        if (baseCat == "Supply")
        {
            string? type = ProducerType(subject);
            if (type == null) return null;
            if (AnyUnderConstruction(type)) return null;
            if (CanBuildAnother(type) && CanAffordBuild(type)) return BuildDecision(type, cat);
            return null; // gated (Unlock) or unaffordable (Income)
        }
        if (baseCat == "Yield")
            return BuyUpgradeForEffect(EffectType.StationYield, subject, cat);

        return null;
    }

    // ---- Gems (the consumable remedy class) ----

    /// Accelerate the timer behind `cat` with a gem, if the policy and floors allow. Construction in flight is
    /// skippable here even though it is excluded from re-purchase — the two rules are distinct.
    AgentDecision? ConsiderConsumable(double now, string cat)
    {
        if (!GemsAllowed()) return null;
        var (baseCat, subject) = Split(cat);
        if (baseCat == "Capacity" || baseCat == "Supply")
        {
            string? type = baseCat == "Supply" ? ProducerType(subject) : subject;
            if (type != null) { var d = SkipConstruction(now, type, cat); if (d != null) return d; }
        }
        if (cat == PressureLedger.Throughput) return ConsiderSkipJob(now, cat);
        return null;
    }

    AgentDecision? ConsiderDiagnosticGem(double now, List<string> active)
    {
        if (!GemsAllowed() || !active.Contains(PressureLedger.OrderRefill)) return null;
        int slots = _h.Orders.VisibleSlotCount;
        for (int s = 0; s < slots; s++)
        {
            if (_h.Orders.OrderAt(s) != null) continue;
            var timer = TimerRef.OrderRefill(s);
            var skip = TrySkip(now, timer, PressureLedger.OrderRefill);
            if (skip != null) return skip;
        }
        return null;
    }

    AgentDecision? SkipConstruction(double now, string type, string cat)
    {
        foreach (var id in ConstructionSites(type))
        {
            var skip = TrySkip(now, TimerRef.Construction(id), cat);
            if (skip != null) return skip;
        }
        return null;
    }

    AgentDecision? ConsiderSkipJob(double now, string cat)
    {
        if (!GemsAllowed()) return null;
        string? pick = null;
        float best = -1;
        foreach (var id in PlacedStations())
        {
            float rem = _h.Jobs.HeadSecondsRemaining(id, now);
            if (rem > best) { best = rem; pick = id; }
        }
        return pick == null ? null : TrySkip(now, TimerRef.Job(pick), cat);
    }

    /// Price via the REAL TimeSkip.CostFor / CanSkip — never a copy of the formula — and gate on the purse,
    /// the reserve, and the optimality-decayed waste floor.
    AgentDecision? TrySkip(double now, TimerRef timer, string cat)
    {
        if (!GemsAllowed() || !_h.TimeSkip.CanSkip(timer, now)) return null;
        float remaining = SecondsRemaining(timer, now);
        if (remaining < EffectiveMinSkip()) return null;
        int cost = _h.TimeSkip.CostFor(timer, now);
        if (!_h.Gems.CanAfford(cost) || _h.Gems.Gems - cost < _p.GemReserve) return null;
        return new AgentDecision
        { Type = AgentDecision.T.Skip, Timer = timer, Cost = cost, ForPressure = cat };
    }

    bool GemsAllowed() => _gemsEnabled && _p.GemPolicy != GemPolicy.Hoard;

    /// Waste floor: at optimality 1 it is MinSkipSeconds; it decays toward 1s as the dial drops, so a sloppy
    /// player skips nearly-finished timers and wastes the drip (SecondsPerGemRealised falls).
    double EffectiveMinSkip() => Math.Max(1.0, _p.MinSkipSeconds * Opt);

    float SecondsRemaining(TimerRef timer, double now) => timer.Kind switch
    {
        TimerKind.Job => _h.Jobs.HeadSecondsRemaining(timer.StationId, now),
        TimerKind.Construction => _h.Builds.SiteSecondsRemaining(timer.StationId, now),
        TimerKind.OrderRefill => _h.Orders.RefillRemaining(timer.Slot, now),
        _ => -1f
    };

    // ---- Upgrade / build construction ----

    AgentDecision? BuyUpgradeForEffect(EffectType desired, string? restrictType, string cat)
    {
        foreach (var id in PlacedStations())
        {
            if (restrictType != null && _h.Jobs.StationTypeOf(id) != restrictType) continue;
            foreach (var track in _h.Upgrades.TracksFor(id))
            {
                if (_h.Upgrades.IsLocked(track) || !TrackHasEffect(track, desired)) continue;
                var next = _h.Upgrades.NextTier(id, track);
                if (next == null || _h.Wallet.Money - next.Cost < _p.CashReserve) continue;
                return new AgentDecision
                { Type = AgentDecision.T.Upgrade, StationId = id, TrackId = track.Id, Cost = next.Cost, ForPressure = cat };
            }
        }
        return null;
    }

    AgentDecision BuildDecision(string type, string cat) => new()
    {
        Type = AgentDecision.T.Build,
        StationType = type,
        Cell = _h.FindFreeCell(),
        Cost = _h.Builds.BuildCost(type),
        ForPressure = cat
    };

    // ---- Predicates ----

    static bool TrackHasEffect(UpgradeTrackModel track, EffectType type)
    {
        foreach (var tier in track.Tiers)
            foreach (var e in tier.Effects)
                if (e.type == type) return true;
        return false;
    }

    bool IsActionable(string cat)
    {
        var (b, _) = Split(cat);
        return b == PressureLedger.Storage || b == PressureLedger.Throughput
            || b == "Capacity" || b == "Supply" || b == "Yield";
    }

    bool HasRunningJob()
    {
        foreach (var id in PlacedStations())
            if (_h.Jobs.HeadSecondsRemaining(id, _lastNow) > 0f) return true;
        return false;
    }

    bool AllSlotsEmpty()
    {
        int slots = _h.Orders.VisibleSlotCount;
        for (int s = 0; s < slots; s++) if (_h.Orders.OrderAt(s) != null) return false;
        return slots > 0;
    }

    bool HoldsSellableGoods()
    {
        foreach (var kv in _h.Pool.All) if (kv.Value > 0) return true; // any goods on hand
        return false;
    }

    bool CanBuildAnother(string type) =>
        _h.IsBuildable(type)
        && _h.TypeOf(type).UnlockLevel <= _h.Progression.PlayerLevel
        && _h.Builds.CountOf(type) < _h.Builds.Cap(type);

    bool CanAffordBuild(string type) => _h.Wallet.Money - _h.Builds.BuildCost(type) >= _p.CashReserve;

    bool AnyUnderConstruction(string type)
    {
        foreach (var kv in _h.Grid.All)
            if (kv.Value.StationType == type && kv.Value.UnderConstruction) return true;
        return false;
    }

    List<string> ConstructionSites(string type)
    {
        var ids = new List<string>();
        foreach (var kv in _h.Grid.All)
            if (kv.Value.StationType == type && kv.Value.UnderConstruction) ids.Add(kv.Value.Id);
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    string? ProducerType(string good)
    {
        string? pick = null;
        foreach (var recipe in _h.Config.Recipes)
            foreach (var o in recipe.Outputs)
                if (o.Resource == good && (pick == null || string.CompareOrdinal(recipe.StationType, pick) < 0))
                    pick = recipe.StationType;
        return pick;
    }

    List<string> PlacedStations()
    {
        var ids = new List<string>();
        foreach (var kv in _h.Grid.All)
            if (!kv.Value.UnderConstruction) ids.Add(kv.Value.Id);
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    static (string, string) Split(string cat)
    {
        int i = cat.IndexOf(':');
        return i < 0 ? (cat, "") : (cat[..i], cat[(i + 1)..]);
    }

    double _lastNow; // set by the runner each step so HasRunningJob can price timers
    public void SetNow(double now) => _lastNow = now;
}
