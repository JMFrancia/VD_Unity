using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;

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
        [SerializeField] Button debugDemolishButton; // M4: demolish the last-built station (player gesture deferred)
        [SerializeField] Button debugResetButton;

        [Tooltip("XP has no HUD until M8 — this debug line is how it is verified meanwhile.")]
        [SerializeField] Text debugXpReadout;

        [Header("Cheat amounts")]
        [SerializeField] int cheatResourceAmount = 5;
        [SerializeField] int cheatMoneyAmount = 100;

        EventBus _bus;
        ResourcePool _pool;
        Progression _progression;
        IReadOnlyList<KeyValuePair<string, string>> _resources; // id → display name, stable order

        public void Init(EventBus bus, ResourcePool pool, Progression progression,
            IReadOnlyList<KeyValuePair<string, string>> resources)
        {
            _bus = bus;
            _pool = pool;
            _progression = progression;
            _resources = resources;

            moneyButton.onClick.AddListener(ToggleTotals);
            totalsCloseButton.onClick.AddListener(() => totalsPopup.SetActive(false));
            debugToggleButton.onClick.AddListener(ToggleDebugMenu);
            debugAddMoneyButton.onClick.AddListener(() => _bus.Publish(new DebugAddMoneyRequested(cheatMoneyAmount)));
            debugAddMoneyButton.GetComponentInChildren<Text>().text = $"+${cheatMoneyAmount}";
            debugDemolishButton.onClick.AddListener(() => _bus.Publish(new DebugDemolishLastRequested()));
            debugResetButton.onClick.AddListener(() => _bus.Publish(new DebugResetRequested()));
            BuildCheatButtons();

            totalsPopup.SetActive(false);
            debugMenu.SetActive(false);

            _bus.Subscribe<MoneyChanged>(e => moneyText.text = $"$ {e.Total}");
            _bus.Subscribe<ResourceChanged>(_ => { if (totalsPopup.activeSelf) RefreshTotals(); });
            _bus.Subscribe<GameReset>(_ => { if (totalsPopup.activeSelf) RefreshTotals(); });
            _bus.Subscribe<ExclusiveUiOpened>(e => { if (e.Source != "debug") debugMenu.SetActive(false); }); // one menu at a time

            // XP is invisible infrastructure until M8 (level/XP milestone) — hide every XP surface for now.
            debugXpReadout.gameObject.SetActive(false);
        }

        void ToggleDebugMenu()
        {
            bool show = !debugMenu.activeSelf;
            debugMenu.SetActive(show);
            if (show) _bus.Publish(new ExclusiveUiOpened("debug")); // retract the build menu / panels
        }

        void BuildCheatButtons()
        {
            foreach (var r in _resources)
            {
                var button = Instantiate(cheatButtonTemplate, cheatButtonList);
                button.GetComponentInChildren<Text>().text = $"+{cheatResourceAmount}  {r.Value}";
                string id = r.Key;
                button.onClick.AddListener(() =>
                    _bus.Publish(new DebugAddResourceRequested(id, cheatResourceAmount)));
            }
        }

        void ToggleTotals()
        {
            bool show = !totalsPopup.activeSelf;
            totalsPopup.SetActive(show);
            if (show) RefreshTotals();
        }

        void RefreshTotals()
        {
            for (int i = totalsList.childCount - 1; i >= 0; i--)
                Destroy(totalsList.GetChild(i).gameObject);
            foreach (var r in _resources)
                Instantiate(resourceRowTemplate, totalsList).Bind(r.Value, _pool.Get(r.Key));
        }
    }
}
