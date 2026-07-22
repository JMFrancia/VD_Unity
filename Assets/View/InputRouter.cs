using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using VoidDay.Core.Events;

namespace VoidDay.View
{
    /// Captures a world tap and publishes the matching input intent — it never acts on the tap (§15).
    /// A tap is a press+release with little movement that isn't a pan (CameraController owns drag) and
    /// isn't over UI. A hit on a running timer's radial (TimerWidget) → timer:skipTapped; otherwise a hit on
    /// a live queue slot (QueueSlot) → input:queueSlotTapped; otherwise a hit on a station body (StationView)
    /// → input:stationTapped. Radials and queue slots are checked first because they sit under the same
    /// station root, and a tap on either is about that job, not about the station.
    /// Whether that tap collects or cancels is Producer's call, not this class's.
    /// A tap on empty world (nothing interactive) → input:backgroundTapped, which dismisses an open panel.
    public sealed class InputRouter : MonoBehaviour
    {
        const float TapMoveThresholdPixels = 20f; // beyond this the gesture was a pan, not a tap

        [Tooltip("Hold this long on a station (without moving) to pick it up for a move (§12.2).")]
        [SerializeField] float longPressSeconds = 0.5f;

        EventBus _bus;
        Camera _camera;

        Vector2 _pressPosition;
        bool _pressStartedOverUi;
        double _pressTime;
        string _pressStationId;   // station under the press, if any (candidate for long-press pickup)
        bool _pickedUp;           // a long-press pickup fired this gesture → suppress the release tap

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
                _pressTime = Time.timeAsDouble;
                _pickedUp = false;
                _pressStationId = _pressStartedOverUi ? null : StationUnder(_pressPosition);
            }
            else if (pointer.press.isPressed)
            {
                TryLongPressPickup(pointer);
            }
            else if (pointer.press.wasReleasedThisFrame)
            {
                if (_pickedUp) return;         // the move ghost owns this gesture — no tap
                if (_pressStartedOverUi) return;
                Vector2 release = pointer.position.ReadValue();
                if (Vector2.Distance(release, _pressPosition) > TapMoveThresholdPixels) return; // it was a pan
                if (IsOverUi()) return;
                TryTapStation(release);
            }
        }

        /// Held long enough on a station without drifting → pick it up (§12.2). One shot per gesture.
        void TryLongPressPickup(Pointer pointer)
        {
            if (_pickedUp || _pressStationId == null) return;
            Vector2 pos = pointer.position.ReadValue();
            if (Vector2.Distance(pos, _pressPosition) > TapMoveThresholdPixels) { _pressStationId = null; return; }
            if (Time.timeAsDouble - _pressTime < longPressSeconds) return;
            _pickedUp = true;
            _bus.Publish(new StationPickedUp(_pressStationId));
        }

        string StationUnder(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                // A radial sits under the station root, so it would otherwise answer as the station and a long
                // press on a timer would pick the building up for a move. The radial is a skip target only.
                if (hit.collider.GetComponentInParent<TimerWidget>() != null) return null;
                var station = hit.collider.GetComponentInParent<StationView>();
                if (station != null) return station.Id;
            }
            return null;
        }

        void TryTapStation(Vector2 screenPosition)
        {
            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f))
            {
                // Checked before both: a running timer's radial floats above the station body and the queue
                // row, and a tap on it is about buying the wait away (§13), not about the station.
                var timer = hit.collider.GetComponentInParent<TimerWidget>();
                if (timer != null)
                {
                    _bus.Publish(new TimerSkipTapped(timer.Timer));
                    return;
                }

                var slot = hit.collider.GetComponentInParent<QueueSlot>();
                if (slot != null)
                {
                    _bus.Publish(new QueueSlotTapped(slot.StationId, slot.SlotIndex));
                    return;
                }

                var station = hit.collider.GetComponentInParent<StationView>();
                if (station != null)
                {
                    _bus.Publish(new StationTapped(station.Id));
                    return;
                }
            }

            _bus.Publish(new BackgroundTapped()); // tapped empty world → dismiss any open panel
        }

        static bool IsOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
