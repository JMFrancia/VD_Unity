using UnityEngine;
using UnityEngine.InputSystem;

namespace VoidDay.View
{
    /// Orthographic, angled ¾ top-down camera (§12.5). Drag pans across the XZ plane via pointer
    /// raycast-to-plane so the grabbed world point stays under the finger 1:1 despite the tilt.
    /// Zoom changes orthographic size: two-finger pinch on touch, mouse-scroll or -/= keys on desktop
    /// (the design is touch-only, but pinch isn't reproducible on a WebGL-desktop browser — §12.5 note —
    /// so scroll/keys exist to make zoom verifiable on the actual build target).
    /// Binds to Pointer (mouse + touch) so pan is verifiable with a browser mouse.
    ///
    /// Framing is presentation, not game data (CLAUDE.md rule 1): every tunable below is a serialized
    /// field read live each frame, so inspector edits take effect immediately in Play mode.
    public sealed class CameraController : MonoBehaviour
    {
        static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

        [Header("Framing")]
        [Range(30f, 80f)] public float pitchDegrees = 57f;
        public float yawDegrees = 0f;

        [Header("Zoom (orthographic size)")]
        [Tooltip("How much of the world fits on screen — this is the actual zoom. Live-editable: drag to preview; pinch/scroll drive it at runtime.")]
        public float zoom = 8f;
        public float minZoom = 4f;
        public float maxZoom = 14f;
        [Tooltip("Desktop/WebGL zoom (§12.5 testing note): orthographic-size change per mouse-scroll notch.")]
        public float scrollZoomStep = 0.5f;
        [Tooltip("Desktop/WebGL zoom: orthographic-size change per second while -/= (or numpad +/-) is held.")]
        public float keyZoomSpeed = 6f;

        [Header("Rig")]
        [Tooltip("Boom distance along the view axis. Orthographic, so this does NOT change framing — it only pulls the camera back for near/far clipping and depth sorting. Use Zoom to change what you see.")]
        public float distance = 50f;

        [Header("Pan")]
        [Tooltip("World-space distance the focus may pan beyond the map edge before clamping.")]
        public float panMargin = 2f;

        Camera _camera;
        Vector3 _focus;
        float _halfExtentX;
        float _halfExtentZ;
        bool _initialized;

        bool _dragging;
        Vector3 _grabPoint;
        float _lastPinchDistance;

        /// Grid extents (world units) come from game data so the camera knows its pan bounds without
        /// depending on GameConfigSO — the only thing it needs from the economy layer.
        public void Init(Vector3 focus, float gridWorldWidth, float gridWorldDepth)
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _halfExtentX = gridWorldWidth * 0.5f;
            _halfExtentZ = gridWorldDepth * 0.5f;
            _focus = focus;
            _initialized = true;
            ClampFocus();
            ApplyCamera();
        }

        void Update()
        {
            if (!_initialized) return; // not yet booted
            UpdatePan();
            UpdatePinchZoom();
            UpdateDesktopZoom();
            ClampFocus();
            ApplyCamera(); // apply every frame so live inspector edits to framing take effect
        }

        /// Desktop/WebGL zoom: mouse scroll (one step per notch) and -/= keys (continuous while held).
        void UpdateDesktopZoom()
        {
            float delta = 0f; // positive = zoom out (bigger orthographic size)

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                // Sign-based so platform scroll-delta magnitudes don't change the step size.
                if (Mathf.Abs(scroll) > 0.01f) delta -= Mathf.Sign(scroll) * scrollZoomStep;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float step = keyZoomSpeed * Time.deltaTime;
                if (keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed) delta -= step;
                if (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed) delta += step;
            }

            if (delta != 0f)
                zoom = Mathf.Clamp(zoom + delta, minZoom, maxZoom);
        }

        void UpdatePan()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;

            // Pinch owns the gesture when two fingers are down; don't also pan.
            if (ActiveTouchCount() >= 2) { _dragging = false; return; }

            Vector2 screen = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                _dragging = TryPlanePoint(screen, out _grabPoint);
            }
            else if (pointer.press.isPressed && _dragging)
            {
                if (TryPlanePoint(screen, out Vector3 current))
                {
                    Vector3 delta = _grabPoint - current; // move focus so grab point returns under cursor
                    delta.y = 0f;
                    _focus += delta;
                }
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                _dragging = false;
            }
        }

        void UpdatePinchZoom()
        {
            var touch = Touchscreen.current;
            if (touch == null) return;

            if (ActiveTouchCount() < 2) { _lastPinchDistance = 0f; return; }

            Vector2 a = touch.touches[0].position.ReadValue();
            Vector2 b = touch.touches[1].position.ReadValue();
            float distance = Vector2.Distance(a, b);

            if (_lastPinchDistance > 0f)
            {
                // Fingers apart (distance grows) -> zoom in -> smaller orthographic size.
                float scale = _lastPinchDistance / Mathf.Max(distance, 0.001f);
                zoom = Mathf.Clamp(zoom * scale, minZoom, maxZoom);
            }
            _lastPinchDistance = distance;
        }

        static int ActiveTouchCount()
        {
            var touch = Touchscreen.current;
            if (touch == null) return 0;
            int count = 0;
            foreach (var t in touch.touches)
                if (t.press.isPressed) count++;
            return count;
        }

        bool TryPlanePoint(Vector2 screen, out Vector3 world)
        {
            Ray ray = _camera.ScreenPointToRay(screen);
            if (GroundPlane.Raycast(ray, out float enter))
            {
                world = ray.GetPoint(enter);
                return true;
            }
            world = default;
            return false;
        }

        void ClampFocus()
        {
            _focus.x = Mathf.Clamp(_focus.x, -_halfExtentX - panMargin, _halfExtentX + panMargin);
            _focus.z = Mathf.Clamp(_focus.z, -_halfExtentZ - panMargin, _halfExtentZ + panMargin);
            _focus.y = 0f;
        }

        void ApplyCamera()
        {
            _camera.orthographicSize = zoom;
            Quaternion rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            transform.rotation = rotation;
            transform.position = _focus - rotation * Vector3.forward * distance;
        }
    }
}
