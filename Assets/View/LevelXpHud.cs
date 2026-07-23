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
    ///
    /// XP that is currently flying as a star burst is held back: the bar draws Core's XP MINUS the amount
    /// still in the air, so it creeps up per star instead of jumping at the moment of the grant. This
    /// listens for the burst events like any other subscriber — the burst controller never calls it.
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
        [Tooltip("How large the badge swells when one XP star lands. Deliberately gentler than a level-up.")]
        [SerializeField] float particlePopScale = 1.12f;
        [Tooltip("Seconds the badge takes to settle back after one XP star lands.")]
        [SerializeField] float particlePopSeconds = 0.16f;

        Progression _progression;
        EventBus _bus;
        float _targetFill;

        /// XP that has left the world but not yet landed on the badge. Held back from the bar.
        int _pendingXp;

        float _popRemaining;

        /// The pop's amplitude and duration are captured when it is TRIGGERED, not read from the serialized
        /// level-up numbers each frame. Reusing badgePopScale/Seconds for a shorter pop would start the sine
        /// mid-curve, so the badge would snap to near-full amplitude and decay rather than pop smaller.
        float _popScale;
        float _popSeconds;

        public void Init(EventBus bus, Progression progression)
        {
            _bus = bus;
            _progression = progression;

            _bus.Subscribe<XpGained>(OnXpGained);
            _bus.Subscribe<LevelUp>(OnLevelUp);
            _bus.Subscribe<GameReset>(OnGameReset);
            _bus.Subscribe<EarnBurstLaunched>(OnEarnBurstLaunched);
            _bus.Subscribe<EarnParticleArrived>(OnEarnParticleArrived);

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
            _bus.Unsubscribe<EarnBurstLaunched>(OnEarnBurstLaunched);
            _bus.Unsubscribe<EarnParticleArrived>(OnEarnParticleArrived);
        }

        void OnXpGained(XpGained _) => Sync();
        void OnGameReset(GameReset _) { _pendingXp = 0; Sync(); barFill.fillAmount = _targetFill; }

        void OnLevelUp(LevelUp _)
        {
            Sync();
            barFill.fillAmount = 0f; // the level that just completed drained; start the new one from empty
            Pop(badgePopScale, badgePopSeconds);
        }

        void OnEarnBurstLaunched(EarnBurstLaunched e)
        {
            if (e.Kind != EarnKind.Xp) return;
            _pendingXp += e.Amount;
            Sync();
        }

        void OnEarnParticleArrived(EarnParticleArrived e)
        {
            if (e.Kind != EarnKind.Xp) return;
            _pendingXp = Mathf.Max(0, _pendingXp - e.Amount);
            Sync();
            Pop(particlePopScale, particlePopSeconds);
        }

        void Pop(float scale, float seconds)
        {
            _popScale = scale;
            _popSeconds = seconds;
            _popRemaining = seconds;
        }

        /// Clamped at 0 rather than tracked across a level-up: stars still in the air when a threshold is
        /// crossed simply drain into the new level from empty. Understated for a moment, self-correcting.
        void Sync()
        {
            levelText.text = _progression.PlayerLevel.ToString();
            int span = _progression.XpSpanOfLevel;
            int shown = Mathf.Max(0, _progression.XpIntoLevel - _pendingXp);
            _targetFill = span <= 0 ? 1f : Mathf.Clamp01((float)shown / span);
        }

        void Update()
        {
            barFill.fillAmount = Mathf.MoveTowards(barFill.fillAmount, _targetFill,
                fillChasePerSecond * Time.deltaTime);

            if (_popRemaining <= 0f) return;
            _popRemaining -= Time.deltaTime;
            float t = Mathf.Clamp01(_popRemaining / _popSeconds);
            badge.localScale = Vector3.one * (1f + (_popScale - 1f) * Mathf.Sin(t * Mathf.PI));
        }
    }
}
