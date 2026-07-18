using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoidDay.Data
{
    /// Single global config (§14). Grid extents (camera bounds + future placement), camera tuning, and the
    /// starting resources (§5.3). Which stations exist and where is scene data, not config (CLAUDE.md rule 4);
    /// environment colors live on the authored ground material and camera.
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

        [Header("Start block (§5.3)")]
        public List<StartingResource> startingResources = new();
    }

    [Serializable]
    public sealed class StartingResource
    {
        public ResourceSO resource;
        public int amount;
    }
}
