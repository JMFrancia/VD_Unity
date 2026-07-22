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
        /// One station mid-build. Timers are absolute timestamps (§13), exactly as JobSystem's are, so the
        /// View can render a countdown by asking rather than by keeping its own clock.
        private sealed class Site
        {
            public readonly string Id;
            public readonly double StartTime;
            public double EndTime; // not readonly: a gem skip pulls it to `now` and lets Tick finish normally
            public Site(string id, double startTime, double endTime)
            {
                Id = id;
                StartTime = startTime;
                EndTime = endTime;
            }
        }

        private readonly Dictionary<string, Site> _sites = new();
        private readonly List<string> _finished = new(); // scratch, so Tick doesn't mutate _sites mid-iteration

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
        ///
        /// Placing does not build: the station enters the grid flagged UnderConstruction, which is what makes
        /// the cell occupied and the cap honest immediately, and Tick finishes it when its timer expires. The
        /// money is charged here, at placement — otherwise a player could queue five builds they cannot afford.
        public void Place(string stationType, GridCoord cell, double now)
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
            var model = new StationModel(id, stationType, type.DisplayName, type.Width, type.Height)
            {
                UnderConstruction = true
            };
            _grid.Add(cell, model);

            float duration = type.BuildSeconds;
            _sites[id] = new Site(id, now, duration <= 0f ? now : now + duration);
            _bus.Publish(new StationConstructionStarted(id, stationType, cell, duration));

            // An instant type still goes through the site path, so the same event pair fires in the same order
            // whether the timer is 0 or 60 — nothing downstream needs a special case (§5.2 does this for
            // zero-duration recipes for the same reason).
            if (duration <= 0f) Complete(id);
        }

        /// Finish every site whose timer has expired. The only place time turns a construction site into a
        /// station; the Systems layer pumps it with an absolute timestamp each frame, as it does JobSystem.
        public void Tick(double now)
        {
            if (_sites.Count == 0) return;

            _finished.Clear();
            foreach (var kv in _sites)
                if (now >= kv.Value.EndTime) _finished.Add(kv.Key);

            foreach (var id in _finished) Complete(id);
        }

        /// Progress of a station still under construction, for the View's countdown. False once it is built —
        /// the same shape as JobSystem.TryGetHeadProgress, so a timer renders from either without caring which.
        public bool TryGetSiteProgress(string stationId, double now, out float fraction, out float secondsRemaining)
        {
            fraction = 0f;
            secondsRemaining = 0f;
            if (!_sites.TryGetValue(stationId, out var site)) return false;

            double span = site.EndTime - site.StartTime;
            fraction = span <= 0 ? 1f : (float)((now - site.StartTime) / span);
            if (fraction < 0f) fraction = 0f;
            if (fraction > 1f) fraction = 1f;
            secondsRemaining = (float)(site.EndTime - now);
            if (secondsRemaining < 0f) secondsRemaining = 0f;
            return true;
        }

        /// Seconds left on this station's construction site, or **-1 when no site exists** — the station is
        /// already built, or was never placed. Same negative-sentinel shape as JobSystem.HeadSecondsRemaining,
        /// so TimeSkip prices either timer through one call.
        public float SiteSecondsRemaining(string stationId, double now) =>
            _sites.TryGetValue(stationId, out var site) ? (float)(site.EndTime - now) : -1f;

        /// Finish a construction site early (the gem sink, §13). A timestamp nudge only: the next Tick runs
        /// Complete on its normal path, so a skipped build publishes the same StationBuilt a natural one does.
        public void SkipSite(string stationId, double now)
        {
            if (!_sites.TryGetValue(stationId, out var site))
                throw new InvalidOperationException($"No construction site to skip at '{stationId}'");
            site.EndTime = now;
        }

        /// The construction site becomes the station: it stops being under construction, gains a job queue, and
        /// announces itself with the same station:built every listener already reacts to.
        private void Complete(string stationId)
        {
            _sites.Remove(stationId);
            var cell = CellOf(stationId);
            _grid.TryGet(cell, out var model);
            model.UnderConstruction = false;

            _jobs.Register(stationId, model.StationType, Type(model.StationType).QueueDepthBase);
            _lastBuiltId = stationId;
            _bus.Publish(new StationBuilt(stationId, model.StationType, cell));
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
