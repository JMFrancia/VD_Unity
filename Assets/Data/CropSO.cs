using UnityEngine;

namespace VoidDay.Data
{
    /// A resource that a field grows (wheat, corn). Adds the world billboard art the field renders rising
    /// out of the soil as the crop grows (§12.6). It *is-a* ResourceSO, so recipes, icons, and the economy
    /// treat it exactly like any other resource — only the field's crop view reads the extra sprite.
    [CreateAssetMenu(menuName = "VoidDay/Crop", fileName = "Crop")]
    public sealed class CropSO : ResourceSO
    {
        [Tooltip("World-space sprite of the growing plant, shown rising out of the field as it grows (§12.6). "
            + "Tall billboard, distinct from the flat UI icon.")]
        public Sprite cropSprite;
    }
}
