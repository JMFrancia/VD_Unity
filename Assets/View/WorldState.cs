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
        IReadOnlyDictionary<string, ResourceSO> _resources; // id → display data; the crop icon on slots + ready indicator
        IReadOnlyDictionary<string, Transform> _stationRoots;
        readonly List<Rig> _rigs = new();
        string _openStationId; // whose panel is open — its queue shows and its radial hides (BUG-03 design)

        public void Init(EventBus bus, JobSystem jobs, RecipeCatalog catalog,
            IReadOnlyDictionary<string, ResourceSO> resources, IReadOnlyDictionary<string, Transform> stationRoots)
        {
            _jobs = jobs;
            _catalog = catalog;
            _resources = resources;
            _stationRoots = stationRoots; // shared live map — StationRegistry adds/removes runtime stations here
            bus.Subscribe<StationPanelOpened>(e => _openStationId = e.StationId);
            bus.Subscribe<StationPanelClosed>(_ => _openStationId = null);
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

        void BuildSlots(Rig rig)
        {
            int depth = _jobs.QueueDepth(rig.StationId);
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
                if (HasRecipes(rig.StationId) && _jobs.QueueDepth(rig.StationId) != rig.Slots.Count)
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

        void UpdateSlots(Rig rig, double now, bool panelOpen)
        {
            if (rig.Slots.Count == 0) return;
            // The queue is a panel-open detail now (BUG-03): the row shows only while this station's panel is
            // open — the closed-panel "what's cooking" readout is the radial instead. Tap-to-cancel still works
            // because the slots are live exactly when the panel is open.
            var row = rig.Widget.QueueRow.gameObject;
            if (row.activeSelf != panelOpen) row.SetActive(panelOpen);
            if (!panelOpen) return;

            var queue = _jobs.GetQueue(rig.StationId);
            for (int i = 0; i < rig.Slots.Count; i++)
            {
                var slot = rig.Slots[i];
                bool filled = i < queue.Count;
                slot.SetFilled(filled);
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
