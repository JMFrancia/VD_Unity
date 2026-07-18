using System;
using System.Collections.Generic;
using VoidDay.Core.Events;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// The global pool of resource counts (§4.2, §5.1). A pool of numbers, not per-station storage.
    /// Uncapped in M2 (caps arrive in M7). Emits resource:changed on every delta so the View can sync.
    public sealed class ResourcePool
    {
        private readonly Dictionary<string, int> _counts = new();
        private readonly EventBus _bus;

        public ResourcePool(EventBus bus) => _bus = bus;

        public int Get(string id) => _counts.TryGetValue(id, out var n) ? n : 0;

        public IReadOnlyDictionary<string, int> All => _counts;

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
