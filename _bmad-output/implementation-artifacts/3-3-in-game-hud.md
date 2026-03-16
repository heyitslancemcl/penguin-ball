# Story 3.3: In-Game HUD

Status: ready-for-dev

## Story

As a player,
I want a clean HUD showing my lives, fire timer, and golden ball status while I play,
so that I always know my current state without breaking immersion.

## Acceptance Criteria

1. **Given** a level starts **When** the HUD initialises **Then** lives counter shows 5, golden ball slot shows uncollected (or collected if already saved), fire timer is hidden
2. **Given** the player loses a life **When** `OnBallStateChanged` fires with Dead state **Then** the lives counter decrements immediately and visibly
3. **Given** the fire ability is activated **When** `OnFireAbilityActivated` SO event fires **Then** the fire timer appears and counts down for the configured duration (from `LevelConfig`); it disappears when the ability expires
4. **Given** the golden ball is collected in-level **When** `OnGoldenBallCollected` fires **Then** the golden ball slot updates to collected state immediately
5. **Given** `RestartLevel()` is called **When** the HUD resets **Then** lives counter returns to its current preserved value (not reset to 5), fire timer is hidden, golden ball slot reflects save state
6. **Given** the HUD is driven by events **When** its MonoBehaviour lifecycle is inspected **Then** all SO event subscriptions are in `OnEnable()` and unsubscriptions are in `OnDisable()` — no memory leaks

## Tasks / Subtasks

- [ ] Task 1: Create `IHUDService` interface (AC: 1, 5)
  - [ ] Create `Assets/_Project/Scripts/UI/IHUDService.cs`:
    ```csharp
    public interface IHUDService
    {
        /// <summary>
        /// Called by LevelManager.StartLevel() on both fresh start and retry.
        /// Resets fire timer and golden ball slot; reads current lives from ILevelManager.
        /// </summary>
        void Reset(LevelConfigSO levelConfig);
    }
    ```
  - [ ] `LevelManager.StartLevel()` (Story 2.2) has a `// TODO: ServiceLocator.Get<IHUDService>().Reset(CurrentLevel)` stub — wire it now:
    ```csharp
    // Remove the TODO comment and replace with:
    ServiceLocator.Get<IHUDService>().Reset(CurrentLevel);
    ```

