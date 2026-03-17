using PenguineBall.Core;
using UnityEngine;

namespace PenguineBall.Input
{
    /// <summary>
    /// Registers the correct IInputProvider with ServiceLocator and handles the
    /// initial calibration. Runs after BootstrapManager (-100) via order -99.
    /// Attach to the Bootstrap GameObject alongside BootstrapManager.
    /// ADR-001: Input Abstraction Layer.
    /// </summary>
    [DefaultExecutionOrder(-99)]
    public class InputBootstrapper : MonoBehaviour
    {
        [SerializeField] private GameObject _virtualJoystickPrefab;

        private void Awake()
        {
            if (SystemInfo.supportsGyroscope)
            {
                ServiceLocator.Register<IInputProvider>(new GyroscopeInputProvider());
                // VirtualJoystick is NOT shown when gyroscope is active
            }
            else
            {
                var joystickProvider = new JoystickInputProvider();
                ServiceLocator.Register<IInputProvider>(joystickProvider);

                if (_virtualJoystickPrefab != null)
                {
                    var joystickGO = Instantiate(_virtualJoystickPrefab);
                    DontDestroyOnLoad(joystickGO);
                    var controller = joystickGO.GetComponentInChildren<VirtualJoystickController>();
                    joystickProvider.SetJoystick(controller);
                }
            }
        }

        private void Start()
        {
            // Initial baseline captured in Start() so all Awake() registrations
            // are complete. Subsequent calibrations happen via InputCalibrationHandler
            // and on level load (Story 2.2).
            // TODO: wire Calibrate() to LevelManager.OnLevelLoad in Story 2.2
            ServiceLocator.Get<IInputProvider>().Calibrate();
        }
    }
}
