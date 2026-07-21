using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// panel.silo (mockup 65:2) — the shared silo. Storage is ONE capacity across every good (§7, Hay Day's
    /// model), so this surface answers two questions: how full am I, and what is taking up the room. It shows
    /// the capacity bar, the contents, and a single tiered expand track.
    ///
    /// Self-selects on station type off the shared StationPanelRequested, exactly as OrderBoardPanel does —
    /// one routing event, no System-side table of which panel belongs to which building.
    public sealed class SiloPanel : MonoBehaviour
    {
        [Header("Chrome (authored)")]
        [SerializeField] GameObject panelRoot;
        [SerializeField] Button closeButton;

        [Header("Capacity block")]
        [SerializeField] Text capacityText;
        [SerializeField] Text capacityNote;
        [SerializeField] Image capacityBarFill;

        [Header("Lists")]
        [SerializeField] Transform storedList;
        [SerializeField] ResourceRow storedRowTemplate;
        [SerializeField] Transform upgradeList;
        [SerializeField] UpgradeRow upgradeRowTemplate;

        [Header("Which station opens this")]
        [SerializeField] StationSO siloStation;
        [SerializeField] UiThemeSO theme;

        [Header("Copy")]
        [Tooltip("Note beside the capacity while there is room.")]
        [SerializeField] string roomNote = "shared by every good";
        [Tooltip("Note beside the capacity once collection is being refused.")]
        [SerializeField] string fullNote = "full — sell or spend to collect";

        EventBus _bus;
        ResourcePool _pool;
        JobSystem _jobs;
        UpgradeSystem _upgrades;
        Wallet _wallet;
        IReadOnlyList<ResourceSO> _resources;

        string _openStationId;
        bool _open;

        public void Init(EventBus bus, ResourcePool pool, JobSystem jobs, UpgradeSystem upgrades,
            Wallet wallet, IReadOnlyList<ResourceSO> resources)
        {
            _bus = bus;
            _pool = pool;
            _jobs = jobs;
            _upgrades = upgrades;
            _wallet = wallet;
            _resources = resources;

            closeButton.onClick.AddListener(Close);
            panelRoot.SetActive(false);

            _bus.Subscribe<StationPanelRequested>(OnPanelRequested);
            _bus.Subscribe<BackgroundTapped>(OnBackgroundTapped);
            _bus.Subscribe<ExclusiveUiOpened>(OnExclusiveUiOpened);
            _bus.Subscribe<GameReset>(OnGameReset);
            _bus.Subscribe<ResourceChanged>(OnResourceChanged);     // the bar and the contents move
            _bus.Subscribe<EffectsRecalculated>(OnEffectsChanged);  // an expand purchase raised capacity
            _bus.Subscribe<MoneyChanged>(OnMoneyChanged);           // Buy may have become (un)affordable
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<StationPanelRequested>(OnPanelRequested);
            _bus.Unsubscribe<BackgroundTapped>(OnBackgroundTapped);
            _bus.Unsubscribe<ExclusiveUiOpened>(OnExclusiveUiOpened);
            _bus.Unsubscribe<GameReset>(OnGameReset);
            _bus.Unsubscribe<ResourceChanged>(OnResourceChanged);
            _bus.Unsubscribe<EffectsRecalculated>(OnEffectsChanged);
            _bus.Unsubscribe<MoneyChanged>(OnMoneyChanged);
        }

        void OnPanelRequested(StationPanelRequested e)
        {
            // Tapping any other station closes this one — panels are one-at-a-time.
            if (_jobs.StationTypeOf(e.StationId) != siloStation.stationType) { Close(); return; }
            _openStationId = e.StationId;
            _open = true;
            panelRoot.SetActive(true);
            _bus.Publish(new ExclusiveUiOpened("silo")); // retract the build menu / other panels
            Rebuild();
        }

        void OnBackgroundTapped(BackgroundTapped _) => Close();
        void OnExclusiveUiOpened(ExclusiveUiOpened e) { if (e.Source != "silo") Close(); }
        void OnGameReset(GameReset _) => Close();
        void OnResourceChanged(ResourceChanged _) => RebuildIfOpen();
        void OnEffectsChanged(EffectsRecalculated _) => RebuildIfOpen();
        void OnMoneyChanged(MoneyChanged _) => RebuildIfOpen();

        void Close()
        {
            if (!_open) return; // only a real open→closed transition announces itself (many callers)
            _open = false;
            panelRoot.SetActive(false);
            _bus.Publish(new ExclusiveUiClosed("silo"));
        }

        void RebuildIfOpen()
        {
            if (_open) Rebuild();
        }

        void Rebuild()
        {
            BuildCapacity();
            BuildStored();
            BuildUpgrades();
        }

        void BuildCapacity()
        {
            int stored = _pool.TotalStored;
            int capacity = _pool.Capacity;
            bool full = stored >= capacity;

            capacityText.text = $"{stored} / {capacity}";
            capacityText.color = full ? theme.warning : theme.ink;
            capacityNote.text = full ? fullNote : roomNote;
            capacityNote.color = full ? theme.warning : theme.ink;

            capacityBarFill.fillAmount = capacity <= 0 ? 0f : Mathf.Clamp01((float)stored / capacity);
            capacityBarFill.color = full ? theme.warning : theme.accent;
        }

        /// Only goods actually held are listed — a roster of zeroes answers nothing, and the question this
        /// list exists to answer is "what is taking up my room".
        void BuildStored()
        {
            Clear(storedList);
            foreach (var so in _resources)
            {
                int count = _pool.Get(so.id);
                if (count <= 0) continue;
                var row = Instantiate(storedRowTemplate, storedList);
                row.Bind(so.displayName, so.icon, count);
            }
        }

        /// Same shape as StationPanel's upgrades tab — one row per track, describing the tier on offer.
        void BuildUpgrades()
        {
            Clear(upgradeList);
            string stationId = _openStationId;
            foreach (var track in _upgrades.TracksFor(stationId))
            {
                int tier = _upgrades.TierOf(stationId, track.Id);
                bool maxed = tier >= track.MaxTier;

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

        static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }
    }
}
