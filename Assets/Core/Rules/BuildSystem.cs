using System;
using System.Collections.Generic;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.World;

namespace VoidDay.Core.Rules
{
    /// The build/demolish/move rules (§4.3), pure C#. Owns grid occupancy, per-type caps, the money cost of
    /// building and the refund on demolish, and job-queue registration for the placed instance — all the
    /// things that decide *whether* a station may be placed and *what it costs*. The Systems/View layers only
    /// preview validity (the ghost tint) and translate the drop into a PlaceRequested; this is the single
    /// authority that charges money and mutates the world. Announces station:built/moved/demolished; the
    /// prefab-instantiating StationRegistry and the XP-awarding Progression just listen.
    ///
    /// Cost and cap are read through the value seam (ResolveKind.BuildCost / StationCap) so M6's build.cost
    /// effect and M8's level-raised caps extend the read site instead of reopening this code.
    public sealed class BuildSystem
    {
        private readonly EventBus _bus;
        private readonly StationGrid _grid;
        private readonly JobSystem _jobs;
        private readonly Wallet _wallet;
        private readonly ValueResolver _resolver;
        private readonly IReadOnlyDictionary<string, StationTypeModel> _types;
        private readonly Func<int> _playerLevel;
        private readonly float _refundPercent;

        private int _nextInstance;
        private string _lastBuiltId;

        public BuildSystem(EventBus bus, StationGrid grid, JobSystem jobs, Wallet wallet, ValueResolver resolver,
            IReadOnlyDictionary<string, StationTypeModel> types, Func<int> playerLevel, float refundPercent)
        {
            _bus = bus;
            _grid = grid;
            _jobs = jobs;
            _wallet = wallet;
            _resolver = resolver;
            _types = types;
            _playerLevel = playerLevel;
            _refundPercent = refundPercent;
        }

        /// Register a scene-authored, pre-placed station (CLAUDE.md rule 4) into the grid + producer at boot.
        /// No money, no cap check, no event — it is already placed; this just teaches the Core it exists.
        public void RegisterPreplaced(string id, string stationType, GridCoord cell)
        {
            var type = Type(stationType);
            _grid.Add(cell, new StationModel(id, stationType, type.DisplayName, type.Width, type.Height));
            _jobs.Register(id, stationType, type.QueueDepthBase);
        }

        /// Resolved (seam) money cost to build one of a type — the build menu reads this to show cost / afford.
        public int BuildCost(string stationType) =>
            (int)_resolver.Resolve(Type(stationType).BuildCostBase, ResolveKind.BuildCost, new ResolveContext(null));

        /// Resolved (seam) per-type cap — the build menu reads this for the owned/cap badge. The cap belongs
        /// to the TYPE (no instance exists yet), so the type is what the context carries.
        public int Cap(string stationType) =>
            (int)_resolver.Resolve(Type(stationType).CapBase, ResolveKind.StationCap,
                new ResolveContext(null, stationType: stationType));

        /// Count of currently-placed stations of a type (across pre-placed + runtime). Drives the cap badge.
        public int CountOf(string stationType)
        {
            int n = 0;
            foreach (var kv in _grid.All)
                if (kv.Value.StationType == stationType) n++;
            return n;
        }

        /// Place a new station. The View only emits PlaceRequested for a valid, affordable, unlocked, under-cap
        /// cell, so any violation here is a contract breach (a bug), not a normal outcome — hence fail-loud.
        public void Place(string stationType, GridCoord cell)
        {
            var type = Type(stationType);
            if (type.UnlockLevel > _playerLevel())
                throw new InvalidOperationException(
                    $"Station type '{stationType}' unlocks at level {type.UnlockLevel}, player is level {_playerLevel()}");
            if (!_grid.InBounds(cell))
                throw new InvalidOperationException($"Cannot place '{stationType}' at {cell}: outside the grid");
            if (_grid.IsOccupied(cell))
                throw new InvalidOperationException($"Cannot place '{stationType}' at {cell}: cell is occupied");

            int cap = Cap(stationType);
            if (CountOf(stationType) >= cap)
                throw new InvalidOperationException($"Cannot place '{stationType}': cap {cap} reached");

            int cost = BuildCost(stationType);
            if (_wallet.Money < cost)
                throw new InvalidOperationException($"Cannot afford '{stationType}': costs {cost}, have {_wallet.Money}");

            _wallet.Add(-cost);
            string id = $"{stationType}#{_nextInstance++}";
            _grid.Add(cell, new StationModel(id, stationType, type.DisplayName, type.Width, type.Height));
            _jobs.Register(id, stationType, type.QueueDepthBase);
            _lastBuiltId = id;
            _bus.Publish(new StationBuilt(id, stationType, cell));
        }

        /// Move a placed station to another cell. Free (§4.3). Same-cell drop is a no-op.
        public void Move(string stationId, GridCoord cell)
        {
            var current = CellOf(stationId);
            if (cell.Equals(current)) return;
            if (!_grid.InBounds(cell))
                throw new InvalidOperationException($"Cannot move '{stationId}' to {cell}: outside the grid");
            if (_grid.IsOccupied(cell))
                throw new InvalidOperationException($"Cannot move '{stationId}' to {cell}: cell is occupied");

            _grid.TryGet(current, out var model);
            _grid.Remove(current);
            _grid.Add(cell, model);
            _bus.Publish(new StationMoved(stationId, cell));
        }

        /// Demolish a placed station: 50% refund of its build cost (§4.3), remove from grid + producer.
        public void Demolish(string stationId)
        {
            var cell = CellOf(stationId);
            _grid.TryGet(cell, out var model);
            int refund = (int)Math.Floor(BuildCost(model.StationType) * _refundPercent);

            _grid.Remove(cell);
            _jobs.Unregister(stationId);
            if (refund != 0) _wallet.Add(refund);
            if (_lastBuiltId == stationId) _lastBuiltId = null;
            _bus.Publish(new StationDemolished(stationId));
        }

        /// Debug-only M4 affordance: demolish the most-recently-built station, if one still stands.
        public void DemolishLast()
        {
            if (_lastBuiltId != null) Demolish(_lastBuiltId);
        }

        private GridCoord CellOf(string stationId)
        {
            foreach (var kv in _grid.All)
                if (kv.Value.Id == stationId) return kv.Key;
            throw new InvalidOperationException($"No placed station with id '{stationId}'");
        }

        private StationTypeModel Type(string stationType) =>
            _types.TryGetValue(stationType, out var t)
                ? t
                : throw new InvalidOperationException($"Unknown station type '{stationType}'");
    }
}
