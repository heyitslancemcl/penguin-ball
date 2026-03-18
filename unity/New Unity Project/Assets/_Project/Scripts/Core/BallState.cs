namespace PenguineBall.Core
{
    /// <summary>
    /// All possible ball states. Transitions managed by BallStateManager (Story 1.4).
    /// </summary>
    public enum BallState
    {
        Rolling,
        Stunned,
        Recovery,
        Dead
    }
}
