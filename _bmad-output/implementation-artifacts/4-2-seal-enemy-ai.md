# Story 4.2: Seal Enemy AI

Status: ready-for-dev

## Story

As a player,
I want to encounter patrolling Seal enemies that slide into me and knock me off course,
so that I must time my movement to avoid or defeat them.

## Acceptance Criteria

1. **Given** a Seal is placed in a level **When** the level starts **Then** the Seal patrols its configured waypoint path continuously at its configured speed
2. **Given** the Seal is patrolling and reaches a waypoint **When** the waypoint is reached **Then** the Seal reverses direction smoothly — no snapping or teleporting
3. **Given** the ball contacts a Seal without fire active **When** the collision registers **Then** the Seal applies a directional knock-off impulse to the ball; if the ball is knocked off the platform to the kill plane, death is registered
4. **Given** the ball contacts a Seal with fire active **When** the collision registers **Then** the Seal is defeated; a defeat animation plays; `OnEnemyDefeated` SO event fires
5. **Given** the fire ability blast wave fires near a Seal **When** `Physics.OverlapSphere` includes the Seal **Then** the Seal is defeated via the same `IDefeatable.Defeat()` path as direct contact
6. **Given** Seal instances are used in a level **When** the Seal is defeated **Then** `gameObject.SetActive(false)` returns it to an inactive state — no `Destroy()` call; level reload restores all Seals

## Tasks / Subtasks

- [ ] Task 1: Create `SealController` MonoBehaviour (AC: 1–5)
  - [ ] Create `Assets/_Project/Scripts/Enemies/SealController.cs`:
    ```csharp
    public class SealController : MonoBehaviour, IDefeatable
    {
        [Header("Patrol")]
        [SerializeField] private Transform[] _waypoints;
        [SerializeField] private float _moveSpeed = 2.5f;

        [Header("Combat")]
        [SerializeField] private float _knockOffForce = 14f;
        [SerializeField] private float _defeatAnimDuration = 0.6f;

        [Header("Events")]
        [SerializeField] private GameEventSO _onEnemyDefeated;

        private Rigidbody   _rigidbody;
        private Animator    _animator;  // optional — null-checked throughout
        private IBallStateManager _stateManager;

        private int  _waypointIndex     = 0;
        private int  _waypointDirection = 1;
        private bool _defeated;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _animator  = GetComponent<Animator>(); // null if no animator assigned
        }

        private void Start()
        {
            _stateManager = ServiceLocator.Get<IBallStateManager>();

            if (_waypoints != null && _waypoints.Length >= 2)
                StartCoroutine(PatrolCoroutine());
            // If fewer than 2 waypoints: Seal is stationary — no patrol
        }

        // ─── Patrol ───────────────────────────────────────────────────────────

        private IEnumerator PatrolCoroutine()
        {
            while (!_defeated)
            {
                // Pause patrol while ball is stunned
                while (_stateManager.CurrentState == BallState.Stunned)
                    yield return null;

                if (_defeated) yield break;

                Vector3 target = _waypoints[_waypointIndex].position;

                // Move toward current waypoint
                while (Vector3.Distance(transform.position, target) > 0.05f)
                {
                    if (_defeated) yield break;

                    while (_stateManager.CurrentState == BallState.Stunned)
                        yield return null;

                    // Use MovePosition for kinematic Rigidbody — keeps physics collision valid
                    Vector3 next = Vector3.MoveTowards(
                        transform.position, target, _moveSpeed * Time.deltaTime);
                    _rigidbody.MovePosition(next);

                    // Face direction of travel
                    Vector3 dir = (target - transform.position).normalized;
                    if (dir.sqrMagnitude > 0.001f)
                        _rigidbody.MoveRotation(Quaternion.LookRotation(dir));

                    yield return null;
                }

                AdvanceWaypoint();
                yield return null;
            }
        }

        private void AdvanceWaypoint()
        {
            _waypointIndex += _waypointDirection;

            if (_waypointIndex >= _waypoints.Length)
            {
                _waypointDirection = -1;
                _waypointIndex     = Mathf.Max(0, _waypoints.Length - 2);
            }
            else if (_waypointIndex < 0)
            {
                _waypointDirection = 1;
                _waypointIndex     = Mathf.Min(1, _waypoints.Length - 1);
            }
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

            // Knock-off: push ball away from seal's centre
            var rb = col.gameObject.GetComponent<Rigidbody>();
            if (rb == null) return;

            Vector3 knockDir = (col.transform.position - transform.position).normalized;
            // Ensure a meaningful upward component so the ball arcs before hitting the kill plane
            knockDir.y = Mathf.Max(knockDir.y, 0.25f);
            knockDir.Normalize();

            rb.AddForce(knockDir * _knockOffForce, ForceMode.Impulse);
        }

        // ─── IDefeatable ──────────────────────────────────────────────────────

        public void Defeat()
        {
            if (_defeated) return;
            _defeated = true;

            StopAllCoroutines();
            _onEnemyDefeated.Raise();
            _animator?.SetTrigger("Defeat");

            StartCoroutine(DefeatSequence());
        }

        private IEnumerator DefeatSequence()
        {
            yield return new WaitForSeconds(_defeatAnimDuration);
            gameObject.SetActive(false); // pool-compatible — no Destroy
            // NOTE: _defeated is NOT reset here intentionally.
            // Level reload resets all scene objects. Pool reuse would call
            // OnEnable/Start which reinitialises state correctly.
        }
    }
    ```

