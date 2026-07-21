using UnityEngine;

namespace VoidDay.View
{
    /// The per-station in-world state rig (§12.6, sheet 34:2), instantiated under each station root by
    /// WorldState. The prefab authors the pieces: a billboarded anchor above the body holding the radial
    /// progress ring and the ready icon, and a non-billboarded QueueRow in front of the body that WorldState
    /// fills with QueueSlot instances. This component only animates what it owns; state decisions stay in
    /// WorldState (view-sync) and Core.
    ///
    /// The radial is a quad rendered by the VoidDay/RadialProgress shader (its _Fill drives the sweep), not a
    /// UGUI Image — world-space canvases don't render in this project's URP camera setup, so the whole rig is
    /// meshes. Progress is pushed through a MaterialPropertyBlock so every station shares one material asset.
    public sealed class StationStateWidget : MonoBehaviour
    {
        static readonly int FillId = Shader.PropertyToID("_Fill");

        [SerializeField] GameObject radialRoot;
        [SerializeField] MeshRenderer radialRenderer; // VoidDay/RadialProgress material; _Fill = job progress
        [SerializeField] GameObject readyRoot;
        [SerializeField] SpriteRenderer readyIcon; // the finished good's crop icon, hops while output waits
        [SerializeField] GameObject storageFullRoot; // world.storageFull — warning triangle, deliberately still
        [SerializeField] Transform queueRow;
        [SerializeField] float hopAmplitude = 0.12f;
        [SerializeField] float hopSpeed = 6f;

        public Transform QueueRow => queueRow;

        Vector3 _readyBasePosition;
        MaterialPropertyBlock _mpb;

        void Awake()
        {
            _readyBasePosition = readyRoot.transform.localPosition;
            _mpb = new MaterialPropertyBlock();
            radialRoot.SetActive(false);
            readyRoot.SetActive(false);
            storageFullRoot.SetActive(false);
        }

        public void SetRadialVisible(bool visible)
        {
            if (radialRoot.activeSelf != visible) radialRoot.SetActive(visible);
        }

        public void SetReady(bool ready)
        {
            if (readyRoot.activeSelf != ready) readyRoot.SetActive(ready);
        }

        /// world.storageFull (§4.4, §12.6). Deliberately does NOT hop — the ready icon's bounce says "tap me",
        /// and this state is the one where tapping will not collect. Stillness plus the warning triangle is the
        /// distinction the spec asks for.
        public void SetStorageFull(bool full)
        {
            if (storageFullRoot.activeSelf != full) storageFullRoot.SetActive(full);
        }

        /// The crop icon for the finished good (WorldState resolves it from the completed job's recipe output).
        public void SetReadyIcon(Sprite icon)
        {
            if (readyIcon.sprite != icon) readyIcon.sprite = icon;
        }

        public void SetRadialProgress(float fraction)
        {
            radialRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FillId, fraction);
            radialRenderer.SetPropertyBlock(_mpb);
        }

        void Update()
        {
            if (!readyRoot.activeSelf) return;
            float hop = Mathf.Abs(Mathf.Sin(Time.time * hopSpeed)) * hopAmplitude;
            readyRoot.transform.localPosition = _readyBasePosition + new Vector3(0f, hop, 0f);
        }
    }
}
