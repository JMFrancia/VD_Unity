using System.Collections.Generic;
using UnityEngine;

namespace VoidDay.View
{
    /// The cluster of growing crops on a field (§12.6). Sits on the field prefab body and holds the authored
    /// Crop instances childed to the plot (count/positions are prefab layout, not data). WorldState drives it
    /// from Core's head progress: Begin when a crop job starts, Grow every frame, Hide when the field goes idle.
    /// The whole patch rises in unison.
    public sealed class CropField : MonoBehaviour
    {
        [SerializeField] List<CropController> crops = new();

        public void Begin(Sprite cropSprite)
        {
            foreach (var crop in crops) crop.StartGrow(cropSprite);
        }

        public void Grow(float t)
        {
            foreach (var crop in crops) crop.UpdateGrow(t);
        }

        public void Hide()
        {
            foreach (var crop in crops) crop.Hide();
        }
    }
}