- [ ] Task 2: Add `OnEnable` reset for pool reuse (AC: 6)
  - [ ] Add `OnEnable()` to `SealController` to reset state when a pooled Seal is reactivated:
    ```csharp
    private void OnEnable()
    {
        _defeated         = false;
        _waypointIndex    = 0;
        _waypointDirection = 1;
        // PatrolCoroutine is re-started by Start() on first enable.
        // For pool reuse (SetActive false→true after Start has run),
        // manually restart patrol:
        StopAllCoroutines();
        if (_waypoints != null && _waypoints.Length >= 2 && _stateManager != null)
            StartCoroutine(PatrolCoroutine());
    }
    ```
  - [ ] Note: `_stateManager` is null on first `OnEnable()` (before `Start()` runs) — the `&& _stateManager != null` guard handles this. Patrol starts properly from `Start()` on first activation.

- [ ] Task 3: Configure Seal Rigidbody and Collider (AC: 1, 3)
  - [ ] Seal Rigidbody settings (CRITICAL):
    - `isKinematic = true` — Seal moves via `MovePosition`, not physics forces; prevents seal being knocked away by ball
    - `useGravity = false` — kinematic body ignores gravity
    - `Interpolate = Interpolate` — smooth visual movement between physics steps
    - `Collision Detection = Continuous` — prevents fast ball tunnelling through the seal
  - [ ] Kinematic Rigidbody + non-trigger Collider = receives `OnCollisionEnter` callbacks from dynamic Rigidbodies (the ball) ✓
  - [ ] Add `BoxCollider` or `CapsuleCollider` matching seal shape — NOT a trigger; solid collision
  - [ ] Do NOT add `PhysicsMaterial` with zero friction to seal — the ball should feel like it hits something solid

- [ ] Task 4: Create Seal prefab (AC: 1–6)
  - [ ] Create `Assets/_Project/Prefabs/Enemies/Seal.prefab`:
    - Mesh: capsule placeholder (axis along Z for patrol direction), scaled to ~(0.8, 0.8, 1.2)
    - Material: dark grey placeholder
    - Components: `SealController`, `Rigidbody` (kinematic), `CapsuleCollider`, optional `Animator`
  - [ ] Wire `[SerializeField]` in Prefab Inspector:
    - `_onEnemyDefeated` → `OnEnemyDefeated.asset`
  - [ ] Tag: `Enemy` (create tag if not already present)
  - [ ] Physics Layer: `Enemy` layer (created in Story 4.1) — ensures `FireBlastWave._blastLayers` mask includes Seals
  - [ ] `_waypoints` array is empty in the prefab — assigned per-scene-instance after placement

