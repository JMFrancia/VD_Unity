using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;

namespace VoidDay.Systems
{
    /// Drives the Core UpgradeSystem (CLAUDE.md layer note). It translates the panel's purchase intent into a
    /// Core call, and keeps the Core's per-instance registration in sync with the world by listening to the
    /// station-lifecycle facts — a station built at runtime gets its upgrade tracks, a demolished one drops
    /// them. It holds no rule: the charge, the tier bump, and the effect resolve all live in Core.
    public sealed class UpgradesSystem : MonoBehaviour
    {
        EventBus _bus;
        UpgradeSystem _upgrades;

        public void Init(EventBus bus, UpgradeSystem upgrades)
        {
            _bus = bus;
            _upgrades = upgrades;

            _bus.Subscribe<UpgradePurchaseRequested>(OnPurchase);
            _bus.Subscribe<StationBuilt>(OnStationBuilt);
            _bus.Subscribe<StationDemolished>(OnStationDemolished);
            _bus.Subscribe<GameReset>(OnGameReset);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<UpgradePurchaseRequested>(OnPurchase);
            _bus.Unsubscribe<StationBuilt>(OnStationBuilt);
            _bus.Unsubscribe<StationDemolished>(OnStationDemolished);
            _bus.Unsubscribe<GameReset>(OnGameReset);
        }

        void OnPurchase(UpgradePurchaseRequested e) => _upgrades.Purchase(e.StationId, e.UpgradeId);
        void OnStationBuilt(StationBuilt e) => _upgrades.Register(e.StationId, e.StationType);
        void OnStationDemolished(StationDemolished e) => _upgrades.Unregister(e.StationId);
        void OnGameReset(GameReset _) => _upgrades.ResetLevels();
    }
}
