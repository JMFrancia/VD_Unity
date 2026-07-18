using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoidDay.Data
{
    /// Single global config (§14). Grid, camera bounds, environment tint, and the start block
    /// (starting resources + pre-placed stations, §5.3). Every tunable in M1 lives here or on a referenced SO.
    [CreateAssetMenu(menuName = "VoidDay/Game Config", fileName = "GameConfig")]
    public sealed class GameConfigSO : ScriptableObject
    {
        [Header("Grid (§4.1)")]
        public int gridCols = 20;
        public int gridRows = 30;
        public float cellSize = 1f;

        [Header("Camera (§12.5)")]
        [Range(30f, 80f)] public float cameraPitchDegrees = 57f;
        public float cameraYawDegrees = 0f;
        [Tooltip("Boom distance of the camera from its focus. Ortho, so this only affects clipping, not framing.")]
        public float cameraDistance = 50f;
        public float cameraMinZoom = 4f;
        public float cameraMaxZoom = 14f;
        public float cameraStartZoom = 8f;
        [Tooltip("How many cells beyond the map edge the camera focus may pan before clamping.")]
        public float panMarginCells = 2f;
        [Tooltip("Desktop/WebGL zoom (§12.5 testing note): orthographic-size change per mouse-scroll notch.")]
        public float scrollZoomStep = 0.5f;
        [Tooltip("Desktop/WebGL zoom: orthographic-size change per second while -/= (or numpad +/-) is held.")]
        public float keyZoomSpeed = 6f;

        [Header("Environment (§12.5)")]
        public Color groundColor = new Color(0.49f, 0.745f, 0.353f);   // #7DBE5A warm grass
        public Color backdropColor = new Color(0.102f, 0.078f, 0.188f); // #1A1430 soft indigo void

        [Header("Start block (§5.3)")]
        public List<StartingResource> startingResources = new();
        public List<PrePlacedStation> prePlacedStations = new();
    }

    [Serializable]
    public sealed class StartingResource
    {
        public ResourceSO resource;
        public int amount;
    }

    [Serializable]
    public sealed class PrePlacedStation
    {
        public StationSO station;
        public int col;
        public int row;
    }
}
