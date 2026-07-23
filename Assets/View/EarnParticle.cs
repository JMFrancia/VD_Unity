using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace VoidDay.View
{
    /// The feel of a flying earn particle. Every number and both eases are here so a tuning pass is an
    /// inspector pass — one serialized instance lives on EarnBurstController and is passed by value into
    /// each Launch.
    [Serializable]
    public struct FlightSettings
    {
        [Tooltip("How far a particle scatters from the launch point before it flies home, in canvas units.")]
        [Min(0f)] public float scatterRadius;

        [Tooltip("Seconds spent scattering outward.")]
        [Min(0f)] public float scatterSeconds;

        public Ease scatterEase;

        [Tooltip("Seconds spent flying from the scatter point to the destination.")]
        [Min(0f)] public float flightSeconds;

        [Tooltip("Random +/- spread on flightSeconds so the stream does not arrive in lockstep.")]
        [Min(0f)] public float flightSecondsJitter;

        public Ease flightEase;
    }

    /// One flying icon. It knows nothing about money, XP or resources — the controller hands it a sprite, a
    /// launch point, a destination and a callback, and it flies.
    ///
    /// The flight FOLLOWS the destination rect rather than baking its position: a destination pill can still
    /// be sliding into place while the first particles are already in the air.
    [RequireComponent(typeof(Image))]
    public sealed class EarnParticle : MonoBehaviour
    {
        RectTransform _rect;
        RectTransform _parent;
        RectTransform _target;
        Action _onArrive;
        Tween _tween;
        bool _arrived;

        /// Sizing comes from this prefab's own authored rect — the controller never resizes it.
        public void Launch(Sprite icon, Vector2 fromLocal, RectTransform target, FlightSettings settings,
            Action onArrive)
        {
            _rect = (RectTransform)transform;
            _parent = (RectTransform)_rect.parent;
            _target = target;
            _onArrive = onArrive;

            GetComponent<Image>().sprite = icon;
            _rect.anchoredPosition = fromLocal;

            Vector2 scatterPoint = fromLocal + UnityEngine.Random.insideUnitCircle * settings.scatterRadius;
            float flightSeconds = settings.flightSeconds
                + UnityEngine.Random.Range(-settings.flightSecondsJitter, settings.flightSecondsJitter);

            _tween = DOTween.Sequence()
                .Append(_rect.DOAnchorPos(scatterPoint, settings.scatterSeconds).SetEase(settings.scatterEase))
                .Append(DOVirtual.Float(0f, 1f, flightSeconds,
                        t => _rect.anchoredPosition = Vector2.Lerp(scatterPoint, TargetLocal(), t))
                    .SetEase(settings.flightEase))
                .OnComplete(Arrive);
        }

        /// The destination lives on a different Overlay canvas, so its position is re-read through screen
        /// space every tick. Overlay means the camera argument is null in both directions — passing
        /// Camera.main compiles and silently produces wrong coordinates.
        Vector2 TargetLocal()
        {
            // A destination can legitimately be torn down mid-flight: a reset drops the transient resource
            // pills while their icons are still in the air. Freeze in place rather than throw — the particle
            // still credits its chunk on destroy, which is what keeps the counters exact.
            if (_target == null) return _rect.anchoredPosition;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, _target.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, screen, null, out var local);
            return local;
        }

        /// Fires exactly once, whether the flight completed or the particle was torn down mid-air. The
        /// deferred-credit scheme depends on that guarantee: a stranded particle would leave its chunk in
        /// the destination's pending count forever, understating the counter for the rest of the session.
        void Arrive()
        {
            if (_arrived) return;
            _arrived = true;

            var callback = _onArrive;
            _onArrive = null;
            callback?.Invoke();

            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (!_arrived)
            {
                _arrived = true;
                var callback = _onArrive;
                _onArrive = null;
                callback?.Invoke();
            }

            _tween?.Kill();
            _tween = null;
        }
    }
}
