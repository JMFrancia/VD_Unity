using UnityEngine;

namespace VoidDay.View
{
    /// One growing crop in a field (§12.6). A tall billboard sprite that rises straight up out of the soil as
    /// the field's job progresses: at t=0 its top edge sits just under the surface (hidden by the opaque soil),
    /// at t=1 its base rests on the surface. The rise IS the growth — no scaling, no stage swaps. Pure view:
    /// WorldState drives it from Core's 0→1 head progress. The graphic child holds the SpriteRenderer + Billboard
    /// and starts deactivated, so an idle field shows nothing.
    public sealed class CropController : MonoBehaviour
    {
        [SerializeField] GameObject cropGraphic;
        [SerializeField] SpriteRenderer spriteRenderer;

        float _travel; // world height the sprite slides through, = full sprite height (top-under-soil → base-on-soil)

        void Awake() => cropGraphic.SetActive(false);

        /// Begin a fresh growth: show this crop's sprite, snap fully beneath the soil, activate the graphic.
        public void StartGrow(Sprite cropSprite)
        {
            spriteRenderer.sprite = cropSprite;
            // Travel = the sprite's world-space height, so t sweeps it from top-just-under-surface to base-on-surface.
            _travel = cropSprite.bounds.size.y * spriteRenderer.transform.lossyScale.y;
            cropGraphic.SetActive(true);
            UpdateGrow(0f);
        }

        /// Slide the crop by normalized growth t∈[0,1]. Bottom-pivoted sprite: y=0 rests the base on the surface,
        /// y=-_travel sinks it fully under. The below-surface portion is occluded by the opaque soil bed.
        public void UpdateGrow(float t)
        {
            var p = cropGraphic.transform.localPosition;
            p.y = -(1f - Mathf.Clamp01(t)) * _travel;
            cropGraphic.transform.localPosition = p;
        }

        /// Field went idle / the crop was harvested — hide until the next StartGrow.
        public void Hide() => cropGraphic.SetActive(false);
    }
}
