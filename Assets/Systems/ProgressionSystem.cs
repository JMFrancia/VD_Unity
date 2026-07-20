using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

namespace VoidDay.Systems
{
    /// Turns domain events into XP awards (§9). Collecting a job pays a flat per-action amount from
    /// XpConfigSO; fulfilling an order pays the XP the order itself carries, derived from what it requested.
    ///
    /// This is why the Order Board never touches progression state — it announces order:fulfilled and this
    /// listener decides that XP is owed. M9 adds egg grants the same way, without editing the board.
    public sealed class ProgressionSystem : MonoBehaviour
    {
        EventBus _bus;
        Progression _progression;
        XpConfigModel _xpConfig;

        public void Init(EventBus bus, Progression progression, XpConfigModel xpConfig)
        {
            _bus = bus;
            _progression = progression;
            _xpConfig = xpConfig;

            _bus.Subscribe<JobCollected>(OnJobCollected);
            _bus.Subscribe<OrderFulfilled>(OnOrderFulfilled);
            _bus.Subscribe<StationBuilt>(OnStationBuilt);
            _bus.Subscribe<GameReset>(OnGameReset);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<JobCollected>(OnJobCollected);
            _bus.Unsubscribe<OrderFulfilled>(OnOrderFulfilled);
            _bus.Unsubscribe<StationBuilt>(OnStationBuilt);
            _bus.Unsubscribe<GameReset>(OnGameReset);
        }

        void OnJobCollected(JobCollected _) => _progression.AwardXp(_xpConfig.PerJobCollected, "job");

        void OnOrderFulfilled(OrderFulfilled e) => _progression.AwardXp(e.Xp, "order");

        void OnStationBuilt(StationBuilt _) => _progression.AwardXp(_xpConfig.PerStationBuilt, "build");

        void OnGameReset(GameReset _) => _progression.Reset();
    }
}
