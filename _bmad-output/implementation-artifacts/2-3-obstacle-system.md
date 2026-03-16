# Story 2.3: Obstacle System

Status: ready-for-dev

## Story

As a player,
I want to encounter a variety of obstacles that challenge me in different ways,
so that each level feels distinct and requires different skills to navigate.

## Acceptance Criteria

1. **Given** the ball contacts a `StunObstacle` **When** the collision registers **Then** `BallStateManager.TryTransitionTo(Stunned)` is called; a 1-second swipe-up window opens after the stun animation completes; a swipe-up arrow pulse prompt appears
2. **Given** the ball contacts a `FatalBounce` obstacle **When** the collision registers **Then** a strong directional impulse sends the ball off course toward the kill plane; death registers when the kill plane is reached
3. **Given** the ball contacts a `BreakableWall` without fire active **When** the collision registers **Then** the wall blocks the ball's path; no shattering occurs
4. **Given** the ball contacts a `BreakableWall` with fire active **When** the collision registers **Then** the wall shatter animation plays and the collider is disabled the same frame — no invisible blocking
5. **Given** the ball contacts an `IceSpike` without fire active **When** the collision registers **Then** death is triggered
6. **Given** the ball contacts an `IceSpike` with fire active **When** the collision registers **Then** the ice spike is destroyed and the ball continues
7. **Given** a `MovingPlatform` is in the scene **When** the level runs **Then** the platform moves via coroutine interpolation (not `Update()` polling); movement pauses while ball is in Stunned state
8. **Given** the ball falls through a `GroundHole` **When** the kill plane trigger fires **Then** death is registered with the same cinematic arc as a normal kill plane death

## Tasks / Subtasks

- [ ] Task 1: Create `ObstacleType` enum and base class (AC: 1–8)
  - [ ] Create `Assets/_Project/Scripts/Obstacles/ObstacleType.cs`:
    ```csharp
    public enum ObstacleType
    {
        StunObstacle,
        FatalBounce,
        BreakableWall,
        IceSpike,
        MovingPlatform,
        GroundHole
    }
    ```
  - [ ] Create `Assets/_Project/Scripts/Obstacles/ObstacleBehaviour.cs` (abstract MonoBehaviour):
    ```csharp
    public abstract class ObstacleBehaviour : MonoBehaviour
    {
        public abstract ObstacleType Type { get; }

        protected IBallStateManager BallStateManager =>
            ServiceLocator.Get<IBallStateManager>();
    }
    ```
  - [ ] All obstacle scripts inherit from `ObstacleBehaviour`

- [ ] Task 2: Implement `StunObstacle` (AC: 1)
  - [ ] Create `Assets/_Project/Scripts/Obstacles/StunObstacle.cs`
  - [ ] `public override ObstacleType Type => ObstacleType.StunObstacle;`
  - [ ] `[SerializeField] private float _stunWindowOverride = 0f;` — if > 0, overrides `LevelConfig` default (per-obstacle tuning)
  - [ ] `OnCollisionEnter(Collision col)`: check for Ball tag → `BallStateManager.TryTransitionTo(BallState.Stunned)`
  - [ ] Note: The 1-second stun window and swipe-up prompt are managed by `BallStateManager` (Story 1.4) — this obstacle only triggers the transition; it does not manage timing
  - [ ] Add `Collider` (box or mesh, matching obstacle shape) — no trigger, solid collision

- [ ] Task 3: Implement `FatalBounce` (AC: 2)
  - [ ] Create `Assets/_Project/Scripts/Obstacles/FatalBounce.cs`
  - [ ] `[SerializeField] private float _bounceForce = 20f;` — tunable per obstacle
  - [ ] `[SerializeField] private Vector3 _bounceDirection = Vector3.up;` — configurable in Inspector (can be any world direction)
  - [ ] `OnCollisionEnter(Collision col)`: check for Ball tag →
    ```csharp
    var rb = col.gameObject.GetComponent<Rigidbody>();
    if (rb != null)
        rb.AddForce(_bounceDirection.normalized * _bounceForce, ForceMode.Impulse);
    // Death registered by KillPlane when ball exits play area — do NOT call TryTransitionTo here
    ```
  - [ ] Note: Death is registered by `KillPlane` (Story 1.3), not by this obstacle. The impulse sends the ball toward the kill plane naturally.

