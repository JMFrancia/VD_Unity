using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;

namespace VoidDay.View
{
    /// The live quest-progress pill (Figma frame 04). A single pill that drops from behind the XP bar when a
    /// quest advances, chases its bar to the new progress, holds a beat, then retracts. When a tick COMPLETES a
    /// quest the pill instead holds flashing green, inviting a tap that collects the reward.
    ///
    /// One pill, newest-progress-wins: a fresh QuestProgressed / QuestCompleted refreshes the same pill rather
    /// than spawning a second. It listens to quest FACTS and publishes the collect INTENT (rule 2) — it never
    /// touches QuestLog. Descriptions ride in on QuestGranted (the only quest event that carries the text) and
    /// are cached so a later progress/completion event can label the pill.
    ///
    /// The slide/fade/scale "emerge from behind a pill" motion mirrors ResourcePillRail; the bar chase mirrors
    /// LevelXpHud (MoveTowards, so the bar creeps rather than snaps). Every timing and colour here is
    /// presentation — a View field read live, never a QuestSO value.
    public sealed class QuestPill : MonoBehaviour
    {
        [Header("Chrome (authored)")]
        [SerializeField] CanvasGroup group;
        [SerializeField] Button button;          // the whole pill is the collect button while completed
        [SerializeField] Image background;        // the cream pill face — tinted green while flashing
        [SerializeField] Text descriptionText;
        [SerializeField] Image barFill;           // Image Type=Filled, Horizontal
        [SerializeField] Text percentText;

        [Tooltip("The XP pill this tucks behind and emerges from. Same canvas + top-center anchor, so its " +
                 "anchoredPosition is the pill's hidden position.")]
        [SerializeField] RectTransform hidePoint;

        [Header("Slide feel")]
        [SerializeField] float slideSeconds = 0.32f;
        [SerializeField] Ease slideEase = Ease.OutBack;
        [Tooltip("Scale of the pill while tucked behind the XP bar.")]
        [SerializeField] float hiddenScale = 0.9f;

        [Header("Bar feel")]
        [Tooltip("Fractions of the bar per second the fill chases its target — mirrors LevelXpHud.")]
        [SerializeField] float fillChasePerSecond = 1.1f;

        [Header("Progress hold")]
        [Tooltip("Seconds a progress pill lingers at its new value before it retracts.")]
        [SerializeField] float progressHoldSeconds = 2.2f;

        [Header("Completion")]
        [Tooltip("Seconds a completed pill stays up flashing before retracting untapped. Spec floor: 20s.")]
        [SerializeField] float completionHoldSeconds = 20f;
        [Tooltip("Shown in place of the quest description while the pill is inviting a collect tap.")]
        [SerializeField] string completionPrompt = "Complete!  Tap to collect";
        [Tooltip("Colour the pill face pulses toward while flashing green in the completion state.")]
        [SerializeField] Color completionFlashColor = new(0.49f, 0.745f, 0.353f, 1f);
        [Tooltip("Seconds for one full flash cycle (resting → green → resting) in the completion state.")]
        [SerializeField] float flashPeriod = 0.7f;

        EventBus _bus;
        readonly Dictionary<string, string> _descriptions = new();

        enum State { Hidden, Progress, Completion }
        State _state = State.Hidden;
        string _currentId;      // quest the pill currently represents
        string _shownFillId;    // quest the bar's current fillAmount belongs to — a change snaps the bar to 0
        Vector2 _shownPos;
        Color _restingBg;
        float _targetFill;
        float _holdRemaining;
        float _flashTime;

        RectTransform Rect => (RectTransform)transform;

