using System.Text;
using VoidDay.Balance.Schema;
using VoidDay.Core.Model;

namespace VoidDay.Balance.Sim;

/// Drives one seeded player through the real economy and produces the per-level report. Single continuous
/// session: 1s of activity per productive action, then an exact jump to the next timer boundary when the
/// player has nothing to do. Every number here rides the real Core rules — the runner only sequences them.
public sealed class SimRunner
{
    readonly BalanceConfig _config;
    readonly SimProfile _profile;
    readonly int _seed;
    readonly bool _gemsEnabled;

    public SimRunner(BalanceConfig config, SimProfile profile, int seed, bool gemsEnabled = true)
    {
        _config = config;
        _profile = profile;
        _seed = seed;
        _gemsEnabled = gemsEnabled;
    }

    float Opt => _profile.Optimality;

    // Per-open-level bookkeeping (diffed against the collector's cumulative counters at close).
    LevelReport _report = null!;
    int _entMoneyEarned, _entMoneySpent, _entGemsEarned, _entGemsSpent;
    int _entOrdersFul, _entOrdersSkip, _entJobs;
    int _entQuestsCompleted, _entQuestRewardXp, _entQuestRewardMoney, _entQuestRewardGems, _entQuestRewardResources;

    public SimResult Run()
    {
        // Order stream = seed (injected exactly as GameBoot does); agent stream = a separate, deterministic
        // mix of the seed — so changing player behaviour never reshuffles the order sequence.
        int agentSeed = unchecked(_seed * 1103515245 + 12345);
        var harness = new CoreHarness(_config, _seed);
        var ledger = new PressureLedger();
        var metrics = new MetricsCollector(harness.Bus, _config.Quests); // subscribe BEFORE the starting-state emit
        var questCollector = new QuestCollector(harness.Bus); // records completions; the loop collects them top-level
        var chain = new RecipeChain(harness, _config);
        var agent = new PlayerAgent(harness, _profile, chain, ledger, new Random(agentSeed), _gemsEnabled);
        harness.EmitStartingState();

        var clock = new SimClock();
        var result = new SimResult
        { ConfigName = _config.Name, Seed = _seed, Optimality = Opt };

        int level = harness.Progression.PlayerLevel;
        OpenLevel(level, 0, metrics);

        int lastXpTotal = harness.Progression.XpTotal;
        double lastXpTime = 0;
        double maxSeconds = _profile.MaxSimulatedHours * 3600.0;
        double stallSeconds = _profile.StallGuardMinutes * 60.0;
        bool justWaited = false;

        while (true)
        {
            double now = clock.Now;
            harness.Builds.Tick(now);   // bring stations online first (construction end IS an event boundary)
            harness.Jobs.Tick(now);     // then complete finished jobs
            harness.Orders.Tick(now);   // then refill order slots against the latest producible set

            // Collect any quest that completed (top-level, not reentrant — see QuestCollector); a reward's XP
            // may level up, which the LevelUp drain below then picks up in this same iteration.
            questCollector.DrainCollections();

            while (metrics.LevelUps.Count > 0)
            {
                int reached = metrics.LevelUps.Dequeue();
                CloseLevel(now, metrics, ledger);
                result.Levels.Add(_report);
                OpenLevel(reached, now, metrics);
            }

            if (harness.Progression.XpTotal > lastXpTotal) { lastXpTotal = harness.Progression.XpTotal; lastXpTime = now; }

            if (harness.Progression.IsMaxLevel) { result.Stop = StopReason.MaxLevel; break; }
            if (now >= maxSeconds) { result.Stop = StopReason.MaxHours; result.StopDetail = $"{_profile.MaxSimulatedHours}h"; break; }
            if (now - lastXpTime >= stallSeconds)
            { result.Stop = StopReason.Stalled; result.StopDetail = $"no XP for {_profile.StallGuardMinutes} min"; break; }

            agent.SetNow(now);
            // Pressure the world is under right now, accrued GROSS over whatever the clock advances this
            // iteration (an action slice or an idle jump) — continuous accrual, per the spec.
            var active = agent.ActivePressureNow();
            double before = clock.Now;

            var d = agent.Decide(now);

            switch (d.Type)
            {
                case AgentDecision.T.Collect:
                    Act(clock, Cost(_profile.Actions.Tap), ref justWaited);
                    harness.Jobs.Collect(d.StationId, clock.Now, false);
                    break;

                case AgentDecision.T.Fulfill:
                    Act(clock, Cost(_profile.Actions.Fulfill), ref justWaited);
                    harness.Orders.Fulfill(d.OrderId, clock.Now);
                    break;

                case AgentDecision.T.Queue:
                    Act(clock, Cost(_profile.Actions.Queue), ref justWaited);
                    harness.Jobs.QueueJob(d.StationId, d.RecipeId, clock.Now);
                    break;

                case AgentDecision.T.Build:
                    Act(clock, Cost(_profile.Actions.Purchase), ref justWaited);
                    harness.Builds.Place(d.StationType, d.Cell, clock.Now);
                    _report.Purchases.Add(new PurchaseRecord
                    { At = clock.Now, Kind = "build", Target = d.StationType, Cost = d.Cost, ForPressure = d.ForPressure });
                    break;

                case AgentDecision.T.Upgrade:
                    Act(clock, Cost(_profile.Actions.Purchase), ref justWaited);
                    harness.Upgrades.Purchase(d.StationId, d.TrackId);
                    _report.Purchases.Add(new PurchaseRecord
                    { At = clock.Now, Kind = "upgrade", Target = d.TrackId, Cost = d.Cost, ForPressure = d.ForPressure });
                    break;

                case AgentDecision.T.Skip:
                {
                    Act(clock, Cost(_profile.Actions.Purchase), ref justWaited);
                    // Read the remaining wait BEFORE the skip pulls the timer to now — that is the compressed
                    // wall-clock, credited GROSS to pressure and separately to GemRelief.
                    float remaining = Remaining(harness, d.Timer, clock.Now);
                    harness.TimeSkip.Skip(d.Timer, clock.Now);
                    _report.SecondsPurchased += remaining;
                    ledger.AccrueGemRelief(d.ForPressure, remaining);
                    _report.Purchases.Add(new PurchaseRecord
                    { At = clock.Now, Kind = "skip", Target = d.Timer.ToString(), Cost = d.Cost, ForPressure = d.ForPressure });
                    break;
                }

                case AgentDecision.T.Wait:
                {
                    double? next = clock.NextEvent(harness);
                    if (next == null) { result.Stop = StopReason.Stalled; result.StopDetail = "no live timers"; goto done; }
                    double delta = next.Value - clock.Now;
                    _report.WaitingSeconds += delta;
                    clock.JumpTo(next.Value);
                    justWaited = true;

                    double waste = delta * (1f - Opt) * 0.15; // idle waste — pure added delay, grows as opt drops
                    if (waste > 0) { clock.Advance(waste); _report.WaitingSeconds += waste; }
                    break;
                }
            }

            double slice = clock.Now - before;
            if (slice > 0) foreach (var cat in active) ledger.Accrue(cat, slice); // gross, continuous
        }

        done:
        CloseLevel(clock.Now, metrics, ledger);
        result.Levels.Add(_report);
        result.TotalSeconds = clock.Now;
        result.LevelReached = harness.Progression.PlayerLevel;
        return result;
    }

