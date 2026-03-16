# Story 6.1: Boss Controller & Phase System

Status: ready-for-dev

## Story

As a player,
I want to face a challenging multi-phase boss that escalates in difficulty,
so that the final battle feels like a climactic test of everything I've learned.

## Acceptance Criteria

1. **Given** the player has 4+ golden balls and enters the boss level **When** it loads **Then** `BossController` initialises in Phase 1; phase data (intervals, speeds, patterns) is loaded from `LevelConfig`
2. **Given** Phase 1 is active **When** the level starts **Then** the boss fires at a slow, readable rate; the golden ball is accessible (only during Phase 1)
3. **Given** the player lands a fireball hit **When** `IBossController.RegisterHit()` is called **Then** `BossController` transitions to the next phase; speed, rate, and pattern complexity increase; golden ball is removed after Phase 1
4. **Given** Phase 4 is active **When** the boss attacks **Then** ice spikes, exploding ice blocks, and fire power-up throws all occur simultaneously at the fastest configured rate
5. **Given** the boss throws an exploding ice block **When** it lands **Then** it flashes for 0.5s then `Physics.OverlapSphere` stuns the ball if in range
6. **Given** the boss throws a fire power-up **When** it is thrown **Then** a wind-up animation plays; the power-up has a 2–3s collection window before it disappears; collecting it activates the fire ability
7. **Given** the ball dies during the boss level **When** death is confirmed **Then** standard 5-lives retry applies (Story 2.5); boss phase resets to Phase 1 (scene reload)

## Tasks / Subtasks

- [ ] Task 1: Add `BossPhaseConfig` and phase array to `LevelConfigSO` (AC: 1, 2, 4)
  - [ ] Create `Assets/_Project/Scripts/Progression/BossPhaseConfig.cs`:
    ```csharp
    [System.Serializable]
    public class BossPhaseConfig
    {
        [Header("Ice Spike Attack")]
        public float iceSpikeInterval  = 2.5f; // seconds between spikes
        public float iceSpikeSpeed     = 6f;

        [Header("Exploding Block Attack")]
        public bool  throwsExplodingBlocks      = false;
        public float explodingBlockInterval     = 4f;
        public float explodingBlockBlastRadius  = 3f;

        [Header("Fire Pickup Attack")]
        public bool  throwsFirePickup      = false;
        public float firePickupInterval    = 8f;
        public float firePickupWindowSecs  = 2.5f; // how long pickup is collectible
    }
    ```
  - [ ] Open `Assets/_Project/Scripts/Progression/LevelConfigSO.cs`
  - [ ] Replace the empty boss header with:
    ```csharp
    [Header("Boss Phases (4 entries — index 0 = Phase 1)")]
    public BossPhaseConfig[] bossPhases = new BossPhaseConfig[4];
    ```
  - [ ] Open `Assets/_Project/ScriptableObjects/LevelConfigs/biome1_boss.asset` in Inspector
  - [ ] Populate `bossPhases[0..3]` with escalating values:

    | Phase | iceSpikeInterval | throwsBlocks | throwsFirePickup |
    |-------|-----------------|--------------|-----------------|
    | 1 | 3.0s | false | false |
    | 2 | 2.0s | true (4s) | false |
    | 3 | 1.5s | true (3s) | true (8s) |
    | 4 | 0.8s | true (2s) | true (5s) |

- [ ] Task 2: Create `IBossController` interface (AC: 3)
  - [ ] Create `Assets/_Project/Scripts/Core/IBossController.cs`:
    ```csharp
    public interface IBossController
    {
        int  CurrentPhase { get; }
        bool IsDefeated   { get; }
        void RegisterHit(); // called by FireballProjectile (Story 6.2)
    }
    ```

