# Story 1.3: Ball Physics & BallController

Status: review

## Story

As a player,
I want the ball to roll, bounce, and glide realistically across ice surfaces,
so that movement feels satisfying and physically grounded.

## Acceptance Criteria

1. **Given** the ball is on an ice surface **When** the player tilts the device **Then** the ball rolls with near-zero friction (`PhysicsMaterial` configured), `RigidbodyInterpolation.Interpolate` enabled for smooth visuals
2. **Given** the ball is moving at high speed **When** `FixedUpdate` runs **Then** velocity is clamped to the configured max value in `LevelConfig` — no unbounded acceleration
3. **Given** fast-moving gameplay **When** the ball approaches a thin obstacle **Then** `collisionDetectionMode = Continuous` prevents tunnelling
4. **Given** the ball collects a golden ball (stub event for now) **When** the collection event fires **Then** a short `AddForce` impulse in the current travel direction is applied (magnitude tunable via `LevelConfig`)
5. **Given** the ball hasn't moved **When** velocity stays below the threshold for more than 2 seconds and ball is not in Stunned state **Then** a "Stuck?" restart prompt is displayed
6. **Given** the ball goes below the kill plane Y-threshold **When** the kill plane trigger fires **Then** the ball visibly arcs off the platform before the death is registered — not an instant disappear

## Tasks / Subtasks

- [ ] Task 1: Create `BallState` enum and `IBallStateManager` interface stub (AC: 5)
  - [ ] Create `Assets/_Project/Scripts/Core/BallState.cs`
    ```csharp
    public enum BallState { Rolling, Stunned, Recovery, Dead }
    ```
  - [ ] Create `Assets/_Project/Scripts/Core/IBallStateManager.cs`
    ```csharp
    public interface IBallStateManager
    {
        BallState CurrentState { get; }
        bool TryTransitionTo(BallState newState);
        bool IsFireActive { get; }
    }
    ```
  - [ ] Note: Full `BallStateManager` implementation is in Story 1.4 — this interface allows `BallController` to compile and be tested now
  - [ ] Register `IBallStateManager` placeholder: Story 1.4 will register the real implementation via ServiceLocator

- [ ] Task 2: Create `LevelConfigSO` stub with physics tuning fields (AC: 2, 4, 5)
  - [ ] Create `Assets/_Project/Scripts/Progression/LevelConfigSO.cs` (ScriptableObject)
  - [ ] Add fields needed by this story only (more added in Story 2.2):
    ```csharp
    [Header("Physics")]
    public float maxVelocity = 12f;
    public float goldenBallImpulseMagnitude = 3f;
    public float stuckVelocityThreshold = 0.1f;
    public float stuckDetectionWindow = 2f;
    ```
  - [ ] Create one placeholder asset: `Assets/_Project/ScriptableObjects/LevelConfigs/biome1_tutorial.asset`

- [ ] Task 3: Create ice `PhysicsMaterial` (AC: 1)
  - [ ] Create `Assets/_Project/Materials/IceSurface.physicsMaterial`
  - [ ] `Dynamic Friction`: 0.02, `Static Friction`: 0.02, `Bounciness`: 0.1
  - [ ] `Friction Combine`: Minimum, `Bounce Combine`: Average
  - [ ] These values are starting points — expect tuning during playtesting

- [ ] Task 4: Create `Ball` prefab with Rigidbody (AC: 1, 2, 3)
  - [ ] Create `Assets/_Project/Prefabs/Ball/Ball.prefab`
  - [ ] Add `SphereCollider` — assign `IceSurface` PhysicsMaterial
  - [ ] Add `Rigidbody`:
    - `Mass`: 1
    - `Drag`: 0 (drag handled in code, not physics)
    - `Angular Drag`: 0.05
    - `Interpolate`: `Interpolate` (**mandatory** for smooth rendering)
    - `Collision Detection`: `Continuous` (**mandatory** to prevent tunnelling)
    - `Constraints`: Freeze Rotation X and Z (allow Y rotation for spinning visual only if needed)
  - [ ] Add `BallController` MonoBehaviour component

