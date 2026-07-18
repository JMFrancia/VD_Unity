using System.Collections.Generic;
using UnityEngine;

namespace VoidDay.Data
{
    /// One asset per station type (§14). The single generic Producer is data, not subclasses (CLAUDE.md).
    /// Pure game data — a station's look lives in its authored prefab (CLAUDE.md rule 4), its placement in
    /// the scene. This asset carries what the economy needs to know about the type.
    [CreateAssetMenu(menuName = "VoidDay/Station", fileName = "Station")]
    public sealed class StationSO : ScriptableObject
    {
        [Header("Identity")]
        public string stationType;   // "field", "silo", "orderBoard"
        public string displayName;

        [Header("Footprint (§4.1) — 1x1 for now")]
        public int width = 1;
        public int height = 1;

        [Header("Production (§4.3, §5.2)")]
        [Tooltip("Base job-queue depth (§4.3). Read through the resolve seam; upgrades (M5) and level (M8) add to it.")]
        public int queueDepth = 3;

        [Tooltip("Recipes available at this station (§5.2). Empty for non-producers (Silo, Order Board).")]
        public List<RecipeSO> recipes = new();
    }
}