- [ ] Task 3: Create `BossController` MonoBehaviour (AC: 1–4, 7)
  - [ ] Create `Assets/_Project/Scripts/Enemies/BossController.cs`:
    ```csharp
    public class BossController : MonoBehaviour, IBossController
    {
        [Header("References")]
        [SerializeField] private Transform    _projectileSpawnPoint;
        [SerializeField] private Transform    _blockSpawnPoint;
        [SerializeField] private Transform    _firePickupSpawnPoint;
        [SerializeField] private GoldenBall   _goldenBall;   // Phase 1 only
        [SerializeField] private Animator     _animator;
        [SerializeField] private GameEventSO  _onLevelCompleted;

        private IBallStateManager _stateManager;
        private Transform         _ballTransform;
        private BossPhaseConfig[] _phases;

        private int  _hitCount;
        private int  _currentPhaseIndex; // 0-based
        private bool _defeated;

        private Coroutine _iceSpikeCoroutine;
        private Coroutine _blockCoroutine;
        private Coroutine _firePickupCoroutine;

        public int  CurrentPhase => _currentPhaseIndex + 1; // 1-based for readability
        public bool IsDefeated   => _defeated;

        private void Awake()
        {
            ServiceLocator.Register<IBossController>(this);
        }

        private void Start()
        {
            _stateManager  = ServiceLocator.Get<IBallStateManager>();
            _ballTransform = GameObject.FindGameObjectWithTag("Ball")?.transform;

            var config = ServiceLocator.Get<ILevelManager>().CurrentLevel;
            _phases = config?.bossPhases;

            if (_phases == null || _phases.Length == 0)
            {
                Debug.LogError("BossController: no bossPhases configured in LevelConfigSO");
                return;
            }

            EnterPhase(0);
        }

        // ─── Phase Management ─────────────────────────────────────────────────

        private void EnterPhase(int phaseIndex)
        {
            StopAllAttackCoroutines();
            _currentPhaseIndex = Mathf.Clamp(phaseIndex, 0, _phases.Length - 1);

            var phase = _phases[_currentPhaseIndex];

            // Remove golden ball access after Phase 1
            if (_currentPhaseIndex >= 1 && _goldenBall != null)
            {
                var save    = ServiceLocator.Get<ISaveService>();
                var levelId = ServiceLocator.Get<ILevelManager>().CurrentLevel.levelId;
                if (!save.GetLevelData(levelId).goldenBallCollected)
                    _goldenBall.gameObject.SetActive(false); // not collected — remove it
            }

            _iceSpikeCoroutine = StartCoroutine(IceSpikeLoop(phase));

            if (phase.throwsExplodingBlocks)
                _blockCoroutine = StartCoroutine(ExplodingBlockLoop(phase));

            if (phase.throwsFirePickup)
                _firePickupCoroutine = StartCoroutine(FirePickupLoop(phase));

            _animator?.SetInteger("Phase", CurrentPhase);
            Debug.Log($"BossController: entering Phase {CurrentPhase}");
        }

        // ─── IBossController ──────────────────────────────────────────────────

        public void RegisterHit()
        {
            if (_defeated) return;

            _hitCount++;
            _animator?.SetTrigger("Hit");

            if (_hitCount >= 4)
            {
                Defeat();
                return;
            }

            // Transition to next phase (cap at last phase)
            int nextPhase = Mathf.Min(_currentPhaseIndex + 1, _phases.Length - 1);
            EnterPhase(nextPhase);
        }

        private void Defeat()
        {
            _defeated = true;
            StopAllAttackCoroutines();
            _animator?.SetTrigger("Defeat");
            StartCoroutine(DefeatSequence());
        }

        private IEnumerator DefeatSequence()
        {
            yield return new WaitForSeconds(2f); // defeat animation plays
            _onLevelCompleted.Raise();           // triggers save + rank reveal
        }

        // ─── Attack Loops ─────────────────────────────────────────────────────

        private IEnumerator IceSpikeLoop(BossPhaseConfig phase)
        {
            yield return new WaitForSeconds(phase.iceSpikeInterval * 0.5f); // stagger start
            while (!_defeated)
            {
                FireIceSpike(phase.iceSpikeSpeed);
                yield return new WaitForSeconds(phase.iceSpikeInterval);
            }
        }

        private IEnumerator ExplodingBlockLoop(BossPhaseConfig phase)
        {
            yield return new WaitForSeconds(phase.explodingBlockInterval * 0.3f);
            while (!_defeated)
            {
                ThrowExplodingBlock(phase.explodingBlockBlastRadius);
                yield return new WaitForSeconds(phase.explodingBlockInterval);
            }
        }

        private IEnumerator FirePickupLoop(BossPhaseConfig phase)
        {
            yield return new WaitForSeconds(phase.firePickupInterval * 0.7f);
            while (!_defeated)
            {
                ThrowFirePickup(phase.firePickupWindowSecs);
                yield return new WaitForSeconds(phase.firePickupInterval);
            }
        }

        private void StopAllAttackCoroutines()
        {
            if (_iceSpikeCoroutine   != null) StopCoroutine(_iceSpikeCoroutine);
            if (_blockCoroutine      != null) StopCoroutine(_blockCoroutine);
            if (_firePickupCoroutine != null) StopCoroutine(_firePickupCoroutine);
        }

        // ─── Attack Spawners ──────────────────────────────────────────────────

        private void FireIceSpike(float speed)
        {
            if (_ballTransform == null) return;
            var projectile = ObjectPool.Get<IceSpikeProjectile>();
            if (projectile == null) return;

            Vector3 spawnPos  = _projectileSpawnPoint.position;
            Vector3 direction = (_ballTransform.position - spawnPos).normalized;

            projectile.Launch(spawnPos, direction, speed,
                onRelease: () => ObjectPool.Release(projectile));
        }

        private void ThrowExplodingBlock(float blastRadius)
        {
            if (_ballTransform == null) return;
            var block = ObjectPool.Get<ExplodingIceBlock>();
            if (block == null) return;

            // Aim for a point near the ball's current position
            Vector3 targetPos = _ballTransform.position;
            block.Throw(_blockSpawnPoint.position, targetPos, blastRadius,
                onRelease: () => ObjectPool.Release(block));
        }

        private void ThrowFirePickup(float windowSecs)
        {
            if (_ballTransform == null) return;
            var pickup = ObjectPool.Get<ThrownFirePickup>();
            if (pickup == null) return;

            Vector3 targetPos = _ballTransform.position + Random.insideUnitSphere * 1.5f;
            targetPos.y = _ballTransform.position.y; // stay on platform plane

            _animator?.SetTrigger("WindUp");
            pickup.Throw(_firePickupSpawnPoint.position, targetPos, windowSecs,
                onRelease: () => ObjectPool.Release(pickup));
        }
    }
    ```