- [ ] Task 5: Set up Seal waypoint system in scenes (AC: 1, 2)
  - [ ] Waypoint approach: create empty `Transform` child GameObjects under a `WaypointPath` parent GameObject in the scene (NOT in the Seal prefab)
  - [ ] Scene structure for each Seal:
    ```
    SealEnemy (SealController prefab instance)
    WaypointPath_Seal01 (empty parent)
      Waypoint_A (empty Transform — position = patrol start)
      Waypoint_B (empty Transform — position = patrol end)
    ```
  - [ ] In Inspector, drag `Waypoint_A` and `Waypoint_B` into `SealController._waypoints[]` array (in order)
  - [ ] Place one Seal with 2-waypoint patrol path in `biome1_tutorial.unity` for testing
  - [ ] Waypoints should be at the same Y-height as the seal's start position (no vertical movement for basic seals)

- [ ] Task 6: Add Enemy and IceSpike tags to project (AC: 3, 4)
  - [ ] In `Edit → Project Settings → Tags and Layers`:
    - Add tag: `Enemy`
    - Add tag: `IceSpike` (if not already present from Story 2.3)
    - Confirm `Ground` tag exists (added in Story 4.1)
    - Confirm `Ball` tag exists (Story 1.3)
  - [ ] Apply `Enemy` tag to Seal prefab
  - [ ] Apply `IceSpike` tag to `IceSpike.prefab` (Story 2.3) if not already tagged
  - [ ] Confirm `FireBlastWave._blastLayers` LayerMask includes the `Enemy` layer

- [ ] Task 7: Play Mode test checklist (AC: 1–6)
  - [ ] Level loads → Seal patrols between its two waypoints continuously
  - [ ] Seal reaches waypoint B → reverses smoothly toward A (no snapping)
  - [ ] Ball collides with Seal (fire off) → ball is knocked away with upward arc; if arc reaches kill plane, death registers
  - [ ] Ball collides with Seal (fire on, via `OnFireAbilityActivated` trigger) → Seal plays defeat anim, `SetActive(false)`, `OnEnemyDefeated` fires
  - [ ] Fire blast wave near Seal (verify blast radius covers Seal) → `IDefeatable.Defeat()` called, same defeat path
  - [ ] Retry level → Seal is present again at start position (level reload resets everything)

## Dev Notes

### Kinematic Rigidbody — Receives Collision Events

A kinematic `Rigidbody` still receives `OnCollisionEnter` callbacks when a non-kinematic `Rigidbody` (the ball) hits it:

```
Ball (dynamic Rigidbody) collides with Seal (kinematic Rigidbody)
→ SealController.OnCollisionEnter() fires ✓
→ Seal is NOT pushed by ball physics ✓ (kinematic ignores forces)
→ Ball IS pushed by seal collision ✓ (dynamic responds normally)
```

Without `isKinematic = true`, a physics-enabled Seal would be knocked around by the ball, ruining its patrol path. Kinematic gives control to `MovePosition()` only.

### `Rigidbody.MovePosition` vs `transform.position`

```csharp
// ✅ CORRECT — kinematic Rigidbody patrol
_rigidbody.MovePosition(next);

// ❌ WRONG — bypasses physics engine entirely
transform.position = next;
```

`transform.position =` on a kinematic Rigidbody teleports it — collision events between frames are missed. `MovePosition` interpolates through the physics step, maintaining proper collision detection as the seal moves. This is the same reason `MovingPlatform` (Story 2.3) uses the coroutine pattern rather than `Update()` `transform.position`.

