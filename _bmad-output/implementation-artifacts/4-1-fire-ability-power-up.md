# Story 4.1: Fire Ability Power-Up

Status: ready-for-dev

## Story

As a player,
I want to collect a fire ability power-up that lets me defeat enemies, break walls, and clear ice spikes for a limited time,
so that timing and positioning the fire ability becomes a core skill expression.

## Acceptance Criteria

1. **Given** a fire ability power-up exists in a level **When** the ball rolls over it **Then** `OnFireAbilityActivated` SO event fires; `IsFireActive` is set to `true` on `BallStateManager`; the timer starts for the configured duration from `LevelConfig`
2. **Given** the fire ability is active **When** the timer expires **Then** `IsFireActive` is set to `false`; `OnFireAbilityDeactivated` SO event fires; fire visual effects are removed
3. **Given** the fire ability is active and the ball bounces **When** the ball makes contact with a surface **Then** `Physics.OverlapSphere` fires at the impact point; radial force is applied to all enemies and hazards within range; ice spikes in range are destroyed
4. **Given** the fire ability is active and the ball contacts an enemy **Then** the enemy is defeated — handled per enemy type in Stories 4.2 and 4.3; `IDefeatable.Defeat()` is the contract
5. **Given** the fire ability is active and the ball contacts a `BreakableWall` **When** the collision registers **Then** the wall shatters (AC covered in Story 2.3 via `BallStateManager.IsFireActive` — confirmed valid; no changes needed to `BreakableWall`)
6. **Given** fire particles are active **When** running during gameplay **Then** the fire trail particle system is a pre-placed child of the Ball (no `Instantiate`) — enabled on activation, disabled on deactivation; blast wave effects at bounce points are pooled via `ObjectPool`

## Tasks / Subtasks

- [ ] Task 1: Add `IsFireActive` to `IBallStateManager` and `BallStateManager` (AC: 1, 2, 3)
  - [ ] Open `Assets/_Project/Scripts/Core/IBallStateManager.cs` (Story 1.4)
  - [ ] Add:
    ```csharp
    bool IsFireActive { get; }
    void SetFireActive(bool active);
    ```
  - [ ] Open `Assets/_Project/Scripts/Core/BallStateManager.cs`
  - [ ] Add implementation:
    ```csharp
    public bool IsFireActive { get; private set; }

    public void SetFireActive(bool active)
    {
        IsFireActive = active;
    }
    ```
  - [ ] `IsFireActive` is intentionally separate from the Ball state machine (Rolling/Stunned/Recovery/Dead). It is an orthogonal flag — the ball can be Rolling AND fire-active simultaneously
  - [ ] `SetFireActive(false)` does NOT change `CurrentState` — fire expiry has no state transition side effect

- [ ] Task 2: Create `IDefeatable` interface (AC: 4)
  - [ ] Create `Assets/_Project/Scripts/Enemies/IDefeatable.cs`:
    ```csharp
    /// <summary>
    /// Implemented by all enemies. Called by FireBlastWave and direct fire contact.
    /// Stories 4.2 and 4.3 implement this on SealController and WalrusController.
    /// </summary>
    public interface IDefeatable
    {
        void Defeat();
    }
    ```
  - [ ] This is a forward declaration — `IDefeatable` is used by `FireBlastWave` (this story) but implemented by enemies (4.2, 4.3)

- [ ] Task 3: Create `FireAbilityPickup` MonoBehaviour (AC: 1)
  - [ ] Create `Assets/_Project/Scripts/Progression/FireAbilityPickup.cs`:
    ```csharp
    public class FireAbilityPickup : MonoBehaviour
    {
        [SerializeField] private GameEventSO _onFireAbilityActivated;

        private bool _collected;

        private void OnTriggerEnter(Collider other)
        {
            if (_collected) return;
            if (!other.CompareTag("Ball")) return;

            _collected = true;
            _onFireAbilityActivated.Raise();
            gameObject.SetActive(false);
        }
    }
    ```
  - [ ] Add `Collider` with `IsTrigger = true` (sphere, slightly larger than visual mesh)
  - [ ] Add rotating/bobbing visual via simple `Update()` Y-axis rotation — distinguishes it from the golden ball
  - [ ] Create `FireAbilityPickup.prefab`: `Assets/_Project/Prefabs/FireAbilityPickup.prefab`
    - Mesh: sphere with distinct flame-orange material (placeholder)
    - Scale: ~0.5f
    - Slight Y-axis rotation in Update for visual clarity
  - [ ] Wire `_onFireAbilityActivated` → `OnFireAbilityActivated.asset` in Inspector