- [ ] Task 4: Create `ExplodingIceBlock` pooled MonoBehaviour (AC: 5)
  - [ ] Create `Assets/_Project/Scripts/Enemies/ExplodingIceBlock.cs`:
    ```csharp
    public class ExplodingIceBlock : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private Color        _flashColour = Color.white;

        private Rigidbody  _rigidbody;
        private float      _blastRadius;
        private Action     _onRelease;
        private bool       _landed;

        private void Awake() => _rigidbody = GetComponent<Rigidbody>();

        public void Throw(Vector3 from, Vector3 target, float blastRadius, Action onRelease)
        {
            _blastRadius = blastRadius;
            _onRelease   = onRelease;
            _landed      = false;

            transform.position        = from;
            gameObject.SetActive(true);
            _rigidbody.linearVelocity = Vector3.zero;

            // Calculate arc velocity to reach target
            _rigidbody.linearVelocity = CalculateArcVelocity(from, target, arcHeight: 4f);
        }

        private Vector3 CalculateArcVelocity(Vector3 from, Vector3 to, float arcHeight)
        {
            // Simple arc: split into horizontal and vertical components
            Vector3 horizontal = to - from;
            horizontal.y = 0f;
            float dist   = horizontal.magnitude;
            float vx     = dist / 1.2f; // approx 1.2s flight time

            float vy = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * arcHeight);
            Vector3 dir = horizontal.normalized;
            return dir * vx + Vector3.up * vy;
        }

        private void OnCollisionEnter(Collision col)
        {
            if (_landed) return;
            if (!col.gameObject.CompareTag("Ground")) return;

            _landed = true;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.isKinematic    = true;
            StartCoroutine(FlashAndDetonate());
        }

        private IEnumerator FlashAndDetonate()
        {
            // 0.5s warning flash
            Color baseColour = _renderer.material.color;
            float elapsed    = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.PingPong(elapsed * 8f, 1f);
                _renderer.material.color = Color.Lerp(baseColour, _flashColour, t);
                yield return null;
            }

            Detonate();
        }

        private void Detonate()
        {
            // Stun ball if in blast radius
            Collider[] hits = Physics.OverlapSphere(transform.position, _blastRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Ball"))
                {
                    ServiceLocator.Get<IBallStateManager>().TryTransitionTo(BallState.Stunned);
                    break;
                }
            }

            _rigidbody.isKinematic = false;
            gameObject.SetActive(false);
            _onRelease?.Invoke();
        }

        private void OnDisable() => _landed = true; // safety reset on pool release
    }
    ```
  - [ ] Rigidbody: `useGravity = true`, `isKinematic = false`, `Interpolate = Interpolate`
  - [ ] Collider: `BoxCollider`, non-trigger (needs `OnCollisionEnter` with Ground)
  - [ ] Create `ExplodingIceBlock.prefab`: `Assets/_Project/Prefabs/Enemies/ExplodingIceBlock.prefab`
    - Mesh: flat cube, icy blue material
    - Scale: (1.2, 0.6, 1.2)
  - [ ] Pre-warm pool in `BootstrapManager.Awake()`: `ObjectPool.Prewarm<ExplodingIceBlock>(prefab, 4)`

