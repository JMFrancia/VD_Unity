using UnityEngine;

namespace VoidDay.View
{
    /// Forces the game to render inside a fixed-aspect (portrait) rectangle no matter
    /// what shape the output surface is — a wide desktop browser, an itch/Unity-Play
    /// iframe, a phone. The host page can't be relied on to constrain the canvas
    /// (Unity Play ignores the WebGL template), so we do it in-engine: the main camera's
    /// viewport rect is clamped to the largest centred `targetAspect` box that fits the
    /// screen; a separate full-screen background camera clears the leftover bars to the
    /// void colour. The UI canvases render in Screen Space - Camera on this camera, so
    /// they're confined to the same box and — because the box is exactly the CanvasScaler
    /// reference aspect — scale identically to the authored portrait layout.
    ///
    /// Aspect is presentation, not game data (CLAUDE.md rule 1): it's a serialized field
    /// read live each frame, so inspector edits preview immediately in Play mode.
    [RequireComponent(typeof(Camera))]
    public sealed class AspectLetterbox : MonoBehaviour
    {
        [Tooltip("The locked render aspect, width:height. 9:16 matches the portrait phone " +
                 "design and the HudCanvas CanvasScaler reference (1080x1920).")]
        public Vector2 targetAspect = new Vector2(9f, 16f);

        Camera _camera;

        void Awake() => _camera = GetComponent<Camera>();

        // LateUpdate so we run after CameraController has moved/zoomed the rig for the frame.
        void LateUpdate()
        {
            float target = targetAspect.x / targetAspect.y;
            float window = (float)Screen.width / Screen.height;

            Rect rect;
            if (window >= target)
            {
                // Window is wider than target -> pillarbox: clamp width, full height.
                float w = target / window;
                rect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
            }
            else
            {
                // Window is taller than target -> letterbox: clamp height, full width.
                float h = window / target;
                rect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
            }

            _camera.rect = rect;
        }
    }
}