- [ ] Task 4: Create `FireAbilityManager` MonoBehaviour (AC: 1, 2)
  - [ ] Create `Assets/_Project/Scripts/Core/FireAbilityManager.cs` (MonoBehaviour on Bootstrap):
    ```csharp
    public class FireAbilityManager : MonoBehaviour
    {
        [SerializeField] private GameEventSO _onFireAbilityActivated;
        [SerializeField] private GameEventSO _onFireAbilityDeactivated;

        private Coroutine _timerCoroutine;

        private void OnEnable()  => _onFireAbilityActivated.AddListener(HandleFireActivated);
        private void OnDisable() => _onFireAbilityActivated.RemoveListener(HandleFireActivated);

        private void HandleFireActivated()
        {
            // Cancel any existing timer (pickup collected while fire already active — restarts timer)
            if (_timerCoroutine != null)
                StopCoroutine(_timerCoroutine);

            float duration = ServiceLocator.Get<ILevelManager>().CurrentLevel?.fireAbilityDuration ?? 25f;
            ServiceLocator.Get<IBallStateManager>().SetFireActive(true);
            _timerCoroutine = StartCoroutine(FireTimerCoroutine(duration));
        }

        private IEnumerator FireTimerCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration); // scales with Time.timeScale — pauses when game pauses
            ServiceLocator.Get<IBallStateManager>().SetFireActive(false);
            _onFireAbilityDeactivated.Raise();
            _timerCoroutine = null;
        }
    }
    ```
  - [ ] Add `FireAbilityManager` component to Bootstrap GameObject
  - [ ] Wire `[SerializeField]` SO event references in Inspector
  - [ ] `WaitForSeconds` scales with `Time.timeScale` — fire timer correctly pauses when `PauseMenuController` sets `timeScale = 0`
  - [ ] If a second pickup is collected mid-fire: timer restarts (existing coroutine stopped, new one started) — this is the correct and expected behaviour

- [ ] Task 5: Create `FireBlastWave` MonoBehaviour on Ball (AC: 3, 4)
  - [ ] Create `Assets/_Project/Scripts/Core/FireBlastWave.cs` (MonoBehaviour on Ball GameObject):
    ```csharp
    public class FireBlastWave : MonoBehaviour
    {
        [SerializeField] private float _blastRadius   = 3f;
        [SerializeField] private float _blastForce    = 12f;
        [SerializeField] private LayerMask _blastLayers; // assign Enemy + Obstacle layers

        private IBallStateManager _stateManager;

        private void Start()
        {
            _stateManager = ServiceLocator.Get<IBallStateManager>();
        }

        private void OnCollisionEnter(Collision col)
        {
            if (!_stateManager.IsFireActive) return;

            // Ignore collisions with ground/floor tagged objects to avoid constant triggering
            // on the rolling surface — only blast on obstacle/wall/enemy contact
            if (col.gameObject.CompareTag("Ground")) return;

            Vector3 impactPoint = col.GetContact(0).point;
            TriggerBlast(impactPoint);
        }

        private void TriggerBlast(Vector3 origin)
        {
            Collider[] hits = Physics.OverlapSphere(origin, _blastRadius, _blastLayers);

            foreach (var hit in hits)
            {
                // Defeat enemies via IDefeatable contract
                var defeatable = hit.GetComponent<IDefeatable>();
                defeatable?.Defeat();

                // Destroy static ice spikes
                if (hit.CompareTag("IceSpike"))
                    hit.gameObject.SetActive(false); // SetActive(false) — static spikes, no pool

                // Apply radial impulse to any Rigidbody (enemies, projectiles)
                var rb = hit.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = (hit.transform.position - origin).normalized;
                    if (dir == Vector3.zero) dir = Vector3.up;
                    rb.AddForce(dir * _blastForce, ForceMode.Impulse);
                }
            }

            // Spawn pooled blast wave VFX
            SpawnBlastEffect(origin);
        }

        private void SpawnBlastEffect(Vector3 position)
        {
            var effect = ObjectPool.Get<BlastWaveEffect>();
            if (effect == null) return;
            effect.transform.position = position;
            effect.Play(onComplete: () => ObjectPool.Release(effect));
        }
    }
    ```
  - [ ] Add `FireBlastWave` to the Ball prefab (`Assets/_Project/Prefabs/Ball/Ball.prefab`)
  - [ ] Configure Inspector fields: `_blastRadius = 3f`, `_blastForce = 12f`
  - [ ] Set `_blastLayers` to include Enemy and Obstacle physics layers in Inspector — exclude Default/Ground to prevent triggering on every surface roll
  - [ ] **CRITICAL**: `col.gameObject.CompareTag("Ground")` — add "Ground" tag to floor/platform GameObjects in level scenes, otherwise blast fires on every roll contact
  - [ ] Tag convention: platforms/floor = "Ground", enemies = "Enemy", ice spikes = "IceSpike" — consistent with existing "Ball" tag from Story 1.3

