using System.Collections;
using PenguineBall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PenguineBall.Input
{
    /// <summary>
    /// Handles gyroscope recalibration triggers:
    ///   - App resume from background (OnApplicationPause)
    ///   - Three-finger tap (with haptic feedback)
    ///   - Drift detection toast from GyroscopeInputProvider.OnDriftDetected
    /// Attach to the Bootstrap GameObject alongside BootstrapManager.
    /// ADR-001: Input Abstraction Layer.
    /// </summary>
    public class InputCalibrationHandler : MonoBehaviour
    {
        [SerializeField] private Text _toastText;
        [SerializeField] private float _toastDuration = 2.5f;

        private IInputProvider _input;
        private Coroutine _toastCoroutine;

        private void Start()
        {
            _input = ServiceLocator.Get<IInputProvider>();

            // Subscribe to drift events if the provider is a gyroscope
            if (_input is GyroscopeInputProvider gyro)
                gyro.OnDriftDetected += OnDriftDetected;
        }

        private void OnDestroy()
        {
            if (_input is GyroscopeInputProvider gyro)
                gyro.OnDriftDetected -= OnDriftDetected;
        }

        private void Update()
        {
            // Three-finger tap → immediate recalibration with haptic feedback
            if (UnityEngine.Input.touchCount == 3)
            {
                bool allBegan = true;
                for (int i = 0; i < 3; i++)
                {
                    if (UnityEngine.Input.GetTouch(i).phase != TouchPhase.Began)
                    {
                        allBegan = false;
                        break;
                    }
                }

                if (allBegan)
                {
                    _input.Calibrate();
                    Handheld.Vibrate();
                    ShowToast("Recalibrated!");
                }
            }
        }

        /// <summary>
        /// Recapture gyroscope baseline when the app returns from background.
        /// </summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused)
                _input?.Calibrate();
        }

        private void OnDriftDetected()
        {
            ShowToast("Tap with 3 fingers to recalibrate");
        }

        private void ShowToast(string message)
        {
            if (_toastText == null) return;

            if (_toastCoroutine != null)
                StopCoroutine(_toastCoroutine);

            _toastCoroutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            _toastText.text = message;

            // Fade in
            float elapsed = 0f;
            float fadeDuration = 0.3f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _toastText.color = new Color(1f, 1f, 1f, elapsed / fadeDuration);
                yield return null;
            }

            // Hold
            yield return new WaitForSeconds(_toastDuration);

            // Fade out
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _toastText.color = new Color(1f, 1f, 1f, 1f - (elapsed / fadeDuration));
                yield return null;
            }

            _toastText.color = new Color(1f, 1f, 1f, 0f);
            _toastCoroutine = null;
        }
    }
}
