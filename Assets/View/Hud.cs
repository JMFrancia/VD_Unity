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
        [SerializeField] Button debugResetButton;

        EventBus _bus;
        ResourcePool _pool;
        IReadOnlyList<KeyValuePair<string, string>> _resources; // id → display name, stable order

        public void Init(EventBus bus, ResourcePool pool, IReadOnlyList<KeyValuePair<string, string>> resources)
        {
            _bus = bus;
            _pool = pool;
            _resources = resources;

            moneyButton.onClick.AddListener(ToggleTotals);
            totalsCloseButton.onClick.AddListener(() => totalsPopup.SetActive(false));
            debugToggleButton.onClick.AddListener(() => debugMenu.SetActive(!debugMenu.activeSelf));
            debugResetButton.onClick.AddListener(() => _bus.Publish(new DebugResetRequested()));
            BuildCheatButtons();

            totalsPopup.SetActive(false);
            debugMenu.SetActive(false);

            _bus.Subscribe<MoneyChanged>(e => moneyText.text = $"$ {e.Total}");
            _bus.Subscribe<ResourceChanged>(_ => { if (totalsPopup.activeSelf) RefreshTotals(); });
            _bus.Subscribe<GameReset>(_ => { if (totalsPopup.activeSelf) RefreshTotals(); });
        }

        void BuildCheatButtons()
        {
            foreach (var r in _resources)
            {
                var button = Instantiate(cheatButtonTemplate, cheatButtonList);
                button.GetComponentInChildren<Text>().text = $"+5  {r.Value}";
                string id = r.Key;
                button.onClick.AddListener(() => _bus.Publish(new DebugAddResourceRequested(id, 5)));
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
