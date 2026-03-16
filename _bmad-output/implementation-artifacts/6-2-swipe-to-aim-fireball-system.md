# Story 6.2: Swipe-to-Aim Fireball System

Status: ready-for-dev

## Story

As a player,
I want to aim and shoot fireballs at the boss by swiping the screen,
so that defeating the boss requires both dodging and precise offensive targeting.

## Acceptance Criteria

1. **Given** the fire ability is active and the ball is in Rolling state **When** the player swipes the screen **Then** the swipe start→end delta is mapped to a world direction vector; a pooled `FireballProjectile` is launched in that direction
2. **Given** a swipe-to-aim gesture is in progress **When** `BallStateManager` is queried **Then** swipe-to-aim is only processed if `IsFireActive && CurrentState == Rolling` — mutually exclusive with swipe-up recovery (Stunned state); no gesture conflict
3. **Given** a `FireballProjectile` is launched **When** it travels **Then** it continues until it hits the boss or its lifetime expires; it is then returned to the pool
4. **Given** a fireball hits the boss **When** the hit registers **Then** `IBossController.RegisterHit()` is called; a hit reaction animation plays on `BossController`
5. **Given** the boss has been hit 4 times **When** the 4th fireball connects **Then** `BossController` transitions to Defeated; a defeat sequence plays; `OnLevelCompleted` fires
6. **Given** `FireballProjectile` instances are spawned **When** `ObjectPool` is inspected **Then** all fireball instances are pooled — no `Instantiate`/`Destroy` during gameplay
7. **Given** the swipe gesture is evaluated **When** the touch delta is calculated **Then** the minimum swipe distance is `Screen.height * 0.05f` (≈44pt on most iPhones); accidental taps are rejected

## Tasks / Subtasks

- [ ] Task 1: Add `TryGet<T>` to `ServiceLocator` (AC: 4)
  - [ ] Open `Assets/_Project/Scripts/Core/ServiceLocator.cs` (Story 1.1)
  - [ ] Add alongside the existing `Get<T>` method:
    ```csharp
    /// <summary>
    /// Returns true and sets 'service' if the type is registered.
    /// Returns false safely if not registered — no exception thrown.
    /// Use this for optional services (e.g. IBossController only exists in boss level).
    /// </summary>
    public static bool TryGet<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var obj) && obj is T typed)
        {
            service = typed;
            return true;
        }
        service = default;
        return false;
    }
    ```
  - [ ] `IBossController` is the primary use case — only registered during boss level; `TryGet` prevents crashes in non-boss contexts

- [ ] Task 2: Create `FireballProjectile` pooled MonoBehaviour (AC: 3, 4, 5, 6)
  - [ ] Create `Assets/_Project/Scripts/Enemies/FireballProjectile.cs`:
    ```csharp
    public class FireballProjectile : MonoBehaviour
    {
        [SerializeField] private float _maxLifetime = 4f;

        private Rigidbody _rigidbody;
        private Action    _onRelease;
        private float     _lifeTimer;
        private bool      _released;

        private void Awake() => _rigidbody = GetComponent<Rigidbody>();

        public void Launch(Vector3 position, Vector3 direction, float speed, Action onRelease)
        {
            _released          = false;
            _lifeTimer         = 0f;
            _onRelease         = onRelease;
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(direction);
            gameObject.SetActive(true);

            _rigidbody.linearVelocity = Vector3.zero; // clear any residual velocity
            _rigidbody.linearVelocity = direction.normalized * speed;
        }

        private void Update()
        {
            if (_released) return;
            _lifeTimer += Time.deltaTime;
            if (_lifeTimer >= _maxLifetime) Release();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_released) return;
            if (!other.CompareTag("Boss")) return; // only register boss hits — ignore everything else

            if (ServiceLocator.TryGet<IBossController>(out var boss))
                boss.RegisterHit();

            Release();
        }

        private void Release()
        {
            if (_released) return;
            _released = true;
            _rigidbody.linearVelocity = Vector3.zero;
            gameObject.SetActive(false);
            _onRelease?.Invoke();
        }

        private void OnDisable() => _released = true;
    }
    ```
  - [ ] Rigidbody settings:
    - `useGravity = false` — straight-line travel, no arc
    - `isKinematic = false` — velocity-driven
    - `Interpolate = Interpolate`
    - `Collision Detection = Continuous`
  - [ ] Collider: `SphereCollider` with `IsTrigger = true`, radius ~0.25f
  - [ ] Physics layer: use existing `Projectile` layer; configure Layer Collision Matrix so `Projectile` ↔ `Boss` collision is enabled. Fireballs only trigger on Boss — all other contacts are ignored by the tag guard
  - [ ] Create `FireballProjectile.prefab`: `Assets/_Project/Prefabs/Enemies/FireballProjectile.prefab`
    - Mesh: sphere, warm orange-red glowing material (distinct from ice spikes)
    - Scale: (0.3, 0.3, 0.3)
    - Child `ParticleSystem`: short fire trail, 0.15s duration, orange
    - Default: `SetActive(false)`

