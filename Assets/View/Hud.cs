using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// The always-on HUD (§12.1): a warm money pill (top-right → popup.totalResources, mockup 27:2) and the
    /// dark debug button (top-left → menu.debug, mockup 22:2). Money shows 0 in M2 (no cash source until
    /// orders, M3) but binds to money:changed from the start. All chrome is theme-sourced; renders from Core
    /// state, refreshing on economy events. The debug menu exposes only the cheats that actually do something
    /// in M2 (add each resource, reset) — no inert buttons for features that don't exist yet.
    public sealed class Hud : MonoBehaviour
    {
        EventBus _bus;
        ResourcePool _pool;
        IReadOnlyList<KeyValuePair<string, string>> _resources; // id → display name, stable order
        UiThemeSO _t;

        Text _moneyText;
        GameObject _totalsPopup;
        Transform _totalsList;
        GameObject _debugMenu;

        public void Init(EventBus bus, ResourcePool pool, IReadOnlyList<KeyValuePair<string, string>> resources,
            UiThemeSO theme)
        {
            _bus = bus;
            _pool = pool;
            _resources = resources;
            _t = theme;

            BuildChrome();

            _bus.Subscribe<MoneyChanged>(e => _moneyText.text = $"$ {e.Total}");
            _bus.Subscribe<ResourceChanged>(_ => { if (_totalsPopup.activeSelf) RefreshTotals(); });
            _bus.Subscribe<GameReset>(_ => { if (_totalsPopup.activeSelf) RefreshTotals(); });
        }

        void BuildChrome()
        {
            var canvas = UiFactory.Canvas("HudCanvas", sortOrder: 20);
            var root = canvas.transform;

            // Money pill — top-right, farm-neutral.
            var money = UiFactory.TextButton(root, "$ 0", _t.sizeHeading, _t.hudPillBg, _t.hudPillText, _t.radiusPill, out _moneyText);
            var moneyRt = UiFactory.Rect(money);
            moneyRt.anchorMin = moneyRt.anchorMax = new Vector2(1f, 1f);
            moneyRt.pivot = new Vector2(1f, 1f);
            moneyRt.anchoredPosition = new Vector2(-24, -24);
            moneyRt.sizeDelta = new Vector2(240, 96);
            money.onClick.AddListener(ToggleTotals);

            // Debug button — top-left, void-accent (gates dev cheats).
            var debug = UiFactory.TextButton(root, "", 1, _t.debugAccent, _t.accentText, _t.radiusPill, out _);
            var debugRt = UiFactory.Rect(debug);
            debugRt.anchorMin = debugRt.anchorMax = new Vector2(0f, 1f);
            debugRt.pivot = new Vector2(0f, 1f);
            debugRt.anchoredPosition = new Vector2(24, -24);
            debugRt.sizeDelta = new Vector2(104, 104);
            var glyph = UiFactory.RoundedBox(debug.transform, _t.accentText, 8, "Glyph"); // placeholder icon
            glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glyph.rectTransform.sizeDelta = new Vector2(40, 40);
            glyph.raycastTarget = false;
            debug.onClick.AddListener(ToggleDebug);

            BuildTotalsPopup(root);
            BuildDebugMenu(root);
        }

        // ---- Total resources popup (§12.4, mockup 27:2) ----

        void BuildTotalsPopup(Transform root)
        {
            var panel = UiFactory.RoundedBox(root, _t.panelBg, _t.radiusPanel, "TotalsPopup");
            _totalsPopup = panel.gameObject;
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(720, 100);

            Vertical(panel.gameObject, 12, new RectOffset(28, 28, 28, 32));
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = UiFactory.Row(panel.transform, 14);
            UiFactory.Sized(header, 64);
            var coin = UiFactory.RoundedBox(header.transform, _t.resourceChip, _t.radiusChip, "Coin");
            var coinLe = coin.gameObject.AddComponent<LayoutElement>();
            coinLe.preferredWidth = coinLe.minWidth = coinLe.preferredHeight = coinLe.minHeight = 56;
            UiFactory.Flexible(UiFactory.Label(header.transform, "Resources", _t.sizeTitle, _t.ink, TextAnchor.MiddleLeft, FontStyle.Bold));
            var close = UiFactory.CircleButton(header.transform, "✕", _t.sizeLabel, _t.chipBg, _t.ink, 56f, out _);
            close.onClick.AddListener(() => _totalsPopup.SetActive(false));

            _totalsList = UiFactory.VerticalList(panel.transform, 10, new RectOffset(0, 0, 0, 0)).transform;
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
            Clear(_totalsList);
            foreach (var r in _resources)
            {
                var card = UiFactory.Row(_totalsList, 14, _t.tileBg, _t.radiusTile);
                UiFactory.Sized(card, 84);
                var chip = UiFactory.RoundedBox(card.transform, _t.resourceChip, _t.radiusChip, "Chip");
                var chipLe = chip.gameObject.AddComponent<LayoutElement>();
                chipLe.preferredWidth = chipLe.minWidth = chipLe.preferredHeight = chipLe.minHeight = 52;
                UiFactory.Flexible(UiFactory.Label(card.transform, r.Value, _t.sizeBody, _t.ink, TextAnchor.MiddleLeft, FontStyle.Bold));
                UiFactory.Label(card.transform, _pool.Get(r.Key).ToString(), _t.sizeBody, _t.inkMuted, TextAnchor.MiddleRight, FontStyle.Bold);
            }
        }

        // ---- Debug menu (§12.7, mockup 22:2) ----

        void BuildDebugMenu(Transform root)
        {
            var panel = UiFactory.RoundedBox(root, _t.debugPanelBg, _t.radiusPanel, "DebugMenu");
            _debugMenu = panel.gameObject;
            var rt = panel.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24, -150);
            rt.sizeDelta = new Vector2(400, 100);

            Vertical(panel.gameObject, 10, new RectOffset(20, 20, 20, 24));
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiFactory.Sized(UiFactory.Label(panel.transform, "Debug", _t.sizeHeading, _t.debugText, TextAnchor.MiddleLeft, FontStyle.Bold), 48);
            UiFactory.Sized(UiFactory.Label(panel.transform, "PROTOTYPE CHEATS", _t.sizeSmall, _t.debugSubtext, TextAnchor.MiddleLeft), 30);

            foreach (var r in _resources)
            {
                var btn = UiFactory.TextButton(panel.transform, $"+5  {r.Value}", _t.sizeLabel, _t.debugButtonBg, _t.debugText, _t.radiusPill, out var bl);
                bl.alignment = TextAnchor.MiddleLeft;
                bl.rectTransform.offsetMin = new Vector2(24, 0);
                UiFactory.Sized(btn, 76);
                string id = r.Key;
                btn.onClick.AddListener(() => _bus.Publish(new DebugAddResourceRequested(id, 5)));
            }

            var reset = UiFactory.TextButton(panel.transform, "Reset", _t.sizeLabel, _t.debugResetBg, _t.debugResetText, _t.radiusPill, out var rl);
            rl.alignment = TextAnchor.MiddleLeft;
            rl.rectTransform.offsetMin = new Vector2(24, 0);
            UiFactory.Sized(reset, 76);
            reset.onClick.AddListener(() => _bus.Publish(new DebugResetRequested()));

            _debugMenu.SetActive(false);
        }

        void ToggleDebug() => _debugMenu.SetActive(!_debugMenu.activeSelf);

        static VerticalLayoutGroup Vertical(GameObject go, float spacing, RectOffset padding)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