- [ ] Task 4: Implement `BreakableWall` (AC: 3, 4)
  - [ ] Create `Assets/_Project/Scripts/Obstacles/BreakableWall.cs`
  - [ ] `[SerializeField] private Animator _shatterAnimator;` — assign shatter animation in Inspector
  - [ ] `[SerializeField] private Collider _wallCollider;` — assign wall collider in Inspector
  - [ ] `OnCollisionEnter(Collision col)`: check for Ball tag →
    ```csharp
    bool fireActive = BallStateManager.IsFireActive;
    if (!fireActive) return; // wall blocks — no action

    // Fire active: shatter the wall
    _wallCollider.enabled = false; // disable collider SAME FRAME as collision
    _shatterAnimator.SetTrigger("Shatter");
    // Optionally: Destroy(gameObject, 2f) after animation completes
    ```
  - [ ] CRITICAL: `_wallCollider.enabled = false` must happen in `OnCollisionEnter` — NOT in an animation event or coroutine. If disabled after the frame, the ball can be blocked by the invisible collider for one frame. Test this carefully.
  - [ ] Create `BreakableWall.prefab`: `Assets/_Project/Prefabs/Obstacles/BreakableWall.prefab`
  - [ ] Shatter animation: placeholder particle burst or simple scale-to-zero animation (visual polish out of scope)

- [ ] Task 5: Implement `IceSpike` (AC: 5, 6)
  - [ ] Create `Assets/_Project/Scripts/Obstacles/IceSpike.cs`
  - [ ] `OnCollisionEnter(Collision col)`: check for Ball tag →
    ```csharp
    if (BallStateManager.IsFireActive)
    {
        Destroy(gameObject); // ice spike destroyed by fire
        return;
    }
    BallStateManager.TryTransitionTo(BallState.Dead);
    ```
  - [ ] Note: Static `IceSpike` obstacles (placed in level) use `Destroy()` — NOT object pooling. Pooling is for dynamically spawned projectiles (Walrus AI in Story 4.3). Static placed obstacles are fine with Destroy.
  - [ ] Create `IceSpike.prefab`: `Assets/_Project/Prefabs/Obstacles/IceSpike.prefab`
  - [ ] Add pointy mesh collider matching spike shape (or use a capsule collider approximation)

- [ ] Task 6: Implement `MovingPlatform` (AC: 7)
  - [ ] Create `Assets/_Project/Scripts/Obstacles/MovingPlatform.cs`
  - [ ] Movement modes:
    ```csharp
    public enum MovementMode { HorizontalSlide, Spin180 }
    [SerializeField] private MovementMode _mode;
    [SerializeField] private float _moveDistance = 3f;  // for HorizontalSlide
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _spinDuration = 1f;  // for Spin180
    ```
  - [ ] Movement via coroutine — NOT `Update()`:
    ```csharp
    private IEnumerator SlideCoroutine()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + transform.right * _moveDistance;

        while (true)
        {
            // Pause movement while ball is stunned
            while (BallStateManager.CurrentState == BallState.Stunned)
                yield return null;

            yield return MoveToTarget(startPos);
            yield return MoveToTarget(endPos);
        }
    }

    private IEnumerator MoveToTarget(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            // Pause if ball becomes stunned mid-move
            if (BallStateManager.CurrentState == BallState.Stunned)
                yield return null;
            else
                transform.position = Vector3.MoveTowards(
                    transform.position, target, _moveSpeed * Time.deltaTime);
            yield return null;
        }
    }
    ```
  - [ ] `Start()`: `StartCoroutine(SlideCoroutine())` or `StartCoroutine(SpinCoroutine())` based on `_mode`
  - [ ] Create `MovingPlatform.prefab`: `Assets/_Project/Prefabs/Obstacles/MovingPlatform.prefab`

- [ ] Task 7: Implement `GroundHole` (AC: 8)
  - [ ] Create `Assets/_Project/Scripts/Obstacles/GroundHole.cs`
  - [ ] `GroundHole` is a trigger volume (invisible hole in the floor geometry)
  - [ ] Add `Collider` with `IsTrigger = true`
  - [ ] `OnTriggerEnter(Collider other)`: check for Ball tag → do nothing — ball falls through naturally
  - [ ] The `KillPlane` (Story 1.3) handles death when the ball reaches the Y-threshold below the hole
  - [ ] Note: The floor geometry around the hole should have a gap — the `GroundHole` collider is just a visual indicator/trigger, not the actual kill mechanism
  - [ ] The ball arcs naturally through the hole and hits the KillPlane — no special arc code needed here
  - [ ] Create `GroundHole.prefab`: `Assets/_Project/Prefabs/Obstacles/GroundHole.prefab`

- [ ] Task 8: Create obstacle prefabs and add to tutorial level scene (AC: 1–8)
  - [ ] Place one of each obstacle type in `biome1_tutorial.unity` scene for basic testing
  - [ ] Assign all required Inspector fields (colliders, animators, directions)
  - [ ] Test each obstacle type manually in Play Mode
  - [ ] Tag verification: confirm Ball GameObject has "Ball" tag (set up in Story 1.3)

## Dev Notes

### Obstacle Architecture Overview