- [ ] Task 3: Add `Boss` tag to project and boss collider (AC: 4)
  - [ ] In `Edit → Project Settings → Tags and Layers`: add tag `Boss`
  - [ ] Open `Walrus.prefab` or `BossController` GameObject in `biome1_boss.unity`
  - [ ] Add a `BoxCollider` (non-trigger, large — covers boss visual extent) to the Boss GameObject
  - [ ] Tag Boss GameObject: `Boss`
  - [ ] Optionally: create a `Boss` layer and configure Layer Collision Matrix so `Projectile` ↔ `Boss` is enabled, `Projectile` ↔ `Projectile` disabled (already done in Story 4.3)

- [ ] Task 4: Pre-warm `FireballProjectile` pool (AC: 6)
  - [ ] Open `Assets/_Project/Scripts/Core/BootstrapManager.cs`
  - [ ] Add:
    ```csharp
    [SerializeField] private FireballProjectile _fireballProjectilePrefab;
    // In Awake():
    ObjectPool.Prewarm<FireballProjectile>(_fireballProjectilePrefab, count: 5);
    ```
  - [ ] Pool size 5: player can fire rapidly; 5 in-flight at once is generous for a 4-hit requirement
  - [ ] Wire prefab reference in Bootstrap Inspector

- [ ] Task 5: Create `FireballSwipeDetector` MonoBehaviour (AC: 1, 2, 7)
  - [ ] Create `Assets/_Project/Scripts/Core/FireballSwipeDetector.cs` (on Bootstrap or Ball — see note):
    ```csharp
    public class FireballSwipeDetector : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float _minSwipeThreshold = 0.05f; // fraction of Screen.height
        [SerializeField] private float _fireballSpeed     = 15f;

        private IBallStateManager _stateManager;
        private Transform         _ballTransform;
        private Vector2           _touchStartPos;
        private bool              _touchActive;

        private void Start()
        {
            _stateManager  = ServiceLocator.Get<IBallStateManager>();
            _ballTransform = GameObject.FindGameObjectWithTag("Ball")?.transform;
        }

        private void Update()
        {
            // Only active when fire ability is on AND ball is Rolling
            if (!_stateManager.IsFireActive) return;
            if (_stateManager.CurrentState != BallState.Rolling) return;

            ProcessInput();
        }

        private void ProcessInput()
        {
    #if UNITY_EDITOR
            // Mouse fallback for Editor testing
            if (Input.GetMouseButtonDown(0))
            {
                _touchStartPos = Input.mousePosition;
                _touchActive   = true;
            }
            if (Input.GetMouseButtonUp(0) && _touchActive)
            {
                _touchActive = false;
                TryLaunchFireball((Vector2)Input.mousePosition - _touchStartPos);
            }
    #else
            foreach (Touch touch in Input.touches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    _touchStartPos = touch.position;
                    _touchActive   = true;
                }
                if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    && _touchActive)
                {
                    _touchActive = false;
                    TryLaunchFireball(touch.position - _touchStartPos);
                }
            }
    #endif
        }

        private void TryLaunchFireball(Vector2 screenDelta)
        {
            float minPixels = Screen.height * _minSwipeThreshold;
            if (screenDelta.magnitude < minPixels) return; // tap, not swipe — reject

            Vector3 worldDir = ScreenDeltaToWorldDirection(screenDelta);
            LaunchFireball(worldDir);
        }

        private Vector3 ScreenDeltaToWorldDirection(Vector2 screenDelta)
        {
            // Map screen swipe to world direction relative to camera orientation
            Transform cam      = Camera.main.transform;
            Vector3 camRight   = cam.right;   camRight.y = 0f;   camRight.Normalize();
            Vector3 camForward = cam.forward; camForward.y = 0f; camForward.Normalize();

            Vector2 normalised = screenDelta / Screen.height;
            Vector3 worldDir   = camRight * normalised.x + camForward * normalised.y;
            return worldDir.magnitude > 0.001f ? worldDir.normalized : cam.forward;
        }

        private void LaunchFireball(Vector3 direction)
        {
            if (_ballTransform == null) return;

            var projectile = ObjectPool.Get<FireballProjectile>();
            if (projectile == null) return; // pool exhausted — silently skip

            projectile.Launch(
                _ballTransform.position + direction * 0.6f, // spawn slightly ahead of ball
                direction,
                _fireballSpeed,
                onRelease: () => ObjectPool.Release(projectile)
            );
        }
    }
    ```
  - [ ] Add `FireballSwipeDetector` to the Bootstrap GameObject (persists across all scenes; only activates when conditions are met)
  - [ ] The Update guard `if (!_stateManager.IsFireActive) return` and `if (CurrentState != Rolling) return` ensure this only fires during the boss fight when the player has fire and is rolling

