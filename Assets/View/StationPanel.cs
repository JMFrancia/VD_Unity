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
    /// the building (WorldState / world.queueSlots). The chrome is authored in the scene canvas; dynamic
    /// content (tiles, ingredient rows) instantiates authored templates. Opens on StationPanelRequested (the
    /// Producer's tap-resolution "open" outcome); publishes input:jobQueueRequested and never acts on it;
    /// renders from Core state; tracks the building on screen.
    public sealed class StationPanel : MonoBehaviour
    {
        [Header("Chrome (authored)")]
        [SerializeField] RectTransform canvasRect;
        [SerializeField] RectTransform popup;
        [SerializeField] Text titleText;
        [SerializeField] Button closeButton;

        [Header("Recipe tiles")]
        [SerializeField] Transform tilesRow;
        [SerializeField] RecipeTile tileTemplate;

        [Header("Detail card")]
        [SerializeField] GameObject detailHead;   // output name + amount + timer chip
        [SerializeField] Text outputNameText;
        [SerializeField] Text outputAmountText;
        [SerializeField] Text timerText;
        [SerializeField] GameObject freeRow;      // "Free — no inputs" (fallow recipes)
        [SerializeField] GameObject noRecipesRow; // non-producer stations
        [SerializeField] Transform ingredientList;
        [SerializeField] IngredientRow ingredientRowTemplate;

        [Header("Queue action")]
        [SerializeField] Button queueButton;
        [SerializeField] Image queueButtonImage;
        [SerializeField] Text queueLabel;

        [Header("State colors + placement")]
        [SerializeField] UiThemeSO theme;
        [SerializeField] float anchorHeight = 1.6f; // world units above the station pivot to hang the popup from
        [SerializeField] float screenGap = 40f;     // reference px between the building's screen point and the popup

        EventBus _bus;
        JobSystem _jobs;
        RecipeCatalog _catalog;
        ResourcePool _pool;
        IReadOnlyDictionary<string, string> _resourceNames;
        IReadOnlyDictionary<string, Transform> _stationRoots;
        Camera _camera;

        string _openStationId;
        int _selected;
        readonly List<RecipeModel> _recipes = new();

        public void Init(EventBus bus, JobSystem jobs, RecipeCatalog catalog, ResourcePool pool,
            IReadOnlyDictionary<string, string> resourceNames,
            IReadOnlyDictionary<string, Transform> stationRoots, Camera camera)
        {
            _bus = bus;
            _jobs = jobs;
            _catalog = catalog;
            _pool = pool;
            _resourceNames = resourceNames;
            _stationRoots = stationRoots;
            _camera = camera;

            closeButton.onClick.AddListener(Close);
            popup.gameObject.SetActive(false);

            _bus.Subscribe<StationPanelRequested>(OnPanelRequested);
            _bus.Subscribe<BackgroundTapped>(_ => Close()); // tap off the panel dismisses it
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
            popup.gameObject.SetActive(true);
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
            popup.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (IsOpen) PositionOverBuilding();
        }

        // ---- Rebuild from Core state ----

        void Rebuild()
        {
            string stationType = _jobs.StationTypeOf(_openStationId);
            titleText.text = Pretty(stationType);

            _recipes.Clear();
            _recipes.AddRange(_catalog.ForStationType(stationType));
            _selected = _recipes.Count == 0 ? 0 : Mathf.Clamp(_selected, 0, _recipes.Count - 1);

            BuildTiles();
            BuildDetail();
            BuildQueueButton();
        }

        void BuildTiles()
        {
            Clear(tilesRow);
            for (int i = 0; i < _recipes.Count; i++)
            {
                var tile = Instantiate(tileTemplate, tilesRow);
                tile.Bind(RecipeLabel(_recipes[i]), TimerText(_recipes[i]), i == _selected);
                int index = i;
                tile.Button.onClick.AddListener(() => { _selected = index; Rebuild(); });
            }
        }

        void BuildDetail()
        {
            Clear(ingredientList);
            bool hasRecipes = _recipes.Count > 0;
            noRecipesRow.SetActive(!hasRecipes);
            detailHead.SetActive(hasRecipes);
            freeRow.SetActive(false);
            if (!hasRecipes) return;

            var recipe = _recipes[_selected];
            outputNameText.text = OutputName(recipe);
            outputAmountText.text = $"x{OutputAmount(recipe)}";
            timerText.text = TimerText(recipe);

            if (recipe.Inputs.Count == 0)
            {
                freeRow.SetActive(true);
                return;
            }
            foreach (var input in recipe.Inputs)
                Instantiate(ingredientRowTemplate, ingredientList)
                    .Bind(NameOf(input.ResourceId), _pool.Get(input.ResourceId), input.Amount);
        }

        void BuildQueueButton()
        {
            queueButton.onClick.RemoveAllListeners();
            if (_recipes.Count == 0)
            {
                queueButton.interactable = false;
                queueButtonImage.color = theme.lockedBg;
                queueLabel.text = "—";
                queueLabel.color = theme.lockedText;
                return;
            }
            var recipe = _recipes[_selected];
            bool affordable = _pool.CanAfford(recipe.Inputs);
            bool queueFull = _jobs.GetQueue(_openStationId).Count >= _jobs.QueueDepth(_openStationId);
            bool canQueue = affordable && !queueFull;

            queueButton.interactable = canQueue;
            queueButtonImage.color = canQueue ? theme.accent : theme.lockedBg;
            queueLabel.color = canQueue ? theme.accentText : theme.lockedText;
            queueLabel.text = queueFull ? "Queue full" : affordable ? "Queue" : "Can't afford";

            string stationId = _openStationId;
            string recipeId = recipe.Id;
            queueButton.onClick.AddListener(() => _bus.Publish(new JobQueueRequested(stationId, recipeId)));
        }

        // ---- Positioning ----

        void PositionOverBuilding()
        {
            if (!_stationRoots.TryGetValue(_openStationId, out var root)) return;
            Vector3 world = root.position + Vector3.up * anchorHeight;
            Vector2 screen = _camera.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);

            Vector2 half = canvasRect.rect.size * 0.5f;
            float w = popup.rect.width, h = popup.rect.height;
            float x = Mathf.Clamp(local.x, -half.x + w * 0.5f + 12f, half.x - w * 0.5f - 12f);
            float y = Mathf.Clamp(local.y + screenGap, -half.y + 12f, half.y - h - 12f);
            popup.anchoredPosition = new Vector2(x, y);
        }

        // ---- Presentation helpers (View-side — RecipeModel carries no label) ----

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