- [ ] Task 2: Create `HUDController` MonoBehaviour (AC: 1–6)
  - [ ] Create `Assets/_Project/Scripts/UI/HUDController.cs`:
    ```csharp
    public class HUDController : MonoBehaviour, IHUDService
    {
        [Header("Lives")]
        [SerializeField] private TMP_Text _livesText;

        [Header("Golden Ball")]
        [SerializeField] private GameObject _goldenBallUncollected;
        [SerializeField] private GameObject _goldenBallCollected;

        [Header("Fire Timer")]
        [SerializeField] private GameObject _fireTimerContainer;
        [SerializeField] private TMP_Text   _fireTimerText;

        [Header("SO Events")]
        [SerializeField] private GameEventSO _onLivesChanged;
        [SerializeField] private GameEventSO _onGoldenBallCollected;
        [SerializeField] private GameEventSO _onFireAbilityActivated;
        [SerializeField] private GameEventSO _onFireAbilityDeactivated;
        [SerializeField] private GameEventSO _onLevelCompleted;

        private float _fireTimerEndTime;
        private bool  _fireActive;
        private float _currentLevelFireDuration;

        private void Awake() => ServiceLocator.Register<IHUDService>(this);

        private void OnEnable()
        {
            _onLivesChanged.AddListener(RefreshLives);
            _onGoldenBallCollected.AddListener(ShowGoldenBallCollected);
            _onFireAbilityActivated.AddListener(OnFireActivated);
            _onFireAbilityDeactivated.AddListener(OnFireDeactivated);
            _onLevelCompleted.AddListener(OnLevelCompleted);
        }

        private void OnDisable()
        {
            _onLivesChanged.RemoveListener(RefreshLives);
            _onGoldenBallCollected.RemoveListener(ShowGoldenBallCollected);
            _onFireAbilityActivated.RemoveListener(OnFireActivated);
            _onFireAbilityDeactivated.RemoveListener(OnFireDeactivated);
            _onLevelCompleted.RemoveListener(OnLevelCompleted);
        }

        // IHUDService — called by LevelManager.StartLevel() on fresh start AND retry
        public void Reset(LevelConfigSO levelConfig)
        {
            _currentLevelFireDuration = levelConfig.fireAbilityDuration;

            // Lives: read current value from LevelManager (5 on fresh start, preserved on retry)
            RefreshLives();

            // Golden ball: reflect save state for current level
            RefreshGoldenBallSlot();

            // Fire timer: always hidden on reset
            _fireActive = false;
            _fireTimerContainer.SetActive(false);
        }

        private void Update()
        {
            if (!_fireActive) return;

            float remaining = _fireTimerEndTime - Time.time;
            if (remaining < 0f) remaining = 0f;
            _fireTimerText.text = Mathf.CeilToInt(remaining).ToString();
        }

        // --- Event handlers ---

        private void RefreshLives()
        {
            int lives = ServiceLocator.Get<ILevelManager>().CurrentLives;
            _livesText.text = lives.ToString();
        }

        private void RefreshGoldenBallSlot()
        {
            var levelManager = ServiceLocator.Get<ILevelManager>();
            if (levelManager.CurrentLevel == null) return;

            var save = ServiceLocator.Get<ISaveService>();
            bool collected = save.GetLevelData(levelManager.CurrentLevel.levelId).goldenBallCollected;
            SetGoldenBallVisual(collected);
        }

        private void ShowGoldenBallCollected() => SetGoldenBallVisual(true);

        private void SetGoldenBallVisual(bool collected)
        {
            _goldenBallUncollected.SetActive(!collected);
            _goldenBallCollected.SetActive(collected);
        }

        private void OnFireActivated()
        {
            _fireActive = true;
            _fireTimerEndTime = Time.time + _currentLevelFireDuration;
            _fireTimerContainer.SetActive(true);
        }

        private void OnFireDeactivated()
        {
            _fireActive = false;
            _fireTimerContainer.SetActive(false);
        }

        private void OnLevelCompleted()
        {
            // Hide fire timer on level complete — rank reveal takes over
            _fireActive = false;
            _fireTimerContainer.SetActive(false);
        }
    }
    ```
  - [ ] Place `HUDController` on a root `HUD` GameObject inside the GameScene Canvas
  - [ ] Wire all `[SerializeField]` references in Inspector

- [ ] Task 3: Remove `GoldenBallHUDSlot` and absorb into `HUDController` (AC: 4)
  - [ ] Open the GameScene Canvas and locate the `GoldenBallHUDSlot` component added in Story 3.1
  - [ ] `HUDController` now owns the golden ball slot logic — `GoldenBallHUDSlot.cs` is superseded
  - [ ] Remove `GoldenBallHUDSlot` from the GameScene Canvas
  - [ ] Reassign the uncollected/collected child GameObjects to `HUDController`'s `_goldenBallUncollected` / `_goldenBallCollected` fields in the Inspector
  - [ ] Delete `Assets/_Project/Scripts/UI/GoldenBallHUDSlot.cs` — it is no longer needed
  - [ ] CRITICAL: Ensure `GoldenBallHUDSlot.cs` is not referenced from any prefab or other script before deleting