- [ ] Task 6: Verify mutual exclusivity with `SwipeDetector` (AC: 2)
  - [ ] Open `Assets/_Project/Scripts/Core/SwipeDetector.cs` (Story 1.4)
  - [ ] Confirm it only processes input when `CurrentState == BallState.Stunned`:
    ```csharp
    // SwipeDetector.Update() should have:
    if (_stateManager.CurrentState != BallState.Stunned) return;
    ```
  - [ ] `FireballSwipeDetector` requires `CurrentState == BallState.Rolling`
  - [ ] These conditions are mutually exclusive — no explicit coordination needed. Document this in both classes with a comment:
    ```csharp
    // NOTE: Mutually exclusive with FireballSwipeDetector (Rolling state).
    // SwipeDetector only processes Stunned; FireballSwipeDetector only processes Rolling.
    ```

- [ ] Task 7: Play Mode test checklist (AC: 1–7)
  - [ ] Boss level loaded, fire ability collected → swipe screen while Rolling → fireball launches from ball position
  - [ ] Short tap (< threshold) → no fireball launched
  - [ ] Fireball travels in swipe direction, passes through obstacles, hits boss → `RegisterHit()` called → Phase 2 starts
  - [ ] 4 fireball hits → boss defeat sequence, `OnLevelCompleted` fires, rank reveal appears
  - [ ] Ball becomes Stunned (hit a stun obstacle mid-boss) → swipe-up for recovery works; NO fireball launches during stun
  - [ ] Fire expires mid-boss → `FireballSwipeDetector` stops processing immediately
  - [ ] Fireball misses boss → auto-releases after `_maxLifetime` seconds
  - [ ] Rapid 5-finger swipe spam → pool handles gracefully (returns null, skips launch)
  - [ ] Verify Profiler: no `Instantiate` during fireball launches

## Dev Notes

### Mutual Exclusivity — State-Based, Not Explicit Locking

```
SwipeDetector:        only active when CurrentState == Stunned
FireballSwipeDetector: only active when IsFireActive && CurrentState == Rolling
```

The ball cannot be simultaneously Stunned AND Rolling. The two detectors self-separate based on state without any shared flag or lock:

```csharp
// SwipeDetector.Update()
if (_stateManager.CurrentState != BallState.Stunned) return; // → ignores Rolling

// FireballSwipeDetector.Update()
if (_stateManager.CurrentState != BallState.Rolling) return; // → ignores Stunned
```

This is simpler and more robust than a shared mutex. Adding a third gesture type follows the same pattern — add a new component, add the state guard.

### `ScreenDeltaToWorldDirection` — Camera-Relative Mapping

