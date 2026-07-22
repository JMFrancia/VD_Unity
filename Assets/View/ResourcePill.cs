using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace VoidDay.View
{
    /// One transient resource readout on the rail — the same chrome as the money and gem pills, with the
    /// resource's icon in place of the currency glyph.
    ///
    /// Unlike the money pill, this one genuinely has an icon child, so the per-arrival pop lands on the icon
    /// rather than on the whole rect. The pill owns the pop because the pill is the thing that pops; the rail
    /// only decides when.
    public sealed class ResourcePill : MonoBehaviour
    {
        [Header("Chrome (authored)")]
        [SerializeField] Image iconImage;
        [SerializeField] Text countText;
        [SerializeField] CanvasGroup canvasGroup;

        [Tooltip("Count copy. {0} is the resource's current total.")]
        [SerializeField] string countFormat = "{0}";

        [Header("Feel")]
        [Tooltip("How large the icon swells as one resource icon lands, as a multiple of its resting size.")]
        [SerializeField] float pulseScale = 1.18f;
        [Tooltip("Seconds the icon takes to swell and settle back.")]
        [SerializeField] float pulseSeconds = 0.18f;
        [SerializeField] Ease pulseEase = Ease.OutQuad;

        public RectTransform Rect => (RectTransform)transform;
        public CanvasGroup Group => canvasGroup;

        public void Bind(Sprite icon, int count)
        {
            iconImage.sprite = icon;
            SetCount(count);
        }

        public void SetCount(int count) => countText.text = string.Format(countFormat, count);

        public void Pulse()
        {
            var icon = iconImage.rectTransform;
            icon.DOKill();
            icon.localScale = Vector3.one;
            icon.DOScale(pulseScale, pulseSeconds * 0.5f)
                .SetEase(pulseEase)
                .SetLoops(2, LoopType.Yoyo);
        }

        void OnDestroy()
        {
            iconImage.rectTransform.DOKill();
            Rect.DOKill();
            if (canvasGroup != null) canvasGroup.DOKill();
        }
    }
}
