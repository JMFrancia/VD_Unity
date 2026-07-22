using System.Collections.Generic;
using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;

namespace VoidDay.Systems
{
    /// The System that drives the Core Producer (CLAUDE.md layer note). It holds no game rule — it pumps
    /// Tick with an absolute timestamp and translates input intents into Core calls. The rules (timing,
    /// blocking, consumption, the collection predicate) all live in JobSystem (Core).
    ///
    /// Tap-resolution (§4.5) is the one branch that lives here, evaluated through the generic
    /// IsCollectionPossible predicate: a tap either collects, or resolves to a panel-open request. Because
    /// this is the single authority, the panel View just listens — it never re-derives the predicate.
    public sealed class Producer : MonoBehaviour
    {
        EventBus _bus;
        JobSystem _jobs;
        ResourcePool _pool;
        Wallet _wallet;
        IReadOnlyDictionary<string, int> _startingCounts;

        public void Init(EventBus bus, JobSystem jobs, ResourcePool pool, Wallet wallet,
            IReadOnlyDictionary<string, int> startingCounts)
        {
            _bus = bus;
            _jobs = jobs;
            _pool = pool;
            _wallet = wallet;
            _startingCounts = startingCounts;

            _bus.Subscribe<StationTapped>(OnStationTapped);
            _bus.Subscribe<JobQueueRequested>(OnJobQueueRequested);
            _bus.Subscribe<QueueSlotTapped>(OnQueueSlotTapped);
            _bus.Subscribe<DebugAddResourceRequested>(OnDebugAddResource);
            _bus.Subscribe<DebugResetRequested>(OnDebugReset);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<StationTapped>(OnStationTapped);
            _bus.Unsubscribe<JobQueueRequested>(OnJobQueueRequested);
            _bus.Unsubscribe<QueueSlotTapped>(OnQueueSlotTapped);
            _bus.Unsubscribe<DebugAddResourceRequested>(OnDebugAddResource);
            _bus.Unsubscribe<DebugResetRequested>(OnDebugReset);
        }

        void Update()
        {
            if (_jobs == null) return;
            _jobs.Tick(Time.timeAsDouble); // absolute timestamp (§13)
        }

        void OnStationTapped(StationTapped e)
        {
            if (_jobs.IsCollectionPossible(e.StationId))
                _jobs.Collect(e.StationId, Time.timeAsDouble, byPet: false);
            else
                _bus.Publish(new StationPanelRequested(e.StationId));
        }

        void OnJobQueueRequested(JobQueueRequested e) =>
            _jobs.QueueJob(e.StationId, e.RecipeId, Time.timeAsDouble);

        /// The second tap-resolution branch (§4.5), by the same predicate as the station tap: a tap on the
        /// READY head collects it, any other slot cancels its job. Only the head can ever be collectable, so
        /// the slot-0 test and IsCollectionPossible agree by construction — but both are asserted rather than
        /// assumed, because "collect" and "destroy the output for no refund" are one index apart.
        void OnQueueSlotTapped(QueueSlotTapped e)
        {
            if (e.SlotIndex == 0 && _jobs.IsCollectionPossible(e.StationId))
            {
                _jobs.Collect(e.StationId, Time.timeAsDouble, byPet: false);
                return;
            }

            // A finished head with nowhere to put its output is REFUSED, never cancelled. The player asked to
            // collect; binning the output for no refund (§4.4) is the opposite of what they meant, and the two
            // are one predicate apart. Announce the refusal and let the toast/flash/sound answer it.
            if (e.SlotIndex == 0 && _jobs.IsStorageBlocked(e.StationId))
            {
                _bus.Publish(new CollectRefused(e.StationId, "storage-full"));
                return;
            }

            _jobs.CancelJob(e.StationId, e.SlotIndex, Time.timeAsDouble);
        }

        void OnDebugAddResource(DebugAddResourceRequested e) =>
            _pool.Add(e.ResourceId, e.Amount);

        void OnDebugReset(DebugResetRequested _)
        {
            _jobs.ResetAll();                 // clears queues + emits game:reset
            _pool.Reset(_startingCounts);     // back to 1 wheat, 1 corn (emits resource:changed)
            _wallet.Reset();                  // the gem purse is TimeSkipSystem's to reset, not ours
        }
    }
}
