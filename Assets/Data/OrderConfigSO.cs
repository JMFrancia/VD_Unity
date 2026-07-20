using UnityEngine;

namespace VoidDay.Data
{
    /// Every tunable the order board reads (§6, §6.1, §14). Slot count and payout are read through the value
    /// seam, so M6's universal upgrades (order.payout, order.slots) raise these without editing the rules.
    [CreateAssetMenu(menuName = "VoidDay/Order Config", fileName = "OrderConfig")]
    public sealed class OrderConfigSO : ScriptableObject
    {
        [Header("Board (§6)")]
        [Tooltip("Base slot count. Raised by player level and the order.slots effect (M6).")]
        public int slotCount = 3;

        [Tooltip("Seconds a fulfilled or skipped slot takes to refill.")]
        public float refillSeconds = 60f;

        [Header("Generation (§6.1)")]
        [Tooltip("Fewest distinct goods one order can request.")]
        public int minRequestKinds = 1;

        [Tooltip("Most distinct goods one order can request (capped by how many are producible).")]
        public int maxRequestKinds = 2;

        [Tooltip("Largest quantity of a single good an order can request at level 1.")]
        public float maxQuantityAtLevel1 = 3f;

        [Tooltip("How much that ceiling grows per player level.")]
        public float maxQuantityPerLevel = 1f;

        [Tooltip("Selection weight every candidate carries regardless of tier.")]
        public float tierWeightBase = 1f;

        [Tooltip("Extra weight per (level-1) per tier — this is what tilts orders toward processed goods.")]
        public float tierWeightPerLevel = 0.25f;

        [Header("Payout (§6)")]
        [Tooltip("Cash = sum(quantity x resource baseValue) x this.")]
        public float cashMultiplier = 12f;

        [Tooltip("XP = sum(quantity x resource baseValue) x this.")]
        public float xpMultiplier = 1.5f;
    }
}
