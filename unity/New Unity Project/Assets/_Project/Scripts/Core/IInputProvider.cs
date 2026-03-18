using UnityEngine;

namespace PenguineBall.Input
{
    /// <summary>
    /// Abstraction layer for all player movement input.
    /// BallController depends ONLY on this interface — never on a concrete provider.
    /// ADR-001: Input Abstraction Layer.
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// Returns a normalised 2D movement vector.
        /// x = lateral (left/right), y = forward/back.
        /// Range: [-1, 1] on each axis.
        /// </summary>
        Vector2 GetMovementInput();

        /// <summary>
        /// Triggers recalibration. For gyroscope: resets baseline quaternion.
        /// For joystick: no-op.
        /// </summary>
        void Calibrate();

        /// <summary>
        /// Returns true if this provider can operate on the current device.
        /// </summary>
        bool IsAvailable { get; }
    }
}
