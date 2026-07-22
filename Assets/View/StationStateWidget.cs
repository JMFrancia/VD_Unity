using UnityEngine;
using VoidDay.Core.Model;

namespace VoidDay.View
{
    /// The per-station in-world state rig (§12.6, sheet 34:2), instantiated under each station root by
    /// WorldState. The prefab authors the pieces: a billboarded anchor above the body holding the radial
    /// progress ring and the ready icon, and a non-billboarded QueueRow in front of the body that WorldState
    /// fills with QueueSlot instances. This component only animates what it owns; state decisions stay in
    /// WorldState (view-sync) and Core.
    ///
    /// The running job's countdown is a nested TimerWidget — the same authored prefab construction sites use,
    /// so a job timer and a build timer are one visual rather than two that drift apart.
    public sealed class StationStateWidget : MonoBehaviour
    {
        [SerializeField] TimerWidget timer;
        [SerializeField] GameObject readyRoot;
        [SerializeField] SpriteRenderer readyIcon; // the finished good's crop icon, hops while output waits
        [SerializeField] GameObject storageFullRoot; // world.storageFull — warning triangle, deliberately still
        [SerializeField] Transform queueRow;
        [SerializeField] float hopAmplitude = 0.12f;
        [SerializeField] float hopSpeed = 6f;

        public Transform QueueRow => queueRow;

        Vector3 _readyBasePosition;

        void Awake()
        {
            _readyBasePosition = readyRoot.transform.localPosition;
            timer.Show(false);
            readyRoot.SetActive(false);
            storageFullRoot.SetActive(false);
        }

        public void SetTimerVisible(bool visible) => timer.Show(visible);

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

        public void SetTimer(float fraction, float secondsRemaining) =>
            timer.SetProgress(fraction, secondsRemaining);

        /// Pass-throughs to the nested TimerWidget, which is private here on purpose: WorldState drives the
        /// rig, not the widget's internals. Both exist so a job radial can be priced and tapped (§13).
        public void SetTimerRef(TimerRef reference) => timer.Timer = reference;

        public void SetTimerCost(int gems) => timer.SetCost(gems);

        void Update()
        {
            if (!readyRoot.activeSelf) return;
            float hop = Mathf.Abs(Mathf.Sin(Time.time * hopSpeed)) * hopAmplitude;
            readyRoot.transform.localPosition = _readyBasePosition + new Vector3(0f, hop, 0f);
        }
    }
}