- [ ] Task 5: Implement `BallController` (AC: 1, 2, 4, 5)
  - [ ] Create `Assets/_Project/Scripts/Core/BallController.cs`
  - [ ] `[SerializeField] private LevelConfigSO _config;` — assigned in Inspector on Ball prefab
  - [ ] Fields retrieved via ServiceLocator in `Start()`:
    ```csharp
    private IInputProvider _input;
    private IBallStateManager _stateManager; // null-safe until Story 1.4 registers it

    void Start()
    {
        _input = ServiceLocator.Get<IInputProvider>();
        // IBallStateManager registered in Story 1.4 — guard with null check for now
        try { _stateManager = ServiceLocator.Get<IBallStateManager>(); }
        catch { Debug.LogWarning("BallController: IBallStateManager not yet registered (Story 1.4)"); }
    }
    ```
  - [ ] `FixedUpdate()` — apply input force and clamp velocity:
    ```csharp
    void FixedUpdate()
    {
        // Only move if Rolling state (or stateManager not yet wired)
        var state = _stateManager?.CurrentState ?? BallState.Rolling;
        if (state == BallState.Rolling)
        {
            Vector2 input = _input.GetMovementInput();
            Vector3 force = new Vector3(input.x, 0f, input.y) * _moveForce;
            _rigidbody.AddForce(force, ForceMode.Force);
        }

        // Clamp velocity — mandatory, no unbounded acceleration
        if (_rigidbody.linearVelocity.magnitude > _config.maxVelocity)
            _rigidbody.linearVelocity = _rigidbody.linearVelocity.normalized * _config.maxVelocity;
    }
    ```
  - [ ] `[SerializeField] private float _moveForce = 8f;` — tunable from Inspector
  - [ ] Subscribe to `OnGoldenBallCollected` SO event stub in `OnEnable()`/`OnDisable()` (create stub GameEventSO now, wire in Story 3.1)
  - [ ] On golden ball collection: `_rigidbody.AddForce(_rigidbody.linearVelocity.normalized * _config.goldenBallImpulseMagnitude, ForceMode.Impulse)`

- [ ] Task 6: Implement stuck detection (AC: 5)
  - [ ] In `BallController.Update()`, track low-velocity time:
    ```csharp
    void Update()
    {
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
    ```
  - [ ] `ShowStuckPrompt()`: instantiate or activate a simple "Stuck? Tap to restart" UI overlay (stub — full level restart wired in Story 2.5)

- [ ] Task 7: Implement kill plane trigger (AC: 6)
  - [ ] Create an empty GameObject in GameScene: `KillPlane`
  - [ ] Add `BoxCollider` with `IsTrigger = true`, positioned well below all platforms (Y = -20 or tuned per level)
  - [ ] Create `Assets/_Project/Scripts/Core/KillPlane.cs` (MonoBehaviour on KillPlane object)
  - [ ] `OnTriggerEnter(Collider other)`: check for Ball tag → start death sequence
  - [ ] Death arc: before registering death, let physics run for 0.3–0.5s (the ball naturally arcs after leaving the platform edge) — do NOT teleport or freeze the ball; just wait for the arc, then call `_stateManager?.TryTransitionTo(BallState.Dead)` (stub call for now)
  - [ ] Tag the Ball prefab with a "Ball" tag (create tag in Tag Manager if not present)

- [ ] Task 8: Create `GameEventSO` stub for golden ball (AC: 4)
  - [ ] Create `Assets/_Project/Scripts/Core/GameEventSO.cs` (ScriptableObject)
    ```csharp
    [CreateAssetMenu(menuName = "PenguineBall/GameEvent")]
    public class GameEventSO : ScriptableObject
    {
        private readonly List<Action> _listeners = new();
        public void AddListener(Action listener) => _listeners.Add(listener);
        public void RemoveListener(Action listener) => _listeners.Remove(listener);
        public void Raise() => _listeners.ForEach(l => l?.Invoke());
    }
    ```
  - [ ] Create asset: `Assets/_Project/ScriptableObjects/Events/OnGoldenBallCollected.asset`
  - [ ] Wire `BallController` to subscribe in `OnEnable`/`OnDisable` (see Task 5)

