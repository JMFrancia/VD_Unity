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
    /// All effects here are passive (TriggerType.None); triggers arrive in M9. Reach is per effect TYPE, via
    /// EffectScopes: a station.* track bought on Field#0 affects only Field#0 (§3.2 "at the upgrade's own
    /// station"), while a global-scoped one (storage.cap from the Silo, M7; global.speed from the Workshop)
    /// applies everywhere regardless of which station sold it.
    public sealed class UpgradeSystem : IEffectSource
    {
        private readonly EventBus _bus;
        private readonly Wallet _wallet;
        private readonly IReadOnlyDictionary<string, IReadOnlyList<UpgradeTrackModel>> _tracksByType;
        private readonly Func<int> _playerLevel;

        // Per station instance: its type, and how many tiers of each track it has bought.
        private readonly Dictionary<string, string> _typeOf = new();
        private readonly Dictionary<string, Dictionary<string, int>> _levels = new();

        private static readonly IReadOnlyList<UpgradeTrackModel> NoTracks = Array.Empty<UpgradeTrackModel>();

        public UpgradeSystem(EventBus bus, Wallet wallet,
            IReadOnlyDictionary<string, IReadOnlyList<UpgradeTrackModel>> tracksByType, Func<int> playerLevel)
        {
            _bus = bus;
            _wallet = wallet;
            _tracksByType = tracksByType;
            _playerLevel = playerLevel;
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

        /// A track the player has not reached the level for yet (§9). The panel renders it visible-but-locked
        /// so the gate is something to play toward, not something that appears from nowhere at level-up.
        public bool IsLocked(UpgradeTrackModel track) => track.UnlockLevel > _playerLevel();

        /// The tier that a Buy would purchase next, or null if maxed.
        public UpgradeTierModel NextTier(string stationId, UpgradeTrackModel track)
        {
            int tier = TierOf(stationId, track.Id);
            return tier >= track.MaxTier ? null : track.Tiers[tier];
        }

        /// How much MORE queue depth this station's unpurchased upgrade tiers would grant (§8). The in-world
        /// slot row draws `QueueDepth + this` slots and renders the tail locked, so the depth an upgrade would
        /// buy is visible as an empty promise rather than appearing from nowhere on purchase.
        ///
        /// Deliberately only the upgrade ceiling — not level grants. A locked slot means "buyable at this
        /// station", which is what the player can act on; folding in every future level grant would draw a row
        /// of squares nobody can reach for hours. Flat-only, because a whole slot is the only thing a slot row
        /// can draw — a Pct/Mult depth tier would have to go through the resolver's pool, and if one is ever
        /// authored the ceiling shown here would silently under-count it.
        public int RemainingQueueDepth(string stationId)
        {
            if (!_levels.TryGetValue(stationId, out var levels)) return 0;
            int remaining = 0;
            foreach (var track in TracksFor(stationId))
            {
                int purchased = levels.TryGetValue(track.Id, out var n) ? n : 0;
                for (int t = purchased; t < track.MaxTier; t++)
                    foreach (var effect in track.Tiers[t].Effects)
                        if (effect.type == EffectType.StationQueueDepth
                            && effect.trigger == TriggerType.None
                            && effect.value.op == EffectOp.Flat)
                            remaining += (int)effect.value.amount;
            }
            return remaining;
        }

        // ---- Command ----

        public void Purchase(string stationId, string trackId)
        {
            var track = FindTrack(stationId, trackId);
            if (IsLocked(track))
                throw new InvalidOperationException(
                    $"Upgrade '{trackId}' unlocks at level {track.UnlockLevel}, player is level {_playerLevel()}");
            var next = NextTier(stationId, track);
            if (next == null)
                throw new InvalidOperationException($"Upgrade '{trackId}' on '{stationId}' is already maxed");
            if (_wallet.Money < next.Cost)
                throw new InvalidOperationException(
                    $"Cannot afford upgrade '{trackId}': costs {next.Cost}, have {_wallet.Money}");

            _wallet.Add(-next.Cost);
            int tier = TierOf(stationId, trackId) + 1;
            _levels[stationId][trackId] = tier;
            _bus.Publish(new UpgradePurchased(stationId, trackId, tier, next.Cost));
            _bus.Publish(new EffectsRecalculated());
        }

        // ---- IEffectSource: the active passive effects that apply to a resolve ----

        /// Which purchased tiers contribute is decided by the effect TYPE's scope (§3.2), not by which building
        /// sold the upgrade. An own-station effect contributes only to its own station's resolves; a global one
        /// contributes to every resolve, station-scoped or not — which is how the Silo's storage.cap reaches a
        /// station-less cap read (M7) and how a Workshop's global.speed would reach a station's job timer.
        public void Collect(EffectType type, in ResolveContext ctx, List<Effect> into)
        {
            switch (EffectScopes.Of(type))
            {
                case EffectScope.OwnStation:
                    if (ctx.StationId != null) CollectFrom(ctx.StationId, type, ctx, into);
                    break;

                case EffectScope.Global:
                    foreach (var stationId in _levels.Keys) CollectFrom(stationId, type, ctx, into);
                    break;

                // local.* / pet.* need grid + pet positions to measure range against; upgrades carry neither.
                // Declared vocabulary until M10 — no upgrade authors them, so nothing is silently dropped.
                default:
                    break;
            }
        }

        private void CollectFrom(string stationId, EffectType type, in ResolveContext ctx, List<Effect> into)
        {
            if (!_levels.TryGetValue(stationId, out var levels)) return;

            foreach (var track in TracksFor(stationId))
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
