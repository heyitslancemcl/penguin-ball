using PenguineBall.Input;
using PenguineBall.Progression;
using UnityEngine;

namespace PenguineBall.Core
{
    /// <summary>
    /// Drives ball movement via IInputProvider, enforces velocity cap, handles
    /// golden ball impulse, and detects stuck state.
    /// Attach to the Ball prefab/Player sphere alongside Rigidbody + SphereCollider.
    /// ADR-002: Physics Approach. ADR-009: MonoBehaviour lifecycle rules.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BallController : MonoBehaviour
    {
        [SerializeField] private LevelConfigSO _config;
        [SerializeField] private GameEventSO _onGoldenBallCollected;
        [SerializeField] private float _moveForce = 8f;

        private Rigidbody _rigidbody;
        private IInputProvider _input;
        private IBallStateManager _stateManager; // null-safe until Story 1.4
        private float _stuckTimer;

        // ── Unity lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            _input = ServiceLocator.Get<IInputProvider>();

            // IBallStateManager registered in Story 1.4 — guard until then
            try
            {
                _stateManager = ServiceLocator.Get<IBallStateManager>();
            }
            catch
            {
                Debug.LogWarning("BallController: IBallStateManager not yet registered (Story 1.4)");
            }
        }

        private void OnEnable()
        {
            _onGoldenBallCollected?.AddListener(HandleGoldenBallCollected);
        }

        private void OnDisable()
        {
            _onGoldenBallCollected?.RemoveListener(HandleGoldenBallCollected);
        }

        private void FixedUpdate()
        {
            var state = _stateManager?.CurrentState ?? BallState.Rolling;

            if (state == BallState.Rolling)
            {
                Vector2 input = _input.GetMovementInput();
                Vector3 force = new Vector3(input.x, 0f, input.y) * _moveForce;
                _rigidbody.AddForce(force, ForceMode.Force);
            }

            // Velocity cap — mandatory, no unbounded acceleration (AC: 2)
            if (_config != null && _rigidbody.linearVelocity.magnitude > _config.maxVelocity)
                _rigidbody.linearVelocity = _rigidbody.linearVelocity.normalized * _config.maxVelocity;
        }

        private void Update()
        {
            if (_config == null) return;

            var state = _stateManager?.CurrentState ?? BallState.Rolling;
            bool isStuck = _rigidbody.linearVelocity.magnitude < _config.stuckVelocityThreshold
                           && state != BallState.Stunned
                           && state != BallState.Dead;

            if (isStuck)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer >= _config.stuckDetectionWindow)
                    ShowStuckPrompt();
            }
            else
            {
                _stuckTimer = 0f;
            }
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void HandleGoldenBallCollected()
        {
            if (_config == null) return;
            // Speed burst in current travel direction (AC: 4)
            Vector3 direction = _rigidbody.linearVelocity.magnitude > 0.01f
                ? _rigidbody.linearVelocity.normalized
                : transform.forward;

            _rigidbody.AddForce(direction * _config.goldenBallImpulseMagnitude, ForceMode.Impulse);
        }

        private void ShowStuckPrompt()
        {
            // Stub — full level restart wired in Story 2.5
            // Prevents repeat triggers once detected
            _stuckTimer = -999f;
            Debug.Log("BallController: Ball appears stuck — Story 2.5 will show restart prompt");
        }
    }
}