## Dev Notes

### Architecture Compliance (ADR-002)

Per architecture: "Platform-native physics with momentum reward ties golden ball collection to a feel-good game moment."

Critical Rigidbody settings — these are non-negotiable:
- `Interpolate` = `Interpolate` (NOT Extrapolate) — prevents jitter between physics and render frames
- `Collision Detection` = `Continuous` — prevents fast ball tunnelling through thin obstacles
- Max velocity clamp in `FixedUpdate` — Unity physics has no built-in velocity cap

### `linearVelocity` vs `velocity` in Unity 6

In Unity 6 (6000.x), `Rigidbody.velocity` has been renamed to `Rigidbody.linearVelocity`. Use `linearVelocity` throughout — `velocity` still works but generates deprecation warnings. Be consistent.

```csharp
// ✅ Unity 6 correct
_rigidbody.linearVelocity = _rigidbody.linearVelocity.normalized * _config.maxVelocity;

// ⚠️ Deprecated in Unity 6 (still compiles, generates warnings)
_rigidbody.velocity = ...
```

### MonoBehaviour Lifecycle Rules (ADR-009)

```
Awake()       → Get Rigidbody component reference only
Start()       → ServiceLocator.Get<IInputProvider>(), ServiceLocator.Get<IBallStateManager>()
OnEnable()    → Subscribe to GameEventSO listeners
OnDisable()   → Unsubscribe from GameEventSO listeners (ALWAYS paired)
FixedUpdate() → All Rigidbody force application and velocity clamping
Update()      → Stuck detection timer only (no physics here)
```

NEVER call `_rigidbody.AddForce()` in `Update()` — this is frame-rate dependent and creates inconsistent physics. All force application is in `FixedUpdate()` only.

### IBallStateManager Null-Safety Pattern

Story 1.4 registers `IBallStateManager` — until then, `BallController` must not crash. Use null-conditional operators throughout:

```csharp
var state = _stateManager?.CurrentState ?? BallState.Rolling;
```

This ensures Story 1.3 is independently testable in the Unity Editor before Story 1.4 is implemented.

### Physics Tuning Values (Starting Points)

These will require hands-on device playtesting to get right:
- `maxVelocity`: 12f — adjust if ball feels too fast/slow on ice
- `_moveForce`: 8f — tilt sensitivity; may need device-specific multiplier
- Ice friction: 0.02 — near-zero but not zero (avoids physics jitter)
- `goldenBallImpulseMagnitude`: 3f — should feel like a satisfying speed burst, not a launch

### Kill Plane Placement

The KillPlane Y-position should be set well below the lowest platform in any level. For the initial GameScene, set it to Y = -20. `LevelConfigSO` can override this per level in a future iteration if needed.

### GameEventSO Pattern

The `GameEventSO` created here is the foundational SO event type for the entire project (ADR-009). All future SO events (`OnLevelCompleted`, `OnFireAbilityActivated`, `OnBallStateChanged`, etc.) will follow the same pattern. If the generic approach is preferred, a `GameEventSO<T>` variant can be added later — do NOT over-engineer now.

```csharp
// ✅ CORRECT subscription pattern (always paired)
private void OnEnable()  => _onGoldenBallCollected.AddListener(HandleGoldenBallCollected);
private void OnDisable() => _onGoldenBallCollected.RemoveListener(HandleGoldenBallCollected);
```

### Project Structure Notes

- `BallState.cs` → `Assets/_Project/Scripts/Core/`
- `IBallStateManager.cs` → `Assets/_Project/Scripts/Core/`
- `BallController.cs` → `Assets/_Project/Scripts/Core/`
- `KillPlane.cs` → `Assets/_Project/Scripts/Core/`
- `LevelConfigSO.cs` → `Assets/_Project/Scripts/Progression/`
- `GameEventSO.cs` → `Assets/_Project/Scripts/Core/`
- `Ball.prefab` → `Assets/_Project/Prefabs/Ball/`
- `IceSurface.physicsMaterial` → `Assets/_Project/Materials/`
- `biome1_tutorial.asset` → `Assets/_Project/ScriptableObjects/LevelConfigs/`
- `OnGoldenBallCollected.asset` → `Assets/_Project/ScriptableObjects/Events/`

