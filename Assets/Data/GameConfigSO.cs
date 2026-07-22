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

        [Header("Station roster (§4.2, §12.3)")]
        [Tooltip("Every buildable station TYPE — the build menu's source of truth. Distinct from scene-placed instances (which are scene data, CLAUDE.md rule 4); this is the catalog of what CAN be built.")]
        public List<StationSO> stationRoster = new();

        [Tooltip("Fraction of build cost returned on demolish (§4.3). 0.5 = 50%. Data, not a literal.")]
        public float refundPercent = 0.5f;

        [Header("Storage (§7)")]
        [Tooltip("Silo capacity before any upgrade — ONE shared pool across every good (Hay Day's silo model), not a per-resource cap. Raised by the Silo's storage.cap track.")]
        public int startingStorageCapacity = 30;

        [Header("Start block (§5.3)")]
        public List<StartingResource> startingResources = new();

        [Header("Gems")]
        [Tooltip("Gems the player holds at the start of a run, and returns to on a debug reset.")]
        public int startingGems = 5;

        [Tooltip("Seconds of remaining wait that one gem buys — the divisor of a timer skip's price.")]
        public float secondsPerGem = 30f;

        [Tooltip("Floor on a skip's price. A timer that is nearly done still costs this many gems.")]
        public int minGemCost = 1;

        [Header("Economy config (§6, §9)")]
        public OrderConfigSO orderConfig;
        public XpConfigSO xpConfig;

        [Tooltip("The XP → level table and what each level hands out (§9).")]
        public LevelSO levels;
    }

    [Serializable]
    public sealed class StartingResource
    {
        public ResourceSO resource;
        public int amount;
    }
}
