using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// panel.orderBoard (mockup 14:2) — a centered modal listing one card per order slot. Opens when the
    /// tapped station is the Order Board; publishes the fulfill/skip intents and never acts on them.
    ///
    /// Both this and StationPanel listen to the same StationPanelRequested and self-select on station type:
    /// this one owns the Order Board, StationPanel owns anything with recipes. One routing event, no
    /// System-side table of which panel belongs to which building.
    public sealed class OrderBoardPanel : MonoBehaviour
    {
        [Header("Chrome (authored)")]
        [SerializeField] GameObject panelRoot;
        [SerializeField] Transform slotList;
        [SerializeField] OrderCard cardTemplate;
        [SerializeField] Button closeButton;

        [Header("Which station opens this")]
        [SerializeField] StationSO orderBoardStation;

        EventBus _bus;
        OrderBoard _board;
        ResourcePool _pool;
        JobSystem _jobs;
        IReadOnlyDictionary<string, string> _resourceNames;

        readonly List<OrderCard> _cards = new();
        bool _open;

        public void Init(EventBus bus, OrderBoard board, ResourcePool pool, JobSystem jobs,
            IReadOnlyDictionary<string, string> resourceNames)
        {
            _bus = bus;
            _board = board;
            _pool = pool;
            _jobs = jobs;
            _resourceNames = resourceNames;

            closeButton.onClick.AddListener(Close);
            panelRoot.SetActive(false);

            _bus.Subscribe<StationPanelRequested>(OnPanelRequested);
            _bus.Subscribe<BackgroundTapped>(_ => Close());
            _bus.Subscribe<GameReset>(_ => Close());
            _bus.Subscribe<OrderGenerated>(_ => RebuildIfOpen());
            _bus.Subscribe<OrderFulfilled>(_ => RebuildIfOpen());
            _bus.Subscribe<OrderSkipped>(_ => RebuildIfOpen());
            _bus.Subscribe<ResourceChanged>(_ => RebuildIfOpen()); // a Fill may have become (un)affordable
        }

        void OnPanelRequested(StationPanelRequested e)
        {
            // Tapping any other station closes this one — panels are one-at-a-time (see StationPanel).
            if (_jobs.StationTypeOf(e.StationId) != orderBoardStation.stationType) { Close(); return; }
            _open = true;
            panelRoot.SetActive(true);
            Rebuild();
        }

        void Close()
        {
            _open = false;
            panelRoot.SetActive(false);
        }

        void RebuildIfOpen()
        {
            if (_open) Rebuild();
        }

        /// The refill countdown is the one thing that changes with no event behind it, so an open board
        /// re-renders each frame. Cheap at three cards, and it keeps the timer honest.
        void Update()
        {
            if (_open) Rebuild();
        }

        void Rebuild()
        {
            SyncCardCount(_board.VisibleSlotCount);

            for (int slot = 0; slot < _cards.Count; slot++)
            {
                var order = _board.OrderAt(slot);
                if (order == null)
                {
                    _cards[slot].BindRefilling(_board.RefillRemaining(slot, Time.timeAsDouble));
                    continue;
                }

                string orderId = order.Id;
                _cards[slot].BindOrder(order, _pool.All, _resourceNames,
                    onFill: () => _bus.Publish(new OrderFulfillRequested(orderId)),
                    onSkip: () => _bus.Publish(new OrderSkipRequested(orderId)));
            }
        }

        /// Slot count only ever grows (M6/M8), so instantiate up to it and reuse. Rebuilding the whole list
        /// every frame would destroy the card the player is mid-tap on.
        void SyncCardCount(int wanted)
        {
            while (_cards.Count < wanted)
                _cards.Add(Instantiate(cardTemplate, slotList));
        }
    }
}
