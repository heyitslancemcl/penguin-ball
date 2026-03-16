# Story 2.5: Level Flow & Retry

Status: ready-for-dev

## Story

As a player,
I want a complete loop of starting a level with 5 lives, dying, retrying, and either completing the level or being sent back to level select when all lives are spent,
so that the core game loop feels complete and there is meaningful consequence to failure.

## Acceptance Criteria

1. **Given** a level starts **When** `LevelManager.StartLevel()` is called **Then** the player begins with 5 lives; the HUD displays the lives counter at 5; the attempt counter increments in save data
2. **Given** the ball enters the Dead state and lives remain **When** death is confirmed **Then** the death animation plays, lives decrement by 1, and a retry prompt is shown with the updated lives count visible
3. **Given** the player taps retry with lives remaining **When** `RestartLevel()` is called **Then** the HUD resets explicitly (lives count preserved), the ball respawns at the level start position, and the gyroscope baseline is recaptured
4. **Given** the ball enters the Dead state and lives reach 0 **When** death is confirmed **Then** an "Out of lives" screen is shown with no retry option; the player can only return to level select; total attempts are saved
5. **Given** the player completes a level **When** the level complete trigger fires **Then** `completed: true` and remaining lives count are written to save data, save triggers, and the rank reveal screen stub is shown
6. **Given** the player exits to level select mid-level **When** the exit is confirmed **Then** the current attempt is recorded, save triggers, and the level select screen loads cleanly with no scene leaks

## Tasks / Subtasks

- [ ] Task 1: Implement lives management in `LevelManager` (AC: 1, 2, 3, 4)
  - [ ] Add lives tracking to `LevelManager.cs` (Story 2.2):
    ```csharp
    private int _currentLives;
    private int _maxLives => CurrentLevel?.livesCount ?? 5;
    ```
  - [ ] In `StartLevel()` (already exists from Story 2.2), add:
    ```csharp
    _currentLives = _maxLives; // reset to 5 on fresh level start
    _onLivesChanged.Raise(); // SO event — HUD subscribes in Story 3.3
    ```
  - [ ] Add `[SerializeField] private GameEventSO _onLivesChanged;` — create asset below
  - [ ] Add `public int CurrentLives => _currentLives;` read-only property
  - [ ] Subscribe to `OnBallDead` SO event in `OnEnable()`/`OnDisable()`:
    ```csharp
    private void OnEnable()  => _onBallDead.AddListener(HandleBallDeath);
    private void OnDisable() => _onBallDead.RemoveListener(HandleBallDeath);
    ```
  - [ ] `HandleBallDeath()`:
    ```csharp
    private void HandleBallDeath()
    {
        _currentLives--;
        _onLivesChanged.Raise();

        if (_currentLives <= 0)
            StartCoroutine(ShowOutOfLivesCoroutine());
        else
            StartCoroutine(ShowDeathRetryCoroutine());
    }
    ```

- [ ] Task 2: Implement death → retry flow (AC: 2, 3)
  - [ ] Create `Assets/_Project/Scripts/UI/DeathRetryScreen.cs` (MonoBehaviour)
  - [ ] UI elements:
    - "You died!" or similar text
    - Lives remaining display (e.g. "Lives remaining: 3")
    - "Retry" button
    - "Exit to Menu" button
  - [ ] `Show(int livesRemaining, Action onRetry, Action onExit)`:
    ```csharp
    public void Show(int livesRemaining, Action onRetry, Action onExit)
    {
        gameObject.SetActive(true);
        _livesText.text = $"Lives remaining: {livesRemaining}";
        _retryButton.onClick.AddListener(() => { Hide(); onRetry(); });
        _exitButton.onClick.AddListener(() => { Hide(); onExit(); });
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        _retryButton.onClick.RemoveAllListeners();
        _exitButton.onClick.RemoveAllListeners();
    }
    ```
  - [ ] `ShowDeathRetryCoroutine()` in `LevelManager`:
    ```csharp
    private IEnumerator ShowDeathRetryCoroutine()
    {
        yield return new WaitForSeconds(0.8f); // brief pause after death arc
        _deathRetryScreen.Show(
            _currentLives,
            onRetry: () => RestartLevel(),
            onExit: () => ExitToLevelSelect()
        );
    }
    ```
  - [ ] `RestartLevel()` — existing stub from Story 2.2, now fully implemented:
    ```csharp
    public void RestartLevel()
    {
        _deathRetryScreen.Hide();
        // Reload the level scene (lives preserved — NOT reset)
        StartCoroutine(LoadLevelAsync(CurrentLevel));
        // StartLevel() will be called after scene loads, which:
        // - recalibrates gyroscope
        // - increments attemptCount in save
        // - fires OnLivesChanged with preserved count
        // NOTE: lives are NOT reset in RestartLevel — only in fresh StartLevel
    }
    ```
  - [ ] CRITICAL: `RestartLevel()` must NOT reset lives to 5 — only a fresh `LoadLevel()` call resets lives. Retry preserves the current `_currentLives` value.
  - [ ] Fix `StartLevel()` to distinguish fresh start vs retry:
    ```csharp
    private bool _isRetry = false;

    public void LoadLevel(LevelConfigSO config)
    {
        _isRetry = false; // fresh load always resets lives
        CurrentLevel = config;
        StartCoroutine(LoadLevelAsync(config));
    }

    public void RestartLevel()
    {
        _isRetry = true; // retry preserves lives
        StartCoroutine(LoadLevelAsync(CurrentLevel));
    }

    private void StartLevel()
    {
        if (!_isRetry) _currentLives = _maxLives; // only reset on fresh load
        _onLivesChanged.Raise();
        // ... rest of StartLevel from Story 2.2
    }
    ```

