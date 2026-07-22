using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// Syncs each station's in-world state rig to Core (§12.6, sheet 34:2): progress bar while a job runs,
    /// hopping ready icon while output waits, and the job queue as slots in front of the body (world.queueSlots,
    /// panel.station ALT 42:2). Instantiates the authored StationStateWidget prefab under every station root
    /// and fills its QueueRow with QueueSlot instances — count is data (queue depth), look is prefab. Pure
    /// view-sync: polls Core state every frame, holds no rule (CLAUDE.md). Tapping a filled slot is a cancel —
    /// routed by InputRouter off QueueSlot.
    public sealed class WorldState : MonoBehaviour
    {
        [SerializeField] StationStateWidget widgetTemplate;
        [SerializeField] QueueSlot slotTemplate;
        [SerializeField] float slotSpacing = 0.52f; // centre-to-centre distance between queue slots

        [Header("Queue row framing (camera-relative)")]
        [Tooltip("World units from the station towards the viewer — how far the row sits 'below' the building.")]
        [SerializeField] float rowDistance = 1.15f;
        [Tooltip("World units above the ground, so the row clears the station's base and its shadow.")]
        [SerializeField] float rowHeight = 0.3f;

        sealed class Rig
        {
            public string StationId;
            public StationStateWidget Widget;
            public List<QueueSlot> Slots = new();
            public CropField Crops;        // null unless the station body authors a crop cluster (fields)
            public string GrowingRecipeId; // which crop the cluster is currently growing, so StartGrow fires once
        }

        JobSystem _jobs;
        RecipeCatalog _catalog;
        Camera _camera; // the row is framed against it every frame — see PositionQueueRow
        UpgradeSystem _upgrades; // only for the slot-row ceiling — what depth this station could still buy
        IReadOnlyDictionary<string, ResourceSO> _resources; // id → display data; the crop icon on slots + ready indicator
        IReadOnlyDictionary<string, Transform> _stationRoots;
        readonly List<Rig> _rigs = new();
        string _openStationId; // whose panel is open — its queue shows and its radial hides (BUG-03 design)

        public void Init(EventBus bus, JobSystem jobs, RecipeCatalog catalog, UpgradeSystem upgrades,
            IReadOnlyDictionary<string, ResourceSO> resources, IReadOnlyDictionary<string, Transform> stationRoots,
            Camera camera)
        {
            _jobs = jobs;
            _catalog = catalog;
            _upgrades = upgrades;
            _camera = camera;
            _resources = resources;
            _stationRoots = stationRoots; // shared live map — StationRegistry adds/removes runtime stations here
            bus.Subscribe<StationPanelOpened>(e => _openStationId = e.StationId);
            bus.Subscribe<StationPanelClosed>(_ => _openStationId = null);
            // Only the head can be refused, so only slot 0 answers. Listening here rather than in QueueSlot
            // keeps the slot a dumb renderer — it owns the animation, not the question of who it belongs to.
            bus.Subscribe<CollectRefused>(e =>
            {
                var rig = RigFor(e.StationId);
                if (rig != null && rig.Slots.Count > 0) rig.Slots[0].Reject();
            });
            Reconcile();
        }

        /// Build a rig for any station that appeared in the shared roots map (a runtime placement) and drop
        /// the rig for any that vanished (a demolish). Pure view-sync — the roots map is the source of truth.
        void Reconcile()
        {
            foreach (var kv in _stationRoots)
            {
                if (RigFor(kv.Key) != null) continue;
                var rig = new Rig { StationId = kv.Key, Widget = Instantiate(widgetTemplate, kv.Value) };
                rig.Crops = kv.Value.GetComponentInChildren<CropField>(true); // fields author one; everything else null
                if (HasRecipes(kv.Key)) BuildSlots(rig); // non-producers keep a clean front (no queue slots)
                rig.Widget.QueueRow.gameObject.SetActive(false);
                _rigs.Add(rig);
            }

            for (int i = _rigs.Count - 1; i >= 0; i--)
            {
                if (_stationRoots.ContainsKey(_rigs[i].StationId)) continue;
                if (_rigs[i].Widget != null) Destroy(_rigs[i].Widget.gameObject); // usually already gone with its parent
                _rigs.RemoveAt(i);
            }
        }

        Rig RigFor(string stationId)
        {
            foreach (var rig in _rigs)
                if (rig.StationId == stationId) return rig;
            return null;
        }

        bool HasRecipes(string stationId) =>
            _catalog.ForStationType(_jobs.StationTypeOf(stationId)).Count > 0;

        /// The crop icon a job produces: recipe's first output → its ResourceSO icon. Used on queue slots and
        /// the ready indicator so both read as "this good", not a colored placeholder.
        Sprite OutputIcon(string recipeId)
        {
            var recipe = _catalog.Get(recipeId);
            if (recipe.Outputs.Count == 0) return null;
            return _resources.TryGetValue(recipe.Outputs[0].ResourceId, out var so) ? so.icon : null;
        }

        /// Slots drawn for a station: the depth it has now, plus the depth its unpurchased upgrade tiers would
        /// buy. The tail renders locked, so the row shows the whole upgrade path rather than growing a square
        /// out of nowhere on purchase.
        int SlotCount(string stationId) =>
            _jobs.QueueDepth(stationId) + _upgrades.RemainingQueueDepth(stationId);

        void BuildSlots(Rig rig)
        {
            int depth = SlotCount(rig.StationId);
            float startX = -(depth - 1) * slotSpacing * 0.5f;
            for (int i = 0; i < depth; i++)
            {
                var slot = Instantiate(slotTemplate, rig.Widget.QueueRow);
                slot.transform.localPosition = new Vector3(startX + i * slotSpacing, 0f, 0f);
                slot.StationId = rig.StationId;
                slot.SlotIndex = i;
                rig.Slots.Add(slot);
            }
        }

        void Update()
        {
            if (_jobs == null) return;
            Reconcile(); // pick up runtime placements / demolishes before syncing state
            double now = Time.timeAsDouble;
            foreach (var rig in _rigs)
            {
                // Queue depth is resolved (upgrades fold in, §3), so it can change at runtime — rebuild the slot
                // row when it diverges from what's rendered. Polled like everything else here, so it self-heals
                // on a purchase or a reset with no event wiring. Producers only (non-producers keep 0 slots).
                // The count only moves when a LEVEL deepens the queue: buying a tier converts a locked slot to
                // an empty one without changing the total, which is the whole point of drawing to the ceiling.
                if (HasRecipes(rig.StationId) && SlotCount(rig.StationId) != rig.Slots.Count)
                    RebuildSlots(rig);

                bool panelOpen = rig.StationId == _openStationId;
                bool has = _jobs.TryGetHeadProgress(rig.StationId, now, out float fraction, out bool complete);
                bool running = has && !complete;
                // The radial is the glance-able working indicator when the panel is closed; with the panel open
                // the queue slots are visible and show the head's progress, so the radial steps aside (BUG-03).
                bool showRadial = running && !panelOpen;
                rig.Widget.SetRadialVisible(showRadial);
                if (showRadial) rig.Widget.SetRadialProgress(fraction);

                // A completed head is either collectable (ready) or refused for want of silo room (§4.4). The
                // two are mutually exclusive and read differently in-world: bouncing crop vs still warning.
                bool storageBlocked = _jobs.IsStorageBlocked(rig.StationId);
                bool ready = has && complete && !storageBlocked;
                rig.Widget.SetReady(ready);
                rig.Widget.SetStorageFull(storageBlocked);
                if (ready)
                {
                    var queue = _jobs.GetQueue(rig.StationId);
                    if (queue.Count > 0) rig.Widget.SetReadyIcon(OutputIcon(queue[0].RecipeId));
                }
                if (rig.Crops != null) DriveCrops(rig, has, complete, fraction);
                UpdateSlots(rig, now, panelOpen);
            }
        }

        /// Rise the field's crop cluster with the head job's growth: begin when a new crop starts, slide it each
        /// frame (full-grown while it sits ready), hide when the head clears. Only fields have a CropField.
        void DriveCrops(Rig rig, bool has, bool complete, float fraction)
        {
            Sprite sprite = has ? CropSpriteFor(_jobs.GetQueue(rig.StationId)[0].RecipeId) : null;
            if (sprite == null) // idle field, or a non-crop head (fallow) → nothing rising
            {
                if (rig.GrowingRecipeId != null) { rig.Crops.Hide(); rig.GrowingRecipeId = null; }
                return;
            }
            string recipeId = _jobs.GetQueue(rig.StationId)[0].RecipeId;
            if (rig.GrowingRecipeId != recipeId) { rig.Crops.Begin(sprite); rig.GrowingRecipeId = recipeId; }
            rig.Crops.Grow(complete ? 1f : fraction);
        }

        /// The world crop sprite a recipe grows: first output → its CropSO. Null if the output isn't a crop
        /// (a fallow field produces nothing that rises).
        Sprite CropSpriteFor(string recipeId)
        {
            var recipe = _catalog.Get(recipeId);
            if (recipe.Outputs.Count == 0) return null;
            return _resources.TryGetValue(recipe.Outputs[0].ResourceId, out var so) && so is CropSO crop
                ? crop.cropSprite : null;
        }

        /// Tear down and rebuild the slot row to the current resolved depth. Cheap and rare — only runs on the
        /// frame the depth actually changes (upgrade purchased, or reset drops it back to base).
        void RebuildSlots(Rig rig)
        {
            foreach (var slot in rig.Slots)
                if (slot != null) Destroy(slot.gameObject);
            rig.Slots.Clear();
            BuildSlots(rig);
        }

        /// Hang the row beneath the building *on screen*, not along a world axis.
        ///
        /// The row used to be authored at a fixed local offset down world -Z and laid out along world X. The
        /// camera is yawed (310°), so neither is a screen axis: the offset read as "shifted left" and the row
        /// itself as a diagonal staircase. Three slots hid it; the upgrade ceiling's five did not.
        ///
        /// So the row is framed against the camera every frame instead — offset towards the viewer along the
        /// ground, and yawed to match, which makes its local X the screen horizontal. Yaw only: the slots
        /// billboard themselves (pitch included), and pitching the row too would double-apply it. This can't
        /// be authored in the prefab because it depends on a camera that only exists at runtime.
        void PositionQueueRow(Rig rig)
        {
            var row = rig.Widget.QueueRow;
            var station = rig.Widget.transform; // the widget sits at the station root
            Vector3 groundForward = Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized;
            row.position = station.position - groundForward * rowDistance + Vector3.up * rowHeight;
            row.rotation = Quaternion.Euler(0f, _camera.transform.eulerAngles.y, 0f);
        }

        void UpdateSlots(Rig rig, double now, bool panelOpen)
        {
            if (rig.Slots.Count == 0) return;
            PositionQueueRow(rig);
            // The queue is a panel-open detail now (BUG-03): the row shows only while this station's panel is
            // open — the closed-panel "what's cooking" readout is the radial instead. Tap-to-cancel still works
            // because the slots are live exactly when the panel is open.
            var row = rig.Widget.QueueRow.gameObject;
            if (row.activeSelf != panelOpen) row.SetActive(panelOpen);
            if (!panelOpen) return;

            var queue = _jobs.GetQueue(rig.StationId);
            int depth = _jobs.QueueDepth(rig.StationId); // slots at or past this are the not-yet-bought tail
            // Only the head can be collectable (§4.4), and the SAME Core predicate Producer resolves the tap
            // with decides whether it pulses — so a slot never invites a tap that would cancel instead of
            // collect. A storage-blocked head stays plain Filled: it is done but has nowhere to go.
            bool headCollectable = _jobs.IsCollectionPossible(rig.StationId);
            for (int i = 0; i < rig.Slots.Count; i++)
            {
                var slot = rig.Slots[i];
                bool filled = i < queue.Count;
                slot.SetState(i >= depth ? QueueSlot.SlotState.Locked
                    : filled && i == 0 && headCollectable ? QueueSlot.SlotState.Ready
                    : filled ? QueueSlot.SlotState.Filled
                    : QueueSlot.SlotState.Empty);
                if (filled) slot.SetIcon(OutputIcon(queue[i].RecipeId));

                bool runningHead = filled && i == 0 && queue[i].State == JobState.Running;
                if (runningHead && _jobs.TryGetHeadProgress(rig.StationId, now, out float f, out bool complete))
                    slot.SetRunningProgress(true, complete ? 1f : f);
                else
                    slot.SetRunningProgress(false, 0f);
            }
        }
    }
}
