using UnityEngine;

namespace PenguineBall.Core
{
    /// <summary>
    /// Persistent bootstrap manager. Lives on the Bootstrap scene's root GameObject.
    /// Runs before all other MonoBehaviours via [DefaultExecutionOrder(-100)].
    /// Registers all services in Awake so they are available by the time any Start() runs.
    /// Story 6.3 depends on this execution ordering for analytics service registration.
    /// ADR-009: Code Architecture Pattern.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class BootstrapManager : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            RegisterServices();
        }

        private void RegisterServices()
        {
            // Services are registered here in Awake.
            // Retrieval via ServiceLocator.Get<T>() must happen in Start(), never Awake().
            // Stories will add their registrations here as they are implemented:
            //   Story 1.2 → IInputProvider
            //   Story 2.1 → ISaveService
            //   Story 5.1 → IAudioService
            //   Story 6.3 → IAnalyticsService (overwrites null implementation)
        }
    }
}
