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

    /// A tap that landed on empty world — not a station, slot, or UI. Listeners treat it as "dismiss":
    /// an open panel closes itself. Emitter states what happened (the background was tapped), not the response.
    public readonly struct BackgroundTapped { }

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

    /// The station panel actually opened for a producer (StationPanel owns this truth — it fires only after the
    /// producer guard, unlike the StationPanelRequested routing intent which also fires for rejected taps). The
    /// in-world state rig listens: it shows the queue slots for the open station and hides that station's radial.
    public readonly struct StationPanelOpened
    {
        public readonly string StationId;
        public StationPanelOpened(string stationId) { StationId = stationId; }
    }

    /// The station panel closed (any open→closed transition). Peer of StationPanelOpened; the in-world rig hides
    /// the queue slots again and restores the radial for a working station.
    public readonly struct StationPanelClosed { }

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

    // ---- Build & manage input intents (§12.2 — published by the placement View, never acted on by it) ----

    /// A completed placement drag dropped on a valid, affordable, under-cap cell (§12.2). The View guarantees
    /// validity before emitting; Core re-checks and fails loud if the contract is breached.
    public readonly struct PlaceRequested
    {
        public readonly string StationType;
        public readonly GridCoord Cell;
        public PlaceRequested(string stationType, GridCoord cell) { StationType = stationType; Cell = cell; }
    }

    /// A picked-up station dropped on a valid empty cell (§12.2). Move is free (§4.3).
    public readonly struct MoveRequested
    {
        public readonly string StationId;
        public readonly GridCoord Cell;
        public MoveRequested(string stationId, GridCoord cell) { StationId = stationId; Cell = cell; }
    }

    /// Debug-only demolish trigger for M4 (the player-facing gesture is deferred — user decision). Demolishes
    /// the most-recently-built station; routes through Core so the 50% refund rule (§4.3) has one home.
    public readonly struct DebugDemolishLastRequested { }

    /// A placed station was long-pressed to pick it up for a move (§12.2). Published by the input layer, the
    /// placement View listens and begins the move ghost. Not in §15 — a View-routing intent, like
    /// StationPanelRequested, so the pick-up rule (long-press detection) has one home.
    public readonly struct StationPickedUp
    {
        public readonly string StationId;
        public StationPickedUp(string stationId) { StationId = stationId; }
    }

    /// A placement/move ghost drag started (true) or ended (false). The camera listens and suppresses panning
    /// while a ghost is being dragged, so the same finger-drag doesn't also pan the world. View-routing only.
    public readonly struct PlacementActiveChanged
    {
        public readonly bool Active;
        public PlacementActiveChanged(bool active) { Active = active; }
    }

    /// An exclusive UI surface (a menu or panel — build menu, station panel, order board, debug menu) opened.
    /// Every exclusive surface publishes this on open and closes itself when it sees one from a DIFFERENT
    /// source, giving "one menu at a time". Stacking popups (totals, confirmations) neither publish nor listen.
    /// View-routing only — not a domain event.
    public readonly struct ExclusiveUiOpened
    {
        public readonly string Source;
        public ExclusiveUiOpened(string source) { Source = source; }
    }

    // ---- Stations built / moved / demolished (published by Core) ----
    public readonly struct StationBuilt
    {
        public readonly string StationId;
        public readonly string StationType;
        public readonly GridCoord Cell;
        public StationBuilt(string stationId, string stationType, GridCoord cell)
        { StationId = stationId; StationType = stationType; Cell = cell; }
    }

    public readonly struct StationMoved
    {
        public readonly string StationId;
        public readonly GridCoord Cell;
        public StationMoved(string stationId, GridCoord cell) { StationId = stationId; Cell = cell; }
    }

    public readonly struct StationDemolished
    {
        public readonly string StationId;
        public StationDemolished(string stationId) { StationId = stationId; }
    }

    /// §15 progression event. Emitted by M8 when a level-up grants a station-type/cap/upgrade unlock; the
    /// build menu LISTENS for it now (to re-evaluate lock state) but nothing fires it until M8.
    public readonly struct UnlockGranted
    {
        public readonly string Kind;
        public readonly string Id;
        public UnlockGranted(string kind, string id) { Kind = kind; Id = id; }
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

    // ---- Order input intents ----
    public readonly struct OrderFulfillRequested
    {
        public readonly string OrderId;
        public OrderFulfillRequested(string orderId) { OrderId = orderId; }
    }

    public readonly struct OrderSkipRequested
    {
        public readonly string OrderId;
        public OrderSkipRequested(string orderId) { OrderId = orderId; }
    }

    // ---- Orders (published by Core) ----
    public readonly struct OrderGenerated
    {
        public readonly int Slot;
        public readonly OrderModel Order;
        public OrderGenerated(int slot, OrderModel order) { Slot = slot; Order = order; }
    }

    public readonly struct OrderFulfilled
    {
        public readonly string OrderId;
        public readonly int Payout;
        public readonly int Xp;
        public OrderFulfilled(string orderId, int payout, int xp)
        { OrderId = orderId; Payout = payout; Xp = xp; }
    }

    public readonly struct OrderSkipped
    {
        public readonly string OrderId;
        public OrderSkipped(string orderId) { OrderId = orderId; }
    }

    public readonly struct OrderSlotRefilled
    {
        public readonly int Slot;
        public OrderSlotRefilled(int slot) { Slot = slot; }
    }

    // ---- Progression ----
    public readonly struct XpGained
    {
        public readonly int Amount;
        public readonly string Source;
        public XpGained(int amount, string source) { Amount = amount; Source = source; }
    }

    // Debug intent, same routing rationale as DebugAddResourceRequested: add → money:changed.
    public readonly struct DebugAddMoneyRequested
    {
        public readonly int Amount;
        public DebugAddMoneyRequested(int amount) { Amount = amount; }
    }

    // ---- Upgrades (M5) ----

    /// Buy the next tier of a station-upgrade track (§8). §15 lists only {upgradeId}; StationId is added
    /// because station upgrades are per-instance (§3.2 "own station") — the panel targets the station it shows.
    public readonly struct UpgradePurchaseRequested
    {
        public readonly string StationId;
        public readonly string UpgradeId; // the track id
        public UpgradePurchaseRequested(string stationId, string upgradeId)
        { StationId = stationId; UpgradeId = upgradeId; }
    }

    /// §15 effects:recalculated — the active effect set changed (an upgrade was bought, or a reset cleared
    /// them). Views/systems re-read resolved values on this; they never re-derive what changed.
    public readonly struct EffectsRecalculated { }
}
