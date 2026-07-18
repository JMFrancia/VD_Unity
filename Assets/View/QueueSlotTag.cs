using UnityEngine;

namespace VoidDay.View
{
    /// Carries a queue slot's owning station id + slot index on the slot's world-space body, so a pointer
    /// raycast can map a tap on the slot back to a cancel intent (world.queueSlots). View-only — the ids are
    /// Core's currency. The slot's collider is enabled only while the slot is filled, so empty-slot taps miss.
    public sealed class QueueSlotTag : MonoBehaviour
    {
        public string StationId;
        public int SlotIndex;
    }
}
