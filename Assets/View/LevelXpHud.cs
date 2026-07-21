using UnityEngine;
using UnityEngine.UI;
using VoidDay.Core.Events;
using VoidDay.Core.Rules;

namespace VoidDay.View
{
    /// hud.levelXp (§12.1, mockup sheet 37:2) — the level badge and XP bar, top-center. Display only.
    ///
    /// The threshold rule is not here: Core hands over "XP into this level" and "the span of this level" and
    /// this draws the ratio (the UI contract is explicit that the View reads the threshold rather than
    /// computing it). The bar chases its target rather than snapping, so a tick of XP reads as movement.
    public sealed class LevelXpHud : MonoBehaviour
    {
        [Header("Chrome (authored)")]
        [SerializeField] Text levelText;
        [SerializeField] Image barFill;
        [SerializeField] RectTransform badge;

        [Header("Feel")]
        [Tooltip("How fast the bar chases its target fill, in fractions of the bar per second.")]
        [SerializeField] float fillChasePerSecond = 1.2f;
        [Tooltip("How large the badge swells on a level-up, as a multiple of its resting size.")]
        [SerializeField] float badgePopScale = 1.4f;
        [Tooltip("Seconds the badge takes to settle back after a level-up.")]
        [SerializeField] float badgePopSeconds = 0.45f;

        Progression _progression;
        EventBus _bus;
        float _targetFill;
        float _popRemaining;

        public void Init(EventBus bus, Progression progression)
        {
            _bus = bus;
            _progression = progression;

            _bus.Subscribe<XpGained>(OnXpGained);
            _bus.Subscribe<LevelUp>(OnLevelUp);
            _bus.Subscribe<GameReset>(OnGameReset);

            // XP has been banking since M3 with nothing to show it — the bar's first frame is that history.
            Sync();
            barFill.fillAmount = _targetFill;
        }

        void OnDestroy()
        {
            if (_bus == null) return;
            _bus.Unsubscribe<XpGained>(OnXpGained);
            _bus.Unsubscribe<LevelUp>(OnLevelUp);
            _bus.Unsubscribe<GameReset>(OnGameReset);
        }

        void OnXpGained(XpGained _) => Sync();
        void OnGameReset(GameReset _) { Sync(); barFill.fillAmount = _targetFill; }

        void OnLevelUp(LevelUp _)
        {
            Sync();
            barFill.fillAmount = 0f; // the level that just completed drained; start the new one from empty
            _popRemaining = badgePopSeconds;
        }

        void Sync()
        {
            levelText.text = _progression.PlayerLevel.ToString();
            int span = _progression.XpSpanOfLevel;
            _targetFill = span <= 0 ? 1f : Mathf.Clamp01((float)_progression.XpIntoLevel / span);
        }

        void Update()
        {
            barFill.fillAmount = Mathf.MoveTowards(barFill.fillAmount, _targetFill,
                fillChasePerSecond * Time.deltaTime);

            if (_popRemaining <= 0f) return;
            _popRemaining -= Time.deltaTime;
            float t = Mathf.Clamp01(_popRemaining / badgePopSeconds);
            badge.localScale = Vector3.one * (1f + (badgePopScale - 1f) * Mathf.Sin(t * Mathf.PI));
        }
    }
}