- [ ] Task 6: Create `BlastWaveEffect` pooled VFX component (AC: 6)
  - [ ] Create `Assets/_Project/Scripts/Core/BlastWaveEffect.cs` (MonoBehaviour):
    ```csharp
    public class BlastWaveEffect : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _particles;
        private Action _onComplete;

        public void Play(Action onComplete)
        {
            _onComplete = onComplete;
            gameObject.SetActive(true);
            _particles.Play();
            StartCoroutine(WaitAndRelease());
        }

        private IEnumerator WaitAndRelease()
        {
            yield return new WaitForSeconds(_particles.main.duration);
            _particles.Stop();
            gameObject.SetActive(false);
            _onComplete?.Invoke();
        }
    }
    ```
  - [ ] Create `BlastWaveEffect.prefab`: `Assets/_Project/Prefabs/Effects/BlastWaveEffect.prefab`
    - `ParticleSystem`: radial burst, 20–30 particles, warm orange/white colour, 0.5s duration
    - Scale: 1f world units (will visually cover the blast radius)
  - [ ] Pre-warm pool in `BootstrapManager.Awake()`:
    ```csharp
    ObjectPool.Prewarm<BlastWaveEffect>(_blastWaveEffectPrefab, count: 5);
    ```
  - [ ] Wire `[SerializeField] private BlastWaveEffect _blastWaveEffectPrefab;` in `BootstrapManager`

- [ ] Task 7: Add fire trail VFX to Ball prefab (AC: 6)
  - [ ] Open `Assets/_Project/Prefabs/Ball/Ball.prefab`
  - [ ] Add child `GameObject: FireTrail` with `ParticleSystem` component:
    - Mode: continuous trail/sparkle, orange-yellow colour
    - `PlayOnAwake: false`, `Looping: true`
    - Default: inactive (`gameObject.SetActive(false)`)
  - [ ] Create `Assets/_Project/Scripts/Core/FireTrailEffect.cs` (MonoBehaviour on Ball):
    ```csharp
    public class FireTrailEffect : MonoBehaviour
    {
        [SerializeField] private GameEventSO _onFireAbilityActivated;
        [SerializeField] private GameEventSO _onFireAbilityDeactivated;
        [SerializeField] private GameObject  _fireTrailObject;

        private void OnEnable()
        {
            _onFireAbilityActivated.AddListener(ShowTrail);
            _onFireAbilityDeactivated.AddListener(HideTrail);
        }
        private void OnDisable()
        {
            _onFireAbilityActivated.RemoveListener(ShowTrail);
            _onFireAbilityDeactivated.RemoveListener(HideTrail);
        }

        private void ShowTrail() => _fireTrailObject.SetActive(true);
        private void HideTrail() => _fireTrailObject.SetActive(false);
    }
    ```
  - [ ] Add `FireTrailEffect` to Ball prefab; wire `_fireTrailObject` → `FireTrail` child GameObject