- [ ] Task 5: Create `ThrownFirePickup` pooled MonoBehaviour (AC: 6)
  - [ ] Create `Assets/_Project/Scripts/Enemies/ThrownFirePickup.cs`:
    ```csharp
    public class ThrownFirePickup : MonoBehaviour
    {
        [SerializeField] private GameEventSO _onFireAbilityActivated;

        private Action     _onRelease;
        private float      _windowSecs;
        private bool       _collected;
        private bool       _released;
        private Coroutine  _windowCoroutine;

        public void Throw(Vector3 from, Vector3 target, float windowSecs, Action onRelease)
        {
            _onRelease  = onRelease;
            _windowSecs = windowSecs;
            _collected  = false;
            _released   = false;

            transform.position  = from;
            gameObject.SetActive(true);

            if (_windowCoroutine != null) StopCoroutine(_windowCoroutine);
            _windowCoroutine = StartCoroutine(ArcAndWait(from, target));
        }

        private IEnumerator ArcAndWait(Vector3 from, Vector3 target)
        {
            // Arc to target position over 0.8s
            float elapsed = 0f;
            float duration = 0.8f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Parabolic arc: lerp position + add arc height
                Vector3 linear    = Vector3.Lerp(from, target, t);
                float   arcOffset = 4f * t * (1f - t) * 2.5f; // peak at midpoint
                transform.position = linear + Vector3.up * arcOffset;
                yield return null;
            }
            transform.position = target;

            // Collection window
            yield return new WaitForSeconds(_windowSecs);

            if (!_collected) Release(); // window expired — disappear
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected || _released) return;
            if (!other.CompareTag("Ball")) return;

            _collected = true;
            _onFireAbilityActivated.Raise();
            Release();
        }

        private void Release()
        {
            if (_released) return;
            _released = true;
            if (_windowCoroutine != null) StopCoroutine(_windowCoroutine);
            gameObject.SetActive(false);
            _onRelease?.Invoke();
        }

        private void OnDisable() => _released = true;
    }
    ```
  - [ ] Add `SphereCollider` with `IsTrigger = true` (radius ~0.6f — generous collection window)
  - [ ] Wire `_onFireAbilityActivated` → `OnFireAbilityActivated.asset` in Inspector
  - [ ] Create `ThrownFirePickup.prefab`: `Assets/_Project/Prefabs/Enemies/ThrownFirePickup.prefab`
    - Mesh: sphere, warm orange glowing material
    - Particle system child: fire trail during arc, stays active only while moving
  - [ ] Pre-warm pool: `ObjectPool.Prewarm<ThrownFirePickup>(prefab, 2)`

