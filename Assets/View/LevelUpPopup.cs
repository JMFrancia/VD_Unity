using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Data;

namespace VoidDay.View
{
    /// popup.levelUp (§12.4, mockup 24:2) — the celebration: new level, what it unlocked, what it paid.
    ///
    /// Everything it shows comes from the level:up payload, which carries structured facts, not sentences —
    /// the wording lives here as serialized copy so a designer can reword a line without touching Core.
    ///
    /// Level-ups **queue**: one fat XP grant can cross several thresholds, and each gets its own screen,
    /// advanced by the confirm button. Skipping straight to the last level would hide what the middle ones
    /// gave you.
    public sealed class LevelUpPopup : MonoBehaviour
    {
        [Header("Chrome (authored)")]
        [SerializeField] GameObject panelRoot;
        [SerializeField] Text levelText;
        [SerializeField] Button confirmButton;

        [Header("Unlock list")]
        [SerializeField] GameObject unlockSection;   // header + list, hidden when a level unlocks nothing
        [SerializeField] Transform unlockList;
        [SerializeField] UnlockRow unlockRowTemplate;

        [Header("Reward block")]
        [SerializeField] GameObject rewardSection;   // hidden when a level pays nothing
        [SerializeField] Image rewardIcon;
        [SerializeField] Text rewardText;
        [SerializeField] Sprite moneyIcon;
        [SerializeField] Sprite gemIcon;

        [Header("Icons")]
        [Tooltip("Shown for an unlocked upgrade track.")]
        [SerializeField] Sprite upgradeIcon;
        [Tooltip("Shown for a raised cap / queue depth / order-slot count.")]
        [SerializeField] Sprite grantIcon;

        [Header("Copy (§3.6-style wording lives in data, not Core)")]
        [SerializeField] string stationFormat = "{0} station";
        [SerializeField] string upgradeFormat = "{0} upgrade";
        [SerializeField] string recipeFormat = "{0} recipe";
        [SerializeField] string capFormat = "+{0} {1} cap";
        [SerializeField] string queueAllFormat = "+{0} queue depth on every station";
        [SerializeField] string queueOneFormat = "+{0} queue depth at {1}";
        [SerializeField] string slotFormat = "+{0} order slots";
        [SerializeField] string moneyFormat = "${0}";
        [SerializeField] string gemFormat = "{0} gems";

        EventBus _bus;
        IReadOnlyList<StationSO> _roster;
        readonly Queue<LevelUp> _pending = new();

        public void Init(EventBus bus, IReadOnlyList<StationSO> roster)
        {
            _bus = bus;
            _roster = roster;

            confirmButton.onClick.AddListener(Advance);
            panelRoot.SetActive(false);

            _bus.Subscribe<LevelUp>(OnLevelUp);
            _bus.Subscribe<GameReset>(OnGameReset);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<LevelUp>(OnLevelUp);
            _bus.Unsubscribe<GameReset>(OnGameReset);
        }

        void OnLevelUp(LevelUp e)
        {
            _pending.Enqueue(e);
            if (!panelRoot.activeSelf) Advance();
        }

        void OnGameReset(GameReset _)
        {
            _pending.Clear();
            Hide();
        }

        /// Show the next queued level, or close when the queue runs dry.
        void Advance()
        {
            if (_pending.Count == 0) { Hide(); return; }

            bool wasOpen = panelRoot.activeSelf;
            var level = _pending.Dequeue();
            panelRoot.SetActive(true);
            // Announced on the first screen only — a menu should retract once, not once per queued level.
            if (!wasOpen) _bus.Publish(new ExclusiveUiOpened("levelUp"));

            levelText.text = level.Level.ToString();
            BuildUnlocks(level.Unlocks);
            BuildReward(level.Rewards);
        }

        void Hide()
        {
            if (!panelRoot.activeSelf) return;
            panelRoot.SetActive(false);
            _bus.Publish(new ExclusiveUiClosed("levelUp"));
        }

        void BuildUnlocks(IReadOnlyList<LevelEntry> unlocks)
        {
            for (int i = unlockList.childCount - 1; i >= 0; i--)
                Destroy(unlockList.GetChild(i).gameObject);

            unlockSection.SetActive(unlocks.Count > 0);
            foreach (var entry in unlocks)
                Instantiate(unlockRowTemplate, unlockList).Bind(IconFor(entry), Describe(entry));
        }

        void BuildReward(IReadOnlyList<LevelEntry> rewards)
        {
            rewardSection.SetActive(rewards.Count > 0);
            if (rewards.Count == 0) return;

            var reward = rewards[0]; // boot validation caps a level at one reward
            rewardIcon.sprite = reward.Kind == LevelEntryKind.Gems ? gemIcon : moneyIcon;
            rewardText.text = Describe(reward);
        }

        string Describe(LevelEntry e) => e.Kind switch
        {
            LevelEntryKind.StationType => string.Format(stationFormat, e.Label),
            LevelEntryKind.Upgrade => string.Format(upgradeFormat, e.Label),
            LevelEntryKind.Recipe => string.Format(recipeFormat, e.Label),
            LevelEntryKind.StationCap => string.Format(capFormat, e.Amount, e.Label),
            LevelEntryKind.QueueDepth => string.IsNullOrEmpty(e.Label)
                ? string.Format(queueAllFormat, e.Amount)
                : string.Format(queueOneFormat, e.Amount, e.Label),
            LevelEntryKind.OrderSlots => string.Format(slotFormat, e.Amount),
            LevelEntryKind.Money => string.Format(moneyFormat, e.Amount),
            LevelEntryKind.Gems => string.Format(gemFormat, e.Amount),
            _ => e.Label
        };

        /// A station shows its own build-menu thumbnail — the same picture the player will go tap to build it.
        Sprite IconFor(LevelEntry e)
        {
            if (e.Kind == LevelEntryKind.Upgrade) return upgradeIcon;
            if (e.Kind != LevelEntryKind.StationType && e.Kind != LevelEntryKind.StationCap) return grantIcon;

            foreach (var so in _roster)
                if (so.stationType == e.Id) return so.buildThumbnail;
            return grantIcon;
        }
    }
}
