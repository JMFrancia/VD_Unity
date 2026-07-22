using TMPro;
using UnityEngine;
using VoidDay.Core.Model;

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

        [Header("Gem skip (§13)")]
        [Tooltip("The whole radial is the tap target — sized to cover the ring and the cost row beneath it.")]
        [SerializeField] Collider tapCollider;
        [Tooltip("Gem glyph + amount beneath the ring. Hidden whenever there is no live timer to price.")]
        [SerializeField] GameObject costRoot;
        [SerializeField] TextMeshPro costLabel;
        [SerializeField] string costFormat = "{0}";

        [Tooltip("Under this many seconds the label reads '9s'; at or above it reads '1:23'.")]
        [SerializeField] int minutesThreshold = 60;

        MaterialPropertyBlock _mpb;

        /// Which timer this widget is drawing — set by whoever owns it (WorldState for a job,
        /// ConstructionSiteView for a build). InputRouter reads it to name the timer a tap is aimed at.
        /// The widget never resolves this itself: it renders what it is handed and decides nothing.
        public TimerRef Timer { get; set; }

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

        /// The gem price of finishing this timer now, handed down by the owner (TimeSkip owns the rule, §13).
        /// Zero or less means "nothing left to buy" — the cost row hides and the collider goes inert, so a
        /// radial with no price does not swallow a tap that would otherwise reach the station behind it.
        public void SetCost(int gems)
        {
            bool skippable = gems > 0;
            if (costRoot.activeSelf != skippable) costRoot.SetActive(skippable);
            if (tapCollider.enabled != skippable) tapCollider.enabled = skippable;
            if (skippable) costLabel.text = string.Format(costFormat, gems);
        }

        /// Ceil, not round: a timer must not read "0s" while there is still time on it.
        string Format(float secondsRemaining)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
            return total < minutesThreshold ? $"{total}s" : $"{total / 60}:{total % 60:00}";
        }
    }
}
