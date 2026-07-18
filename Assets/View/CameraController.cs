using UnityEngine;
using UnityEngine.InputSystem;
using VoidDay.Data;

namespace VoidDay.View
{
    /// Orthographic, angled ¾ top-down camera (§12.5). Drag pans across the XZ plane via pointer
    /// raycast-to-plane so the grabbed world point stays under the finger 1:1 despite the tilt.
    /// Zoom changes orthographic size: two-finger pinch on touch, mouse-scroll or -/= keys on desktop
    /// (the design is touch-only, but pinch isn't reproducible on a WebGL-desktop browser — §12.5 note —
    /// so scroll/keys exist to make zoom verifiable on the actual build target).
    /// Binds to Pointer (mouse + touch) so pan is verifiable with a browser mouse.
    public sealed class CameraController : MonoBehaviour
    {
        static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

        GameConfigSO _config;
        Camera _camera;
        Quaternion _rotation;
        Vector3 _focus;
        float _zoom;

        bool _dragging;
        Vector3 _grabPoint;
        float _lastPinchDistance;

        public void Init(GameConfigSO config, Vector3 focus)
        {
            _config = config;
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _rotation = Quaternion.Euler(config.cameraPitchDegrees, config.cameraYawDegrees, 0f);
            _focus = focus;
            _zoom = config.cameraStartZoom;
            ClampFocus();
            ApplyCamera();
        }

        void Update()
        {
            if (_config == null) return; // not yet booted
            UpdatePan();
            UpdatePinchZoom();
            UpdateDesktopZoom();
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
                if (Mathf.Abs(scroll) > 0.01f) delta -= Mathf.Sign(scroll) * _config.scrollZoomStep;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float step = _config.keyZoomSpeed * Time.deltaTime;
                if (keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed) delta -= step;
                if (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed) delta += step;
            }

            if (delta != 0f)
            {
                _zoom = Mathf.Clamp(_zoom + delta, _config.cameraMinZoom, _config.cameraMaxZoom);
                ApplyCamera();
            }
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
                    ClampFocus();
                    ApplyCamera();
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
                _zoom = Mathf.Clamp(_zoom * scale, _config.cameraMinZoom, _config.cameraMaxZoom);
                ApplyCamera();
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
            float halfX = _config.gridCols * _config.cellSize * 0.5f;
            float halfZ = _config.gridRows * _config.cellSize * 0.5f;
            float margin = _config.panMarginCells * _config.cellSize;
            _focus.x = Mathf.Clamp(_focus.x, -halfX - margin, halfX + margin);
            _focus.z = Mathf.Clamp(_focus.z, -halfZ - margin, halfZ + margin);
            _focus.y = 0f;
        }

        void ApplyCamera()
        {
            _camera.orthographicSize = _zoom;
            transform.rotation = _rotation;
            transform.position = _focus - _rotation * Vector3.forward * _config.cameraDistance;
        }
    }
}
