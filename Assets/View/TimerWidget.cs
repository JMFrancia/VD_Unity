using TMPro;
using UnityEngine;

namespace VoidDay.View
{
    /// The one countdown readout in the game — a billboarded radial ring with the time remaining inside it.
    /// Shared by the in-world job radial (StationStateWidget) and by construction sites, so every timer in the
    /// game reads the same way. Pure presentation: it renders what it is handed and decides nothing, which is
    /// what lets one prefab serve callers that know nothing about each other.
    ///
    /// The ring is a quad on the VoidDay/RadialProgress shader (its _Fill drives the sweep), pushed through a
    /// MaterialPropertyBlock so every instance shares one material asset rather than forking one each. The
    /// label is a 3D TextMeshPro, not UGUI — world-space canvases don't render in this project's URP camera
    /// setup, which is why the whole in-world rig is meshes.
    public sealed class TimerWidget : MonoBehaviour
    {
        static readonly int FillId = Shader.PropertyToID("_Fill");

        [SerializeField] MeshRenderer ringRenderer; // VoidDay/RadialProgress material; _Fill = progress
        [SerializeField] TextMeshPro secondsLabel;

        [Tooltip("Under this many seconds the label reads '9s'; at or above it reads '1:23'.")]
        [SerializeField] int minutesThreshold = 60;

        MaterialPropertyBlock _mpb;

        public void Show(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }

        public void SetProgress(float fraction, float secondsRemaining)
        {
            // Lazily built: this widget spends most of its life inactive, so Awake may not have run by the
            // time the first frame of a job or a build pushes progress into it.
            _mpb ??= new MaterialPropertyBlock();

            ringRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FillId, fraction);
            ringRenderer.SetPropertyBlock(_mpb);
            secondsLabel.text = Format(secondsRemaining);
        }

        /// Ceil, not round: a timer must not read "0s" while there is still time on it.
        string Format(float secondsRemaining)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
            return total < minutesThreshold ? $"{total}s" : $"{total / 60}:{total % 60:00}";
        }
    }
}