- [ ] Task 3: Implement "Out of Lives" screen (AC: 4)
  - [ ] Create `Assets/_Project/Scripts/UI/OutOfLivesScreen.cs` (MonoBehaviour)
  - [ ] UI elements:
    - "Out of lives!" text
    - Total attempts display (e.g. "Attempts: 12")
    - "Back to Menu" button (ONLY option — no retry)
  - [ ] `Show(int totalAttempts, Action onExit)`:
    ```csharp
    public void Show(int totalAttempts, Action onExit)
    {
        gameObject.SetActive(true);
        _attemptsText.text = $"Attempts: {totalAttempts}";
        _exitButton.onClick.AddListener(() => { Hide(); onExit(); });
    }
    ```
  - [ ] `ShowOutOfLivesCoroutine()` in `LevelManager`:
    ```csharp
    private IEnumerator ShowOutOfLivesCoroutine()
    {
        // Save attempts before showing screen
        var save = ServiceLocator.Get<ISaveService>();
        var levelData = save.GetLevelData(CurrentLevel.levelId);
        save.UpdateLevelData(CurrentLevel.levelId, levelData); // triggers Save()

        yield return new WaitForSeconds(0.8f);
        _outOfLivesScreen.Show(
            levelData.attemptCount,
            onExit: () => LoadLevelSelect()
        );
    }
    ```

- [ ] Task 4: Implement level complete flow (AC: 5)
  - [ ] Create `Assets/_Project/Scripts/Core/LevelCompleteZone.cs` (MonoBehaviour)
  - [ ] `OnTriggerEnter(Collider other)`: check for Ball tag → raise `OnLevelCompleted` SO event
  - [ ] Place `LevelCompleteZone` trigger in each level scene at the exit point
  - [ ] `LevelManager` subscribes to `OnLevelCompleted` in `OnEnable()`/`OnDisable()`:
    ```csharp
    private void HandleLevelCompleted()
    {
        // 1. Update save data
        var save = ServiceLocator.Get<ISaveService>();
        var levelData = save.GetLevelData(CurrentLevel.levelId);
        levelData.completed = true;
        save.UpdateLevelData(CurrentLevel.levelId, levelData); // triggers Save()

        // 2. Fire analytics levelComplete event (stub — Story 6.3)
        // TODO: ServiceLocator.Get<IAnalyticsService>().LogLevelComplete(CurrentLevel.levelId);

        // 3. Show rank reveal screen (stub — Story 3.4)
        // TODO: ServiceLocator.Get<IRankRevealService>().Show(rank, score, goldenBallCollected);

        // 4. For now: show a simple "Level Complete!" overlay then return to level select
        StartCoroutine(LevelCompleteStubCoroutine());
    }

    private IEnumerator LevelCompleteStubCoroutine()
    {
        _levelCompleteStubText.SetActive(true); // simple "Level Complete!" text
        yield return new WaitForSeconds(2f);
        _levelCompleteStubText.SetActive(false);
        LoadLevelSelect();
    }
    ```
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnLevelCompleted.asset` — already created in Story 2.1; wire `LevelCompleteZone` to raise it

- [ ] Task 5: Implement exit mid-level flow (AC: 6)
  - [ ] Add a pause/exit button to the in-game UI (simple button anchored top-right)
  - [ ] Create `Assets/_Project/Scripts/UI/PauseMenuController.cs`
  - [ ] Pause menu shows: "Resume" and "Exit to Menu" options
  - [ ] On "Exit to Menu" confirmed:
    ```csharp
    private void ExitToLevelSelect()
    {
        var save = ServiceLocator.Get<ISaveService>();
        save.Save(); // save current attempt data before exiting
        ServiceLocator.Get<ILevelManager>().LoadLevelSelect();
    }
    ```
  - [ ] Add recalibration option to pause menu: "Recalibrate Tilt" button → `ServiceLocator.Get<IInputProvider>().Calibrate()` (architecture requirement)
  - [ ] Time.timeScale handling: `Time.timeScale = 0` when paused, `Time.timeScale = 1` on resume — remember to restore on scene unload

- [ ] Task 6: Create required SO events and UI assets (AC: 1, 2, 3, 4)
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnLivesChanged.asset` (GameEventSO)
  - [ ] Create `DeathRetryScreen.prefab`: `Assets/_Project/Prefabs/UI/DeathRetryScreen.prefab`
  - [ ] Create `OutOfLivesScreen.prefab`: `Assets/_Project/Prefabs/UI/OutOfLivesScreen.prefab`
  - [ ] Add `DeathRetryScreen` and `OutOfLivesScreen` to GameScene Canvas (hidden by default)
  - [ ] Wire `[SerializeField]` references in `LevelManager` Inspector:
    - `_onBallDead` → `OnBallDead.asset`
    - `_onLivesChanged` → `OnLivesChanged.asset`
    - `_deathRetryScreen` → DeathRetryScreen component
    - `_outOfLivesScreen` → OutOfLivesScreen component
    - `_levelCompleteStubText` → placeholder "Level Complete!" GameObject