- [ ] Task 6: Build `biome1_boss.unity` scene (AC: 1–7)
  - [ ] Open `Assets/_Project/Scenes/Levels/biome1_boss.unity` (placeholder from Story 2.2)
  - [ ] Build minimal boss arena:
    - Circular or square platform (~15×15 world units)
    - Boss GameObject at far end of arena, facing player start
    - Ball spawns at near end (central)
    - Kill planes on all sides and below
  - [ ] `BossController` hierarchy:
    ```
    BossEnemy (BossController component)
      ProjectileSpawnPoint  (empty Transform — front-facing, at "chest" height)
      BlockSpawnPoint       (empty Transform — overhead position for blocks)
      FirePickupSpawnPoint  (empty Transform — same as or near BlockSpawnPoint)
      BossModel             (placeholder mesh + Animator)
    ```
  - [ ] `GoldenBall` prefab instance placed on a side platform reachable in Phase 1
  - [ ] Wire `BossController` Inspector references:
    - `_goldenBall` → scene `GoldenBall` instance
    - `_onLevelCompleted` → `OnLevelCompleted.asset`
    - All spawn point Transforms
  - [ ] Do NOT place a `LevelCompleteZone` trigger — boss defeat raises `OnLevelCompleted` directly (Task 3's `DefeatSequence`)

- [ ] Task 7: Pre-warm all boss pools in `BootstrapManager` (AC: 4, 5, 6)
  - [ ] Open `Assets/_Project/Scripts/Core/BootstrapManager.cs`
  - [ ] Add pool pre-warms (alongside existing pools from Stories 4.1, 4.3):
    ```csharp
    [SerializeField] private ExplodingIceBlock  _explodingIceBlockPrefab;
    [SerializeField] private ThrownFirePickup   _thrownFirePickupPrefab;
    // In Awake():
    ObjectPool.Prewarm<ExplodingIceBlock>(_explodingIceBlockPrefab, 4);
    ObjectPool.Prewarm<ThrownFirePickup>(_thrownFirePickupPrefab, 2);
    ```
  - [ ] Wire prefab references in Bootstrap Inspector

- [ ] Task 8: Play Mode test checklist (AC: 1–7)
  - [ ] Boss level loads → Phase 1 active, ice spikes fire slowly, golden ball visible
  - [ ] Simulate `RegisterHit()` call → Phase 2 transitions, fire rate increases, blocks start
  - [ ] After 4th `RegisterHit()` → defeat sequence plays, `OnLevelCompleted` fires, rank reveal screen shown (not "Great start!" — boss is not tutorial)
  - [ ] Exploding block lands → 0.5s flash → stun triggers if ball in radius
  - [ ] Boss throws fire pickup (Phase 3+) → arc animation → collection window → ball picks up → `OnFireAbilityActivated` fires
  - [ ] Fire pickup window expires uncollected → pickup disappears, no crash
  - [ ] Ball dies → retry with standard 5-lives flow; boss resets to Phase 1 (scene reload)
  - [ ] Phase 1 → Phase 2 transition → golden ball removed if not yet collected
  - [ ] Level accessible only with 4+ golden balls (enforced by `LevelSelectController` from Story 2.4)

## Dev Notes

### `BossController` Raises `OnLevelCompleted` Directly

Unlike regular levels where a `LevelCompleteZone` trigger raises `OnLevelCompleted`, the boss level completion is triggered programmatically by `BossController.DefeatSequence()`:

```csharp
_onLevelCompleted.Raise(); // fires after 2s defeat animation
```

This is intentional — boss defeat is a scripted sequence, not a spatial trigger. The SO event drives the same save + rank reveal flow as regular level completion. No `LevelCompleteZone` is placed in the boss arena.

### Phase Transitions — `StopAllAttackCoroutines` First

```csharp
private void EnterPhase(int phaseIndex)
{
    StopAllAttackCoroutines(); // ← CRITICAL — must happen before starting new coroutines
    // ...
    _iceSpikeCoroutine = StartCoroutine(IceSpikeLoop(phase));
}
```

Without stopping existing coroutines first, each `RegisterHit()` call would ADD new attack loops on top of the old ones. By Phase 4, there would be 4× the attack rate. The stop-then-start pattern ensures exactly one set of attack loops runs per phase.

### Exploding Block Arc — `CalculateArcVelocity`

```csharp
private Vector3 CalculateArcVelocity(Vector3 from, Vector3 to, float arcHeight)
```

This is an approximation — it doesn't precisely control arc height under all conditions. For MVP accuracy is not critical; the block needs to visually arc from boss to ball area and land somewhere near the ball. Tune `arcHeight` and flight time based on playtest feel. If precision is needed, use a proper projectile motion formula.

### `ExplodingIceBlock` — `isKinematic` Toggle

```csharp
// On landing:
_rigidbody.isKinematic = true;  // freeze in place during flash
// After detonation:
_rigidbody.isKinematic = false; // re-enable for pool reuse (physics resets)
```

Setting `isKinematic = true` on landing prevents the block from sliding or bouncing after impact — it should sit still and flash. Resetting to `false` before `SetActive(false)` ensures the pool-reused block starts with normal physics.

### `ThrownFirePickup` — Parabolic Arc via Lerp

```csharp
float arcOffset = 4f * t * (1f - t) * 2.5f; // parabola: 0 at t=0, peak at t=0.5, 0 at t=1
```

This is a simple easing function for a visual arc — not physics-based. It's more predictable than a Rigidbody arc and easier to control aesthetically. The pickup moves smoothly from spawn point to target in exactly 0.8s regardless of distance.

### Golden Ball Phase 1 Only — Save Guard

```csharp
if (_currentPhaseIndex >= 1 && _goldenBall != null)
{
    var save = ServiceLocator.Get<ISaveService>();
    if (!save.GetLevelData(levelId).goldenBallCollected)
        _goldenBall.gameObject.SetActive(false);
}
```

The save guard is critical: if the player already collected the golden ball in a prior attempt and retried, the ball would be hidden by `GoldenBall.Start()` anyway. But if they're on their first attempt and transition to Phase 2 without collecting it, `SetActive(false)` removes the now-inaccessible ball cleanly. No dangling golden ball floating in Phase 3-4 arena.

### `IBossController` ServiceLocator Registration — Boss Scene Only

`BossController.Awake()` registers `IBossController` via ServiceLocator:
```csharp
ServiceLocator.Register<IBossController>(this);
```

This registration only exists when the boss level is loaded. Story 6.2's `FireballProjectile` accesses it — but `FireballProjectile` is also only active during the boss level. No other story uses `IBossController`.

If `ServiceLocator.Get<IBossController>()` is called in a non-boss level, it will throw. `FireballProjectile` should guard: `ServiceLocator.TryGet<IBossController>(out var boss)` — check if `TryGet` is supported, otherwise add it to `ServiceLocator`.

### Project Structure Notes

- `BossPhaseConfig.cs` → `Assets/_Project/Scripts/Progression/`
- `IBossController.cs` → `Assets/_Project/Scripts/Core/`
- `BossController.cs` → `Assets/_Project/Scripts/Enemies/`
- `ExplodingIceBlock.cs` → `Assets/_Project/Scripts/Enemies/`
- `ThrownFirePickup.cs` → `Assets/_Project/Scripts/Enemies/`
- `ExplodingIceBlock.prefab` → `Assets/_Project/Prefabs/Enemies/`
- `ThrownFirePickup.prefab` → `Assets/_Project/Prefabs/Enemies/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `BossController` registers `IBossController`; `ExplodingIceBlock` retrieves `IBallStateManager`
- `ObjectPool<T>` (1.1) — all three new pooled types pre-warmed in BootstrapManager
- `GameEventSO` (1.3) — `OnLevelCompleted` raised by `BossController`; `OnFireAbilityActivated` raised by `ThrownFirePickup`
- `IBallStateManager` (1.4/4.1) — `TryTransitionTo(Stunned)` in `ExplodingIceBlock.Detonate()`
- `ISaveService` (2.1) — golden ball collected check in phase transition
- `LevelConfigSO` (2.2) — `bossPhases` array added here
- `ILevelManager` (2.2) — `CurrentLevel` for config and save data
- `GoldenBall.cs` (3.1) — `_goldenBall` reference for Phase 1 access guard
- `IceSpikeProjectile.cs` (4.3) — boss reuses the same pooled class for its ice spike attacks
- `OnFireAbilityActivated.asset` (3.2) — raised by `ThrownFirePickup` on collection

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md` (FR8: boss 4-phase survival)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (object pooling)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 6.1`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
