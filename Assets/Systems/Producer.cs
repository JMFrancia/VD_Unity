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
            _bus.Subscribe<JobCancelRequested>(OnJobCancelRequested);
            _bus.Subscribe<DebugAddResourceRequested>(OnDebugAddResource);
            _bus.Subscribe<DebugResetRequested>(OnDebugReset);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<StationTapped>(OnStationTapped);
            _bus.Unsubscribe<JobQueueRequested>(OnJobQueueRequested);
            _bus.Unsubscribe<JobCancelRequested>(OnJobCancelRequested);
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

        void OnJobCancelRequested(JobCancelRequested e) =>
            _jobs.CancelJob(e.StationId, e.JobIndex, Time.timeAsDouble);

        void OnDebugAddResource(DebugAddResourceRequested e) =>
            _pool.Add(e.ResourceId, e.Amount);

        void OnDebugReset(DebugResetRequested _)
        {
            _jobs.ResetAll();                 // clears queues + emits game:reset
            _pool.Reset(_startingCounts);     // back to 1 wheat, 1 corn (emits resource:changed)
            _wallet.Reset();
        }
    }
}
