using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

namespace VoidDay.View
{
    /// panel.station (§4.5) — recipe rows + job queue only for M2 (no upgrades, no pet slot yet). Opens on
    /// StationPanelRequested (the tap-resolution "open" outcome, decided by the Producer System). Publishes
    /// input intents on interaction and never acts on them; it renders from Core state and refreshes on the
    /// job/economy events. There is no collect button — collection is a map tap (§4.4).
    public sealed class StationPanel : MonoBehaviour
    {
        EventBus _bus;
        JobSystem _jobs;
        RecipeCatalog _catalog;
        ResourcePool _pool;
        IReadOnlyDictionary<string, string> _resourceNames;

        GameObject _root;
        Transform _recipeList;
        Transform _queueList;
        Text _titleText;

        string _openStationId;

        // The head job's live progress fill, refreshed each frame (queue rows are rebuilt only on events).
        Image _headFill;

        public void Init(EventBus bus, JobSystem jobs, RecipeCatalog catalog, ResourcePool pool,
            IReadOnlyDictionary<string, string> resourceNames)
        {
            _bus = bus;
            _jobs = jobs;
            _catalog = catalog;
            _pool = pool;
            _resourceNames = resourceNames;

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
            _openStationId = e.StationId;
            _root.SetActive(true);
            Rebuild();
        }

        void RefreshIf(string stationId)
        {
            if (IsOpen && stationId == _openStationId) Rebuild();
        }

        void Close()
        {
            _openStationId = null;
            _headFill = null;
            _root.SetActive(false);
        }

        void Update()
        {
            if (!IsOpen || _headFill == null) return;
            if (_jobs.TryGetHeadProgress(_openStationId, Time.timeAsDouble, out float fraction, out bool complete))
                _headFill.fillAmount = complete ? 1f : fraction;
        }

        // ---- Build ----

        void BuildChrome()
        {
            var canvas = UiFactory.Canvas("StationPanelCanvas", sortOrder: 10);
            _root = canvas.gameObject;

            var panel = UiFactory.Panel(_root.transform, UiFactory.PanelBg);
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.58f);   // lower ~58% of the screen (portrait, §2)
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var column = UiFactory.VerticalList(panel.transform, spacing: 10, padding: new RectOffset(24, 24, 24, 24));
            UiFactory.Stretch(UiFactory.Rect(column));

            // Title bar: station name + close.
            var header = UiFactory.Row(column.transform, spacing: 8);
            UiFactory.Sized(header, 72);
            _titleText = UiFactory.Label(header.transform, "", 40, TextAnchor.MiddleLeft);
            var close = UiFactory.Button(header.transform, "✕", 36, out _);
            UiFactory.Rect(close).gameObject.AddComponent<LayoutElement>().preferredWidth = 72;
            close.onClick.AddListener(Close);

            UiFactory.Label(column.transform, "Recipes", 30, TextAnchor.MiddleLeft);
            _recipeList = UiFactory.VerticalList(column.transform, spacing: 8, padding: new RectOffset(0, 0, 0, 0)).transform;

            UiFactory.Label(column.transform, "Queue", 30, TextAnchor.MiddleLeft);
            _queueList = UiFactory.VerticalList(column.transform, spacing: 8, padding: new RectOffset(0, 0, 0, 0)).transform;

            _root.SetActive(false);
        }

        void Rebuild()
        {
            _headFill = null;
            string stationType = _jobs.StationTypeOf(_openStationId);
            _titleText.text = $"{Pretty(stationType)}";

            Clear(_recipeList);
            foreach (var recipe in _catalog.ForStationType(stationType))
                BuildRecipeRow(recipe);

            Clear(_queueList);
            BuildQueueRows();
        }

        void BuildRecipeRow(RecipeModel recipe)
        {
            var row = UiFactory.Row(_recipeList, spacing: 12);
            UiFactory.Sized(row, 84);

            string timer = recipe.Duration <= 0f ? "instant" : $"{recipe.Duration:0.#}s";
            string cost = recipe.Inputs.Count == 0 ? "Fallow" : Describe(recipe.Inputs);
            var label = UiFactory.Label(row.transform, $"{cost}  →  {Describe(recipe.Outputs)}   ({timer})", 28);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            bool affordable = _pool.CanAfford(recipe.Inputs);
            bool queueFull = _jobs.GetQueue(_openStationId).Count >= _jobs.QueueDepth(_openStationId);
            bool canQueue = affordable && !queueFull;

            var btn = UiFactory.Button(row.transform, queueFull ? "Full" : "Queue", 28, out var btnLabel);
            UiFactory.Rect(btn).gameObject.AddComponent<LayoutElement>().preferredWidth = 160;
            btn.interactable = canQueue;
            btn.GetComponent<Image>().color = canQueue ? UiFactory.Accent : UiFactory.Disabled;
            if (!affordable) { label.color = UiFactory.Warning; btnLabel.text = "Need"; }

            string recipeId = recipe.Id;
            string stationId = _openStationId;
            btn.onClick.AddListener(() => _bus.Publish(new JobQueueRequested(stationId, recipeId)));
        }

        void BuildQueueRows()
        {
            var queue = _jobs.GetQueue(_openStationId);
            UiFactory.Label(_queueList, $"{queue.Count} / {_jobs.QueueDepth(_openStationId)}", 24, TextAnchor.MiddleLeft);

            for (int i = 0; i < queue.Count; i++)
            {
                var job = queue[i];
                var row = UiFactory.Row(_queueList, spacing: 12);
                UiFactory.Sized(row, 64);

                // A fill sits behind the label to show the head job's progress.
                if (i == 0 && job.State != JobState.Queued)
                {
                    var fill = UiFactory.Panel(row.transform, new Color(0.36f, 0.85f, 0.45f, 0.35f));
                    fill.type = Image.Type.Filled;
                    fill.fillMethod = Image.FillMethod.Horizontal;
                    fill.fillOrigin = 0;
                    fill.fillAmount = job.State == JobState.Complete ? 1f : 0f;
                    UiFactory.Stretch(fill.rectTransform);
                    fill.transform.SetAsFirstSibling();
                    _headFill = fill;
                }

                string recipeName = Describe(_catalog.Get(job.RecipeId).Outputs);
                string state = job.State switch
                {
                    JobState.Running => "running",
                    JobState.Complete => "ready — tap station",
                    _ => "queued"
                };
                var label = UiFactory.Label(row.transform, $"{recipeName}  ·  {state}", 26);
                label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

                int index = i;
                string stationId = _openStationId;
                var cancel = UiFactory.Button(row.transform, "✕", 26, out _);
                UiFactory.Rect(cancel).gameObject.AddComponent<LayoutElement>().preferredWidth = 64;
                cancel.GetComponent<Image>().color = UiFactory.Warning;
                cancel.onClick.AddListener(() => _bus.Publish(new JobCancelRequested(stationId, index)));
            }
        }

        // ---- Helpers ----

        string Describe(IReadOnlyList<ResourceAmount> amounts)
        {
            if (amounts.Count == 0) return "—";
            var sb = new StringBuilder();
            for (int i = 0; i < amounts.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(amounts[i].Amount).Append(' ').Append(NameOf(amounts[i].ResourceId));
            }
            return sb.ToString();
        }

        string NameOf(string resourceId) =>
            _resourceNames.TryGetValue(resourceId, out var n) ? n : resourceId;

        static string Pretty(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }
    }
}
