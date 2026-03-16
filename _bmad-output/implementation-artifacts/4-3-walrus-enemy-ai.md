# Story 4.3: Walrus Enemy AI

Status: ready-for-dev

## Story

As a player,
I want to encounter stationary Walrus enemies that fire ice spike projectiles at me,
so that I must dodge ranged attacks while navigating the level — or use fire to clear them.

## Acceptance Criteria

1. **Given** a Walrus is placed in a level **When** the level starts **Then** the Walrus is stationary at its configured position; it begins firing ice spike projectiles at its configured interval from `LevelConfig`
2. **Given** the Walrus fires a projectile **When** the fire interval triggers **Then** a pooled `IceSpikeProjectile` is launched toward the ball's current position; it travels until it hits the ball, a surface, or its lifetime expires
3. **Given** an ice spike projectile hits the ball without fire active **When** the collision registers **Then** death is triggered via `BallStateManager.TryTransitionTo(BallState.Dead)`
4. **Given** an ice spike projectile hits the ball with fire active **When** the collision registers **Then** the projectile is released to pool; the ball continues unharmed
5. **Given** the ball contacts a Walrus directly without fire active **When** the collision registers **Then** the Walrus applies a knock-off impulse; the ball is redirected
6. **Given** the ball contacts a Walrus with fire active (direct or blast wave) **When** the collision registers **Then** the Walrus is defeated; `OnEnemyDefeated` SO event fires
7. **Given** ice spike projectiles are spawned **When** `ObjectPool` is inspected **Then** all `IceSpikeProjectile` instances are pooled — no `Instantiate`/`Destroy` during gameplay

## Tasks / Subtasks

- [ ] Task 1: Expand `LevelConfigSO` with Walrus fields (AC: 1)
  - [ ] Open `Assets/_Project/Scripts/Progression/LevelConfigSO.cs` (Story 2.2)
  - [ ] Add under `[Header("Gameplay")]`:
    ```csharp
    [Header("Walrus AI")]
    public float walrusFireInterval    = 2.5f; // seconds between projectile launches
    public float walrusProjectileSpeed = 6f;   // world units per second
    public float walrusKnockOffForce   = 10f;  // direct contact impulse
    ```
  - [ ] Set matching defaults on all existing `LevelConfigSO` assets in Inspector — these fields will default to `0f` in old assets, which would break firing (interval of 0 = infinite fire rate); set them to sensible values manually

- [ ] Task 2: Create `IceSpikeProjectile` pooled MonoBehaviour (AC: 2, 3, 4, 7)
  - [ ] Create `Assets/_Project/Scripts/Enemies/IceSpikeProjectile.cs`:
    ```csharp
    public class IceSpikeProjectile : MonoBehaviour
    {
        [SerializeField] private float _maxLifetime = 6f; // auto-release if no collision

        private Rigidbody          _rigidbody;
        private IBallStateManager  _stateManager;
        private Action             _onRelease;
        private float              _lifeTimer;
        private bool               _released;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            _stateManager = ServiceLocator.Get<IBallStateManager>();
        }

        /// <summary>
        /// Called by WalrusController when getting a projectile from the pool.
        /// </summary>
        public void Launch(Vector3 position, Vector3 direction, float speed, Action onRelease)
        {
            _released          = false;
            _lifeTimer         = 0f;
            _onRelease         = onRelease;
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(direction);
            gameObject.SetActive(true);

            // Zero out any residual velocity from prior launch, then set new
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.linearVelocity = direction.normalized * speed;
        }

        private void Update()
        {
            _lifeTimer += Time.deltaTime;
            if (_lifeTimer >= _maxLifetime)
                Release();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_released) return;

            if (other.CompareTag("Ball"))
            {
                // Cache stateManager reference — null guard for scene tear-down edge case
                if (_stateManager != null && !_stateManager.IsFireActive)
                    _stateManager.TryTransitionTo(BallState.Dead);
                // Ball hit: release projectile regardless of fire state
            }
            // Any trigger contact (ball OR surface/wall) releases the projectile.
            // Projectile layer is configured to NOT collide with other projectiles
            // via Physics Layer Collision Matrix — no same-type collision guard needed.
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

        private void OnDisable()
        {
            // Safety: ensure Release is called if disabled externally (e.g. scene reload)
            _released = true;
        }
    }
    ```
  - [ ] Rigidbody settings on `IceSpikeProjectile`:
    - `useGravity = false` — travels in a straight line, no arc
    - `isKinematic = false` — velocity-driven movement
    - `Interpolate = Interpolate`
    - `Collision Detection = Continuous` — fast projectile must not tunnel through the ball
  - [ ] Collider: `CapsuleCollider` with `IsTrigger = true` — thin capsule aligned to Z-axis, radius ~0.15f
  - [ ] Physics Layer: `Projectile` (create if not present) — configure Layer Collision Matrix so `Projectile` does NOT collide with `Projectile`