## Dev Notes

### Lives Are Session State — Not Persisted

Lives (`_currentLives`) are held in `LevelManager` memory only. They are NOT written to `SaveData`. When a player exits a level (out of lives or menu exit), their attempt count is saved, but not their remaining lives — the next time they enter the level they start with 5 fresh lives.

The only save-relevant death data is `attemptCount` (incremented on `StartLevel()`) and `completed`/`goldenBallCollected` flags.

### `RestartLevel()` vs `LoadLevel()` — Lives Reset Rule

```
LoadLevel(config)   → isRetry = false → lives RESET to 5
RestartLevel()      → isRetry = true  → lives PRESERVED
```

This is critical. If `RestartLevel()` accidentally resets lives, the player can retry infinitely with 5 lives each time. The `_isRetry` flag passed through to `StartLevel()` is the safeguard.

### Death Timing — 0.8s Delay

The 0.8s delay before showing the death/out-of-lives screen gives the death arc animation time to complete naturally (ball falls off platform, hits kill plane, death state triggers). Adjust this value based on playtest feel — too short feels abrupt, too long feels sluggish.

### Time.timeScale in Pause Menu

```csharp
// ✅ Pause
Time.timeScale = 0f;

// ✅ Resume
Time.timeScale = 1f;

// ⚠️ CRITICAL: Always reset on scene unload
private void OnDestroy() => Time.timeScale = 1f;
```

If the player exits while paused without the `OnDestroy` guard, `Time.timeScale = 0` persists into the level select scene and breaks all animations and UI timers.

### Level Complete Flow — Stub for Story 3.4

The rank reveal screen is fully implemented in Story 3.4. For now, `HandleLevelCompleted()` shows a simple placeholder text for 2 seconds then returns to level select. The TODO comment in the code signals exactly where Story 3.4 will inject the real rank reveal.

Do NOT attempt to pre-implement rank reveal logic here — wait for Story 3.4 which has full context.

### `LevelCompleteZone` Placement

Each level scene needs a `LevelCompleteZone` trigger at the exit. For placeholder scenes (biome1_level01 etc.), place it at a sensible position — a simple box trigger at the "end" of the level. Level design is out of scope but the trigger must exist for the flow to work end-to-end.

### SO Event Subscription Pattern — Review

All subscriptions in `LevelManager` must follow the established pattern:

```csharp
private void OnEnable()
{
    _onBallDead.AddListener(HandleBallDeath);
    _onLevelCompleted.AddListener(HandleLevelCompleted);
}
private void OnDisable()
{
    _onBallDead.RemoveListener(HandleBallDeath);
    _onLevelCompleted.RemoveListener(HandleLevelCompleted);
}
```

`LevelManager` is on Bootstrap (DontDestroyOnLoad), so `OnDisable` fires on app quit — this is correct and expected.

### Project Structure Notes

- `DeathRetryScreen.cs` → `Assets/_Project/Scripts/UI/`
- `OutOfLivesScreen.cs` → `Assets/_Project/Scripts/UI/`
- `PauseMenuController.cs` → `Assets/_Project/Scripts/UI/`
- `LevelCompleteZone.cs` → `Assets/_Project/Scripts/Core/`
- `DeathRetryScreen.prefab` → `Assets/_Project/Prefabs/UI/`
- `OutOfLivesScreen.prefab` → `Assets/_Project/Prefabs/UI/`
- `OnLivesChanged.asset` → `Assets/_Project/ScriptableObjects/Events/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `PauseMenuController` retrieves `IInputProvider` for recalibration
- `IInputProvider` (1.2) — pause menu recalibration button
- `IBallStateManager` (1.3/1.4) — `OnBallDead` event raised by `BallStateManager`
- `ISaveService` (2.1) — attempt count, completion flag written on death/complete/exit
- `ILevelManager` (2.2) — `RestartLevel()`, `LoadLevelSelect()` — expanded here
- `OnBallDead.asset` (1.4) — subscribed by `LevelManager`
- `OnLevelCompleted.asset` (2.1) — subscribed by `LevelManager`, raised by `LevelCompleteZone`

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (HUD reset in RestartLevel)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-001` (recalibration in pause menu)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 2.5`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
