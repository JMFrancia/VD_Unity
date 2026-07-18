using System.Collections.Generic;
using VoidDay.Core.Model;

namespace VoidDay.Core.Events
{
    // The M2 slice of the §15 event catalog. Every event is an immutable payload. Emitters describe what
    // happened, never what should happen in response — a listener decides for itself whether it cares.

    // ---- Boot ----
    public readonly struct DataLoaded { }
    public readonly struct GameStarted { }

    /// Emitted when the debug menu resets the run to the start state (§13). Views resync from Core on this.
    public readonly struct GameReset { }

    // ---- Input intents (published by the View/input layer, never acted on by it) ----
    public readonly struct StationTapped
    {
        public readonly string StationId;
        public StationTapped(string stationId) { StationId = stationId; }
    }

    public readonly struct JobQueueRequested
    {
        public readonly string StationId;
        public readonly string RecipeId;
        public JobQueueRequested(string stationId, string recipeId) { StationId = stationId; RecipeId = recipeId; }
    }

    public readonly struct JobCancelRequested
    {
        public readonly string StationId;
        public readonly int JobIndex;
        public JobCancelRequested(string stationId, int jobIndex) { StationId = stationId; JobIndex = jobIndex; }
    }

    // Debug intents. §15 defines no debug:* names (open item); each cheat routes through a natural domain
    // effect (add → resource:changed; reset → game:reset) rather than reaching past the bus.
    public readonly struct DebugAddResourceRequested
    {
        public readonly string ResourceId;
        public readonly int Amount;
        public DebugAddResourceRequested(string resourceId, int amount) { ResourceId = resourceId; Amount = amount; }
    }

    public readonly struct DebugResetRequested { }

    /// Routing outcome of tap-resolution (§4.5): a tap that is NOT a collection resolves to "open the panel".
    /// Not in §15 — added so the tap-resolution rule has a single System-side authority and the panel View
    /// simply listens. Keeps the View from re-deriving IsCollectionPossible and double-triggering.
    public readonly struct StationPanelRequested
    {
        public readonly string StationId;
        public StationPanelRequested(string stationId) { StationId = stationId; }
    }

    // ---- Jobs (published by Core) ----
    public readonly struct JobQueued
    {
        public readonly string StationId;
        public readonly string RecipeId;
        public readonly int JobIndex;
        public JobQueued(string stationId, string recipeId, int jobIndex)
        { StationId = stationId; RecipeId = recipeId; JobIndex = jobIndex; }
    }

    public readonly struct JobStarted
    {
        public readonly string StationId;
        public JobStarted(string stationId) { StationId = stationId; }
    }

    public readonly struct JobCompleted
    {
        public readonly string StationId;
        public readonly IReadOnlyList<ResourceAmount> Outputs;
        public JobCompleted(string stationId, IReadOnlyList<ResourceAmount> outputs)
        { StationId = stationId; Outputs = outputs; }
    }

    public readonly struct JobCollected
    {
        public readonly string StationId;
        public readonly IReadOnlyList<ResourceAmount> Outputs;
        public readonly bool ByPet;
        public JobCollected(string stationId, IReadOnlyList<ResourceAmount> outputs, bool byPet)
        { StationId = stationId; Outputs = outputs; ByPet = byPet; }
    }

    public readonly struct JobCancelled
    {
        public readonly string StationId;
        public readonly int JobIndex;
        public JobCancelled(string stationId, int jobIndex) { StationId = stationId; JobIndex = jobIndex; }
    }

    // ---- Stations ----
    public readonly struct StationBlocked
    {
        public readonly string StationId;
        public readonly string Reason;
        public StationBlocked(string stationId, string reason) { StationId = stationId; Reason = reason; }
    }

    // ---- Economy ----
    public readonly struct ResourceChanged
    {
        public readonly string ResourceId;
        public readonly int Delta;
        public readonly int Total;
        public ResourceChanged(string resourceId, int delta, int total)
        { ResourceId = resourceId; Delta = delta; Total = total; }
    }

    public readonly struct MoneyChanged
    {
        public readonly int Delta;
        public readonly int Total;
        public MoneyChanged(int delta, int total) { Delta = delta; Total = total; }
    }
}