Static obstacles (placed by level designer in scene) — no pooling:
- `StunObstacle`, `FatalBounce`, `BreakableWall`, `IceSpike`, `GroundHole`

Dynamic obstacles (spawned at runtime) — require pooling:
- `IceSpikeProjectile` (Walrus AI, Story 4.3) — pooled
- Fire particles (Story 4.1) — pooled
- Exploding ice blocks (Boss, Story 6.1) — pooled

Do NOT add object pooling to static level obstacles — they are placed once in the scene and stay there (or are destroyed once via `Destroy()`).

### `BreakableWall` Collider Timing — Critical

The single most common implementation mistake for `BreakableWall`:

```csharp
// ✅ CORRECT — disable collider in OnCollisionEnter (same frame)
void OnCollisionEnter(Collision col)
{
    _wallCollider.enabled = false;  // immediate
    _shatterAnimator.SetTrigger("Shatter");
}

// ❌ WRONG — ball gets blocked for one or more frames
void OnCollisionEnter(Collision col)
{
    _shatterAnimator.SetTrigger("Shatter");
    // Animation event fires collider disable 0.3s later — ball already stopped
}

// ❌ WRONG — coroutine delay causes invisible blocking
IEnumerator ShatterCoroutine()
{
    yield return new WaitForSeconds(0.1f);
    _wallCollider.enabled = false; // too late
}
```

### `FatalBounce` — No Direct Death Call

`FatalBounce` only applies an impulse. It does NOT call `TryTransitionTo(Dead)` directly. The ball's natural physics trajectory after the impulse sends it off the platform and into the `KillPlane` trigger. This gives the player a brief moment to see the ball arc dramatically before death — which is intentional cinematic design.

### `MovingPlatform` — Coroutine vs Update

Architecture explicitly requires coroutine interpolation, not `Update()` polling:

```csharp
// ✅ CORRECT — coroutine interpolation
private IEnumerator SlideCoroutine() { ... }

// ❌ WRONG — Update polling
void Update()
{
    transform.position = Vector3.MoveTowards(transform.position, _target, speed * Time.deltaTime);
}
```

The coroutine approach allows natural pause/resume during Stunned state without any additional state management.

### `IceSpike` Static vs Projectile

Two different types of ice spike exist in this game:
1. **Static `IceSpike`** (this story) — placed in level scene, uses `Destroy()` when hit with fire
2. **`IceSpikeProjectile`** (Story 4.3, Walrus AI) — dynamically spawned, uses object pooling

These are separate classes. Do NOT try to make one class serve both purposes.

### `BallStateManager.IsFireActive` Usage

All obstacles that check fire state use `BallStateManager.IsFireActive`:

```csharp
// Retrieve once in Start() — don't call ServiceLocator every OnCollisionEnter
private IBallStateManager _stateManager;
void Start() => _stateManager = ServiceLocator.Get<IBallStateManager>();

void OnCollisionEnter(Collision col)
{
    if (_stateManager.IsFireActive) { ... }
}
```

Cache the reference — don't call `ServiceLocator.Get<>()` in `OnCollisionEnter()` (called every physics frame on contact).

### Stun Window Duration Per Obstacle

`StunObstacle` can override the default stun window duration via `_stunWindowOverride`. The full wiring for per-obstacle duration is deferred — for now the `BallStateManager` uses its default `_stunWindowDuration`. Story 2.5 or a future polish pass can add per-obstacle duration injection.

### Project Structure Notes

- `ObstacleType.cs` → `Assets/_Project/Scripts/Obstacles/`
- `ObstacleBehaviour.cs` → `Assets/_Project/Scripts/Obstacles/`
- `StunObstacle.cs` → `Assets/_Project/Scripts/Obstacles/`
- `FatalBounce.cs` → `Assets/_Project/Scripts/Obstacles/`
- `BreakableWall.cs` → `Assets/_Project/Scripts/Obstacles/`
- `IceSpike.cs` → `Assets/_Project/Scripts/Obstacles/`
- `MovingPlatform.cs` → `Assets/_Project/Scripts/Obstacles/`
- `GroundHole.cs` → `Assets/_Project/Scripts/Obstacles/`
- All obstacle prefabs → `Assets/_Project/Prefabs/Obstacles/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — obstacle base class retrieves `IBallStateManager`
- `BallState` enum (1.3) — used in `StunObstacle` and `MovingPlatform`
- `IBallStateManager` (1.3/1.4) — `TryTransitionTo()` and `IsFireActive` used throughout
- `KillPlane` (1.3) — handles death for `FatalBounce` and `GroundHole` outcomes
- `LevelConfigSO` (2.2) — referenced for per-level tuning (stun window, etc.)

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-005: Obstacle Classification`
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (object pooling, moving platforms coroutine)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 2.3`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
