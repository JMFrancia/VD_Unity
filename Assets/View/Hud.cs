using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// The always-on HUD (§12.1): the money pill (top-right → popup.totalResources, mockup 27:2) and the
    /// debug button (top-left → menu.debug, mockup 22:2). The chrome is authored in the scene canvas; this
    /// component holds the wiring and syncs from Core events. Dynamic rows (totals list, per-resource cheat
    /// buttons) instantiate authored templates — count is data, look is prefab.
    public sealed class Hud : MonoBehaviour
    {
        [Header("Money pill")]
        [SerializeField] Button moneyButton;
        [SerializeField] Text moneyText;

        [Tooltip("The pill rect that pops as each coin lands. MoneyPill has no icon child — the '$' is part " +
                 "of the counter text — so the whole pill is what pulses.")]
        [SerializeField] RectTransform moneyPill;

        [SerializeField] float pulseScale = 1.18f;
        [SerializeField] float pulseSeconds = 0.18f;
        [SerializeField] Ease pulseEase = Ease.OutQuad;

        [Header("Gem pill (hud.gems)")]
        [SerializeField] Text gemText;

        [Header("Totals popup (§12.4, 27:2)")]
        [SerializeField] GameObject totalsPopup;
        [SerializeField] Transform totalsList;
        [SerializeField] Button totalsCloseButton;
        [SerializeField] ResourceRow resourceRowTemplate;

        [Header("Debug menu (§12.7, 22:2)")]
        [SerializeField] Button debugToggleButton;
        [SerializeField] GameObject debugMenu;
        [SerializeField] Transform cheatButtonList;
        [SerializeField] Button cheatButtonTemplate;
        [SerializeField] Button debugAddMoneyButton;
        [SerializeField] Button debugAddGemsButton;
        [SerializeField] Button debugDemolishButton; // M4: demolish the last-built station (player gesture deferred)
        [SerializeField] Button debugLevelUpButton;  // §12.7: grant exactly enough XP to cross the next threshold
        [SerializeField] Button debugResetButton;

        [Tooltip("Exact XP numbers behind hud.levelXp's bar. {0} level, {1} XP into it, {2} the level's span.")]
        [SerializeField] Text debugXpReadout;
        [SerializeField] string xpReadoutFormat = "L{0} · {1}/{2} xp";

        [Header("Cheat amounts")]
        [SerializeField] int cheatResourceAmount = 5;
        [SerializeField] int cheatMoneyAmount = 100;
        [SerializeField] int cheatGemAmount = 3;

        [Tooltip("Gem pill copy. {0} is the balance; the gem glyph is authored art beside this text.")]
        [SerializeField] string gemFormat = "{0}";

        EventBus _bus;
        ResourcePool _pool;
        Progression _progression;
        IReadOnlyList<ResourceSO> _resources; // display data (name + icon), stable config order

        int _trueMoney;    // what the Wallet actually holds
        int _pendingMoney; // earned but still in the air — the counter withholds this until the coins land

        public void Init(EventBus bus, ResourcePool pool, Progression progression,
            IReadOnlyList<ResourceSO> resources)
        {
            _bus = bus;
            _pool = pool;
            _progression = progression;
            _resources = resources;

            moneyButton.onClick.AddListener(ToggleTotals);
            totalsCloseButton.onClick.AddListener(() => SetTotals(false));
            debugToggleButton.onClick.AddListener(ToggleDebugMenu);
            debugAddMoneyButton.onClick.AddListener(() => _bus.Publish(new DebugAddMoneyRequested(cheatMoneyAmount)));
            debugAddMoneyButton.GetComponentInChildren<Text>().text = $"+${cheatMoneyAmount}";
            debugAddGemsButton.onClick.AddListener(() => _bus.Publish(new DebugAddGemsRequested(cheatGemAmount)));
            debugAddGemsButton.GetComponentInChildren<Text>().text = $"+{cheatGemAmount} gems";
            debugDemolishButton.onClick.AddListener(() => _bus.Publish(new DebugDemolishLastRequested()));
            debugLevelUpButton.onClick.AddListener(() => _bus.Publish(new DebugLevelUpRequested()));
            debugResetButton.onClick.AddListener(() => _bus.Publish(new DebugResetRequested()));
            BuildCheatButtons();

            totalsPopup.SetActive(false);
            debugMenu.SetActive(false);

            _bus.Subscribe<MoneyChanged>(OnMoneyChanged);
            _bus.Subscribe<EarnBurstLaunched>(OnEarnBurstLaunched);
            _bus.Subscribe<EarnParticleArrived>(OnEarnParticleArrived);
            _bus.Subscribe<GemsChanged>(e => gemText.text = string.Format(gemFormat, e.Total));
            _bus.Subscribe<ResourceChanged>(_ => { if (totalsPopup.activeSelf) RefreshTotals(); });
            _bus.Subscribe<GameReset>(_ => { if (totalsPopup.activeSelf) RefreshTotals(); });
            _bus.Subscribe<ExclusiveUiOpened>(e => // one menu at a time — totals + debug both retract for any other surface
            {
                if (e.Source != "debug") SetDebugMenu(false);
                if (e.Source != "totals") SetTotals(false);
            });

            _bus.Subscribe<XpGained>(_ => RefreshXpReadout());
            _bus.Subscribe<LevelUp>(_ => RefreshXpReadout());
            _bus.Subscribe<GameReset>(_ => RefreshXpReadout());
            RefreshXpReadout();
        }

        /// The three earn-particle handlers are the only ones here with teardown, because they are the only
        /// ones that were written as named methods. The other four are pre-existing lambdas and are left
        /// alone deliberately — see LOG.md.
        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<MoneyChanged>(OnMoneyChanged);
            _bus.Unsubscribe<EarnBurstLaunched>(OnEarnBurstLaunched);
            _bus.Unsubscribe<EarnParticleArrived>(OnEarnParticleArrived);
        }

        void OnMoneyChanged(MoneyChanged e)
        {
            _trueMoney = e.Total;
            RefreshMoney();
        }

        /// A burst has left: withhold the whole payout, so the counter does NOT jump to the new total.
        void OnEarnBurstLaunched(EarnBurstLaunched e)
        {
            if (e.Kind != EarnKind.Money) return;
            _pendingMoney += e.Amount;
            RefreshMoney();
        }

        /// A coin landed: release its chunk and pop the pill. The counter climbs one coin at a time and lands
        /// on the exact payout when the last one arrives.
        void OnEarnParticleArrived(EarnParticleArrived e)
        {
            if (e.Kind != EarnKind.Money) return;
            _pendingMoney -= e.Amount;
            RefreshMoney();
            Pulse();
        }

        void RefreshMoney() => moneyText.text = $"$ {_trueMoney - _pendingMoney}";

        void Pulse()
        {
            moneyPill.DOKill();
            moneyPill.localScale = Vector3.one;
            moneyPill.DOScale(pulseScale, pulseSeconds * 0.5f)
                .SetEase(pulseEase)
                .SetLoops(2, LoopType.Yoyo);
        }

        /// The bar shows progress; this shows the numbers behind it — the pair is what makes a threshold
        /// tuning pass checkable without instrumenting anything.
        void RefreshXpReadout() => debugXpReadout.text = string.Format(xpReadoutFormat,
            _progression.PlayerLevel, _progression.XpIntoLevel, _progression.XpSpanOfLevel);

        void ToggleDebugMenu() => SetDebugMenu(!debugMenu.activeSelf);

        /// Both HUD popups go through one setter each so open and close are announced on the real transition
        /// only — the exclusivity handler closes them blind, and a repeat close must stay silent.
        void SetDebugMenu(bool show)
        {
            if (debugMenu.activeSelf == show) return;
            debugMenu.SetActive(show);
            if (show) _bus.Publish(new ExclusiveUiOpened("debug")); // retract the build menu / panels
            else _bus.Publish(new ExclusiveUiClosed("debug"));
        }

        void BuildCheatButtons()
        {
            foreach (var r in _resources)
            {
                var button = Instantiate(cheatButtonTemplate, cheatButtonList);
                button.GetComponentInChildren<Text>().text = $"+{cheatResourceAmount}  {r.displayName}";
                string id = r.id;
                button.onClick.AddListener(() =>
                    _bus.Publish(new DebugAddResourceRequested(id, cheatResourceAmount)));
            }
        }

        void ToggleTotals() => SetTotals(!totalsPopup.activeSelf);

        void SetTotals(bool show)
        {
            if (totalsPopup.activeSelf == show) return;
            totalsPopup.SetActive(show);
            if (show)
            {
                _bus.Publish(new ExclusiveUiOpened("totals")); // retract the order board / build menu / other panels
                RefreshTotals();
            }
            else _bus.Publish(new ExclusiveUiClosed("totals"));
        }

        void RefreshTotals()
        {
            for (int i = totalsList.childCount - 1; i >= 0; i--)
                Destroy(totalsList.GetChild(i).gameObject);
            foreach (var r in _resources)
                Instantiate(resourceRowTemplate, totalsList).Bind(r.displayName, r.icon, _pool.Get(r.id));
        }
    }
}
