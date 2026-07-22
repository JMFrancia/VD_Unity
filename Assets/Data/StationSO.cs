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

        [Header("Build & gating (§4.3, §9, §12.3)")]
        [Tooltip("Does the player build this type? Off = the type still exists in the roster (recipes, upgrades, "
            + "pricing, scene-placed instances all read it) but it never appears in the build menu. Off for the "
            + "one-off fixtures the farm ships with rather than the player placing.")]
        public bool buildable = true;

        [Tooltip("Money to build one (§4.3). Read through the resolve seam; M6's build.cost effect discounts it.")]
        public int buildCost;

        [Tooltip("Per-type cap on placed instances (§4.3). Read through the seam; M8 raises it by level. Starts 2 for Field, 1 for the rest.")]
        public int cap = 1;

        [Tooltip("Seconds a placed station spends under construction before it exists and can be used. The site "
            + "occupies its cell and counts against the cap for the whole time. 0 = finishes on the frame it is "
            + "placed, i.e. the old build-instantly behaviour.")]
        [Min(0f)] public float buildSeconds = 15f;

        [Tooltip("Player level that unlocks building this type (§9). 1 = buildable from the start. > current level = shown locked in the build menu.")]
        public int unlockLevel = 1;

        [Tooltip("Build-menu thumbnail (§12.3) — the station's own model rendered at the world camera's angle. Baked from the prefab into Assets/Art/UI/StationThumbs.")]
        public Sprite buildThumbnail;

        [Tooltip("Authored prefab instantiated when this type is placed at runtime (CLAUDE.md rule 4). Required for types buildable now (unlockLevel 1); level-locked types add theirs when M8 makes them placeable.")]
        public GameObject prefab;

        [Header("Production (§4.3, §5.2)")]
        [Tooltip("Base job-queue depth (§4.3). Read through the resolve seam; upgrades (M5) and level (M8) add to it.")]
        public int queueDepth = 3;

        [Tooltip("Recipes available at this station (§5.2). Empty for non-producers (Silo, Order Board).")]
        public List<RecipeSO> recipes = new();

        [Header("Upgrades (§8)")]
        [Tooltip("Station-upgrade tracks bought in this station's panel (job speed, queue depth, output yield). "
            + "Per-instance and passive/own-station (§3.2). Universal/Silo tracks live on Workshop/Silo (M6/M7).")]
        public List<UpgradeSO> upgrades = new();
    }
}
