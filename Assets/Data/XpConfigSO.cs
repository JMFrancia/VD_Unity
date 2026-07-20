using UnityEngine;

namespace VoidDay.Data
{
    /// XP per action (§9, §14). Order XP is not here — an order carries its own, derived from what it
    /// requests (OrderConfigSO.xpMultiplier). Building a station and hatching an egg join this in M4/M9.
    [CreateAssetMenu(menuName = "VoidDay/XP Config", fileName = "XpConfig")]
    public sealed class XpConfigSO : ScriptableObject
    {
        [Tooltip("XP awarded each time a completed job's output is collected.")]
        public int perJobCollected = 2;

        [Tooltip("XP awarded each time a station is built (§9).")]
        public int perStationBuilt = 5;
    }
}
