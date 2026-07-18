using UnityEngine;

namespace VoidDay.View
{
    /// The per-station in-world state rig (§12.6, sheet 34:2), instantiated under each station root by
    /// WorldState. The prefab authors the pieces: a billboarded anchor above the body holding the progress
    /// bar and the ready icon, and a non-billboarded QueueRow in front of the body that WorldState fills
    /// with QueueSlot instances. This component only animates what it owns; state decisions stay in
    /// WorldState (view-sync) and Core.
    public sealed class StationStateWidget : MonoBehaviour
    {
        [SerializeField] GameObject barRoot;
        [SerializeField] Transform barFill;
        [SerializeField] GameObject readyRoot;
        [SerializeField] Transform queueRow;
        [SerializeField] float hopAmplitude = 0.12f;
        [SerializeField] float hopSpeed = 6f;

        public Transform QueueRow => queueRow;

        Vector3 _fillScale;    // authored full-width scale/pos, cached so progress can lerp from them
        Vector3 _fillPosition;
        Vector3 _readyBasePosition;

        void Awake()
        {
            _fillScale = barFill.localScale;
            _fillPosition = barFill.localPosition;
            _readyBasePosition = readyRoot.transform.localPosition;
            barRoot.SetActive(false);
            readyRoot.SetActive(false);
        }

        public void SetRunning(bool running)
        {
            if (barRoot.activeSelf != running) barRoot.SetActive(running);
        }

        public void SetReady(bool ready)
        {
            if (readyRoot.activeSelf != ready) readyRoot.SetActive(ready);
        }

        public void SetProgress(float fraction)
        {
            float width = _fillScale.x;
            barFill.localScale = new Vector3(width * fraction, _fillScale.y, _fillScale.z);
            barFill.localPosition = _fillPosition + new Vector3(-width * 0.5f * (1f - fraction), 0f, 0f);
        }

        void Update()
        {
            if (!readyRoot.activeSelf) return;
            float hop = Mathf.Abs(Mathf.Sin(Time.time * hopSpeed)) * hopAmplitude;
            readyRoot.transform.localPosition = _readyBasePosition + new Vector3(0f, hop, 0f);
        }
    }
}
