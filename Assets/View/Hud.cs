using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;

namespace VoidDay.View
{
    /// The always-on HUD (§12.1): money counter (top-right, opens the total-resources popup) and the debug
    /// button (top-left, toggles the debug menu). Money shows 0 in M2 — no cash source until orders (M3) —
    /// but binds to money:changed from the start. Renders from Core state, refreshing on economy events.
    public sealed class Hud : MonoBehaviour
    {
        EventBus _bus;
        ResourcePool _pool;
        IReadOnlyList<KeyValuePair<string, string>> _resources; // id → display name, stable order

        Text _moneyText;
        GameObject _totalsPopup;
        Transform _totalsList;
        GameObject _debugMenu;

        public void Init(EventBus bus, ResourcePool pool, IReadOnlyList<KeyValuePair<string, string>> resources)
        {
            _bus = bus;
            _pool = pool;
            _resources = resources;

            BuildChrome();

            _bus.Subscribe<MoneyChanged>(e => _moneyText.text = $"$ {e.Total}");
            _bus.Subscribe<ResourceChanged>(_ => { if (_totalsPopup.activeSelf) RefreshTotals(); });
            _bus.Subscribe<GameReset>(_ => { if (_totalsPopup.activeSelf) RefreshTotals(); });
        }

        void BuildChrome()
        {
            var canvas = UiFactory.Canvas("HudCanvas", sortOrder: 20);
            var root = canvas.transform;

            // Money counter — top-right.
            var money = UiFactory.Button(root, "$ 0", 40, out _moneyText);
            var moneyRt = UiFactory.Rect(money);
            moneyRt.anchorMin = moneyRt.anchorMax = new Vector2(1f, 1f);
            moneyRt.pivot = new Vector2(1f, 1f);
            moneyRt.anchoredPosition = new Vector2(-24, -24);
            moneyRt.sizeDelta = new Vector2(240, 96);
            money.GetComponent<Image>().color = UiFactory.PanelBg;
            money.onClick.AddListener(ToggleTotals);

            // Debug button — top-left.
            var debug = UiFactory.Button(root, "DEBUG", 34, out _);
            var debugRt = UiFactory.Rect(debug);
            debugRt.anchorMin = debugRt.anchorMax = new Vector2(0f, 1f);
            debugRt.pivot = new Vector2(0f, 1f);
            debugRt.anchoredPosition = new Vector2(24, -24);
            debugRt.sizeDelta = new Vector2(220, 96);
            debug.GetComponent<Image>().color = UiFactory.PanelBg;
            debug.onClick.AddListener(ToggleDebug);

            BuildTotalsPopup(root);
            BuildDebugMenu(root);
        }

        // ---- Total resources popup (§12.4) ----

        void BuildTotalsPopup(Transform root)
        {
            var panel = UiFactory.Panel(root, UiFactory.PanelBg);
            _totalsPopup = panel.gameObject;
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(680, 800);

            var column = UiFactory.VerticalList(panel.transform, 10, new RectOffset(28, 28, 28, 28));
            UiFactory.Stretch(UiFactory.Rect(column));

            var header = UiFactory.Row(column.transform, 8);
            UiFactory.Sized(header, 72);
            var title = UiFactory.Label(header.transform, "Resources", 40);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var close = UiFactory.Button(header.transform, "✕", 36, out _);
            UiFactory.Rect(close).gameObject.AddComponent<LayoutElement>().preferredWidth = 72;
            close.onClick.AddListener(() => _totalsPopup.SetActive(false));

            _totalsList = UiFactory.VerticalList(column.transform, 6, new RectOffset(0, 0, 0, 0)).transform;
            _totalsPopup.SetActive(false);
        }

        void ToggleTotals()
        {
            bool show = !_totalsPopup.activeSelf;
            _totalsPopup.SetActive(show);
            if (show) RefreshTotals();
        }

        void RefreshTotals()
        {
            for (int i = _totalsList.childCount - 1; i >= 0; i--)
                Destroy(_totalsList.GetChild(i).gameObject);

            foreach (var r in _resources)
            {
                var row = UiFactory.Row(_totalsList, 12);
                UiFactory.Sized(row, 64);
                var name = UiFactory.Label(row.transform, r.Value, 30);
                name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                UiFactory.Label(row.transform, _pool.Get(r.Key).ToString(), 30, TextAnchor.MiddleRight);
            }
        }

        // ---- Debug menu (§12.7) ----

        void BuildDebugMenu(Transform root)
        {
            var panel = UiFactory.Panel(root, UiFactory.PanelBg);
            _debugMenu = panel.gameObject;
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24, -136);
            rt.sizeDelta = new Vector2(360, 96 + _resources.Count * 84 + 84);

            var column = UiFactory.VerticalList(panel.transform, 8, new RectOffset(16, 16, 16, 16));
            UiFactory.Stretch(UiFactory.Rect(column));
            UiFactory.Sized(UiFactory.Label(column.transform, "Debug", 30), 40);

            foreach (var r in _resources)
            {
                var btn = UiFactory.Button(column.transform, $"+5 {r.Value}", 28, out _);
                UiFactory.Sized(btn, 76);
                string id = r.Key;
                btn.onClick.AddListener(() => _bus.Publish(new DebugAddResourceRequested(id, 5)));
            }

            var reset = UiFactory.Button(column.transform, "Reset", 28, out _);
            UiFactory.Sized(reset, 76);
            reset.GetComponent<Image>().color = UiFactory.Warning;
            reset.onClick.AddListener(() => _bus.Publish(new DebugResetRequested()));

            _debugMenu.SetActive(false);
        }

        void ToggleDebug() => _debugMenu.SetActive(!_debugMenu.activeSelf);
    }
}
