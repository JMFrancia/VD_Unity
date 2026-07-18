using System;
using System.Collections.Generic;
using VoidDay.Core.Events;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// The generic Producer, in Core (§4.1, §4.4). One state machine drives every station's job queue —
    /// buildings and fields differ only by data (station type → recipes). Owns the core friction:
    /// **a completed job's output blocks the queue until collected.** Timing, blocking, consumption, and the
    /// generic IsCollectionPossible predicate all live here; the Systems layer only pumps Tick and routes
    /// intents, the View only renders. Timers are absolute timestamps (§13).
    public sealed class JobSystem
    {
        private sealed class Station
        {
            public readonly string Id;
            public readonly string StationType;
            public readonly int QueueDepthBase;
            public readonly List<Job> Queue = new();
            public Station(string id, string stationType, int queueDepthBase)
            {
                Id = id;
                StationType = stationType;
                QueueDepthBase = queueDepthBase;
            }
        }

        private readonly Dictionary<string, Station> _stations = new();
        private readonly EventBus _bus;
        private readonly ResourcePool _pool;
        private readonly RecipeCatalog _catalog;
        private readonly ValueResolver _resolver;

        public JobSystem(EventBus bus, ResourcePool pool, RecipeCatalog catalog, ValueResolver resolver)
        {
            _bus = bus;
            _pool = pool;
            _catalog = catalog;
            _resolver = resolver;
        }

        public void Register(string stationId, string stationType, int queueDepthBase)
        {
            if (_stations.ContainsKey(stationId))
                throw new InvalidOperationException($"Station '{stationId}' already registered");
            _stations[stationId] = new Station(stationId, stationType, queueDepthBase);
        }

        // ---- Queries (the View reads these to render; reading Core state is not a rule) ----

        public IReadOnlyList<Job> GetQueue(string stationId) => GetStation(stationId).Queue;

        public string StationTypeOf(string stationId) => GetStation(stationId).StationType;

        public int QueueDepth(string stationId)
        {
            var s = GetStation(stationId);
            return (int)_resolver.Resolve(s.QueueDepthBase, ResolveKind.QueueDepth, new ResolveContext(stationId));
        }

        /// Generic collection predicate (§4.5, 00-summary gotcha). A tap collects iff this is true, else it
        /// opens the panel. M7 adds "&& storage has room" as another false-reason WITHOUT touching the tap
        /// branch — that is the whole point of routing tap-resolution through one predicate.
        public bool IsCollectionPossible(string stationId)
        {
            var s = GetStation(stationId);
            return s.Queue.Count > 0 && s.Queue[0].State == JobState.Complete;
        }

        public bool TryGetHeadProgress(string stationId, double now, out float fraction, out bool complete)
        {
            fraction = 0f;
            complete = false;
            var s = GetStation(stationId);
            if (s.Queue.Count == 0) return false;
            var head = s.Queue[0];
            if (head.State == JobState.Complete) { fraction = 1f; complete = true; return true; }
            if (head.State != JobState.Running) return false;
            double span = head.EndTime - head.StartTime;
            fraction = span <= 0 ? 1f : (float)((now - head.StartTime) / span);
            if (fraction < 0f) fraction = 0f;
            if (fraction > 1f) fraction = 1f;
            return true;
        }

        // ---- Commands (the Systems layer calls these to translate input intents) ----

        public void QueueJob(string stationId, string recipeId, double now)
        {
            var s = GetStation(stationId);
            var recipe = _catalog.Get(recipeId);
            if (recipe.StationType != s.StationType)
                throw new InvalidOperationException($"Recipe '{recipeId}' ({recipe.StationType}) not valid at '{stationId}' ({s.StationType})");
            if (s.Queue.Count >= QueueDepth(stationId))
                throw new InvalidOperationException($"Queue full at '{stationId}'");

            var inputs = Effective(recipe.Inputs, ResolveKind.InputCost, stationId);
            if (!_pool.CanAfford(inputs))
                throw new InvalidOperationException($"Cannot afford recipe '{recipeId}' — inputs consumed at queue time (§4.4)");

            _pool.Consume(inputs);                          // inputs consumed at queue time (§4.4)
            var job = new Job(recipeId, inputs);
            s.Queue.Add(job);
            _bus.Publish(new JobQueued(stationId, recipeId, s.Queue.Count - 1));
            TryStartHead(s, now);
        }

        public void CancelJob(string stationId, int jobIndex, double now)
        {
            var s = GetStation(stationId);
            if (jobIndex < 0 || jobIndex >= s.Queue.Count)
                throw new InvalidOperationException($"No job at index {jobIndex} on '{stationId}'");
            var job = s.Queue[jobIndex];

            // Full refund only if not yet started; none once running/complete (§4.4).
            if (job.State == JobState.Queued)
                foreach (var c in job.ConsumedInputs) _pool.Add(c.ResourceId, c.Amount);

            s.Queue.RemoveAt(jobIndex);
            _bus.Publish(new JobCancelled(stationId, jobIndex));
            if (jobIndex == 0) TryStartHead(s, now);        // head gone → the next job may start
        }

        public void Collect(string stationId, double now, bool byPet)
        {
            if (!IsCollectionPossible(stationId))
                throw new InvalidOperationException($"Nothing to collect at '{stationId}'");
            var s = GetStation(stationId);
            var head = s.Queue[0];
            foreach (var o in head.Outputs) _pool.Add(o.ResourceId, o.Amount);
            s.Queue.RemoveAt(0);
            _bus.Publish(new JobCollected(stationId, head.Outputs, byPet));
            TryStartHead(s, now);                           // collection unblocks the queue
        }

        /// Advance every running head that has reached its EndTime. This is the only place time drives state;
        /// the Systems layer pumps it with an absolute timestamp each frame.
        public void Tick(double now)
        {
            foreach (var s in _stations.Values)
            {
                if (s.Queue.Count == 0) continue;
                var head = s.Queue[0];
                if (head.State == JobState.Running && now >= head.EndTime)
                    Complete(s, head, now);
            }
        }

        public void ResetAll()
        {
            foreach (var s in _stations.Values) s.Queue.Clear();
            _bus.Publish(new GameReset());
        }

        // ---- Internal ----

        private void TryStartHead(Station s, double now)
        {
            if (s.Queue.Count == 0) return;
            var head = s.Queue[0];
            if (head.State != JobState.Queued) return;       // already running, or complete and blocking

            var recipe = _catalog.Get(head.RecipeId);
            float duration = _resolver.Resolve(recipe.Duration, ResolveKind.RecipeDuration, new ResolveContext(s.Id));
            head.State = JobState.Running;
            head.StartTime = now;
            head.EndTime = duration <= 0f ? now : now + duration;
            _bus.Publish(new JobStarted(s.Id));
            if (duration <= 0f) Complete(s, head, now);      // instant recipe still produces a collectable (§5.2)
        }

        private void Complete(Station s, Job head, double now)
        {
            head.State = JobState.Complete;
            var recipe = _catalog.Get(head.RecipeId);
            head.Outputs = Effective(recipe.Outputs, ResolveKind.OutputQuantity, s.Id);
            _bus.Publish(new JobCompleted(s.Id, head.Outputs));
            _bus.Publish(new StationBlocked(s.Id, "output-uncollected"));
        }

        /// Resolve each amount through the value seam (00-summary decision #2). Passthrough in M2.
        private List<ResourceAmount> Effective(IReadOnlyList<ResourceAmount> src, ResolveKind kind, string stationId)
        {
            var list = new List<ResourceAmount>(src.Count);
            foreach (var a in src)
            {
                int amount = (int)Math.Round(_resolver.Resolve(a.Amount, kind, new ResolveContext(stationId, a.ResourceId)));
                list.Add(new ResourceAmount(a.ResourceId, amount));
            }
            return list;
        }

        private Station GetStation(string stationId) =>
            _stations.TryGetValue(stationId, out var s)
                ? s
                : throw new InvalidOperationException($"No station registered with id '{stationId}'");
    }
}
