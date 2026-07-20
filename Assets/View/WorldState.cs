using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

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
        }

        JobSystem _jobs;
        RecipeCatalog _catalog;
        IReadOnlyDictionary<string, Transform> _stationRoots;
        readonly List<Rig> _rigs = new();

        public void Init(JobSystem jobs, RecipeCatalog catalog, IReadOnlyDictionary<string, Transform> stationRoots)
        {
            _jobs = jobs;
            _catalog = catalog;
            _stationRoots = stationRoots; // shared live map — StationRegistry adds/removes runtime stations here
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
                bool has = _jobs.TryGetHeadProgress(rig.StationId, now, out float fraction, out bool complete);
                bool running = has && !complete;
                rig.Widget.SetRunning(running);
                rig.Widget.SetReady(has && complete);
                if (running) rig.Widget.SetProgress(fraction);
                UpdateSlots(rig, now);
            }
        }

        void UpdateSlots(Rig rig, double now)
        {
            if (rig.Slots.Count == 0) return;
            var queue = _jobs.GetQueue(rig.StationId);
            bool anyJobs = queue.Count > 0; // the row shows only while the queue is non-empty (idle stays clean)
            var row = rig.Widget.QueueRow.gameObject;
            if (row.activeSelf != anyJobs) row.SetActive(anyJobs);
            if (!anyJobs) return;

            for (int i = 0; i < rig.Slots.Count; i++)
            {
                var slot = rig.Slots[i];
                bool filled = i < queue.Count;
                slot.SetFilled(filled);

                bool runningHead = filled && i == 0 && queue[i].State == JobState.Running;
                if (runningHead && _jobs.TryGetHeadProgress(rig.StationId, now, out float f, out bool complete))
                    slot.SetRunningProgress(true, complete ? 1f : f);
                else
                    slot.SetRunningProgress(false, 0f);
            }
        }
    }
}