- [ ] Task 3: Create `IceSpikeProjectile` prefab (AC: 2, 7)
  - [ ] Create `Assets/_Project/Prefabs/Enemies/IceSpikeProjectile.prefab`:
    - Mesh: thin elongated capsule, icy blue-white material (placeholder)
    - Scale: (0.15, 0.15, 0.8) — narrow and fast-looking
    - Optional: `TrailRenderer` for motion blur effect (short trail, 0.1s duration)
    - Components: `IceSpikeProjectile.cs`, `Rigidbody` (as above), `CapsuleCollider` (trigger)
    - Layer: `Projectile`
    - Default: `SetActive(false)` — pool manages activation

- [ ] Task 4: Pre-warm `IceSpikeProjectile` pool (AC: 7)
  - [ ] Open `Assets/_Project/Scripts/Core/BootstrapManager.cs`
  - [ ] Add pool pre-warm in `Awake()`:
    ```csharp
    [SerializeField] private IceSpikeProjectile _iceSpikeProjectilePrefab;
    // In Awake():
    ObjectPool.Prewarm<IceSpikeProjectile>(_iceSpikeProjectilePrefab, count: 8);
    ```
  - [ ] Pool size 8: supports 3 Walruses each with up to 2-3 in-flight projectiles simultaneously plus margin
  - [ ] Wire `_iceSpikeProjectilePrefab` → `IceSpikeProjectile.prefab` in Bootstrap Inspector

- [ ] Task 5: Create `WalrusController` MonoBehaviour (AC: 1, 2, 5, 6)
  - [ ] Create `Assets/_Project/Scripts/Enemies/WalrusController.cs`:
    ```csharp
    public class WalrusController : MonoBehaviour, IDefeatable
    {
        [Header("Combat")]
        [SerializeField] private Transform   _projectileSpawnPoint; // empty child Transform
        [SerializeField] private float       _knockOffForce = 10f;  // direct contact fallback

        [Header("Events")]
        [SerializeField] private GameEventSO _onEnemyDefeated;

        private IBallStateManager _stateManager;
        private Transform         _ballTransform;
        private LevelConfigSO     _levelConfig;
        private bool              _defeated;

        private void Start()
        {
            _stateManager = ServiceLocator.Get<IBallStateManager>();
            _levelConfig  = ServiceLocator.Get<ILevelManager>().CurrentLevel;
            _ballTransform = GameObject.FindGameObjectWithTag("Ball")?.transform;

            StartCoroutine(FireCoroutine());
        }

        private void OnEnable()
        {
            // Pool reuse reset
            _defeated = false;
        }

        // ─── Firing ───────────────────────────────────────────────────────────

        private IEnumerator FireCoroutine()
        {
            float interval = _levelConfig != null ? _levelConfig.walrusFireInterval : 2.5f;
            // Initial delay: stagger multiple walruses slightly to avoid sync
            yield return new WaitForSeconds(Random.Range(0f, interval * 0.5f));

            while (!_defeated)
            {
                yield return new WaitForSeconds(interval);
                if (_defeated || _ballTransform == null) continue;
                FireProjectile();
            }
        }

        private void FireProjectile()
        {
            var projectile = ObjectPool.Get<IceSpikeProjectile>();
            if (projectile == null) return; // pool exhausted — skip this shot

            Vector3 spawnPos  = _projectileSpawnPoint != null
                ? _projectileSpawnPoint.position
                : transform.position + transform.forward;

            Vector3 direction = (_ballTransform.position - spawnPos).normalized;
            float   speed     = _levelConfig != null ? _levelConfig.walrusProjectileSpeed : 6f;

            projectile.Launch(spawnPos, direction, speed,
                onRelease: () => ObjectPool.Release(projectile));
        }

        // ─── Collision ────────────────────────────────────────────────────────

        private void OnCollisionEnter(Collision col)
        {
            if (_defeated) return;
            if (!col.gameObject.CompareTag("Ball")) return;

            if (_stateManager.IsFireActive)
            {
                Defeat();
                return;
            }

            // Direct contact without fire: knock ball away
            var rb = col.gameObject.GetComponent<Rigidbody>();
            if (rb == null) return;

            float force = _levelConfig != null ? _levelConfig.walrusKnockOffForce : 10f;
            Vector3 knockDir = (col.transform.position - transform.position).normalized;
            knockDir.y = Mathf.Max(knockDir.y, 0.2f);
            knockDir.Normalize();

            rb.AddForce(knockDir * force, ForceMode.Impulse);
        }

        // ─── IDefeatable ──────────────────────────────────────────────────────

        public void Defeat()
        {
            if (_defeated) return;
            _defeated = true;

            StopAllCoroutines();
            _onEnemyDefeated.Raise();

            // Stop any currently flying projectiles? Not needed — they auto-release on hit or lifetime
            StartCoroutine(DefeatSequence());
        }

        private IEnumerator DefeatSequence()
        {
            // Placeholder: brief pause before hiding
            // Story 5.1 will add SFX here via IAudioService
            yield return new WaitForSeconds(0.5f);
            gameObject.SetActive(false);
        }
    }
    ```
  - [ ] Add `WalrusController` to the Walrus prefab
  - [ ] Wire `[SerializeField]` references in Inspector

