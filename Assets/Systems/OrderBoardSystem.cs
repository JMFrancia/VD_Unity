using UnityEngine;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;

namespace VoidDay.Systems
{
    /// Drives the Core OrderBoard: pumps Tick with an absolute timestamp and translates the order intents
    /// into Core calls. Holds no rule — refill timing, payout, and what fulfilling costs all live in Core.
    ///
    /// Named *System to avoid colliding with VoidDay.Core.Rules.OrderBoard, which is the thing it drives.
    public sealed class OrderBoardSystem : MonoBehaviour
    {
        EventBus _bus;
        OrderBoard _board;
        Wallet _wallet;

        public void Init(EventBus bus, OrderBoard board, Wallet wallet)
        {
            _bus = bus;
            _board = board;
            _wallet = wallet;

            _bus.Subscribe<OrderFulfillRequested>(OnFulfillRequested);
            _bus.Subscribe<OrderSkipRequested>(OnSkipRequested);
            _bus.Subscribe<DebugAddMoneyRequested>(OnDebugAddMoney);
            _bus.Subscribe<GameReset>(OnGameReset);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<OrderFulfillRequested>(OnFulfillRequested);
            _bus.Unsubscribe<OrderSkipRequested>(OnSkipRequested);
            _bus.Unsubscribe<DebugAddMoneyRequested>(OnDebugAddMoney);
            _bus.Unsubscribe<GameReset>(OnGameReset);
        }

        void Update()
        {
            if (_board == null) return;
            _board.Tick(Time.timeAsDouble); // absolute timestamps (§13), same clock as the job queue
        }

        void OnFulfillRequested(OrderFulfillRequested e) => _board.Fulfill(e.OrderId, Time.timeAsDouble);

        void OnSkipRequested(OrderSkipRequested e) => _board.Skip(e.OrderId, Time.timeAsDouble);

        void OnGameReset(GameReset _) => _board.Reset(Time.timeAsDouble);

        void OnDebugAddMoney(DebugAddMoneyRequested e) => _wallet.Add(e.Amount);
    }
}
