using NUnit.Framework;
using PenguineBall.Input;
using UnityEngine;

namespace PenguineBall.Tests
{
    /// <summary>
    /// EditMode tests for input provider contracts.
    /// GyroscopeInputProvider requires a real device for full coverage;
    /// these tests validate the interface contract and joystick parity logic.
    /// </summary>
    public class InputProviderTests
    {
        // ── JoystickInputProvider ─────────────────────────────────────────────────

        [Test]
        public void JoystickProvider_IsAlwaysAvailable()
        {
            var provider = new JoystickInputProvider();
            Assert.IsTrue(provider.IsAvailable);
        }

        [Test]
        public void JoystickProvider_Calibrate_IsNoOp()
        {
            var provider = new JoystickInputProvider();
            Assert.DoesNotThrow(() => provider.Calibrate());
        }

        [Test]
        public void JoystickProvider_GetMovementInput_ReturnsZero_WhenNoJoystickSet()
        {
            var provider = new JoystickInputProvider();
            Assert.That(provider.GetMovementInput(), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void JoystickProvider_GetMovementInput_ReturnsZero_AfterNullJoystickSet()
        {
            var provider = new JoystickInputProvider();
            provider.SetJoystick(null);
            Assert.That(provider.GetMovementInput(), Is.EqualTo(Vector2.zero));
        }

        // ── IInputProvider contract ───────────────────────────────────────────────

        [Test]
        public void JoystickProvider_ImplementsIInputProvider()
        {
            IInputProvider provider = new JoystickInputProvider();
            Assert.IsNotNull(provider);
        }

        [Test]
        public void JoystickProvider_GetMovementInput_OutputWithinUnitRange()
        {
            // Without a real joystick controller, input should be zero (in range)
            var provider = new JoystickInputProvider();
            var input = provider.GetMovementInput();
            Assert.That(input.magnitude, Is.LessThanOrEqualTo(1f));
        }
    }
}
