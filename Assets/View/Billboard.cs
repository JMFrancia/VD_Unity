using UnityEngine;

namespace VoidDay.View
{
    /// Rotates world-space UI to face the camera each frame (§12.6). Because the camera is orthographic
    /// and fixed-angle, this is a constant yaw/pitch match, not per-object perspective correction.
    /// Built here for later world-space UI (progress bar, ready icon, hearts) — nothing attaches to it in M1.
    public sealed class Billboard : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;

        void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
        }

        void LateUpdate()
        {
            if (targetCamera == null) return;
            transform.rotation = targetCamera.transform.rotation;
        }
    }
}
