namespace PenguineBall.Core
{
    /// <summary>
    /// Interface for ball state management. Full implementation in Story 1.4.
    /// Stubbed here so BallController can compile and be tested independently.
    /// </summary>
    public interface IBallStateManager
    {
        BallState CurrentState { get; }
        bool TryTransitionTo(BallState newState);
        bool IsFireActive { get; }
    }
}