```csharp
Vector3 camRight   = cam.right;   camRight.y = 0f;   camRight.Normalize();
Vector3 camForward = cam.forward; camForward.y = 0f; camForward.Normalize();
Vector3 worldDir   = camRight * normalised.x + camForward * normalised.y;
```

Flattening Y from `cam.right` and `cam.forward` projects the camera's orientation onto the XZ plane. This makes swipe direction feel natural regardless of camera pitch:
- Swipe right → ball launches to the camera's right in world space
- Swipe up → ball launches away from camera in world space (toward boss)

If the camera is directly overhead (top-down), this mapping is 1:1 with screen space. If angled, it correctly compensates.

Without camera-relative mapping, a swipe up always maps to world +Z — which is wrong if the camera is rotated.

### Spawn Offset — `direction * 0.6f`

```csharp
projectile.Launch(
    _ballTransform.position + direction * 0.6f, // ← 0.6f ahead of ball centre
    direction, ...
)
```

Spawning exactly at ball centre can cause the fireball to immediately trigger the ball's own trigger colliders (if any) or clip through the ball mesh. The 0.6f offset spawns it just ahead of the ball in the launch direction, avoiding self-collision.

### `FireballProjectile` Ignores All Colliders Except `Boss`

The `OnTriggerEnter` guard:
```csharp
if (!other.CompareTag("Boss")) return;
```

Means fireballs pass THROUGH ice spikes, obstacles, and enemies. This is intentional for the boss fight — the player's fireballs cut through everything and only count against the boss. This also prevents frustrating mid-flight interceptions.

If the designer wants fireballs blocked by walls in future, add wall tag checks and `Release()` calls.

### Fireball Hits Boss — No Visual Feedback in This Story

`boss.RegisterHit()` triggers `_animator?.SetTrigger("Hit")` in `BossController`. The hit reaction animation (flash, recoil) is driven by the Boss Animator. For MVP, a placeholder animation (static pose) is fine — visual polish is out of scope.

The important functional test: 4 hits → boss defeated → `OnLevelCompleted` → rank reveal screen (NOT "Great start!" — boss level doesn't contain "tutorial" in its levelId).

### `Camera.main` — Cache Reference

`ScreenDeltaToWorldDirection` calls `Camera.main` on every invocation. `Camera.main` uses `FindGameObjectWithTag` internally — moderate performance cost. For a gesture that fires at most a few times per second, this is acceptable for MVP.

If profiling shows it's a bottleneck, cache in `Start()`:
```csharp
private Camera _mainCamera;
private void Start() { _mainCamera = Camera.main; }
```

### `_minSwipeThreshold = 0.05f` — Device Size Calculation

```
iPhone SE (4th gen): Screen.height ≈ 1334 px
0.05f × 1334 = 66.7 px ≈ 50pt at 2x resolution

iPhone Pro Max: Screen.height ≈ 2796 px
0.05f × 2796 = 139.8 px ≈ 46pt at 3x resolution
```

Both are above the 44pt minimum touch target. Adjust `_minSwipeThreshold` if playtesting reveals the gesture feels too sensitive or requires too much effort. Values of `0.03f` (more sensitive) to `0.08f` (requires larger swipe) are reasonable.

### Project Structure Notes

- `FireballSwipeDetector.cs` → `Assets/_Project/Scripts/Core/`
- `FireballProjectile.cs` → `Assets/_Project/Scripts/Enemies/`
- `FireballProjectile.prefab` → `Assets/_Project/Prefabs/Enemies/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `TryGet<T>` added here; `FireballSwipeDetector` retrieves `IBallStateManager`
- `ObjectPool<T>` (1.1) — `FireballProjectile` pooled; pre-warmed in BootstrapManager
- `BallState` enum (1.3/1.4) — `Rolling` state guard in `FireballSwipeDetector`
- `IBallStateManager` (1.4/4.1) — `IsFireActive` and `CurrentState` checked each frame
- `SwipeDetector` (1.4) — confirmed mutually exclusive (Stunned vs Rolling state)
- `IBossController` (6.1) — `RegisterHit()` called on fireball hit; `TryGet` guards non-boss levels

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md` (FR8: swipe-to-aim fireball, 4 hits to defeat boss)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-001` (touch input handling)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 6.2`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