- [ ] Task 6: Create Walrus prefab (AC: 1, 5, 6)
  - [ ] Create `Assets/_Project/Prefabs/Enemies/Walrus.prefab`:
    - Mesh: cube placeholder (wider, stockier than Seal), blue-grey material
    - Scale: ~(1.2, 1.0, 1.0)
    - Child: empty `ProjectileSpawnPoint` Transform at front-face position (for projectile spawn offset)
    - Components: `WalrusController`, `Rigidbody` (isKinematic = true), `BoxCollider` (non-trigger)
    - Tag: `Enemy`, Layer: `Enemy`
  - [ ] Rigidbody: `isKinematic = true` — Walrus is fully stationary; receives collision callbacks
  - [ ] Wire `_projectileSpawnPoint` → `ProjectileSpawnPoint` child Transform
  - [ ] Wire `_onEnemyDefeated` → `OnEnemyDefeated.asset`

- [ ] Task 7: Place Walrus in test scenes (AC: 1–7)
  - [ ] Place one `Walrus.prefab` instance in `biome1_tutorial.unity` at a reachable-but-dangerous position
  - [ ] Place one in `biome1_level01.unity`
  - [ ] Play Mode test checklist:
    - [ ] Walrus fires projectile toward ball every `walrusFireInterval` seconds
    - [ ] Multiple Walruses in the same scene stagger their fire (random initial offset)
    - [ ] Projectile hits ball (fire off) → death triggered
    - [ ] Projectile hits ball (fire on) → projectile released, ball continues
    - [ ] Projectile hits wall → projectile released to pool
    - [ ] Projectile lifetime expires without hitting anything → auto-released
    - [ ] Ball touches Walrus (fire off) → knock-off impulse applied
    - [ ] Ball touches Walrus (fire on) → Walrus defeated, `OnEnemyDefeated` fires
    - [ ] Fire blast wave near Walrus → `IDefeatable.Defeat()` called
    - [ ] Verify pool: check profiler Allocations — no `Instantiate` calls during firing
    - [ ] Retry level → Walrus resets, fires again from start

## Dev Notes

### `IceSpikeProjectile` vs Static `IceSpike` — Two Separate Classes

These are distinct and must NOT be merged:

| Type | Class | Pooling | Movement | Death path |
|------|-------|---------|----------|------------|
| Static obstacle | `IceSpike.cs` (Story 2.3) | `Destroy()` | Stationary | Direct ball hit |
| Walrus projectile | `IceSpikeProjectile.cs` (this story) | `ObjectPool` | Velocity-driven | Trigger on ball |

`IceSpike` is placed by the level designer and stays in the scene. `IceSpikeProjectile` is a runtime pool object launched by Walrus. Merging them would create unnecessary complexity.

### `_released` Bool Guard — Critical for Pool Safety

```csharp
private void OnTriggerEnter(Collider other)
{
    if (_released) return; // ← without this, fast objects can trigger twice
    // ...
    Release();
}
```

A fast projectile can overlap multiple colliders in a single physics step, causing `OnTriggerEnter` to fire twice before `SetActive(false)` propagates. Without the `_released` guard, `ObjectPool.Release()` is called twice — which can corrupt the pool state. The guard makes `Release()` idempotent.

### Projectile Physics Layer — No Self-Collision

Configure `Edit → Project Settings → Physics → Layer Collision Matrix`:
- `Projectile` layer: disable self-collision (uncheck `Projectile × Projectile`)
- `Projectile` layer: enable collision with `Default`, `Ball`, `Ground`, `Obstacle`

This prevents two in-flight ice spikes from triggering each other and being prematurely released.

### Staggered Fire Interval — Avoiding Synchronisation

```csharp
// Initial random delay prevents all Walruses firing in perfect unison
yield return new WaitForSeconds(Random.Range(0f, interval * 0.5f));
```

