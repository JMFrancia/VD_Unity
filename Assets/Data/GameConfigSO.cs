using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoidDay.Data
{
    /// Single global config (§14). Grid extents (drives the economy grid + the camera's pan bounds) and the
    /// starting resources (§5.3). Which stations exist and where is scene data, not config (CLAUDE.md rule 4).
    /// Camera framing is presentation and lives on CameraController; environment colors on the ground material.
    [CreateAssetMenu(menuName = "VoidDay/Game Config", fileName = "GameConfig")]
    public sealed class GameConfigSO : ScriptableObject
    {
        [Header("Grid (§4.1)")]
        public int gridCols = 20;
        public int gridRows = 30;
        public float cellSize = 1f;

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
