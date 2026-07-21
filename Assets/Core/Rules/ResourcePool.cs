using System;
using System.Collections.Generic;
using VoidDay.Core.Events;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// The global pool of resource counts (§4.2, §5.1). A pool of numbers, not per-station storage.
    /// Emits resource:changed on every delta so the View can sync.
    ///
    /// Storage (§7) is ONE SHARED CAPACITY across every good, Hay Day's silo model: 40 wheat and 10 corn fill
    /// a 50 pool exactly as 50 wheat would. There are no per-resource caps — hoarding any one good squeezes
    /// every other, and that pressure is the point.
    ///
    /// Capacity gates COLLECTION, not addition: Add stays dumb and never clamps, because §4.4 forbids
    /// destroying output. HasRoomFor is what the collection predicate consults, so a full pool blocks the
    /// station instead of silently eating the job's yield.
    public sealed class ResourcePool
    {
        private readonly Dictionary<string, int> _counts = new();
        private readonly EventBus _bus;
        private readonly ValueResolver _resolver;
        private int _baseCapacity;

        public ResourcePool(EventBus bus, ValueResolver resolver)
        {
            _bus = bus;
            _resolver = resolver;
        }

        public int Get(string id) => _counts.TryGetValue(id, out var n) ? n : 0;

        public IReadOnlyDictionary<string, int> All => _counts;

        /// Boot sets the authored starting capacity (GameConfigSO). A pool left at 0 is uncapped — only
        /// reachable from a headless test that never set one.
        public void SetBaseCapacity(int capacity) => _baseCapacity = capacity;

        /// Everything the player is holding, summed across goods — what fills the silo.
        public int TotalStored
        {
            get
            {
                int total = 0;
                foreach (var n in _counts.Values) total += n;
                return total;
            }
        }

        /// The live capacity: the authored base with every storage.cap effect folded in (the Silo track).
        /// Read through the seam, so buying a tier raises it with no cache to invalidate. No resource in the
        /// context — one pool, so a storage.cap effect narrowed to a single good would be meaningless.
        public int Capacity =>
            (int)_resolver.Resolve(_baseCapacity, ResolveKind.StorageCap, new ResolveContext(null));

        public bool IsUncapped => _baseCapacity <= 0;

        public bool HasRoomFor(int amount) => IsUncapped || TotalStored + amount <= Capacity;

        /// Does this job's whole output fit? All-or-nothing (§4.4) — output lands together or not at all.
        public bool HasRoomForAll(IReadOnlyList<ResourceAmount> outputs)
        {
            int incoming = 0;
            foreach (var o in outputs) incoming += o.Amount;
            return HasRoomFor(incoming);
        }

        public bool CanAfford(IReadOnlyList<ResourceAmount> inputs)
        {
            foreach (var i in inputs)
                if (Get(i.ResourceId) < i.Amount) return false;
            return true;
        }

        /// Fail loud: a negative total is a bug upstream (inputs must be checked before consuming), not a
        /// state to clamp past.
        public void Add(string id, int delta)
        {
            int total = Get(id) + delta;
            if (total < 0)
                throw new InvalidOperationException($"Resource '{id}' would go negative ({total}) — afford-check missing?");
            _counts[id] = total;
            _bus.Publish(new ResourceChanged(id, delta, total));
        }

        public void Consume(IReadOnlyList<ResourceAmount> inputs)
        {
            foreach (var i in inputs) Add(i.ResourceId, -i.Amount);
        }

        /// Debug reset (§13): drive every known resource back to its starting count, emitting the delta for
        /// each that actually changed so the HUD/popup resync through the normal channel.
        public void Reset(IReadOnlyDictionary<string, int> startingCounts)
        {
            var ids = new HashSet<string>(_counts.Keys);
            foreach (var k in startingCounts.Keys) ids.Add(k);
            foreach (var id in ids)
            {
                startingCounts.TryGetValue(id, out int target);
                int delta = target - Get(id);
                if (delta != 0) Add(id, delta);
            }
        }
    }
}
