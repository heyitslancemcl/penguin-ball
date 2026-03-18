using NUnit.Framework;
using PenguineBall.Core;

namespace PenguineBall.Tests
{
    /// <summary>
    /// EditMode tests for BallState enum and IBallStateManager contract.
    /// BallController physics tests require PlayMode (Rigidbody needs the runtime).
    /// </summary>
    public class BallControllerTests
    {
        // ── BallState ─────────────────────────────────────────────────────────────

        [Test]
        public void BallState_HasExpectedValues()
        {
            Assert.That((int)BallState.Rolling,   Is.EqualTo(0));
            Assert.That((int)BallState.Stunned,   Is.EqualTo(1));
            Assert.That((int)BallState.Recovery,  Is.EqualTo(2));
            Assert.That((int)BallState.Dead,      Is.EqualTo(3));
        }

        [Test]
        public void BallState_DefaultIsRolling()
        {
            BallState state = default;
            Assert.That(state, Is.EqualTo(BallState.Rolling));
        }

        // ── GameEventSO ───────────────────────────────────────────────────────────

        [Test]
        public void GameEventSO_AddListener_AndRaise_InvokesCallback()
        {
            var evt = UnityEngine.ScriptableObject.CreateInstance<GameEventSO>();
            bool invoked = false;
            evt.AddListener(() => invoked = true);

            evt.Raise();

            Assert.IsTrue(invoked);
            UnityEngine.Object.DestroyImmediate(evt);
        }

        [Test]
        public void GameEventSO_RemoveListener_StopsCallback()
        {
            var evt = UnityEngine.ScriptableObject.CreateInstance<GameEventSO>();
            int count = 0;
            System.Action listener = () => count++;

            evt.AddListener(listener);
            evt.RemoveListener(listener);
            evt.Raise();

            Assert.That(count, Is.EqualTo(0));
            UnityEngine.Object.DestroyImmediate(evt);
        }

        [Test]
        public void GameEventSO_Raise_WithNoListeners_DoesNotThrow()
        {
            var evt = UnityEngine.ScriptableObject.CreateInstance<GameEventSO>();
            Assert.DoesNotThrow(() => evt.Raise());
            UnityEngine.Object.DestroyImmediate(evt);
        }

        [Test]
        public void GameEventSO_MultipleListeners_AllInvoked()
        {
            var evt = UnityEngine.ScriptableObject.CreateInstance<GameEventSO>();
            int count = 0;
            evt.AddListener(() => count++);
            evt.AddListener(() => count++);
            evt.AddListener(() => count++);

            evt.Raise();

            Assert.That(count, Is.EqualTo(3));
            UnityEngine.Object.DestroyImmediate(evt);
        }
    }
}
