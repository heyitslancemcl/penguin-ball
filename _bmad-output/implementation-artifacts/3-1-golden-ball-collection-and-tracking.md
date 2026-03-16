# Story 3.1: Golden Ball Collection & Tracking

Status: ready-for-dev

## Story

As a player,
I want to collect a golden ball hidden in each level and have it tracked permanently,
so that my skill and exploration are rewarded and contribute toward unlocking the boss.

## Acceptance Criteria

1. **Given** a golden ball exists in a level **When** the ball rolls over it **Then** the `OnGoldenBallCollected` ScriptableObject event fires; `goldenBallCollected: true` is written to save data for that level; save triggers immediately
2. **Given** the golden ball is collected **When** the collection event fires **Then** a short `AddForce` impulse is applied in the current travel direction (magnitude from `LevelConfig`); a brief golden particle burst plays at the collection point
3. **Given** a level is replayed and the golden ball was already collected **When** the level loads **Then** the golden ball collectible is not present in the scene — it cannot be double-collected
4. **Given** the golden ball HUD slot is visible **When** the golden ball is collected mid-level **Then** the HUD slot updates immediately to show collected state
5. **Given** save data is inspected after collection **When** `goldenBallCollected` is queried for that levelId **Then** it returns `true` and persists across app restarts

## Tasks / Subtasks

- [ ] Task 1: Create `GoldenBall` MonoBehaviour (AC: 1, 2, 3)
  - [ ] Create `Assets/_Project/Scripts/Progression/GoldenBall.cs`:
    ```csharp
    public class GoldenBall : MonoBehaviour
    {
        [SerializeField] private GameEventSO _onGoldenBallCollected;
        [SerializeField] private ParticleSystem _collectParticles;

        private bool _collected;

        private void Start()
        {
            // Check save data — hide if already collected in a prior session
            var save = ServiceLocator.Get<ISaveService>();
            var levelId = ServiceLocator.Get<ILevelManager>().CurrentLevel.levelId;
            var levelData = save.GetLevelData(levelId);
            if (levelData.goldenBallCollected)
                gameObject.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_collected) return;
            if (!other.CompareTag("Ball")) return;

            _collected = true;

            // Detach and play particle burst at collection point
            // before disabling this object
            if (_collectParticles != null)
            {
                _collectParticles.transform.SetParent(null);
                _collectParticles.Play();
                Destroy(_collectParticles.gameObject, _collectParticles.main.duration + 0.5f);
            }

            _onGoldenBallCollected.Raise();
            gameObject.SetActive(false);
        }
    }
    ```
  - [ ] Add `Collider` with `IsTrigger = true` — sphere collider matching the visual mesh radius
  - [ ] Add `[Header("Ball")]` tag to the ball GameObject if not already set (Story 1.3 confirms "Ball" tag exists)
  - [ ] CRITICAL: `_collected` bool guard prevents double-fire if two collision callbacks fire in the same frame. `gameObject.SetActive(false)` immediately suppresses further triggers.