- [ ] Task 8: Place fire ability pickup in test scenes (AC: 1–3)
  - [ ] Place one `FireAbilityPickup` prefab in `biome1_tutorial.unity` at a reachable position
  - [ ] Place one in `biome1_level01.unity`
  - [ ] Add "Ground" tag to platform/floor objects in those scenes (required for blast wave tag filtering)
  - [ ] Play Mode test checklist:
    - [ ] Roll over pickup → fire trail appears on ball, HUD timer starts counting down
    - [ ] Ball bounces off a wall → blast ring VFX appears at impact point
    - [ ] Fire timer expires → trail disappears, HUD timer hides
    - [ ] Collect second pickup mid-fire → timer restarts from full duration
    - [ ] Verify no `Instantiate`/`Destroy` calls (use Unity Profiler Allocations if needed)

## Dev Notes

### `IsFireActive` — Orthogonal to Ball State Machine

`IsFireActive` is NOT a `BallState` value. It is a separate boolean flag on `BallStateManager` that coexists with the state machine:

```
BallState:    Rolling  ─── Stunned ─── Recovery ─── Dead
IsFireActive: true/false (independent — can be true in any non-Dead state)
```

When the ball dies (`Dead` state), `IsFireActive` is NOT automatically reset. `FireAbilityManager.HandleFireActivated()` sets it, and the timer coroutine resets it. If the player dies mid-fire:
- `IsFireActive` remains true until the timer expires or `RestartLevel()` fires
- On `RestartLevel()`, `LevelManager.StartLevel()` calls `ServiceLocator.Get<IBallStateManager>().Reset()` which should also reset `IsFireActive`:

```csharp
// In BallStateManager.Reset() (Story 2.2):
public void Reset()
{
    StopAllCoroutines();
    _swipeWindowOpen = false;
    IsFireActive = false;  // ADD THIS — reset fire state on level restart
    CurrentState = BallState.Rolling;
}
```

Add this line to `BallStateManager.Reset()` — without it, a player who dies while fire is active will restart with fire still visually active (HUD timer running) but the underlying coroutine in `FireAbilityManager` may have expired.

### Blast Wave — "Ground" Tag Filter

The `OnCollisionEnter` guard `if (col.gameObject.CompareTag("Ground")) return;` is essential:

```csharp
// ✅ Without this guard, blast fires on EVERY roll contact with the floor:
// - Constant OverlapSphere queries each physics frame on ground
// - Constant pool churn of BlastWaveEffect
// - Enemies/ice spikes immediately defeated on fire activation

// ❌ If you forget "Ground" tag on platforms, blast never fires (returns early on everything)
```

Convention: all walkable surfaces (platforms, floors, ramps) use the "Ground" tag. Obstacles/enemies/walls do NOT use "Ground". Only tag actual walkable geometry.

### `FireAbilityManager` Timer — Restart on Second Pickup

```csharp
if (_timerCoroutine != null)
    StopCoroutine(_timerCoroutine); // stop old timer

_timerCoroutine = StartCoroutine(FireTimerCoroutine(duration)); // restart
```

If the player is already fire-active and collects a second pickup, the timer resets to full duration. `SetFireActive(true)` is called again — this is idempotent (already true, no state change) but the timer extension is the meaningful effect.

### Static `IceSpike` in Blast Wave — `SetActive(false)` Not `Destroy`

```csharp
// In FireBlastWave.TriggerBlast():
if (hit.CompareTag("IceSpike"))
    hit.gameObject.SetActive(false); // NOT Destroy
```

Static `IceSpike` objects (placed in scene) use `Destroy()` in `IceSpike.OnCollisionEnter()` (Story 2.3) for direct ball contact. The blast wave uses `SetActive(false)` instead — this is a pragmatic choice since the blast wave can hit multiple spikes and `Destroy()` in a loop can cause issues if the collection changes mid-iteration. Both result in the spike disappearing — the distinction doesn't matter to gameplay.

