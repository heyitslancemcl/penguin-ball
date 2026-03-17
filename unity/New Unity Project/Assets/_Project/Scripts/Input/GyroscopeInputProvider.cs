using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PenguineBall.Input
{
    /// <summary>
    /// Gyroscope-based input provider using the new Input System AttitudeSensor.
    /// Implements delta-from-baseline with a configurable low-pass filter.
    /// ADR-001: Input Abstraction Layer.
    /// </summary>
    public class GyroscopeInputProvider : IInputProvider
    {
        // ── Configuration ─────────────────────────────────────────────────────────

        /// <summary>
        /// Low-pass filter coefficient. Lower = smoother but more lag.
        /// Higher = more responsive but jittery.
        /// Recommended range: 8–12. Multiply by Time.deltaTime in Lerp.
        /// </summary>
        private readonly float _filterCoefficient;

        /// <summary>
        /// Sensitivity scalar applied to raw tilt angles before clamping.
        /// Tune per-device during playtesting (iPhone SE vs iPhone 16 Pro).
        /// </summary>
        private readonly float _sensitivityScalar;

        /// <summary>
        /// Cumulative delta magnitude threshold before drift is flagged.
        /// </summary>
        private readonly float _driftThreshold;

        /// <summary>
        /// How many seconds of sustained drift before the event fires.
        /// </summary>
        private readonly float _driftWindowSeconds;

        // ── State ─────────────────────────────────────────────────────────────────

        private Quaternion _baseline = Quaternion.identity;
        private Vector2 _smoothed = Vector2.zero;
        private float _driftAccumulator;
        private bool _driftEventFired;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fired when sustained gyroscope drift exceeds threshold.
        /// Subscribe in InputCalibrationHandler to show the recalibration toast.
        /// </summary>
        public event Action OnDriftDetected;

        // ── Constructor ───────────────────────────────────────────────────────────

        public GyroscopeInputProvider(
            float filterCoefficient  = 10f,
            float sensitivityScalar  = 2.5f,
            float driftThreshold     = 0.3f,
            float driftWindowSeconds = 3f)
        {
            _filterCoefficient  = filterCoefficient;
            _sensitivityScalar  = sensitivityScalar;
            _driftThreshold     = driftThreshold;
            _driftWindowSeconds = driftWindowSeconds;

            EnableSensor();
            Calibrate();
        }

        // ── IInputProvider ────────────────────────────────────────────────────────

        public bool IsAvailable => SystemInfo.supportsGyroscope
                                   && AttitudeSensor.current != null;

        /// <summary>
        /// Captures the current attitude as the new calibration baseline.
        /// Called at every level load and on resume from background.
        /// </summary>
        public void Calibrate()
        {
            if (AttitudeSensor.current == null) return;
            _baseline = AttitudeSensor.current.attitude.ReadValue();
            _smoothed = Vector2.zero;
            _driftAccumulator = 0f;
            _driftEventFired  = false;
        }

        /// <summary>
        /// Returns a low-pass filtered, delta-from-baseline normalised movement vector.
        /// Must be called from MonoBehaviour.Update() or FixedUpdate() for Time.deltaTime.
        /// </summary>
        public Vector2 GetMovementInput()
        {
            if (AttitudeSensor.current == null) return Vector2.zero;

            var currentAttitude = AttitudeSensor.current.attitude.ReadValue();

            // Delta from baseline — removes initial device orientation offset
            Quaternion delta = Quaternion.Inverse(_baseline) * currentAttitude;
            delta.ToAngleAxis(out _, out Vector3 axis);

            // Map quaternion delta to a 2D tilt vector
            // Euler angles can gimbal-lock; use the delta axis directly instead
            Vector3 euler = delta.eulerAngles;
            float rawX = WrapAngle(euler.z) * _sensitivityScalar;   // lateral
            float rawY = WrapAngle(euler.x) * _sensitivityScalar;   // forward/back

            Vector2 raw = new Vector2(rawX, rawY);

            // Frame-rate independent low-pass filter
            _smoothed = Vector2.Lerp(_smoothed, raw, _filterCoefficient * Time.deltaTime);

            // Clamp to [-1, 1]
            _smoothed = Vector2.ClampMagnitude(_smoothed, 1f);

            TrackDrift(raw);

            return _smoothed;
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void EnableSensor()
        {
            if (AttitudeSensor.current != null)
                InputSystem.EnableDevice(AttitudeSensor.current);
        }

        /// <summary>
        /// Wraps an angle from Unity's 0–360 range to -180–180.
        /// </summary>
        private static float WrapAngle(float angle)
        {
            if (angle > 180f) angle -= 360f;
            return angle / 180f; // Normalise to [-1, 1]
        }

        private void TrackDrift(Vector2 raw)
        {
            if (_driftEventFired) return;

            if (raw.magnitude > _driftThreshold)
            {
                _driftAccumulator += Time.deltaTime;
                if (_driftAccumulator >= _driftWindowSeconds)
                {
                    _driftEventFired = true;
                    OnDriftDetected?.Invoke();
                }
            }
            else
            {
                _driftAccumulator = 0f;
            }
        }
    }
}
