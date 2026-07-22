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

    /// A tap that landed on an in-world queue slot. Deliberately NOT "cancel requested": a tap on the ready
    /// head collects, on anything else it cancels, and that branch is a rule — it belongs to Producer's
    /// tap-resolution (§4.5), the single authority, not to the View that captured the tap. The emitter says
    /// what happened; the listener decides what it means.
    public readonly struct QueueSlotTapped
    {
        public readonly string StationId;
        public readonly int SlotIndex;
        public QueueSlotTapped(string stationId, int slotIndex) { StationId = stationId; SlotIndex = slotIndex; }
    }

    /// A collect the player explicitly asked for that Core refused (§4.4 — the head is done but its output
    /// has nowhere to go). A FACT, not an instruction: the toast, the slot's reject flash and the sound each
    /// listen and decide for themselves. Distinct from StationBlocked, which reports the station's standing
    /// condition — this fires once per refused *attempt*, which is what makes it worth a sound.
    public readonly struct CollectRefused
    {
        public readonly string StationId;
        public readonly string Reason; // matches StationBlocked's vocabulary — "storage-full"
        public CollectRefused(string stationId, string reason) { StationId = stationId; Reason = reason; }
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

    /// A placement/move drag was released over a cell that cannot take the station — off-grid, or occupied.
    /// The View owns the validity preview (§12.2), so it owns the rejection too: Core never sees this drop.
    public readonly struct PlaceRejected
    {
        public readonly string StationType;
        public readonly string Reason;
        public PlaceRejected(string stationType, string reason) { StationType = stationType; Reason = reason; }
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

    /// The peer of ExclusiveUiOpened: the same surface went open→closed, whatever closed it (its ✕, a
    /// background tap, another surface taking exclusivity). Published once per real transition, never on a
    /// close-while-already-closed. View-routing only.
    public readonly struct ExclusiveUiClosed
    {
        public readonly string Source;
        public ExclusiveUiClosed(string source) { Source = source; }
    }

    /// A UI control was pressed that announces nothing else — a tab switch, a recipe tile. Buttons that DO
    /// produce a domain event (Queue, Buy, Fill, Skip) never publish this, so one press is one announcement.
    /// View-routing only.
    public readonly struct UiTapped
    {
        public readonly string Source;
        public UiTapped(string source) { Source = source; }
    }

    // ---- Stations built / moved / demolished (published by Core) ----

    /// A placement was accepted and paid for, and the station is now under construction (§4.3). The peer of
    /// StationBuilt, which fires later when the build timer expires — that one still means "this station now
    /// exists and is operable", which is why XP, the real prefab and the completion sound all ride it unchanged.
    /// A construction site holds its cell and counts against the cap from this moment.
    public readonly struct StationConstructionStarted
    {
        public readonly string StationId;
        public readonly string StationType;
        public readonly GridCoord Cell;
        public readonly float Duration; // seconds; 0 when the type builds instantly
        public StationConstructionStarted(string stationId, string stationType, GridCoord cell, float duration)
        { StationId = stationId; StationType = stationType; Cell = cell; Duration = duration; }
    }

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

    /// §15 progression event. Emitted when a level-up grants a station-type/cap/upgrade unlock; the build
    /// menu listens to re-evaluate lock state. Kind is a LevelEntryKind name; Id names the subject.
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

    /// The gem balance moved. Peer of MoneyChanged — the gem pill binds to it from the first frame, and
    /// GemPurse.EmitCurrent pushes the starting value so the HUD needs no special first-frame path.
    public readonly struct GemsChanged
    {
        public readonly int Delta;
        public readonly int Total;
        public GemsChanged(int delta, int total) { Delta = delta; Total = total; }
    }

    /// The shared silo is full and a completed job's output could not fit (§7, §4.4). ResourceId is the good
    /// that was turned away — informative for a toast, not a per-resource cap (there is one pool). The
    /// station's blocked state rides the existing StationBlocked with reason "storage-full".
    public readonly struct StorageFull
    {
        public readonly string ResourceId;
        public StorageFull(string resourceId) { ResourceId = resourceId; }
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

    /// §15 level:up. Fires ONCE PER LEVEL crossed — a single fat XP grant that spans three thresholds
    /// publishes three of these, so nothing has to reconstruct the steps from a jump. Unlocks are what the
    /// level opened up (station types, upgrade tracks, raised caps/queue/slots); Rewards are what it paid out.
    /// Entries are structured facts — the popup owns the wording.
    public readonly struct LevelUp
    {
        public readonly int Level;
        public readonly IReadOnlyList<LevelEntry> Unlocks;
        public readonly IReadOnlyList<LevelEntry> Rewards;
        public LevelUp(int level, IReadOnlyList<LevelEntry> unlocks, IReadOnlyList<LevelEntry> rewards)
        { Level = level; Unlocks = unlocks; Rewards = rewards; }
    }

    /// Debug intent (§12.7 "level up"), same routing rationale as the other cheats: it grants exactly the XP
    /// still owed to the next threshold and lets the normal xp:gained → level:up path do the rest.
    public readonly struct DebugLevelUpRequested { }

    // Debug intent, same routing rationale as DebugAddResourceRequested: add → money:changed.
    public readonly struct DebugAddMoneyRequested
    {
        public readonly int Amount;
        public DebugAddMoneyRequested(int amount) { Amount = amount; }
    }

    // Debug intent, same routing rationale as DebugAddMoneyRequested: add → gems:changed.
    public readonly struct DebugAddGemsRequested
    {
        public readonly int Amount;
        public DebugAddGemsRequested(int amount) { Amount = amount; }
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

    /// A tier of a station-upgrade track was actually bought. effects:recalculated fires alongside it, but
    /// that one also fires on a reset and says nothing about who paid what — this names the purchase itself.
    public readonly struct UpgradePurchased
    {
        public readonly string StationId;
        public readonly string UpgradeId; // the track id
        public readonly int Tier;         // the tier now owned (1-based)
        public readonly int Cost;
        public UpgradePurchased(string stationId, string upgradeId, int tier, int cost)
        { StationId = stationId; UpgradeId = upgradeId; Tier = tier; Cost = cost; }
    }

    /// §15 effects:recalculated — the active effect set changed (an upgrade was bought, or a reset cleared
    /// them). Views/systems re-read resolved values on this; they never re-derive what changed.
    public readonly struct EffectsRecalculated { }
}
