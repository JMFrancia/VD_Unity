using UnityEngine;
using VoidDay.Data;

namespace VoidDay.View
{
    /// A station placed in the scene (§12.6). The prefab authors the body; this component binds it to its
    /// StationSO. The GameObject's name is the station's Core instance id, so every placed station must be
    /// uniquely named (GameBoot validates this at boot).
    public sealed class StationView : MonoBehaviour
    {
        [SerializeField] StationSO station;

        public StationSO Station => station;
        public string Id => gameObject.name;
    }
}
