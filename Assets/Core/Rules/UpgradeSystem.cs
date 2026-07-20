using System;
using System.Collections.Generic;
using VoidDay.Core.Events;
using VoidDay.Core.Model;

namespace VoidDay.Core.Rules
{
    /// Station upgrades (§8), and the M5 source of active Effects (§3). It owns two things: the per-station
    /// purchased tier of each track, and — as an IEffectSource — the resolve of which of those tiers' effects
    /// are currently active for a given station. Buying a tier charges the wallet, bumps the tier, and
    /// announces effects:recalculated so views/systems re-read resolved values.
    ///
    /// Scope this milestone is passive, own-station only: a track bought on Field#0 affects only Field#0
    /// (§3.2 "at the upgrade's own station"). Global/order/build scopes and triggered effects arrive in later
    /// milestones and slot into Collect without touching the resolver or its call sites.
    public sealed class UpgradeSystem : IEffectSource
    {
        private readonly EventBus _bus;
        private readonly Wallet _wallet;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<UpgradeTrackModel>> _tracksByType;

        // Per station instance: its type, and how many tiers of each track it has bought.
        private readonly Dictionary<string, string> _typeOf = new();
        private readonly Dictionary<string, Dictionary<string, int>> _levels = new();

        private static readonly IReadOnlyList<UpgradeTrackModel> NoTracks = Array.Empty<UpgradeTrackModel>();

        public UpgradeSystem(EventBus bus, Wallet wallet,
            IReadOnlyDictionary<string, IReadOnlyList<UpgradeTrackModel>> tracksByType)
        {
            _bus = bus;
            _wallet = wallet;
            _tracksByType = tracksByType;
        }

        // ---- Lifecycle (Systems layer registers instances as they are placed/demolished) ----

        public void Register(string stationId, string stationType)
        {
            _typeOf[stationId] = stationType;
            if (!_levels.ContainsKey(stationId)) _levels[stationId] = new Dictionary<string, int>();
        }

        public void Unregister(string stationId)
        {
            _typeOf.Remove(stationId);
            _levels.Remove(stationId);
        }

        /// Debug reset (§13): every station drops back to tier 0. Registrations stay — the stations still exist.
        public void ResetLevels()
        {
            foreach (var kv in _levels) kv.Value.Clear();
            _bus.Publish(new EffectsRecalculated());
        }

        // ---- Queries (the panel reads these to render the upgrade rows) ----

        public IReadOnlyList<UpgradeTrackModel> TracksFor(string stationId) =>
            _typeOf.TryGetValue(stationId, out var type) && _tracksByType.TryGetValue(type, out var tracks)
                ? tracks
                : NoTracks;

        public int TierOf(string stationId, string trackId) =>
            _levels.TryGetValue(stationId, out var lv) && lv.TryGetValue(trackId, out var n) ? n : 0;

        public bool IsMaxed(string stationId, string trackId, UpgradeTrackModel track) =>
            TierOf(stationId, trackId) >= track.MaxTier;

        /// The tier that a Buy would purchase next, or null if maxed.
        public UpgradeTierModel NextTier(string stationId, UpgradeTrackModel track)
        {
            int tier = TierOf(stationId, track.Id);
            return tier >= track.MaxTier ? null : track.Tiers[tier];
        }

        // ---- Command ----

        public void Purchase(string stationId, string trackId)
        {
            var track = FindTrack(stationId, trackId);
            var next = NextTier(stationId, track);
            if (next == null)
                throw new InvalidOperationException($"Upgrade '{trackId}' on '{stationId}' is already maxed");
            if (_wallet.Money < next.Cost)
                throw new InvalidOperationException(
                    $"Cannot afford upgrade '{trackId}': costs {next.Cost}, have {_wallet.Money}");

            _wallet.Add(-next.Cost);
            _levels[stationId][trackId] = TierOf(stationId, trackId) + 1;
            _bus.Publish(new EffectsRecalculated());
        }

        // ---- IEffectSource: the active passive effects for a station (own-station scope only in M5) ----

        public void Collect(EffectType type, in ResolveContext ctx, List<Effect> into)
        {
            if (ctx.StationId == null) return; // own-station only this milestone; global sources arrive in M6
            if (!_levels.TryGetValue(ctx.StationId, out var levels)) return;

            foreach (var track in TracksFor(ctx.StationId))
            {
                int tier = levels.TryGetValue(track.Id, out var n) ? n : 0;
                for (int t = 0; t < tier; t++)
                    foreach (var effect in track.Tiers[t].Effects)
                        if (effect.type == type && effect.trigger == TriggerType.None && ResourceMatches(effect, ctx))
                            into.Add(effect);
            }
        }

        // "" scopes to every resource; else only the named one (§3.2). A null ctx resource (e.g. queue depth,
        // which has no resource) matches only an all-resources effect.
        private static bool ResourceMatches(Effect e, in ResolveContext ctx) =>
            string.IsNullOrEmpty(e.resource) || e.resource == ctx.ResourceId;

        private UpgradeTrackModel FindTrack(string stationId, string trackId)
        {
            foreach (var track in TracksFor(stationId))
                if (track.Id == trackId) return track;
            throw new InvalidOperationException($"No upgrade track '{trackId}' available at '{stationId}'");
        }
    }
}