Without this, multiple Walruses placed in the same level all fire simultaneously — a predictable rhythm that's trivially easy to dodge. The stagger makes patterns less regular and more threatening.

### `_ballTransform` Null Check — Scene Edge Cases

```csharp
_ballTransform = GameObject.FindGameObjectWithTag("Ball")?.transform;
```

`FindGameObjectWithTag` returns null if:
- Scene hasn't fully loaded yet (unlikely — `Start()` fires after scene load)
- Ball was destroyed (doesn't happen — ball uses state machine)
- "Ball" tag isn't assigned (would be a setup error from Story 1.3)

The `?.transform` null-conditional prevents a `NullReferenceException`. The `if (_ballTransform == null) continue;` in `FireCoroutine` gracefully skips shots if null (rather than crashing). Add a `Debug.LogWarning` if null is detected.

### `LevelConfig` Null Guard in `WalrusController`

`_levelConfig` comes from `ServiceLocator.Get<ILevelManager>().CurrentLevel` in `Start()`. If `CurrentLevel` is null (e.g. during Editor tests without a proper level loaded), fire interval defaults to `2.5f` and speed defaults to `6f` via the `?? fallback` pattern:

```csharp
float interval = _levelConfig != null ? _levelConfig.walrusFireInterval : 2.5f;
```

### `WaitForSeconds` — Scales with `Time.timeScale`

The fire coroutine uses `WaitForSeconds` — it pauses when `Time.timeScale = 0` (pause menu). Walruses do not fire while the game is paused. This is correct behaviour.

If the designer wants Walruses to fire during pause (unlikely), switch to `WaitForSecondsRealtime`. For MVP, `WaitForSeconds` is correct.

### Pool Size 8 — Sizing Rationale

```
3 Walruses × 2.5s interval × 6f speed × ~3s flight time ≈ 3-4 projectiles per Walrus in flight
Worst case: all 3 fire simultaneously → 3 projectiles launched at once
Pool size 8 = comfortable headroom (3 in flight + 5 buffer)
```

If a pool Get() returns null (`pool exhausted`), `FireProjectile()` silently skips the shot. This is the correct graceful degradation — a missed shot is barely noticeable, but a crash is unacceptable.

### `ObjectPool.Release` Lambda — Closure Safety

```csharp
projectile.Launch(spawnPos, direction, speed,
    onRelease: () => ObjectPool.Release(projectile));
```

The `projectile` local variable is captured by the lambda. Since `projectile` is a local in `FireProjectile()` (not a field), each invocation creates a fresh closure capturing its own instance. This is safe — no shared state issues between concurrent in-flight projectiles.

### Direct Contact Knock-Off — Walrus vs Seal Comparison

Walrus direct-contact knock-off (`walrusKnockOffForce = 10f`) is slightly weaker than Seal (`_knockOffForce = 14f`). Rationale: Walrus is stationary — it doesn't have the kinetic energy of a patrolling Seal. It's primarily a ranged threat. The knock-off on direct contact is a secondary "don't walk into it" consequence.

### Project Structure Notes

- `WalrusController.cs` → `Assets/_Project/Scripts/Enemies/`
- `IceSpikeProjectile.cs` → `Assets/_Project/Scripts/Enemies/`
- `Walrus.prefab` → `Assets/_Project/Prefabs/Enemies/`
- `IceSpikeProjectile.prefab` → `Assets/_Project/Prefabs/Enemies/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `WalrusController.Start()` retrieves services; `IceSpikeProjectile.Start()` retrieves `IBallStateManager`
- `ObjectPool<T>` (1.1) — `IceSpikeProjectile` pooled; `Prewarm` added to `BootstrapManager`
- `GameEventSO` (1.3) — `OnEnemyDefeated` raised on Walrus defeat
- `BallState` enum (1.3/1.4) — `TryTransitionTo(BallState.Dead)` on projectile hit
- `IBallStateManager` (1.4/4.1) — `IsFireActive`, `TryTransitionTo`
- `LevelConfigSO` (2.2) — `walrusFireInterval`, `walrusProjectileSpeed`, `walrusKnockOffForce` added here
- `ILevelManager` (2.2) — `CurrentLevel` retrieved for config values
- `OnEnemyDefeated.asset` (3.2) — wired to `_onEnemyDefeated` in Inspector
- `IDefeatable` (4.1) — implemented by `WalrusController`
- `FireBlastWave` (4.1) — calls `IDefeatable.Defeat()` on Walrus within blast radius

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (object pooling for projectiles)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-005` (enemy classification, static vs pooled)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 4.3`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
