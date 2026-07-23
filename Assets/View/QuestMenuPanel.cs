using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;

namespace VoidDay.View
{
    /// panel.quests (Figma frame 01) — the quest menu. Follows the SiloPanel / OrderBoardPanel contract: a
    /// panelRoot that starts inactive, opens from its own HUD button, publishes ExclusiveUiOpened("quests") /
    /// ExclusiveUiClosed so it is one-menu-at-a-time, and closes when another exclusive surface opens or the
    /// background is tapped.
    ///
    /// It renders from QuestLog's read snapshot: ready-to-collect quests pinned to the top and highlighted,
    /// in-progress quests below. It never mutates quest state — tapping a ready row publishes the
    /// CollectQuestRequested intent and lets Core apply the reward.
    ///
    /// The open button lives on HudCanvas but is wired here (a [SerializeField] Button, like SiloPanel's
    /// closeButton) rather than in Hud.cs, so the menu stays self-contained and no extra routing event is
    /// needed to cross from the HUD to the panel.
    public sealed class QuestMenuPanel : MonoBehaviour
    {
        [Header("Chrome (authored)")]
        [SerializeField] GameObject panelRoot;
        [SerializeField] Button openButton;   // the gold quest button on HudCanvas
        [SerializeField] Button closeButton;

        [Header("List")]
        [SerializeField] Transform contentRoot;   // the ScrollRect Content (VerticalLayoutGroup)
        [SerializeField] QuestRow rowTemplate;     // prefab, instantiated per quest
        [Tooltip("Optional 'no quests yet' label, shown when the active list is empty.")]
        [SerializeField] GameObject emptyLabel;

        EventBus _bus;
        QuestLog _log;
        bool _open;

        public void Init(EventBus bus, QuestLog log)
        {
            _bus = bus;
            _log = log;

            openButton.onClick.AddListener(Toggle);
            closeButton.onClick.AddListener(Close);
            panelRoot.SetActive(false);

            _bus.Subscribe<QuestGranted>(OnQuestGranted);
            _bus.Subscribe<QuestProgressed>(OnQuestProgressed);
            _bus.Subscribe<QuestCompleted>(OnQuestCompleted);
            _bus.Subscribe<QuestCollected>(OnQuestCollected);
            _bus.Subscribe<ExclusiveUiOpened>(OnExclusiveUiOpened);
            _bus.Subscribe<BackgroundTapped>(OnBackgroundTapped);
            _bus.Subscribe<GameReset>(OnGameReset);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<QuestGranted>(OnQuestGranted);
            _bus.Unsubscribe<QuestProgressed>(OnQuestProgressed);
            _bus.Unsubscribe<QuestCompleted>(OnQuestCompleted);
            _bus.Unsubscribe<QuestCollected>(OnQuestCollected);
            _bus.Unsubscribe<ExclusiveUiOpened>(OnExclusiveUiOpened);
            _bus.Unsubscribe<BackgroundTapped>(OnBackgroundTapped);
            _bus.Unsubscribe<GameReset>(OnGameReset);
        }

        void OnQuestGranted(QuestGranted _) => RebuildIfOpen();
        void OnQuestProgressed(QuestProgressed _) => RebuildIfOpen();
        void OnQuestCompleted(QuestCompleted _) => RebuildIfOpen();
        void OnQuestCollected(QuestCollected _) => RebuildIfOpen();
        void OnExclusiveUiOpened(ExclusiveUiOpened e) { if (e.Source != "quests") Close(); }
        void OnBackgroundTapped(BackgroundTapped _) => Close();
        void OnGameReset(GameReset _) => Close();

        void Toggle() { if (_open) Close(); else Open(); }

        void Open()
        {
            if (_open) return;
            _open = true;
            panelRoot.SetActive(true);
            _bus.Publish(new ExclusiveUiOpened("quests")); // retract the build menu / other panels
            Rebuild();
        }

        void Close()
        {
            if (!_open) return; // only a real open→closed transition announces itself (many callers)
            _open = false;
            panelRoot.SetActive(false);
            _bus.Publish(new ExclusiveUiClosed("quests"));
        }

        void RebuildIfOpen()
        {
            if (_open) Rebuild();
        }

        /// Clear-and-rebuild (like SiloPanel.BuildStored) — the list is short and only re-renders on a quest
        /// event while open. Ready quests are pinned above active ones; order within each group is grant order.
        void Rebuild()
        {
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.GetChild(i).gameObject);

            var quests = _log.ActiveQuests();
            if (emptyLabel != null) emptyLabel.SetActive(quests.Count == 0);

            var pending = new List<QuestStatus>();
            foreach (var q in quests)
                if (q.Ready) AddRow(q); else pending.Add(q);
            foreach (var q in pending) AddRow(q);
        }

        void AddRow(QuestStatus q)
        {
            var row = Instantiate(rowTemplate, contentRoot);
            row.Bind(q.Description, q.Progress, q.Ready);
            if (!q.Ready) return;

            string id = q.Id; // hoist: the closure must capture this row's id, not the loop's last
            row.Button.onClick.AddListener(() => _bus.Publish(new CollectQuestRequested(id)));
        }
    }
}
