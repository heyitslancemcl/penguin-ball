using UnityEngine;

namespace PenguineBall.Input
{
    /// <summary>
    /// Virtual joystick fallback input provider.
    /// Registered when SystemInfo.supportsGyroscope is false.
    /// Outputs identical Vector2 range as GyroscopeInputProvider for gameplay parity (AC: 6).
    /// ADR-001: Input Abstraction Layer.
    /// </summary>
    public class JoystickInputProvider : IInputProvider
    {
        private VirtualJoystickController _joystick;

        /// <summary>
        /// Always true — joystick is the universal fallback.
        /// </summary>
        public bool IsAvailable => true;

        /// <summary>
        /// No-op — nothing to calibrate on a virtual joystick.
        /// </summary>
        public void Calibrate() { }

        /// <summary>
        /// Assign the instantiated joystick component so GetMovementInput can read it.
        /// Called by BootstrapManager after instantiating the VirtualJoystick prefab.
        /// </summary>
        public void SetJoystick(VirtualJoystickController joystick)
        {
            _joystick = joystick;
        }

        /// <summary>
        /// Returns normalised joystick input. Matches GyroscopeInputProvider [-1, 1] range.
        /// </summary>
        public Vector2 GetMovementInput()
        {
            if (_joystick == null) return Vector2.zero;
            return _joystick.Input;
        }
    }
}
