using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// panel.station — the CHOSEN ALT / Full HayDay model (docs/UI-Mockups.md node 42:2). A **floating recipe
    /// popup near the building**, not an all-in-one modal: a row of recipe icon tiles, the selected recipe's
    /// have/need + timer detail, and one Queue action. The job queue is NOT here — it renders in-world under
    /// the building (WorldState / world.queueSlots). Upgrades + pet assignment are deferred (not in this
    /// surface). Opens on StationPanelRequested (the Producer's tap-resolution "open" outcome); publishes
    /// input:jobQueueRequested and never acts on it; renders from Core state; tracks the building on screen.
    public sealed class StationPanel : MonoBehaviour
    {
        const float PopupWidth = 660f;
        const float AnchorHeight = 1.6f; // world units above the station pivot to hang the popup from
        const float ScreenGap = 40f;     // reference px between the building's screen point and the popup

        EventBus _bus;
        JobSystem _jobs;
        RecipeCatalog _catalog;
        ResourcePool _pool;
        IReadOnlyDictionary<string, string> _resourceNames;
        IReadOnlyDictionary<string, Transform> _stationRoots;
        UiThemeSO _t;
        Camera _camera;

        RectTransform _canvasRect;
        RectTransform _popup;
        Text _titleText;
        Transform _tilesRow;
        Transform _detailCard;
        Button _queueButton;
        Text _queueLabel;

        string _openStationId;
        int _selected;
        readonly List<RecipeModel> _recipes = new();

        public void Init(EventBus bus, JobSystem jobs, RecipeCatalog catalog, ResourcePool pool,
            IReadOnlyDictionary<string, string> resourceNames,
            IReadOnlyDictionary<string, Transform> stationRoots, UiThemeSO theme, Camera camera)
        {
            _bus = bus;
            _jobs = jobs;
            _catalog = catalog;
            _pool = pool;
            _resourceNames = resourceNames;
            _stationRoots = stationRoots;
            _t = theme;
            _camera = camera;

            BuildChrome();

            _bus.Subscribe<StationPanelRequested>(OnPanelRequested);
            _bus.Subscribe<JobQueued>(e => RefreshIf(e.StationId));
            _bus.Subscribe<JobStarted>(e => RefreshIf(e.StationId));
            _bus.Subscribe<JobCompleted>(e => RefreshIf(e.StationId));
            _bus.Subscribe<JobCollected>(e => RefreshIf(e.StationId));
            _bus.Subscribe<JobCancelled>(e => RefreshIf(e.StationId));
            _bus.Subscribe<ResourceChanged>(_ => { if (IsOpen) Rebuild(); }); // afford states may have changed
            _bus.Subscribe<GameReset>(_ => Close());
        }

        bool IsOpen => _openStationId != null;

        void OnPanelRequested(StationPanelRequested e)
        {
            if (_openStationId != e.StationId) _selected = 0; // keep selection only when re-tapping the same one
            _openStationId = e.StationId;
            _popup.gameObject.SetActive(true);
            Rebuild();
            PositionOverBuilding();
        }

        void RefreshIf(string stationId)
        {
            if (IsOpen && stationId == _openStationId) Rebuild();
        }

        void Close()
        {
            _openStationId = null;
            _popup.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (IsOpen) PositionOverBuilding();
        }

        // ---- Chrome ----

        void BuildChrome()
        {
            var canvas = UiFactory.Canvas("StationPopupCanvas", sortOrder: 10);
            _canvasRect = (RectTransform)canvas.transform;

            var box = UiFactory.RoundedBox(canvas.transform, _t.panelBg, _t.radiusPanel, "RecipePopup");
            _popup = box.rectTransform;
            _popup.anchorMin = _popup.anchorMax = new Vector2(0.5f, 0.5f);
            _popup.pivot = new Vector2(0.5f, 0f); // grows upward from the building's screen point
            _popup.sizeDelta = new Vector2(PopupWidth, 100f);
            VerticalGroup(box.gameObject, 14, new RectOffset(24, 24, 24, 28));
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header: station name + close ✕.
            var header = UiFactory.Row(_popup, 8);
            UiFactory.Sized(header, 56);
            _titleText = UiFactory.Label(header.transform, "", _t.sizeHeading, _t.ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Flexible(_titleText);
            var close = UiFactory.CircleButton(header.transform, "✕", _t.sizeLabel, _t.chipBg, _t.ink, 56f, out _);
            close.onClick.AddListener(Close);

            // Recipe icon tiles.
            var tiles = UiFactory.Row(_popup, 12);
            tiles.childAlignment = TextAnchor.MiddleCenter;
            UiFactory.Sized(tiles, 150);
            _tilesRow = tiles.transform;

            // Selected-recipe detail card.
            var detail = UiFactory.RoundedBox(_popup, _t.cardBg, _t.radiusCard, "Detail");
            VerticalGroup(detail.gameObject, 8, new RectOffset(20, 20, 18, 18));
            detail.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _detailCard = detail.transform;

            // Queue action.
            _queueButton = UiFactory.TextButton(_popup, "Queue", _t.sizeTitle, _t.accent, _t.accentText,
                _t.radiusButton, out _queueLabel);
            UiFactory.Sized(_queueButton, 100);

            _popup.gameObject.SetActive(false);
        }

        static VerticalLayoutGroup VerticalGroup(GameObject go, float spacing, RectOffset padding)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        // ---- Rebuild from Core state ----

        void Rebuild()
        {
            string stationType = _jobs.StationTypeOf(_openStationId);
            _titleText.text = Pretty(stationType);

            _recipes.Clear();
            _recipes.AddRange(_catalog.ForStationType(stationType));
            _selected = _recipes.Count == 0 ? 0 : Mathf.Clamp(_selected, 0, _recipes.Count - 1);

            BuildTiles();
            BuildDetail();
            BuildQueueButton();
        }

        void SelectAndRefresh(int index)
        {
            _selected = index;
            BuildTiles();
            BuildDetail();
            BuildQueueButton();
        }

        void BuildTiles()
        {
            Clear(_tilesRow);
            for (int i = 0; i < _recipes.Count; i++)
            {
                var recipe = _recipes[i];

                var tile = UiFactory.RoundedBox(_tilesRow, _t.tileBg, _t.radiusTile, "Tile");
                UiFactory.Width(tile, 132);
                var tileBtn = tile.gameObject.AddComponent<Button>();
                int index = i;
                tileBtn.onClick.AddListener(() => SelectAndRefresh(index));

                if (i == _selected)
                {
                    var sel = UiFactory.RoundedOutline(tile.transform, _t.accent, _t.radiusTile, 6f, "Sel");
                    UiFactory.Stretch(sel.rectTransform);
                    sel.raycastTarget = false;
                }

                var col = UiFactory.VerticalList(tile.transform, 4, new RectOffset(8, 8, 16, 12));
                UiFactory.Stretch(UiFactory.Rect(col));
                col.childAlignment = TextAnchor.UpperCenter;
                col.childForceExpandWidth = false;

                var chip = UiFactory.RoundedBox(col.transform, _t.resourceChip, _t.radiusChip, "Chip");
                Square(chip, 64);
                chip.raycastTarget = false;

                var name = UiFactory.Label(col.transform, RecipeLabel(recipe), _t.sizeLabel, _t.ink, TextAnchor.LowerCenter, FontStyle.Bold);
                name.raycastTarget = false;
                name.horizontalOverflow = HorizontalWrapMode.Wrap;   // wrap long labels ("Fallow Wheat") inside the tile
                name.verticalOverflow = VerticalWrapMode.Overflow;
                var nameLe = name.gameObject.AddComponent<LayoutElement>();
                nameLe.preferredWidth = nameLe.minWidth = 116;       // tile inner width (132 − 8 padding each side)
                nameLe.preferredHeight = 56;
                UiFactory.Label(col.transform, TimerText(recipe), _t.sizeSmall, _t.inkMuted, TextAnchor.MiddleCenter)
                    .raycastTarget = false;
            }
        }

        void BuildDetail()
        {
            Clear(_detailCard);
            if (_recipes.Count == 0)
            {
                UiFactory.Label(_detailCard, "No recipes", _t.sizeBody, _t.inkMuted);
                return;
            }
            var recipe = _recipes[_selected];

            // Output name + amount + timer chip.
            var head = UiFactory.Row(_detailCard, 10);
            UiFactory.Sized(head, 56);
            UiFactory.Label(head.transform, OutputName(recipe), _t.sizeTitle, _t.ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Label(head.transform, $"x{OutputAmount(recipe)}", _t.sizeBody, _t.inkMuted, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.Flexible(UiFactory.Label(head.transform, "", 1, _t.ink)); // spacer pushes the timer right
            var timerChip = UiFactory.RoundedBox(head.transform, _t.chipBg, _t.radiusChip, "Timer");
            UiFactory.Width(timerChip, 150);
            var timerLabel = UiFactory.Label(timerChip.transform, TimerText(recipe), _t.sizeLabel, _t.inkMuted, TextAnchor.MiddleCenter);
            UiFactory.Stretch(timerLabel.rectTransform);

            // Input have/need rows (fallow recipes have none).
            if (recipe.Inputs.Count == 0)
            {
                UiFactory.Label(_detailCard, "Free — no inputs", _t.sizeBody, _t.inkMuted);
                return;
            }
            foreach (var input in recipe.Inputs)
            {
                int held = _pool.Get(input.ResourceId);
                bool ok = held >= input.Amount;

                var row = UiFactory.Row(_detailCard, 12);
                UiFactory.Sized(row, 52);
                var chip = UiFactory.RoundedBox(row.transform, _t.resourceChip, 10, "Chip");
                Square(chip, 46);
                UiFactory.Flexible(UiFactory.Label(row.transform, NameOf(input.ResourceId), _t.sizeBody, _t.ink));
                UiFactory.Label(row.transform, $"{held} / {input.Amount}", _t.sizeBody,
                    ok ? _t.ink : _t.warning, TextAnchor.MiddleRight, FontStyle.Bold);
                Badge(row.transform, ok ? "✓" : "!", ok ? _t.accent : _t.warning);
            }
        }

        void BuildQueueButton()
        {
            if (_recipes.Count == 0)
            {
                _queueButton.interactable = false;
                _queueButton.image.color = _t.lockedBg;
                _queueLabel.text = "—";
                _queueLabel.color = _t.lockedText;
                return;
            }
            var recipe = _recipes[_selected];
            bool affordable = _pool.CanAfford(recipe.Inputs);
            bool queueFull = _jobs.GetQueue(_openStationId).Count >= _jobs.QueueDepth(_openStationId);
            bool canQueue = affordable && !queueFull;

            _queueButton.interactable = canQueue;
            _queueButton.image.color = canQueue ? _t.accent : _t.lockedBg;
            _queueLabel.color = canQueue ? _t.accentText : _t.lockedText;
            _queueLabel.text = queueFull ? "Queue full" : affordable ? "Queue" : "Can't afford";

            _queueButton.onClick.RemoveAllListeners();
            string stationId = _openStationId;
            string recipeId = recipe.Id;
            _queueButton.onClick.AddListener(() => _bus.Publish(new JobQueueRequested(stationId, recipeId)));
        }

        // ---- Positioning ----

        void PositionOverBuilding()
        {
            if (!_stationRoots.TryGetValue(_openStationId, out var root)) return;
            Vector3 world = root.position + Vector3.up * AnchorHeight;
            Vector2 screen = _camera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local);

            Vector2 half = _canvasRect.rect.size * 0.5f;
            float w = _popup.rect.width, h = _popup.rect.height;
            float x = Mathf.Clamp(local.x, -half.x + w * 0.5f + 12f, half.x - w * 0.5f - 12f);
            float y = Mathf.Clamp(local.y + ScreenGap, -half.y + 12f, half.y - h - 12f);
            _popup.anchoredPosition = new Vector2(x, y);
        }

        // ---- Small builders ----

        static void Square(Component c, float size)
        {
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = size;
            le.preferredHeight = le.minHeight = size;
        }

        void Badge(Transform parent, string glyph, Color bg)
        {
            var badge = UiFactory.RoundedBox(parent, bg, 21, "Mark");
            Square(badge, 42);
            var l = UiFactory.Label(badge.transform, glyph, _t.sizeSmall, _t.accentText, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.Stretch(l.rectTransform);
            l.raycastTarget = false;
        }

        // ---- Presentation helpers (View-side — RecipeModel carries no label; Core is out of scope for this rework) ----

        /// Grow recipes label as their output ("Wheat"); input-less Fallow recipes get the "Fallow" prefix.
        string RecipeLabel(RecipeModel r) => r.Inputs.Count == 0 ? $"Fallow {OutputName(r)}" : OutputName(r);
        string OutputName(RecipeModel r) => r.Outputs.Count == 0 ? "—" : NameOf(r.Outputs[0].ResourceId);
        static int OutputAmount(RecipeModel r) => r.Outputs.Count == 0 ? 0 : r.Outputs[0].Amount;
        static string TimerText(RecipeModel r) => r.Duration <= 0f ? "instant" : $"{r.Duration:0.#}s";

        string NameOf(string resourceId) => _resourceNames.TryGetValue(resourceId, out var n) ? n : resourceId;
        static string Pretty(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