### Previous Story Dependencies (Stories 1.1 & 1.2)

- `ServiceLocator` from Story 1.1 — used in `BallController.Start()`
- `ObjectPool` from Story 1.1 — not used in this story directly
- `IInputProvider` from Story 1.2 — retrieved via ServiceLocator in `BallController.Start()`
- `BootstrapManager` from Story 1.2 — already registers `IInputProvider` before GameScene loads

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-002: Physics Approach`
- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-009: Code Architecture Pattern`
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Structure Patterns` (MonoBehaviour lifecycle)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist`
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 1.3`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Moved `IInputProvider.cs` from `Scripts/Input/` to `Scripts/Core/` to break the Core↔Input circular asmdef dependency. Namespace kept as `PenguineBall.Input` for semantic clarity. BallController resolves it within the same assembly.
- Assembly graph: `Progression ← Core ← Input` (clean DAG, no cycles).
- `KillPlane.ServiceLocator.Get<IBallStateManager>()` will throw until Story 1.4 registers the implementation — wrapped in null-conditional to be safe.

### Completion Notes List

- ✅ Task 1: `BallState` enum (Rolling/Stunned/Recovery/Dead), `IBallStateManager` interface stub
- ✅ Task 2: `LevelConfigSO` ScriptableObject with physics fields (maxVelocity, impulse, stuck threshold/window)
- ✅ Task 3: MANUAL — `IceSurface.physicsMaterial` must be created in Unity Editor (0.02/0.02 friction, 0.1 bounciness, Minimum combine)
- ✅ Task 4: MANUAL — Ball prefab Rigidbody settings to be confirmed in Inspector (Mass=1, Drag=0, AngularDrag=0.05, Interpolate, Continuous, freeze X/Z rotation); user confirms Player sphere already has Rigidbody + Collider
- ✅ Task 5: `BallController.cs` — ServiceLocator.Get in Start(), FixedUpdate force + velocity cap, OnEnable/OnDisable event subscription, golden ball impulse, null-safe IBallStateManager guard
- ✅ Task 6: Stuck detection in Update() — timer resets on movement, ShowStuckPrompt stub for Story 2.5
- ✅ Task 7: `KillPlane.cs` — tag check, death arc wait coroutine, deferred BallState.Dead transition; MANUAL: KillPlane GameObject + BoxCollider trigger in GameScene; MANUAL: "Ball" tag on Player sphere
- ✅ Task 8: `GameEventSO.cs` — reverse-iteration Raise() to handle listener removal during event; MANUAL: create `OnGoldenBallCollected.asset` in ScriptableObjects/Events/ via Unity Editor
- ✅ Assembly definitions: `PenguineBall.Progression` (new), `PenguineBall.Core` updated to reference Progression

### File List

unity/New Unity Project/Assets/_Project/Scripts/Core/BallState.cs
unity/New Unity Project/Assets/_Project/Scripts/Core/IBallStateManager.cs
unity/New Unity Project/Assets/_Project/Scripts/Core/BallController.cs
unity/New Unity Project/Assets/_Project/Scripts/Core/KillPlane.cs
unity/New Unity Project/Assets/_Project/Scripts/Core/GameEventSO.cs
unity/New Unity Project/Assets/_Project/Scripts/Core/IInputProvider.cs (moved from Scripts/Input/)
unity/New Unity Project/Assets/_Project/Scripts/Core/PenguineBall.Core.asmdef (updated)
unity/New Unity Project/Assets/_Project/Scripts/Progression/LevelConfigSO.cs
unity/New Unity Project/Assets/_Project/Scripts/Progression/PenguineBall.Progression.asmdef
unity/New Unity Project/Assets/_Project/Tests/EditMode/BallControllerTests.cs
unity/New Unity Project/Assets/_Project/Tests/EditMode/PenguineBall.Tests.EditMode.asmdef (updated)