    /// A productive action: a one-off reaction lag (only when resuming from a wait) plus the action's own
    /// cost, both charged to ActingSeconds. Lag grows as optimality drops — one of the dial's four mechanisms.
    void Act(SimClock clock, double actionCost, ref bool justWaited)
    {
        double lag = justWaited ? _profile.ReactionLagSeconds / Math.Max(Opt, 0.1f) : 0;
        justWaited = false;
        clock.Advance(lag + actionCost);
        _report.ActingSeconds += lag + actionCost;
    }

    static double Cost(float seconds) => seconds;

    void OpenLevel(int level, double now, MetricsCollector m)
    {
        _report = new LevelReport
        {
            Level = level,
            EnteredAt = now,
            MoneyAtEntry = m.Money,
            GemsAtEntry = m.Gems
        };
        _entMoneyEarned = m.MoneyEarned; _entMoneySpent = m.MoneySpent;
        _entGemsEarned = m.GemsEarned; _entGemsSpent = m.GemsSpent;
        _entOrdersFul = m.OrdersFulfilled; _entOrdersSkip = m.OrdersSkipped; _entJobs = m.JobsCollected;
        _entQuestsCompleted = m.QuestsCompleted;
        _entQuestRewardXp = m.QuestRewardXp; _entQuestRewardMoney = m.QuestRewardMoney;
        _entQuestRewardGems = m.QuestRewardGems; _entQuestRewardResources = m.QuestRewardResources;
    }