- [ ] Task 4: Build HUD Canvas layout in GameScene (AC: 1–5)
  - [ ] Open `Assets/_Project/Scenes/GameScene.unity`
  - [ ] Add `Canvas` (Screen Space — Overlay, reference resolution 390×844) if not already present
  - [ ] Inside Canvas, create root `HUD` GameObject with `HUDController` component
  - [ ] **Lives area** (top-left):
    - `GameObject: LivesContainer` — horizontal layout group
    - `TMP_Text: LivesText` — "5", font size 36, bold
    - Optional: small heart icon `Image` to the left (placeholder white square is fine)
  - [ ] **Golden ball slot** (top-right):
    - `GameObject: GoldenBallSlot`
    - `GameObject: GoldenBallUncollected` — grey circle Image (placeholder)
    - `GameObject: GoldenBallCollected` — gold-filled circle Image (placeholder)
    - `GoldenBallUncollected` active by default; `GoldenBallCollected` inactive by default
  - [ ] **Fire timer** (bottom-centre, above pause button):
    - `GameObject: FireTimerContainer` — inactive by default
    - `TMP_Text: FireTimerText` — large countdown number, warm orange colour
    - Optional: fire icon Image to the left (placeholder)
  - [ ] Anchor all HUD elements inside safe area (use `SafeAreaPanel` from Unity's device simulator or add top/bottom padding of 44pt manually for iPhone notch)
  - [ ] Use `[Header]` attributes on HUD GameObjects for clarity in Hierarchy

- [ ] Task 5: Update `ILevelManager` to expose `CurrentLives` (AC: 1, 2, 5)
  - [ ] Open `Assets/_Project/Scripts/Core/ILevelManager.cs`
  - [ ] Add:
    ```csharp
    int CurrentLives { get; }
    ```
  - [ ] `LevelManager.cs` (Story 2.5) already has `public int CurrentLives => _currentLives;` — the interface just needs the declaration added
  - [ ] `HUDController.RefreshLives()` reads this property via `ServiceLocator.Get<ILevelManager>().CurrentLives`

- [ ] Task 6: Wire all Inspector references (AC: 1–6)
  - [ ] `HUDController` on GameScene Canvas → HUD GameObject:
    - `_livesText` → LivesText TMP_Text
    - `_goldenBallUncollected` → GoldenBallUncollected GameObject
    - `_goldenBallCollected` → GoldenBallCollected GameObject
    - `_fireTimerContainer` → FireTimerContainer GameObject
    - `_fireTimerText` → FireTimerText TMP_Text
    - `_onLivesChanged` → `OnLivesChanged.asset`
    - `_onGoldenBallCollected` → `OnGoldenBallCollected.asset`
    - `_onFireAbilityActivated` → `OnFireAbilityActivated.asset`
    - `_onFireAbilityDeactivated` → `OnFireAbilityDeactivated.asset`
    - `_onLevelCompleted` → `OnLevelCompleted.asset`

- [ ] Task 7: Play Mode test checklist (AC: 1–6)
  - [ ] Level starts → lives shows 5, golden ball shows uncollected, fire timer hidden
  - [ ] Ball dies → lives counter decrements (trigger `OnBallDead` manually if needed via `BallStateManager`)
  - [ ] Replay level where golden ball was already collected → slot shows collected on load
  - [ ] Fire ability activated (trigger `OnFireAbilityActivated` manually) → timer appears, counts down, hides on deactivation
  - [ ] Retry after 1 death → lives shows 4 (not 5), fire timer hidden, golden ball slot correct
  - [ ] Verify `HUDController.Reset()` is called by `LevelManager.StartLevel()` each time

## Dev Notes

### `HUDController` Registration — GameScene Lifecycle

`HUDController` is on the GameScene (always additively loaded, never unloaded). Its `Awake()` registers `IHUDService`:

```csharp
private void Awake() => ServiceLocator.Register<IHUDService>(this);
```

This runs once when GameScene loads. `LevelManager.StartLevel()` is called after level scenes load (after GameScene is already present), so the registration is always in place when `Reset()` is called. No race condition.

If `ServiceLocator.Register` throws on duplicate registration (i.e. it was already registered from a previous call), ensure `ServiceLocator` has an `Overwrite` mode or remove-then-re-register pattern. Since `HUDController.Awake()` only runs once, this should not be an issue.

### Fire Timer — `Time.time` vs `Time.unscaledTime`

The fire timer uses `Time.time`:

```csharp
_fireTimerEndTime = Time.time + _currentLevelFireDuration;
// In Update():
float remaining = _fireTimerEndTime - Time.time;
```

`Time.time` is affected by `Time.timeScale`. When the pause menu sets `Time.timeScale = 0`, the timer display freezes — correct behaviour. The underlying fire ability duration in `BallStateManager` should also use `Time.time`-based tracking (not `unscaledTime`) so the timer display and actual expiry stay in sync.

If `PauseMenuController` (Story 2.5) uses `Time.timeScale = 0`, confirm `BallStateManager`'s fire ability coroutine uses `WaitForSeconds` (which scales with `timeScale`) not `WaitForSecondsRealtime`.

### `GoldenBallHUDSlot` Supersession

`GoldenBallHUDSlot.cs` was created as a minimal stub in Story 3.1 with the explicit note that Story 3.3 would absorb it. The golden ball slot logic is now inside `HUDController`:

```
Story 3.1: GoldenBallHUDSlot.cs (stub) → Story 3.3: absorbed into HUDController.cs (delete slot file)
```

Verify the GameScene Canvas does not still have a `GoldenBallHUDSlot` component after deletion — Unity will show a Missing Script warning in the Inspector if the component was attached and the script deleted. Remove the component from the GameObject first, then delete the file.

### `RefreshLives()` Pattern — Pull Not Push

`HUDController` does not receive the live count as an event payload. It pulls from `ILevelManager.CurrentLives` when notified:

```csharp
// ✅ Pull on notification
private void RefreshLives() =>
    _livesText.text = ServiceLocator.Get<ILevelManager>().CurrentLives.ToString();

// ❌ Push via event payload (would require GameEventSOInt — unnecessary complexity)
private void RefreshLives(int livesRemaining) => _livesText.text = livesRemaining.ToString();
```

The pull pattern is simpler here because `CurrentLives` is always available via ServiceLocator and the notification fires after the value has already been updated in `LevelManager`.

### AC5 — Retry Lives Preservation

On retry, the call chain is:

```
LevelManager.RestartLevel()
  → _isRetry = true
  → LoadLevelAsync(CurrentLevel)
  → StartLevel()
    → _currentLives unchanged (because _isRetry == true)
    → _onLivesChanged.Raise()
      → HUDController.RefreshLives()
        → reads CurrentLives (e.g. 4) → shows "4"
    → ServiceLocator.Get<IHUDService>().Reset(CurrentLevel)
      → reads CurrentLives → "4" again (idempotent)
```

`HUDController.Reset()` and `RefreshLives()` both read from the same source of truth (`ILevelManager.CurrentLives`) so showing 4 lives on a retry is guaranteed correct without any special case logic in the HUD.

### Safe Area / Notch Handling

For MVP: add 44pt padding at the top and bottom of the Canvas so HUD elements don't sit behind the iPhone notch or home indicator. A quick approach:

```
Canvas RectTransform → top inset: 44px, bottom inset: 34px
```

A more robust approach is a `SafeAreaPanel` component that reads `Screen.safeArea` and adjusts `RectTransform` anchors at runtime. If the project already has one from the Unity Mobile URP template, use it. If not, the manual padding is acceptable for MVP.

### TextMeshPro Dependency

Uses `TMP_Text` (TextMeshPro) per the established convention from Story 2.4. Import TextMeshPro Essentials (Window → TextMeshPro → Import TMP Essential Resources) if the GameScene shows TMP rendering errors.

### Project Structure Notes

- `IHUDService.cs` → `Assets/_Project/Scripts/UI/`
- `HUDController.cs` → `Assets/_Project/Scripts/UI/`
- `GoldenBallHUDSlot.cs` → DELETE (superseded)

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `HUDController.Awake()` registers `IHUDService`
- `GameEventSO` (1.3) — `OnLivesChanged`, `OnGoldenBallCollected`, `OnFireAbilityActivated`, `OnFireAbilityDeactivated`, `OnLevelCompleted`
- `ISaveService` (2.1) — `RefreshGoldenBallSlot()` reads `goldenBallCollected` from save
- `LevelConfigSO` (2.2) — `fireAbilityDuration` drives timer duration
- `ILevelManager` (2.2 + 2.5) — `CurrentLevel` and `CurrentLives` read via ServiceLocator
- `OnLivesChanged.asset` (2.5) — raised by `LevelManager.HandleBallDeath()`
- `OnGoldenBallCollected.asset` (3.1) — raised by `GoldenBall.OnTriggerEnter()`
- `GoldenBallHUDSlot.cs` (3.1) — superseded and deleted in this story
- `OnFireAbilityActivated.asset` (3.2) — asset created in Story 3.2, raised in Story 4.1
- `OnFireAbilityDeactivated.asset` (3.2) — asset created in Story 3.2, raised in Story 4.1

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (HUD reset in RestartLevel, SO event OnEnable/OnDisable pattern)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 3.3`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
