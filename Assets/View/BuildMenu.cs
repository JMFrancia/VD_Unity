using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// menu.build (§12.3, mockup 20:2). A tray of every station type, opened from the hammer button. Each
    /// entry's state — available / locked / cap-reached / can't-afford — is recomputed from Core state
    /// (player level, per-type count vs cap, money vs cost) whenever anything that could change it fires.
    /// The tray retracts the instant a placement drag starts (§12.2). The chrome is authored in the scene
    /// canvas; entries instantiate an authored template (count is data, look is prefab).
    ///
    /// The unlock:granted listener is written now so the menu re-evaluates lock state when a level-up grants
    /// an unlock — but nothing fires that event until M8 (the lock display is static this milestone).
    public sealed class BuildMenu : MonoBehaviour
    {
        const string SourceId = "build"; // this surface's id in the one-menu-at-a-time protocol

        [SerializeField] Button toggleButton;      // the hammer build button
        [SerializeField] GameObject tray;
        [SerializeField] Transform entryList;
        [SerializeField] BuildMenuEntry entryTemplate;
        [SerializeField] PlacementController placement;
        [SerializeField] UiThemeSO theme;

        [Header("Build button placement (§12.2 — bottom-left closed, above the tray open)")]
        [Tooltip("Anchored position of the hammer button while the tray is closed (bottom-left).")]
        [SerializeField] Vector2 buttonClosedPosition = new Vector2(40f, 40f);
        [Tooltip("Anchored position of the hammer button while the tray is open (lifted above the tray).")]
        [SerializeField] Vector2 buttonOpenPosition = new Vector2(40f, 415f);

        EventBus _bus;
        BuildSystem _build;
        Wallet _wallet;
        Func<int> _playerLevel;
        RectTransform _buttonRect;

        readonly List<Entry> _entries = new();

        sealed class Entry
        {
            public string StationType;
            public int UnlockLevel;
            public BuildMenuEntry View;
        }

        public void Init(EventBus bus, BuildSystem build, Wallet wallet, Func<int> playerLevel,
            IReadOnlyList<StationSO> roster)
        {
            _bus = bus;
            _build = build;
            _wallet = wallet;
            _playerLevel = playerLevel;

            foreach (var so in roster)
            {
                var view = Instantiate(entryTemplate, entryList);
                view.Bind(placement, so.stationType, so.displayName, so.buildThumbnail);
                _entries.Add(new Entry { StationType = so.stationType, UnlockLevel = so.unlockLevel, View = view });
            }

            _buttonRect = toggleButton.GetComponent<RectTransform>();
            toggleButton.onClick.AddListener(() => SetTrayOpen(!tray.activeSelf));
            SetTrayOpen(false);

            _bus.Subscribe<MoneyChanged>(OnMoneyChanged);
            _bus.Subscribe<StationBuilt>(OnStationBuilt);
            _bus.Subscribe<StationDemolished>(OnStationDemolished);
            _bus.Subscribe<UnlockGranted>(OnUnlockGranted);   // dormant until M8 fires it
            _bus.Subscribe<GameReset>(OnGameReset);
            _bus.Subscribe<PlacementActiveChanged>(OnPlacementActiveChanged); // retract the tray on drag
            _bus.Subscribe<ExclusiveUiOpened>(OnExclusiveUiOpened);           // another menu opened → retract

            Refresh();
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<MoneyChanged>(OnMoneyChanged);
            _bus.Unsubscribe<StationBuilt>(OnStationBuilt);
            _bus.Unsubscribe<StationDemolished>(OnStationDemolished);
            _bus.Unsubscribe<UnlockGranted>(OnUnlockGranted);
            _bus.Unsubscribe<GameReset>(OnGameReset);
            _bus.Unsubscribe<PlacementActiveChanged>(OnPlacementActiveChanged);
            _bus.Unsubscribe<ExclusiveUiOpened>(OnExclusiveUiOpened);
        }

        /// Open/close the tray, reposition the hammer button (bottom-left ↔ above the tray), and — on open —
        /// announce it so the other exclusive menus retract.
        void SetTrayOpen(bool open)
        {
            tray.SetActive(open);
            _buttonRect.anchoredPosition = open ? buttonOpenPosition : buttonClosedPosition;
            if (open) _bus.Publish(new ExclusiveUiOpened(SourceId));
        }

        void OnMoneyChanged(MoneyChanged _) => Refresh();
        void OnStationBuilt(StationBuilt _) => Refresh();
        void OnStationDemolished(StationDemolished _) => Refresh();
        void OnUnlockGranted(UnlockGranted _) => Refresh();
        void OnGameReset(GameReset _) => Refresh();
        void OnPlacementActiveChanged(PlacementActiveChanged e) { if (e.Active) SetTrayOpen(false); }
        void OnExclusiveUiOpened(ExclusiveUiOpened e) { if (e.Source != SourceId && tray.activeSelf) SetTrayOpen(false); }

        void Refresh()
        {
            int level = _playerLevel();
            foreach (var e in _entries)
            {
                int cost = _build.BuildCost(e.StationType);
                int cap = _build.Cap(e.StationType);
                int count = _build.CountOf(e.StationType);

                BuildMenuEntry.State state =
                    e.UnlockLevel > level ? BuildMenuEntry.State.Locked
                    : count >= cap ? BuildMenuEntry.State.CapReached
                    : _wallet.Money < cost ? BuildMenuEntry.State.CantAfford
                    : BuildMenuEntry.State.Available;

                e.View.Apply(state, cost, count, cap, e.UnlockLevel, theme);
            }
        }
    }
}
