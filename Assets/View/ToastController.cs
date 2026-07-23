using UnityEngine;
using VoidDay.Core.Events;

namespace VoidDay.View
{
    /// Owns the toast corner (toast.generic, UI-Mockups node 36:2). It listens to domain facts and decides,
    /// alone, which of them deserve a notice — the same contract SfxController has for sound (CLAUDE.md rule
    /// 2, and UI-Inventory: there are no `ui:*` events; nothing tells a toast to appear).
    ///
    /// Core supplies the fact, the View supplies the English — every string here is a serialized field, the
    /// same split LevelUpPopup uses for its grant lines.
    public sealed class ToastController : MonoBehaviour
    {
        [Header("Scene wiring")]
        [Tooltip("Corner container the toasts stack inside — authored with a VerticalLayoutGroup.")]
        [SerializeField] RectTransform stack;
        [SerializeField] Toast template;

        [Header("Timing")]
        [SerializeField] float lifetime = 3f;
        [SerializeField] float fadeSeconds = 0.35f;

        [Header("Copy + icons (Core gives facts, the View gives English)")]
        [SerializeField] string storageFullMessage = "Not enough storage! Upgrade Silo";
        [SerializeField] Sprite storageFullIcon;
        [SerializeField] string questGrantedPrefix = "New Quest: ";
        [SerializeField] Sprite questGrantedIcon;

        EventBus _bus;

        public void Init(EventBus bus)
        {
            _bus = bus;
            template.gameObject.SetActive(false); // the template is a hidden authoring seat, never shown
            _bus.Subscribe<CollectRefused>(OnCollectRefused);
            _bus.Subscribe<QuestGranted>(OnQuestGranted);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<CollectRefused>(OnCollectRefused);
            _bus.Unsubscribe<QuestGranted>(OnQuestGranted);
        }

        // Reason-agnostic today because storage is the only way a collect gets refused (§4.4). A second
        // reason means a switch here, not a new listener.
        void OnCollectRefused(CollectRefused _) => Show(storageFullMessage, storageFullIcon);

        // The Core-generated description is the fact ("Harvest 10 wheat"); the View prepends the "New Quest:"
        // English. ToastStack handles two near-simultaneous grants — no dedup needed here.
        void OnQuestGranted(QuestGranted e) => Show(questGrantedPrefix + e.Description, questGrantedIcon);

        void Show(string message, Sprite icon)
        {
            var toast = Instantiate(template, stack);
            toast.gameObject.SetActive(true);
            toast.Show(message, icon, lifetime, fadeSeconds);
        }
    }
}
