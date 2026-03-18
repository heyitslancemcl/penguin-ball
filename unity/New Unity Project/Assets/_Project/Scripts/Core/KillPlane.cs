using System.Collections;
using UnityEngine;

namespace PenguineBall.Core
{
    /// <summary>
    /// Trigger placed well below all platforms (Y = -20 default).
    /// Lets the ball arc naturally off the edge before registering death.
    /// Attach to an empty GameObject with a trigger BoxCollider in GameScene.
    /// </summary>
    public class KillPlane : MonoBehaviour
    {
        [SerializeField] private float _deathArcWaitSeconds = 0.4f;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Ball")) return;
            StartCoroutine(DeathSequence(other.gameObject));
        }

        private IEnumerator DeathSequence(GameObject ball)
        {
            // Let physics run so ball arcs visibly off the edge (AC: 6)
            yield return new WaitForSeconds(_deathArcWaitSeconds);

            var stateManager = ServiceLocator.Get<IBallStateManager>();
            stateManager?.TryTransitionTo(BallState.Dead);
        }
    }
}
