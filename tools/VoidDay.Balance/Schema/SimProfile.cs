namespace VoidDay.Balance.Schema;

/// The simulated player's behaviour — deliberately NOT game balance (spec: "Player behaviour is not game
/// balance and lives in its own file"). The whole `profile/*` namespace is read-only to `patch` (M5), so an
/// agent can never lower the loss by making the player smarter instead of the game better.
public sealed class SimProfile
{
    public string Name = "typical";

    /// 0..1. One dial driving remedy pick, reaction lag, action threshold, idle waste and gem efficiency.
    public float Optimality = 0.65f;

    public ActionCosts Actions = new();
    public float ReactionLagSeconds = 2.0f;
    public int CashReserve = 0;
    public RecipePolicy RecipePolicy = RecipePolicy.DemandChain;

    public GemPolicy GemPolicy = GemPolicy.WorstPressure;
    public int GemReserve = 0;         // never spend gems below this
    public int MinSkipSeconds = 30;    // waste floor: don't skip a timer with less remaining (at optimality 1)

    public int MaxSimulatedHours = 40;
    public int StallGuardMinutes = 45; // no XP for this long ⇒ abort and report

    /// The "typical" default; a "perfect" floor sets Optimality = 1.
    public static SimProfile Typical() => new();

    public static SimProfile Perfect()
    {
        var p = new SimProfile { Name = "perfect", Optimality = 1.0f };
        return p;
    }
}

/// Seconds a single player action consumes — the source of ActingSeconds.
public sealed class ActionCosts
{
    public float Tap = 1.5f;      // collect a ready job
    public float Queue = 2.5f;    // queue a recipe
    public float Fulfill = 3.0f;  // fulfil an order
    public float Purchase = 4.0f; // buy a station / upgrade / gem skip
}

/// How the agent chooses what to produce.
public enum RecipePolicy { DemandChain, GreedyValuePerSecond }

/// How the agent spends gems.
public enum GemPolicy { WorstPressure, Hoard, LongestTimer }
