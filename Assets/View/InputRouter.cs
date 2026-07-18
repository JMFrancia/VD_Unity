using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VoidDay.Core.Events;

namespace VoidDay.View
{
    /// Captures a world tap and publishes the matching input intent — it never acts on the tap (§15).
    /// A tap is a press+release with little movement that isn't a pan (CameraController owns drag) and
    /// isn't over UI. A hit on a filled queue slot (QueueSlotTag) → input:jobCancelRequested; otherwise a
    /// hit on a station body (StationTag) → input:stationTapped. Queue slots are checked first because they
    /// sit under the same station root, and a tap on a slot means "cancel this job", not "tap the station".
    public sealed class InputRouter : MonoBehaviour
    {
        const float TapMoveThresholdPixels = 20f; // beyond this the gesture was a pan, not a tap

        EventBus _bus;
        Camera _camera;

        Vector2 _pressPosition;
        bool _pressStartedOverUi;

        public void Init(EventBus bus, Camera camera)
        {
            _bus = bus;
            _camera = camera;
        }

        void Update()
        {
            if (_bus == null) return;
            var pointer = Pointer.current;
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame)
            {
                _pressPosition = pointer.position.ReadValue();
                _pressStartedOverUi = IsOverUi();
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                if (_pressStartedOverUi) return;
                Vector2 release = pointer.position.ReadValue();
                if (Vector2.Distance(release, _pressPosition) > TapMoveThresholdPixels) return; // it was a pan
                if (IsOverUi()) return;
                TryTapStation(release);
            }
        }

        void TryTapStation(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 500f)) return;

            var slot = hit.collider.GetComponentInParent<QueueSlotTag>();
            if (slot != null)
            {
                _bus.Publish(new JobCancelRequested(slot.StationId, slot.SlotIndex));
                return;
            }

            var tag = hit.collider.GetComponentInParent<StationTag>();
            if (tag == null) return;
            _bus.Publish(new StationTapped(tag.StationId));
        }

        static bool IsOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
