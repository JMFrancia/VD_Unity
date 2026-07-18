using UnityEngine;

namespace VoidDay.View
{
    /// Carries a placed station's instance id on its root GameObject so a pointer raycast can map the hit
    /// collider back to a station id (§4.5 tap-resolution). View-only — the id itself is Core's currency.
    public sealed class StationTag : MonoBehaviour
    {
        public string StationId;
    }
}
