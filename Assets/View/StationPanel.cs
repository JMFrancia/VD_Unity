using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// panel.station — the CHOSEN ALT / Full HayDay model (docs/UI-Mockups.md node 42:2), a **floating popup
    /// near the building**, not an all-in-one modal. Two tabs (M5): **Recipes** — a row of recipe icon tiles,
    /// the selected recipe's have/need + timer, one Queue action (the job queue itself renders in-world under
    /// the building, WorldState); and **Upgrades** — one `pattern.purchaseRow` per station-upgrade track with
    /// a procedural effect description, tier progression, and Buy. The chosen mockup deferred the upgrade
    /// opener as TBD; a tab in this popup is that opener (user decision).
    ///
    /// Opens on StationPanelRequested (the Producer's tap-resolution "open" outcome); publishes intents
    /// (input:jobQueueRequested / input:upgradePurchaseRequested) and never acts on them; renders from Core
    /// state; tracks the building on screen.
    public sealed class StationPanel : MonoBehaviour
    {
        enum Tab { Recipes, Upgrades }

        [Header("Chrome (authored)")]
        [SerializeField] RectTransform canvasRect;
        [SerializeField] RectTransform popup;
        [SerializeField] Text titleText;
        [SerializeField] Button closeButton;

        [Header("Tabs")]
        [SerializeField] Button recipesTabButton;
        [SerializeField] GameObject recipesTabSelected; // "selected" underline/pill, like RecipeTile's outline
        [SerializeField] GameObject recipesView;        // wraps the tile row + detail card + queue button
        [SerializeField] Button upgradesTabButton;
        [SerializeField] GameObject upgradesTabSelected;
        [SerializeField] GameObject upgradesView;       // wraps the upgrade list

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

        [Header("Upgrades")]
        [SerializeField] Transform upgradeList;
        [SerializeField] UpgradeRow upgradeRowTemplate;

        [Header("Locked-recipe copy")]
        [Tooltip("Detail-card timer line when the selected recipe is level-gated. {0} = the level it opens at.")]
        [SerializeField] string lockedDetailFormat = "Unlocks at level {0}";
        [Tooltip("Queue button label when the selected recipe is level-gated. {0} = the level it opens at.")]
        [SerializeField] string lockedQueueFormat = "Unlock at Lv {0}";

        [Header("State colors + placement")]
        [SerializeField] UiThemeSO theme;
        [SerializeField] float anchorHeight = 1.6f; // world units above the station pivot to hang the popup from
        [SerializeField] float screenGap = 40f;     // reference px between the building's screen point and the popup

        EventBus _bus;
        JobSystem _jobs;
        RecipeCatalog _catalog;
        ResourcePool _pool;
        Wallet _wallet;
        UpgradeSystem _upgrades;
        IReadOnlyDictionary<string, ResourceSO> _resources;
        IReadOnlyDictionary<string, Transform> _stationRoots;
        Camera _camera;

        string _openStationId;
        Tab _tab;
        int _selected;
        readonly List<RecipeModel> _recipes = new();

        public void Init(EventBus bus, JobSystem jobs, RecipeCatalog catalog, ResourcePool pool, Wallet wallet,
            UpgradeSystem upgrades, IReadOnlyDictionary<string, ResourceSO> resources,
            IReadOnlyDictionary<string, Transform> stationRoots, Camera camera)
        {
            _bus = bus;
            _jobs = jobs;
            _catalog = catalog;
            _pool = pool;
            _wallet = wallet;
            _upgrades = upgrades;
            _resources = resources;
            _stationRoots = stationRoots;
            _camera = camera;

            closeButton.onClick.AddListener(Close);
            recipesTabButton.onClick.AddListener(() => SwitchTab(Tab.Recipes));
            upgradesTabButton.onClick.AddListener(() => SwitchTab(Tab.Upgrades));
            popup.gameObject.SetActive(false);

            _bus.Subscribe<StationPanelRequested>(OnPanelRequested);
            _bus.Subscribe<BackgroundTapped>(_ => Close()); // tap off the panel dismisses it
            _bus.Subscribe<ExclusiveUiOpened>(e => { if (e.Source != "station") Close(); }); // one menu at a time
            _bus.Subscribe<JobQueued>(e => RefreshIf(e.StationId));
            _bus.Subscribe<JobStarted>(e => RefreshIf(e.StationId));
            _bus.Subscribe<JobCompleted>(e => RefreshIf(e.StationId));
            _bus.Subscribe<JobCollected>(e => RefreshIf(e.StationId));
            _bus.Subscribe<JobCancelled>(e => RefreshIf(e.StationId));
            _bus.Subscribe<ResourceChanged>(_ => { if (IsOpen) Rebuild(); }); // recipe afford states may have changed
            _bus.Subscribe<MoneyChanged>(_ => { if (IsOpen) Rebuild(); });     // upgrade afford states
            _bus.Subscribe<EffectsRecalculated>(_ => { if (IsOpen) Rebuild(); }); // a tier was bought
            _bus.Subscribe<GameReset>(_ => Close());
        }

        bool IsOpen => _openStationId != null;

        void OnPanelRequested(StationPanelRequested e)
        {
            // This is the *recipe/upgrade* popup, so it owns producers only. The Order Board (and later the
            // Silo and Workshop) have their own panels and self-select the same way off this event.
            if (_catalog.ForStationType(_jobs.StationTypeOf(e.StationId)).Count == 0) { Close(); return; }

            if (_openStationId != e.StationId) { _selected = 0; _tab = Tab.Recipes; } // reset only for a new station
            _openStationId = e.StationId;
            popup.gameObject.SetActive(true);
            _bus.Publish(new StationPanelOpened(e.StationId)); // WorldState swaps this station's radial for its queue
            _bus.Publish(new ExclusiveUiOpened("station")); // retract the build menu / other panels
            Rebuild();
            PositionOverBuilding();
        }

        void SwitchTab(Tab tab)
        {
            _bus.Publish(new UiTapped("tab")); // a tab switch announces nothing else — this is its only voice
            _tab = tab;
            Rebuild();
            PositionOverBuilding();
        }

        void RefreshIf(string stationId)
        {
            if (IsOpen && stationId == _openStationId) Rebuild();
        }

        void Close()
        {
            if (_openStationId == null) return; // already closed — don't spam StationPanelClosed (many callers)
            _openStationId = null;
            popup.gameObject.SetActive(false);
            _bus.Publish(new StationPanelClosed()); // WorldState restores the working station's radial, hides the queue
            _bus.Publish(new ExclusiveUiClosed("station"));
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

            bool hasUpgrades = _upgrades.TracksFor(_openStationId).Count > 0;
            upgradesTabButton.gameObject.SetActive(hasUpgrades);
            if (!hasUpgrades && _tab == Tab.Upgrades) _tab = Tab.Recipes;

            recipesTabSelected.SetActive(_tab == Tab.Recipes);
            upgradesTabSelected.SetActive(_tab == Tab.Upgrades);
            recipesView.SetActive(_tab == Tab.Recipes);
            upgradesView.SetActive(_tab == Tab.Upgrades);

            if (_tab == Tab.Recipes) BuildRecipes(stationType);
            else BuildUpgrades();
        }

        void BuildRecipes(string stationType)
        {
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
                var recipe = _recipes[i];
                if (_jobs.IsRecipeLocked(recipe.Id))
                    tile.BindLocked(RecipeLabel(recipe), OutputIcon(recipe),
                        _jobs.RecipeUnlockLevel(recipe.Id), i == _selected);
                else
                    tile.Bind(RecipeLabel(recipe), OutputIcon(recipe), TimerText(recipe), i == _selected);
                int index = i;
                tile.Button.onClick.AddListener(() =>
                {
                    _bus.Publish(new UiTapped("recipeTile")); // selection only — the job intent comes from Queue
                    _selected = index;
                    Rebuild();
                });
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
            timerText.text = _jobs.IsRecipeLocked(recipe.Id)
                ? string.Format(lockedDetailFormat, _jobs.RecipeUnlockLevel(recipe.Id))
                : TimerText(recipe);

            if (recipe.Inputs.Count == 0)
            {
                freeRow.SetActive(true);
                return;
            }
            foreach (var input in recipe.Inputs)
                Instantiate(ingredientRowTemplate, ingredientList)
                    .Bind(NameOf(input.ResourceId), IconOf(input.ResourceId), _pool.Get(input.ResourceId), input.Amount);
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
            bool locked = _jobs.IsRecipeLocked(recipe.Id);
            bool affordable = _pool.CanAfford(recipe.Inputs);
            bool queueFull = _jobs.GetQueue(_openStationId).Count >= _jobs.QueueDepth(_openStationId);
            bool canQueue = !locked && affordable && !queueFull;

            queueButton.interactable = canQueue;
            queueButtonImage.color = canQueue ? theme.accent : theme.lockedBg;
            queueLabel.color = canQueue ? theme.accentText : theme.lockedText;
            // Lock beats the money/queue states — a recipe you can't reach yet has nothing to say about price.
            queueLabel.text = locked ? string.Format(lockedQueueFormat, _jobs.RecipeUnlockLevel(recipe.Id))
                : queueFull ? "Queue full" : affordable ? "Queue" : "Can't afford";

            if (!canQueue) return; // no click intent for a locked / unaffordable / full queue

            string stationId = _openStationId;
            string recipeId = recipe.Id;
            queueButton.onClick.AddListener(() => _bus.Publish(new JobQueueRequested(stationId, recipeId)));
        }

        // ---- Upgrades tab (§8) ----

        void BuildUpgrades()
        {
            Clear(upgradeList);
            string stationId = _openStationId;
            foreach (var track in _upgrades.TracksFor(stationId))
            {
                int tier = _upgrades.TierOf(stationId, track.Id);
                bool maxed = tier >= track.MaxTier;

                // Describe the tier being offered (or, when maxed, the last one) — the effect the row is about.
                var describedTier = maxed ? track.Tiers[track.MaxTier - 1] : track.Tiers[tier];
                string description = TraitDescription.Describe(describedTier.Effects);
                int cost = maxed ? 0 : track.Tiers[tier].Cost;
                bool affordable = !maxed && _wallet.Money >= cost;

                var row = Instantiate(upgradeRowTemplate, upgradeList);
                if (_upgrades.IsLocked(track)) { row.BindLocked(description, track.UnlockLevel); continue; }

                row.Bind(description, tier, track.MaxTier, cost, affordable);
                if (!maxed)
                {
                    string trackId = track.Id;
                    row.Button.onClick.AddListener(() =>
                        _bus.Publish(new UpgradePurchaseRequested(stationId, trackId)));
                }
            }
        }

        // ---- Positioning ----

        void PositionOverBuilding()
        {
            if (!_stationRoots.TryGetValue(_openStationId, out var root)) return;
            Vector3 world = root.position + Vector3.up * anchorHeight;
            Vector2 screen = _camera.WorldToScreenPoint(world);
            // canvasRect renders in Screen Space - Camera on _camera (the letterbox needs the UI confined to
            // the camera's viewport), so the screen->local conversion must go through that same camera. Passing
            // null here would assume an Overlay canvas and pin the popup to a screen corner.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, _camera, out Vector2 local);

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
        Sprite OutputIcon(RecipeModel r) => r.Outputs.Count == 0 ? null : IconOf(r.Outputs[0].ResourceId);

        // Timer and output are shown RESOLVED for the open station (§3) — base recipe data ignores this
        // station's speed/yield upgrades, so read the resolved value from Core (which owns the resolve rule).
        int OutputAmount(RecipeModel r) => _jobs.ResolvedOutput(_openStationId, r.Id);
        string TimerText(RecipeModel r)
        {
            float d = _jobs.ResolvedDuration(_openStationId, r.Id);
            return d <= 0f ? "instant" : $"{d:0.#}s";
        }

        string NameOf(string resourceId) => _resources.TryGetValue(resourceId, out var so) ? so.displayName : resourceId;
        Sprite IconOf(string resourceId) => _resources.TryGetValue(resourceId, out var so) ? so.icon : null;
        static string Pretty(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