### `BlastWaveEffect` Pool Size — 5 Pre-warmed

The ball can bounce rapidly (multiple contacts per second at high speed). Pool size of 5 ensures bursts of bounces don't run out. If 5 are all active simultaneously, `ObjectPool.Get<BlastWaveEffect>()` returns `null` and `SpawnBlastEffect` guards with `if (effect == null) return;` — no crash, just a missed VFX on a fast bounce sequence (acceptable).

### `ObjectPool.Prewarm` — `BootstrapManager` Responsibility

Pre-warming pools in `BootstrapManager.Awake()` ensures pool objects exist before any gameplay begins:

```csharp
// BootstrapManager.Awake():
[SerializeField] private BlastWaveEffect _blastWaveEffectPrefab;
// ...
ObjectPool.Prewarm<BlastWaveEffect>(_blastWaveEffectPrefab, 5);
```

The `ObjectPool<T>` from Story 1.1 must support `Prewarm`. If it doesn't have this method yet, add it:
```csharp
public static void Prewarm<T>(T prefab, int count) where T : MonoBehaviour
{
    for (int i = 0; i < count; i++)
    {
        var obj = Object.Instantiate(prefab);
        obj.gameObject.SetActive(false);
        Release(obj);
    }
}
```

### Physics Layer Setup — Blast Wave Layer Mask

`FireBlastWave._blastLayers` should be set to a LayerMask including:
- `Enemy` layer (for Seal, Walrus — added in 4.2/4.3)
- `Obstacle` layer (for ice spikes — may be on Default layer currently)

Set up physics layers in `Edit → Project Settings → Physics → Layer Collision Matrix` as part of this story. Suggested layers:
- Layer 8: `Ground` (platforms, floors)
- Layer 9: `Enemy`
- Layer 10: `Obstacle` (ice spikes, moving platforms)

Tag and layer are separate — Ground TAG on floor GameObjects, Ground LAYER optional (use tags for `FireBlastWave` guard).

### Project Structure Notes

- `IDefeatable.cs` → `Assets/_Project/Scripts/Enemies/`
- `FireAbilityPickup.cs` → `Assets/_Project/Scripts/Progression/`
- `FireAbilityManager.cs` → `Assets/_Project/Scripts/Core/`
- `FireBlastWave.cs` → `Assets/_Project/Scripts/Core/`
- `BlastWaveEffect.cs` → `Assets/_Project/Scripts/Core/`
- `FireTrailEffect.cs` → `Assets/_Project/Scripts/Core/`
- `FireAbilityPickup.prefab` → `Assets/_Project/Prefabs/`
- `BlastWaveEffect.prefab` → `Assets/_Project/Prefabs/Effects/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `FireAbilityManager`, `FireBlastWave` retrieve services
- `ObjectPool<T>` (1.1) — `BlastWaveEffect` pooling; `Prewarm` method may need adding
- `GameEventSO` (1.3) — `OnFireAbilityActivated`, `OnFireAbilityDeactivated`
- `IBallStateManager` (1.4) — `IsFireActive` and `SetFireActive()` added to interface here; `Reset()` updated to clear fire state
- `LevelConfigSO` (2.2) — `fireAbilityDuration` used by `FireAbilityManager`
- `ILevelManager` (2.2) — `CurrentLevel.fireAbilityDuration` retrieved at activation time
- `BreakableWall.cs` (2.3) — already checks `BallStateManager.IsFireActive`; no changes needed
- `IceSpike.cs` (2.3) — already checks `BallStateManager.IsFireActive`; no changes needed
- `OnFireAbilityActivated.asset` (3.2) — SO event asset already created; used here
- `OnFireAbilityDeactivated.asset` (3.2) — SO event asset already created; raised here by `FireAbilityManager`

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (object pooling, no Instantiate/Destroy during gameplay)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-005` (obstacle interaction pattern)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 4.1`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