    void CloseLevel(double now, MetricsCollector m, PressureLedger ledger)
    {
        _report.ExitedAt = now;
        _report.DurationSeconds = now - _report.EnteredAt;
        _report.MoneyAtExit = m.Money;
        _report.MoneyEarned = m.MoneyEarned - _entMoneyEarned;
        _report.MoneySpent = m.MoneySpent - _entMoneySpent;
        _report.OrdersFulfilled = m.OrdersFulfilled - _entOrdersFul;
        _report.OrdersSkipped = m.OrdersSkipped - _entOrdersSkip;
        _report.JobsCollected = m.JobsCollected - _entJobs;
        _report.QuestsCompleted = m.QuestsCompleted - _entQuestsCompleted;
        _report.QuestRewardXp = m.QuestRewardXp - _entQuestRewardXp;
        _report.QuestRewardMoney = m.QuestRewardMoney - _entQuestRewardMoney;
        _report.QuestRewardGems = m.QuestRewardGems - _entQuestRewardGems;
        _report.QuestRewardResources = m.QuestRewardResources - _entQuestRewardResources;

        _report.GemsAtExit = m.Gems;
        _report.GemsEarned = m.GemsEarned - _entGemsEarned;
        _report.GemsSpent = m.GemsSpent - _entGemsSpent;
        _report.CompressionShare = _report.DurationSeconds > 0 ? _report.SecondsPurchased / _report.DurationSeconds : 0;
        _report.SecondsPerGemRealised = _report.GemsSpent > 0 ? _report.SecondsPurchased / _report.GemsSpent : 0;

        _report.Pressure = ledger.SnapshotPressure();
        _report.GemRelief = ledger.SnapshotRelief();
        ledger.Clear(); // pressure/relief are per level
    }

    static float Remaining(CoreHarness h, TimerRef timer, double now) => timer.Kind switch
    {
        TimerKind.Job => h.Jobs.HeadSecondsRemaining(timer.StationId, now),
        TimerKind.Construction => h.Builds.SiteSecondsRemaining(timer.StationId, now),
        TimerKind.OrderRefill => h.Orders.RefillRemaining(timer.Slot, now),
        _ => 0f
    };

    // ---- Text rendering (the M03 deliverable) ----

    public static string Render(SimResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"sim: config '{r.ConfigName}'  seed {r.Seed}  optimality {r.Optimality:0.##}");
        sb.AppendLine($"stop: {r.Stop}{(r.StopDetail == "" ? "" : " (" + r.StopDetail + ")")}  " +
                      $"reached level {r.LevelReached}  total {Min(r.TotalSeconds)}");
        sb.AppendLine();
        sb.AppendLine("Lvl  Duration   Acting   Waiting   $entry   $exit   Orders  Jobs  Gems±  Bottleneck");
        sb.AppendLine("---  --------   ------   -------   ------   -----   ------  ----  -----  ----------");
        foreach (var l in r.Levels)
        {
            string gems = $"{l.GemsEarned}/{l.GemsSpent}";
            sb.AppendLine(
                $"{l.Level,3}  {Min(l.DurationSeconds),8}  {Min(l.ActingSeconds),7}  {Min(l.WaitingSeconds),8}  " +
                $"{l.MoneyAtEntry,6}  {l.MoneyAtExit,6}  {l.OrdersFulfilled,6}  {l.JobsCollected,4}  {gems,5}  " +
                $"{l.TopPressure()}{ReliefNote(l)}{QuestNote(l)}");
        }
        return sb.ToString();
    }

    static string QuestNote(LevelReport l)
    {
        if (l.QuestsCompleted <= 0) return "";
        var reward = new List<string>();
        if (l.QuestRewardXp > 0) reward.Add($"+{l.QuestRewardXp}xp");
        if (l.QuestRewardMoney > 0) reward.Add($"+${l.QuestRewardMoney}");
        if (l.QuestRewardGems > 0) reward.Add($"+{l.QuestRewardGems}gem");
        if (l.QuestRewardResources > 0) reward.Add($"+{l.QuestRewardResources}res");
        string r = reward.Count > 0 ? " " + string.Join(" ", reward) : "";
        return $"  [quests {l.QuestsCompleted}{r}]";
    }

    static string ReliefNote(LevelReport l)
    {
        if (l.SecondsPurchased <= 0) return "";
        return $"  [gems bought {Min(l.SecondsPurchased)}, {l.SecondsPerGemRealised:0.#} s/gem]";
    }

    static string Min(double seconds) => $"{seconds / 60.0:0.0}m";
}
