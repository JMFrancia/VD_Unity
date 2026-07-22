using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Model;
using VoidDay.Core.Rules;
using VoidDay.Data;

namespace VoidDay.View
{
    /// popup.skipConfirm (mockup 71:2) — the one confirmation between a tapped timer and a gem spend, shared
    /// by all three timer kinds. Chrome and copy are authored in the scene; this only binds the live price.
    ///
    /// It deliberately does NOT publish ExclusiveUiOpened. Every other surface in the project does, and
    /// doing so here would retract the very Order Board this popup was opened from. It is the project's
    /// first *stacking* surface — it sits on top of whatever launched it. It still listens for
    /// ExclusiveUiClosed, so it never outlives the surface underneath it.
    ///
    /// The price falls as the timer runs down, so an open popup re-reads it every frame — the same reason
    /// OrderBoardPanel rebuilds every frame while open. Confirm charges nothing itself: it publishes the
    /// intent and Core does the spending.
    public sealed class SkipConfirmPopup : MonoBehaviour
    {
        [Header("Chrome (authored)")]
        [SerializeField] GameObject popupRoot;
        [SerializeField] Text costText;
        [SerializeField] Image gemGlyph;
        [SerializeField] Button confirmButton;
        [SerializeField] Image confirmButtonImage;
        [SerializeField] Text confirmLabel;
        [SerializeField] Button cancelButton;

        [Tooltip("Shown only when the purse is short. {0} is how many more gems are needed.")]
        [SerializeField] Text shortfallText;

        [Tooltip("Says what the player is buying. One popup serves three timer kinds, so the line is per-kind.")]
        [SerializeField] Text subtitleText;

        [Header("State colors")]
        [SerializeField] UiThemeSO theme;

        [Header("Copy")]
        [SerializeField] string costFormat = "{0}";
        [SerializeField] string shortfallFormat = "You need {0} more gems.";
        [SerializeField] string jobSubtitle = "The job finishes instantly.";
        [SerializeField] string constructionSubtitle = "The building finishes instantly.";
        [SerializeField] string orderRefillSubtitle = "Order slot refills instantly.";

        EventBus _bus;
        TimeSkip _skip;
        GemPurse _gems;

        TimerRef _timer;
        bool _open;

        public void Init(EventBus bus, TimeSkip skip, GemPurse gems)
        {
            _bus = bus;
            _skip = skip;
            _gems = gems;

            confirmButton.onClick.AddListener(Confirm);
            cancelButton.onClick.AddListener(Close);
            popupRoot.SetActive(false);

            _bus.Subscribe<TimerSkipTapped>(OnSkipTapped);
            _bus.Subscribe<ExclusiveUiClosed>(_ => Close()); // never outlive the surface we stack on
            _bus.Subscribe<GameReset>(_ => Close());
        }

        void OnSkipTapped(TimerSkipTapped e)
        {
            if (!_skip.CanSkip(e.Timer, Time.timeAsDouble)) return; // the timer expired between tap and here
            _timer = e.Timer;
            _open = true;
            subtitleText.text = SubtitleFor(e.Timer.Kind);
            popupRoot.SetActive(true);
            Refresh();
        }

        /// The kind never changes while a popup is open, so it is bound once on open rather than in Refresh.
        string SubtitleFor(TimerKind kind) => kind switch
        {
            TimerKind.Job => jobSubtitle,
            TimerKind.Construction => constructionSubtitle,
            TimerKind.OrderRefill => orderRefillSubtitle,
            _ => throw new System.InvalidOperationException($"Unhandled timer kind {kind}")
        };

        /// The timer keeps running behind the popup, so both the price and the afford state are live.
        /// Closing when the timer stops being skippable is what stops a stale popup charging for nothing.
        void Update()
        {
            if (_open) Refresh();
        }

        void Refresh()
        {
            double now = Time.timeAsDouble;
            if (!_skip.CanSkip(_timer, now)) { Close(); return; }

            int cost = _skip.CostFor(_timer, now);
            costText.text = string.Format(costFormat, cost);

            bool affordable = _gems.CanAfford(cost);
            confirmButton.interactable = affordable;
            confirmButtonImage.color = affordable ? theme.gemAccent : theme.lockedBg;
            confirmLabel.color = affordable ? theme.accentText : theme.lockedText;
            gemGlyph.color = affordable ? theme.gemAccent : theme.lockedText;

            shortfallText.gameObject.SetActive(!affordable);
            if (!affordable) shortfallText.text = string.Format(shortfallFormat, cost - _gems.Gems);
        }

        void Confirm()
        {
            var timer = _timer;
            Close();                                       // the board underneath re-renders on the skip
            _bus.Publish(new TimerSkipConfirmed(timer));
        }

        void Close()
        {
            if (!_open) return;
            _open = false;
            popupRoot.SetActive(false);
        }
    }
}
