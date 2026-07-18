using UnityEngine;

namespace VoidDay.View
{
    /// One in-world job-queue slot (world.queueSlots, panel.station ALT 42:2). The prefab authors the look
    /// (billboarded background cube, resource chip, slot-0 mini progress); WorldState instantiates one per
    /// queue-depth unit and drives the state. StationId/SlotIndex identify the slot to InputRouter — a tap
    /// on a filled slot is a cancel request.
    public sealed class QueueSlot : MonoBehaviour
    {
        [SerializeField] Renderer background;
        [SerializeField] Material filledMaterial;
        [SerializeField] Material emptyMaterial;
        [SerializeField] GameObject chip;
        [SerializeField] Collider tapCollider;
        [SerializeField] GameObject miniFillRoot; // track + fill; active only on the running head (slot 0)
        [SerializeField] Transform miniFill;

        public string StationId { get; set; }
        public int SlotIndex { get; set; }

        Vector3 _fillScale;    // authored full-width scale/pos, cached so progress can lerp from them
        Vector3 _fillPosition;

        void Awake()
        {
            _fillScale = miniFill.localScale;
            _fillPosition = miniFill.localPosition;
        }

        public void SetFilled(bool filled)
        {
            background.sharedMaterial = filled ? filledMaterial : emptyMaterial;
            if (chip.activeSelf != filled) chip.SetActive(filled);
            if (tapCollider.enabled != filled) tapCollider.enabled = filled;
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
