using UnityEngine;

namespace VoidDay.View
{
    /// One in-world job-queue slot (world.queueSlots, panel.station ALT 42:2). The prefab authors the look
    /// (billboarded outline sprite, resource chip, lock glyph, slot-0 mini progress); WorldState instantiates
    /// one per slot up to the station's upgrade ceiling and drives the state. StationId/SlotIndex identify the
    /// slot to InputRouter — a tap on a filled slot is a cancel request.
    ///
    /// The three states are one sprite tinted three ways, not three materials: the outline art is white so a
    /// SpriteRenderer colour is the whole difference, which is why the locked look (translucent outline +
    /// translucent padlock) costs no extra asset.
    public sealed class QueueSlot : MonoBehaviour
    {
        public enum SlotState
        {
            Empty,   // unlocked, nothing queued — outline only
            Filled,  // a job sits here — outline + its output chip; tappable to cancel
            Ready,   // the head is done and collectable — pulsing green behind the chip; tapping collects
            Locked   // beyond the current queue depth, buyable via the station's upgrade track
        }

        [Header("Parts (authored)")]
        [SerializeField] SpriteRenderer outline;
        [SerializeField] SpriteRenderer readyFill; // green pad behind the chip, pulsing while collectable
        [SerializeField] SpriteRenderer chipIcon;  // the produced good's icon, shown on a filled slot
        [SerializeField] SpriteRenderer lockIcon;  // padlock, shown on a locked slot
        [SerializeField] Collider tapCollider;
        [SerializeField] GameObject miniFillRoot;  // track + fill; active only on the running head (slot 0)
        [SerializeField] Transform miniFill;

        [Header("State colours")]
        [Tooltip("Outline tint for an unlocked slot (empty or filled).")]
        [SerializeField] Color openOutline = new Color(0.59f, 0.57f, 0.54f, 1f);
        [Tooltip("Outline tint for a locked slot — the same grey, faded back so it reads as not-yet-yours.")]
        [SerializeField] Color lockedOutline = new Color(0.59f, 0.57f, 0.54f, 0.45f);
        [Tooltip("Padlock tint on a locked slot.")]
        [SerializeField] Color lockedGlyph = new Color(1f, 1f, 1f, 0.5f);

        [Header("Reject (refused collect)")]
        [Tooltip("UiThemeSO.warning red — the flash on a collect Core refused.")]
        [SerializeField] Color rejectColor = new Color(0.851f, 0.325f, 0.310f, 1f); // #D9534F
        [SerializeField] float rejectDuration = 0.5f;
        [Tooltip("Flashes per second while rejecting.")]
        [SerializeField] float rejectFlashSpeed = 6f;
        [Tooltip("Sideways chip travel, in the slot's local units.")]
        [SerializeField] float shakeAmplitude = 0.035f;
        [SerializeField] float shakeFrequency = 18f;

        [Header("Ready pulse")]
        // UiThemeSO.accent (#5FA83C), the established "go / available" green — deliberately NOT the lighter
        // ProgressFill green, which is within a few points of the grass and vanished against it in play.
        [Tooltip("Green pad behind the chip when the head is collectable. Alpha is driven by the pulse.")]
        [SerializeField] Color readyFillColor = new Color(0.373f, 0.659f, 0.235f, 1f);
        [SerializeField] float pulseMinAlpha = 0.5f;
        [SerializeField] float pulseMaxAlpha = 1f;
        [Tooltip("Full bright-dim-bright cycles per second.")]
        [SerializeField] float pulseSpeed = 1.6f;

        public string StationId { get; set; }
        public int SlotIndex { get; set; }

        Vector3 _fillScale;    // authored full-width scale/pos, cached so progress can lerp from them
        Vector3 _fillPosition;
        Vector3 _chipPosition; // authored chip seat, so the shake always returns to it
        float _rejectEndsAt;

        void Awake()
        {
            _fillScale = miniFill.localScale;
            _fillPosition = miniFill.localPosition;
            _chipPosition = chipIcon.transform.localPosition;
        }

        /// Core refused a collect from this slot (§4.4 storage-full). One-shot: flash red and shake the chip.
        /// The slot says "no" — it does not decide *why*, and it never changes state.
        public void Reject() => _rejectEndsAt = Time.time + rejectDuration;

        public void SetState(SlotState state)
        {
            bool ready = state == SlotState.Ready;
            bool holdsJob = ready || state == SlotState.Filled; // both show the output chip and take taps
            bool locked = state == SlotState.Locked;

            outline.color = locked ? lockedOutline : openOutline;
            if (chipIcon.enabled != holdsJob) chipIcon.enabled = holdsJob;
            if (lockIcon.enabled != locked) lockIcon.enabled = locked;
            lockIcon.color = lockedGlyph;
            if (readyFill.enabled != ready) readyFill.enabled = ready;
            if (ready) Pulse(); // tint on the frame it turns ready, or it renders white until Update runs

            // Only a slot holding a job is tappable — tapping nothing is meaningless, and a locked slot is a
            // promise, not a control. Empty and locked slots are inert (UI-Inventory world.queueSlots).
            if (tapCollider.enabled != holdsJob) tapCollider.enabled = holdsJob;
            if (locked && miniFillRoot.activeSelf) miniFillRoot.SetActive(false);
        }

        /// The ready pulse. Local because it is pure presentation on a timer — the same reason the ready
        /// icon's hop lives on StationStateWidget rather than in the view-sync loop.
        /// LateUpdate, not Update: WorldState re-asserts every slot's state colour each frame from Update, so
        /// a reject tint written in Update would be overwritten before it ever rendered.
        void LateUpdate()
        {
            if (Time.time < _rejectEndsAt) { DriveReject(); return; }
            if (chipIcon.transform.localPosition != _chipPosition) chipIcon.transform.localPosition = _chipPosition;
            if (readyFill.enabled) Pulse();
        }

        void DriveReject()
        {
            float remaining = Mathf.InverseLerp(0f, rejectDuration, _rejectEndsAt - Time.time); // 1 → 0
            float flash = (Mathf.Sin(Time.time * rejectFlashSpeed * 2f * Mathf.PI) + 1f) * 0.5f;

            outline.color = Color.Lerp(outline.color, rejectColor, flash);
            if (readyFill.enabled) readyFill.color = Color.Lerp(readyFill.color, rejectColor, flash);

            // Decays with the flash so the chip settles rather than stopping dead.
            float offset = Mathf.Sin(Time.time * shakeFrequency * 2f * Mathf.PI) * shakeAmplitude * remaining;
            chipIcon.transform.localPosition = _chipPosition + new Vector3(offset, 0f, 0f);
        }

        void Pulse()
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed * 2f * Mathf.PI) + 1f) * 0.5f;
            var c = readyFillColor;
            c.a = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t);
            readyFill.color = c;
        }

        /// The icon for the good this slot's job produces (WorldState resolves it from the recipe output).
        public void SetIcon(Sprite icon)
        {
            if (chipIcon.sprite != icon) chipIcon.sprite = icon;
        }

        public void SetRunningProgress(bool show, float fraction)
        {
            if (miniFillRoot.activeSelf != show) miniFillRoot.SetActive(show);
            if (!show) return;
            float width = _fillScale.x;
            miniFill.localScale = new Vector3(width * fraction, _fillScale.y, _fillScale.z);
            miniFill.localPosition = _fillPosition + new Vector3(-width * 0.5f * (1f - fraction), 0f, 0f);
        }
    }
}