### Knock-Off Upward Component — Minimum Y

```csharp
knockDir.y = Mathf.Max(knockDir.y, 0.25f);
knockDir.Normalize();
```

Without a minimum Y component, a direct sideways hit produces a purely horizontal impulse — the ball slides along the platform instead of arcing. The `0.25f` minimum ensures the ball always gets some upward motion, making the knock look and feel more like a slide-collision. Tune the value during playtesting — `0.15f` (subtle arc) to `0.4f` (dramatic toss).

### Waypoints Are Scene-Instance Data, Not Prefab Data

`_waypoints[]` is left empty in the Seal prefab. Each scene instance assigns its own waypoints via the Inspector:

```
✅ Seal01 → [WaypointA, WaypointB]  (patrol left-right)
✅ Seal02 → [WaypointC, WaypointD, WaypointE]  (patrol 3 points)
✅ Seal03 → []  (stationary guard — no patrol coroutine starts)
```

Waypoint `Transform` GameObjects live in the scene (not in the prefab). This gives level designers full spatial control without modifying the prefab.

### Ping-Pong Edge Case — Single Waypoint

If `_waypoints.Length == 1`, patrol does not start (`< 2` guard). If `_waypoints.Length == 0`, same. A single stationary Seal is placed at its scene position and just sits there — useful for a guarding enemy that reacts on contact but doesn't move.

### `OnEnable` Pool Reset — Ordering Issue

`OnEnable()` fires before `Start()` on first activation. The `_stateManager != null` guard in `OnEnable()` prevents a `NullReferenceException` on first enable, since `_stateManager` is set in `Start()`. On pool reuse (`SetActive(true)` after prior deactivation), `Start()` does NOT re-run — `OnEnable()` handles re-initialisation:

```
First activation:  Awake() → OnEnable() [_stateManager null, skips patrol] → Start() [sets _stateManager, starts patrol]
Pool reuse:        OnEnable() [_stateManager set, starts patrol] → Start() does NOT run again
```

### `StyleRankScorer` Integration — `OnEnemyDefeated` Already Subscribed

`StyleRankTracker` (Story 3.2) subscribes to `OnEnemyDefeated` and increments `_enemyDefeats`. This happens automatically — no changes needed to `StyleRankTracker`. The wiring is:

```
SealController.Defeat() → _onEnemyDefeated.Raise()
  → StyleRankTracker.OnEnemyDefeated() → _enemyDefeats++
```

### No `Destroy` — Level Reload Handles Respawn

```csharp
// ✅ CORRECT — pool-compatible, no memory alloc
gameObject.SetActive(false);

// ❌ WRONG — permanent destruction; seal won't respawn on retry
Destroy(gameObject);
```

Level reload (`LevelManager.RestartLevel()`) triggers a full scene unload and reload. All Seals in the level scene are reset to their original scene state (active, at their starting position). This is the correct respawn mechanism for level-placed enemies — no explicit respawn logic needed.

### Project Structure Notes

- `SealController.cs` → `Assets/_Project/Scripts/Enemies/`
- `IDefeatable.cs` → `Assets/_Project/Scripts/Enemies/` (created in Story 4.1)
- `Seal.prefab` → `Assets/_Project/Prefabs/Enemies/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `SealController.Start()` retrieves `IBallStateManager`
- `GameEventSO` (1.3) — `OnEnemyDefeated` raised on defeat
- `BallState` enum (1.3/1.4) — stun pause guard: `CurrentState == BallState.Stunned`
- `IBallStateManager` (1.4) — `IsFireActive` checked in `OnCollisionEnter`; `CurrentState` checked in patrol
- `OnEnemyDefeated.asset` (3.2) — SO event asset created in Story 3.2; wired here
- `IDefeatable` (4.1) — interface created in Story 4.1; implemented here

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-005` (enemy classification)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (no Destroy during gameplay, coroutine movement)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 4.2`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
