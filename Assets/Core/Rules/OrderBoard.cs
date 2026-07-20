using System;
using System.Collections.Generic;
using VoidDay.Core.Events;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// The order board's state and rules (§6) — the game's only cash source (§16). Owns the slots, the
    /// refill timers, and what fulfilling or skipping does. Timers are absolute timestamps (§13), same as
    /// the job queue: a slot stores the moment it refills, not a countdown that has to be ticked down.
    ///
    /// Orders never expire — Tick only ever *fills* an empty slot, it never empties a full one.
    public sealed class OrderBoard
    {
        private sealed class Slot
        {
            public OrderModel Order;
            public double RefillAt;
        }

        private readonly List<Slot> _slots = new();
        private readonly EventBus _bus;
        private readonly ResourcePool _pool;
        private readonly Wallet _wallet;
        private readonly OrderGeneration _generation;
        private readonly OrderConfigModel _config;
        private readonly ValueResolver _resolver;
        private readonly Func<IReadOnlyCollection<string>> _producibleIds;
        private readonly Func<int> _playerLevel;

        public OrderBoard(EventBus bus, ResourcePool pool, Wallet wallet, OrderGeneration generation,
            OrderConfigModel config, ValueResolver resolver,
            Func<IReadOnlyCollection<string>> producibleIds, Func<int> playerLevel)
        {
            _bus = bus;
            _pool = pool;
            _wallet = wallet;
            _generation = generation;
            _config = config;
            _resolver = resolver;
            _producibleIds = producibleIds;
            _playerLevel = playerLevel;
        }

        /// Slot count through the seam, so M6's order.slots effect and M8's level-raise extend the board
        /// instead of rewriting it. Read every tick — the board grows the moment the resolved value does.
        public int SlotCount =>
            (int)_resolver.Resolve(_config.SlotCount, ResolveKind.OrderSlots, new ResolveContext(null));

        // ---- Queries (the View renders from these) ----

        public int VisibleSlotCount => Math.Max(SlotCount, _slots.Count);

        public OrderModel OrderAt(int slot) => slot < _slots.Count ? _slots[slot].Order : null;

        /// Seconds until an empty slot refills. Meaningless for a filled slot — the View checks OrderAt first.
        public float RefillRemaining(int slot, double now) =>
            slot < _slots.Count ? (float)Math.Max(0d, _slots[slot].RefillAt - now) : 0f;

        // ---- Ticking ----

        /// Fills any empty slot whose refill moment has passed. Also grows the board when SlotCount rises;
        /// a slot born this way starts empty and refilling, so a new slot still costs the player the timer.
        public void Tick(double now)
        {
            while (_slots.Count < SlotCount)
                _slots.Add(new Slot { RefillAt = now });

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Order != null || now < slot.RefillAt) continue;
                Fill(i, now);
            }
        }

        void Fill(int index, double now)
        {
            var order = _generation.Generate(_producibleIds(), _playerLevel());
            _slots[index].Order = order;
            _bus.Publish(new OrderGenerated(index, order));
            _bus.Publish(new OrderSlotRefilled(index));
        }

        // ---- Player actions ----

        /// Fulfilling consumes the requested goods and pays cash (§6). The XP ride on order:fulfilled —
        /// Progression listens and awards it, so the board never reaches into the progression state.
        public void Fulfill(string orderId, double now)
        {
            int index = IndexOf(orderId);
            var order = _slots[index].Order;

            if (!_pool.CanAfford(order.Requests))
                throw new InvalidOperationException(
                    $"Order '{orderId}' fulfilled without the goods in hand — the Fill control should have been disabled");

            _pool.Consume(order.Requests);
            _wallet.Add(order.Cash);
            Clear(index, now);
            _bus.Publish(new OrderFulfilled(orderId, order.Cash, order.Xp));
        }

        /// Skipping is free and takes no confirm (§6); the slot immediately begins refilling.
        public void Skip(string orderId, double now)
        {
            int index = IndexOf(orderId);
            Clear(index, now);
            _bus.Publish(new OrderSkipped(orderId));
        }

        void Clear(int index, double now)
        {
            _slots[index].Order = null;
            _slots[index].RefillAt = now + _config.RefillSeconds;
        }

        int IndexOf(string orderId)
        {
            for (int i = 0; i < _slots.Count; i++)
                if (_slots[i].Order != null && _slots[i].Order.Id == orderId) return i;
            throw new InvalidOperationException($"No order with id '{orderId}' is on the board");
        }

        /// Debug reset (§13): clear the board and let the next tick refill it immediately.
        public void Reset(double now)
        {
            foreach (var slot in _slots)
            {
                slot.Order = null;
                slot.RefillAt = now;
            }
        }
    }
}
