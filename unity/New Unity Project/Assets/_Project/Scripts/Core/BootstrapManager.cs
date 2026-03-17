using UnityEngine;

namespace PenguineBall.Core
{
    /// <summary>
    /// Persistent bootstrap manager. Lives on the Bootstrap scene's root GameObject.
    /// Runs before all other MonoBehaviours via [DefaultExecutionOrder(-100)].
    /// Input registration is handled by InputBootstrapper (same GameObject, order -99)
    /// to keep Core free of Input assembly dependencies.
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
            // Input    → InputBootstrapper component (PenguineBall.Input assembly)
            // Story 2.1 → ISaveService
            // Story 5.1 → IAudioService
            // Story 6.3 → IAnalyticsService (overwrites null implementation)
        }
    }
}
