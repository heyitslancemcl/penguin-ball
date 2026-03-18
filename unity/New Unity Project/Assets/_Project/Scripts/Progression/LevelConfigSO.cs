using UnityEngine;

namespace PenguineBall.Progression
{
    /// <summary>
    /// Per-level configuration data. Assign to BallController and other systems via Inspector.
    /// Fields expanded in Story 2.2 (LevelManager). Only physics fields needed by Story 1.3 here.
    /// </summary>
    [CreateAssetMenu(menuName = "PenguineBall/LevelConfig")]
    public class LevelConfigSO : ScriptableObject
    {
        [Header("Physics")]
        [Tooltip("Maximum ball speed in m/s. Enforced in FixedUpdate to prevent unbounded acceleration.")]
        public float maxVelocity = 12f;

        [Tooltip("Impulse force applied to ball on golden ball collection. Feel-good speed burst.")]
        public float goldenBallImpulseMagnitude = 3f;

        [Tooltip("Velocity below which the ball is considered potentially stuck.")]
        public float stuckVelocityThreshold = 0.1f;

        [Tooltip("Seconds of sub-threshold velocity before the stuck prompt appears.")]
        public float stuckDetectionWindow = 2f;
    }
}
