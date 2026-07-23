using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
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

        [Header("Build button reveal (hidden on L1 — nothing is buildable yet — pops in on level-up)")]
        [Tooltip("The button stays hidden below this level, then animates in when the player first reaches it.")]
        [SerializeField] int buildButtonRevealLevel = 2;
        [SerializeField] float revealSeconds = 0.4f;
        [SerializeField] Ease revealEase = Ease.OutBack;

        EventBus _bus;
        BuildSystem _build;
        Wallet _wallet;
        Func<int> _playerLevel;
        RectTransform _buttonRect;
        bool _buttonRevealed;

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

            // Tray reads left→right in unlock order (§12.3), so newly-unlocked stations append to the right of
            // ones the player already has. OrderBy is stable, so same-unlock types keep their roster order.
            foreach (var so in roster.OrderBy(s => s.unlockLevel))
            {
                if (!so.buildable) continue;

                var view = Instantiate(entryTemplate, entryList);
                view.Bind(placement, so.stationType, so.displayName, so.buildThumbnail);
                _entries.Add(new Entry { StationType = so.stationType, UnlockLevel = so.unlockLevel, View = view });
            }

            _buttonRect = toggleButton.GetComponent<RectTransform>();
            toggleButton.onClick.AddListener(() => SetTrayOpen(!tray.activeSelf));
            SetTrayOpen(false);
            ApplyButtonReveal();

            _bus.Subscribe<MoneyChanged>(OnMoneyChanged);
            _bus.Subscribe<StationConstructionStarted>(OnConstructionStarted);
            _bus.Subscribe<StationBuilt>(OnStationBuilt);
            _bus.Subscribe<StationDemolished>(OnStationDemolished);
            _bus.Subscribe<UnlockGranted>(OnUnlockGranted);   // dormant until M8 fires it
            _bus.Subscribe<LevelUp>(OnLevelUp);               // reveal the build button when the player reaches the gate level
            _bus.Subscribe<GameReset>(OnGameReset);
            _bus.Subscribe<PlacementActiveChanged>(OnPlacementActiveChanged); // retract the tray on drag
            _bus.Subscribe<ExclusiveUiOpened>(OnExclusiveUiOpened);           // another menu opened → retract

            Refresh();
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<MoneyChanged>(OnMoneyChanged);
            _bus.Unsubscribe<StationConstructionStarted>(OnConstructionStarted);
            _bus.Unsubscribe<StationBuilt>(OnStationBuilt);
            _bus.Unsubscribe<StationDemolished>(OnStationDemolished);
            _bus.Unsubscribe<UnlockGranted>(OnUnlockGranted);
            _bus.Unsubscribe<LevelUp>(OnLevelUp);
            _bus.Unsubscribe<GameReset>(OnGameReset);
            _bus.Unsubscribe<PlacementActiveChanged>(OnPlacementActiveChanged);
            _bus.Unsubscribe<ExclusiveUiOpened>(OnExclusiveUiOpened);
        }

        /// Open/close the tray, reposition the hammer button (bottom-left ↔ above the tray), and — on open —
        /// announce it so the other exclusive menus retract.
        void SetTrayOpen(bool open)
        {
            bool was = tray.activeSelf;
            tray.SetActive(open);
            _buttonRect.anchoredPosition = open ? buttonOpenPosition : buttonClosedPosition;
            if (open) _bus.Publish(new ExclusiveUiOpened(SourceId));
            else if (was) _bus.Publish(new ExclusiveUiClosed(SourceId)); // real transition only — Init closes an already-closed tray
        }

        void OnMoneyChanged(MoneyChanged _) => Refresh();
        // A site counts against the cap the moment it is placed, and a zero-cost type fires no MoneyChanged
        // to trigger the refresh on its own.
        void OnConstructionStarted(StationConstructionStarted _) => Refresh();
        void OnStationBuilt(StationBuilt _) => Refresh();
        void OnStationDemolished(StationDemolished _) => Refresh();
        void OnUnlockGranted(UnlockGranted _) => Refresh();

        // Pop the hammer button in the first time the player crosses the reveal level. LevelUp fires once per
        // level crossed and carries the authoritative new level, so we trust e.Level (not a possibly-lagging
        // _playerLevel()) and guard on _buttonRevealed to keep the animation to a single play.
        void OnLevelUp(LevelUp e)
        {
            if (_buttonRevealed || e.Level < buildButtonRevealLevel) return;
            RevealButton(instant: false);
        }

        // Snap the button to the correct state for the current level (boot / reset — no animation).
        void ApplyButtonReveal()
        {
            if (_playerLevel() >= buildButtonRevealLevel) RevealButton(instant: true);
            else HideButton();
        }

        void RevealButton(bool instant)
        {
            _buttonRevealed = true;
            _buttonRect.DOKill();
            toggleButton.gameObject.SetActive(true);
            if (instant) _buttonRect.localScale = Vector3.one;
            else
            {
                _buttonRect.localScale = Vector3.zero;
                _buttonRect.DOScale(1f, revealSeconds).SetEase(revealEase);
            }
        }

        void HideButton()
        {
            _buttonRevealed = false;
            _buttonRect.DOKill();
            toggleButton.gameObject.SetActive(false);
        }

        void OnGameReset(GameReset _)
        {
            ApplyButtonReveal(); // back to L1 → button hidden again, no animation
            Refresh();
        }
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