- [ ] Task 2: Create `GoldenBallCollectionHandler` (AC: 1, 5)
  - [ ] Create `Assets/_Project/Scripts/Progression/GoldenBallCollectionHandler.cs` (MonoBehaviour on Bootstrap):
    ```csharp
    public class GoldenBallCollectionHandler : MonoBehaviour
    {
        [SerializeField] private GameEventSO _onGoldenBallCollected;

        private void OnEnable()  => _onGoldenBallCollected.AddListener(HandleGoldenBallCollected);
        private void OnDisable() => _onGoldenBallCollected.RemoveListener(HandleGoldenBallCollected);

        private void HandleGoldenBallCollected()
        {
            var save = ServiceLocator.Get<ISaveService>();
            var levelManager = ServiceLocator.Get<ILevelManager>();
            string levelId = levelManager.CurrentLevel?.levelId;

            if (string.IsNullOrEmpty(levelId)) return;

            var levelData = save.GetLevelData(levelId);
            if (levelData.goldenBallCollected) return; // idempotency guard

            levelData.goldenBallCollected = true;
            save.UpdateLevelData(levelId, levelData); // triggers atomic save immediately
        }
    }
    ```
  - [ ] Add `GoldenBallCollectionHandler` component to the Bootstrap GameObject
  - [ ] Wire `[SerializeField] _onGoldenBallCollected` → `OnGoldenBallCollected.asset` in Inspector
  - [ ] Register in `BootstrapManager.Awake()` order — no registration needed (it's a listener, not a service)

- [ ] Task 3: Wire impulse in `BallController` (AC: 2)
  - [ ] Open `Assets/_Project/Scripts/Core/BallController.cs` (Story 1.3)
  - [ ] Add subscription fields and impulse handler:
    ```csharp
    [SerializeField] private GameEventSO _onGoldenBallCollected;
    [SerializeField] private LevelConfigSO _levelConfig; // already exists from Story 1.3

    private void OnEnable()
    {
        // existing subscriptions +
        _onGoldenBallCollected.AddListener(HandleGoldenBallCollected);
    }
    private void OnDisable()
    {
        // existing unsubscriptions +
        _onGoldenBallCollected.RemoveListener(HandleGoldenBallCollected);
    }

    private void HandleGoldenBallCollected()
    {
        var velocity = _rigidbody.linearVelocity; // Unity 6 API
        var direction = velocity.magnitude > 0.1f ? velocity.normalized : transform.forward;
        _rigidbody.AddForce(direction * _levelConfig.goldenBallImpulseMagnitude, ForceMode.Impulse);
    }
    ```
  - [ ] `goldenBallImpulseMagnitude` already exists on `LevelConfigSO` from Story 2.2 — no new field needed
  - [ ] Use `linearVelocity` not `velocity` — Unity 6 renamed Rigidbody API
  - [ ] Guard: if ball is near-stationary (`< 0.1f`), use `transform.forward` to prevent zero-vector impulse

- [ ] Task 4: Create `GoldenBallHUDSlot` (AC: 4)
  - [ ] Create `Assets/_Project/Scripts/UI/GoldenBallHUDSlot.cs` (MonoBehaviour):
    ```csharp
    public class GoldenBallHUDSlot : MonoBehaviour
    {
        [SerializeField] private GameEventSO _onGoldenBallCollected;
        [SerializeField] private GameObject _uncollectedVisual;
        [SerializeField] private GameObject _collectedVisual;

        private void OnEnable()  => _onGoldenBallCollected.AddListener(OnCollected);
        private void OnDisable() => _onGoldenBallCollected.RemoveListener(OnCollected);

        private void Start()
        {
            // Reflect save state on scene load (for replays where ball was already collected)
            var save = ServiceLocator.Get<ISaveService>();
            var levelId = ServiceLocator.Get<ILevelManager>().CurrentLevel?.levelId;
            bool alreadyCollected = !string.IsNullOrEmpty(levelId) &&
                                    save.GetLevelData(levelId).goldenBallCollected;
            SetCollected(alreadyCollected);
        }

        private void OnCollected() => SetCollected(true);

        private void SetCollected(bool collected)
        {
            _uncollectedVisual.SetActive(!collected);
            _collectedVisual.SetActive(collected);
        }
    }
    ```
  - [ ] Place `GoldenBallHUDSlot` on the GameScene Canvas (top-left anchor, inside a simple HUD area)
  - [ ] `_uncollectedVisual`: grey circle image (placeholder)
  - [ ] `_collectedVisual`: gold-filled circle image (placeholder)
  - [ ] Wire `_onGoldenBallCollected` → `OnGoldenBallCollected.asset` in Inspector
  - [ ] Note: Story 3.3 (In-Game HUD) will refactor this slot into the full HUD layout — keep it as a self-contained component now so 3.3 can just reposition and style it

- [ ] Task 5: Create SO event and prefab assets (AC: 1, 2, 3, 4)
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnGoldenBallCollected.asset` (GameEventSO)
  - [ ] Create `GoldenBall.prefab`: `Assets/_Project/Prefabs/GoldenBall.prefab`
    - Mesh: sphere (placeholder — swap for art asset when available)
    - Scale: ~0.4f (smaller than the player ball)
    - Material: gold/yellow unlit material (placeholder)
    - Components: `GoldenBall.cs`, `SphereCollider` (trigger, radius ~0.5f), `ParticleSystem` (burst, golden colour, 0.5s duration)
    - Optional: gentle rotate animation via `Animator` or `Update()` Y-axis spin for visual distinction
  - [ ] Place one `GoldenBall` prefab in `biome1_tutorial.unity` for initial testing
  - [ ] Place one in `biome1_level01.unity` for secondary test

- [ ] Task 6: Wire all Inspector references (AC: 1–5)
  - [ ] `GoldenBall.cs`:
    - `_onGoldenBallCollected` → `OnGoldenBallCollected.asset`
    - `_collectParticles` → child ParticleSystem component
  - [ ] `GoldenBallCollectionHandler.cs` (Bootstrap):
    - `_onGoldenBallCollected` → `OnGoldenBallCollected.asset`
  - [ ] `BallController.cs`:
    - `_onGoldenBallCollected` → `OnGoldenBallCollected.asset`
  - [ ] `GoldenBallHUDSlot.cs` (GameScene Canvas):
    - `_onGoldenBallCollected` → `OnGoldenBallCollected.asset`
    - `_uncollectedVisual` → grey circle GameObject
    - `_collectedVisual` → gold circle GameObject

- [ ] Task 7: Play Mode test checklist (AC: 1–5)
  - [ ] Roll ball into golden ball → verify `goldenBallCollected: true` in save file
  - [ ] Reload level → verify golden ball not present
  - [ ] Restart app → verify HUD slot shows collected state on next load of same level
  - [ ] Check `Application.persistentDataPath` save file directly to confirm atomic write
  - [ ] Verify impulse fires in ball travel direction on collection
  - [ ] Verify no double-fire (add `Debug.Log` in `HandleGoldenBallCollected` and confirm it fires once)

## Dev Notes

### Anti-Double-Collect Guards — Two Layers

The story uses two independent guards against double-collection:

**Layer 1 — `_collected` bool in `GoldenBall.cs`:** Prevents the same physics trigger firing the event twice in one session (Unity can fire `OnTriggerEnter` on the same frame from multiple colliders if the ball is fast).

**Layer 2 — `if (levelData.goldenBallCollected) return` in `GoldenBallCollectionHandler`:** Prevents save data being overwritten on a second collection (defensive — should never reach this guard if Layer 1 works, but belt-and-suspenders is correct here).

### Particle Detach Pattern — Critical

```csharp
// ✅ CORRECT — detach particle from golden ball before disabling
_collectParticles.transform.SetParent(null);
_collectParticles.Play();
Destroy(_collectParticles.gameObject, _collectParticles.main.duration + 0.5f);
gameObject.SetActive(false); // safe to disable now — particles are independent

// ❌ WRONG — disabling parent kills particle system mid-play
gameObject.SetActive(false);
_collectParticles.Play(); // never renders — parent is inactive
```

Detaching first ensures the burst plays to completion even after the golden ball GameObject is deactivated.

### `GoldenBallHUDSlot` — Story 3.3 Handoff Plan

This slot is intentionally minimal — just two swap-able child GameObjects. Story 3.3 (In-Game HUD) will:
1. Reposition the slot into the full HUD layout
2. Add animation to the collected transition (DOTween punch-scale)
3. Integrate with the lives counter and fire timer in a single HUD canvas

Do NOT add layout or animation logic to `GoldenBallHUDSlot` now — keep it purely functional.

### Save Trigger — Immediate on Collection

`goldenBallCollected` is saved immediately on collection (not deferred to level complete). This is correct and intentional — the player earns the golden ball even if they die immediately after collecting it. The save happens inside `HandleGoldenBallCollected()` via `UpdateLevelData()` which calls `Save()` internally (Story 2.1 `ISaveService` contract).

### `ILevelManager.CurrentLevel` Null Guard

Both `GoldenBall.Start()` and `GoldenBallCollectionHandler.HandleGoldenBallCollected()` access `ServiceLocator.Get<ILevelManager>().CurrentLevel`. Always null-check:

```csharp
var levelId = levelManager.CurrentLevel?.levelId;
if (string.IsNullOrEmpty(levelId)) return;
```

`CurrentLevel` can be null if the GoldenBall somehow exists outside a proper level load (e.g. during a scene authoring session in the Editor).

### Impulse Direction — Zero Velocity Guard

```csharp
var velocity = _rigidbody.linearVelocity;
var direction = velocity.magnitude > 0.1f ? velocity.normalized : transform.forward;
_rigidbody.AddForce(direction * _levelConfig.goldenBallImpulseMagnitude, ForceMode.Impulse);
```

If the ball somehow has near-zero velocity when collecting (e.g. it rolled very slowly and stopped on top of the golden ball), `velocity.normalized` would produce `NaN`. Use `transform.forward` as the fallback — this gives a consistent forward impulse rather than an undefined vector.

### Golden Ball Not Pooled — Static Level Object

Golden balls are static level objects placed by the level designer. They are NOT pooled:
- Placed once in the scene
- Collected once (then `SetActive(false)`)
- Never dynamically spawned

Object pooling is reserved for dynamically spawned objects during gameplay (projectiles, fire particles). Do NOT add pooling to `GoldenBall`.

### Project Structure Notes

- `GoldenBall.cs` → `Assets/_Project/Scripts/Progression/`
- `GoldenBallCollectionHandler.cs` → `Assets/_Project/Scripts/Progression/`
- `GoldenBallHUDSlot.cs` → `Assets/_Project/Scripts/UI/`
- `GoldenBall.prefab` → `Assets/_Project/Prefabs/`
- `OnGoldenBallCollected.asset` → `Assets/_Project/ScriptableObjects/Events/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `GoldenBall`, `GoldenBallCollectionHandler`, `BallController`, `GoldenBallHUDSlot` all retrieve services
- `GameEventSO` (1.3) — `OnGoldenBallCollected` uses the base SO event type
- `BallController` (1.3) — `_rigidbody.linearVelocity`, `AddForce`, `_levelConfig.goldenBallImpulseMagnitude`
- `IBallStateManager` (1.4) — not directly used here, but `"Ball"` tag established in Story 1.3
- `ISaveService` (2.1) — `GetLevelData`, `UpdateLevelData` with immediate save
- `ILevelManager` (2.2) — `CurrentLevel.levelId` identifies which level's save data to update
- `LevelConfigSO` (2.2) — `goldenBallImpulseMagnitude` field used for impulse

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-009` (SO event + ServiceLocator hybrid)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (no Instantiate/Destroy during gameplay — static objects exempt)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 3.1`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