        public void Init(EventBus bus)
        {
            _bus = bus;
            _shownPos = Rect.anchoredPosition; // authored shown position; capture before tucking it away
            _restingBg = background.color;

            Rect.anchoredPosition = hidePoint.anchoredPosition;
            Rect.localScale = Vector3.one * hiddenScale;
            group.alpha = 0f;
            group.blocksRaycasts = false;

            button.onClick.AddListener(OnTap);

            _bus.Subscribe<QuestGranted>(OnQuestGranted);
            _bus.Subscribe<QuestProgressed>(OnQuestProgressed);
            _bus.Subscribe<QuestCompleted>(OnQuestCompleted);
            _bus.Subscribe<QuestCollected>(OnQuestCollected);
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<QuestGranted>(OnQuestGranted);
            _bus.Unsubscribe<QuestProgressed>(OnQuestProgressed);
            _bus.Unsubscribe<QuestCompleted>(OnQuestCompleted);
            _bus.Unsubscribe<QuestCollected>(OnQuestCollected);
            Rect.DOKill();
            group.DOKill();
        }

        // A grant carries the only copy of the description text; cache it so progress/completion can label the
        // pill. A grant itself does NOT drop a pill (that is the toast's job — M2).
        void OnQuestGranted(QuestGranted e) => _descriptions[e.QuestId] = e.Description;

        void OnQuestProgressed(QuestProgressed e)
        {
            PrepareFor(e.QuestId);
            _targetFill = Mathf.Clamp01(e.Progress);
            descriptionText.text = Describe(e.QuestId);
            background.color = _restingBg; // a fresh progress cancels any leftover green flash
            _holdRemaining = progressHoldSeconds;
            Enter(State.Progress);
        }

        void OnQuestCompleted(QuestCompleted e)
        {
            PrepareFor(e.QuestId);
            _targetFill = 1f;
            descriptionText.text = completionPrompt;
            _holdRemaining = completionHoldSeconds;
            _flashTime = 0f;
            Enter(State.Completion);
        }

        // Collect can also come from the menu while the pill is up for that quest — retract in step with it.
        void OnQuestCollected(QuestCollected e)
        {
            if (_state == State.Completion && _currentId == e.QuestId) Retract();
        }

        void OnTap()
        {
            if (_state != State.Completion) return;
            _bus.Publish(new CollectQuestRequested(_currentId)); // → QuestLog collect → QuestCollected → Retract
        }

        string Describe(string questId) =>
            _descriptions.TryGetValue(questId, out var d) ? d : "";

        // Point the pill at a quest; if the bar is currently showing a DIFFERENT quest, snap it to empty so the
        // new quest's bar fills up from zero rather than sliding down from the previous quest's value.
        void PrepareFor(string questId)
        {
            _currentId = questId;
            if (_shownFillId != questId) barFill.fillAmount = 0f;
            _shownFillId = questId;
        }

        void Enter(State s)
        {
            bool wasHidden = _state == State.Hidden;
            _state = s;
            group.blocksRaycasts = s == State.Completion; // only a completed pill is tappable
            if (wasHidden) Show();
        }

        void Show()
        {
            Rect.DOKill();
            group.DOKill();
            Rect.DOAnchorPos(_shownPos, slideSeconds).SetEase(slideEase);
            Rect.DOScale(1f, slideSeconds).SetEase(slideEase);
            group.DOFade(1f, slideSeconds);
        }

        void Retract()
        {
            _state = State.Hidden;
            group.blocksRaycasts = false;
            background.color = _restingBg;
            Rect.DOKill();
            group.DOKill();
            Rect.DOAnchorPos(hidePoint.anchoredPosition, slideSeconds).SetEase(slideEase);
            Rect.DOScale(hiddenScale, slideSeconds).SetEase(slideEase);
            group.DOFade(0f, slideSeconds);
        }

        void Update()
        {
            barFill.fillAmount = Mathf.MoveTowards(barFill.fillAmount, _targetFill,
                fillChasePerSecond * Time.deltaTime);
            percentText.text = $"{Mathf.RoundToInt(barFill.fillAmount * 100f)}%";

            if (_state == State.Hidden) return;

            if (_state == State.Completion)
            {
                _flashTime += Time.deltaTime;
                float t = 0.5f + 0.5f * Mathf.Sin(_flashTime * (2f * Mathf.PI / flashPeriod));
                background.color = Color.Lerp(_restingBg, completionFlashColor, t);
            }

            _holdRemaining -= Time.deltaTime;
            if (_holdRemaining <= 0f) Retract();
        }
    }
}
