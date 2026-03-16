# Story 6.3: GameAnalytics Integration

Status: ready-for-dev

## Story

As a developer,
I want analytics events firing throughout all gameplay sessions,
so that I can identify level drop-off, rank distribution, and player behaviour from day one of launch.

## Acceptance Criteria

1. **Given** the app launches **When** the Bootstrap scene initialises **Then** the GameAnalytics SDK initialises; `GameAnalyticsService` registers as `IAnalyticsService` via `ServiceLocator`; SDK failure never crashes the game
2. **Given** a level starts **When** `LevelManager.StartLevel()` is called **Then** `levelStart` event fires with `levelId` from `LevelConfigSO` — never hardcoded
3. **Given** a level is completed **When** the level complete trigger fires **Then** `levelComplete` event fires with `levelId` and `bestRank`
4. **Given** the ball enters Dead state and lives reach 0 **When** the Out of Lives screen is shown **Then** `levelFail` event fires with `levelId` and attempt count
5. **Given** a golden ball is collected **When** `OnGoldenBallCollected` fires **Then** `goldenBallCollected` event fires with `levelId`
6. **Given** the rank reveal screen displays a rank **When** the rank is shown **Then** `rankAchieved` event fires with `levelId` and the session rank value
7. **Given** the rank reveal screen is visible **When** the player taps to continue **Then** `rankScreenDwellTime` event fires with duration in seconds the screen was visible
8. **Given** any analytics call is made **When** the `IAnalyticsService` implementation is inspected **Then** `levelId` is always sourced from `LevelConfigSO` — no hardcoded strings in the analytics layer

## Tasks / Subtasks

- [ ] Task 1: Import and configure GameAnalytics SDK (AC: 1)
  - [ ] Import GameAnalytics SDK from Unity Asset Store (search "GameAnalytics SDK") or download the Unity package from `gameanalytics.com/sdk`
  - [ ] After import: follow SDK setup wizard or add the `GameAnalytics` component to the Bootstrap GameObject
  - [ ] Configure SDK keys in Inspector (or `GameAnalytics.SettingsGA`):
    - `Game Key`: obtained from GameAnalytics dashboard after creating a project
    - `Secret Key`: from the same dashboard
  - [ ] Set platform to iOS in the SDK configuration panel
  - [ ] SDK import does NOT require DOTween Setup or any additional utility run — just import and configure keys

- [ ] Task 2: Create `IAnalyticsService` interface (AC: 2–8)
  - [ ] Create `Assets/_Project/Scripts/Analytics/IAnalyticsService.cs`:
    ```csharp
    public interface IAnalyticsService
    {
        void LogLevelStart(string levelId);
        void LogLevelComplete(string levelId, string bestRank);
        void LogLevelFail(string levelId, int attemptCount);
        void LogGoldenBallCollected(string levelId);
        void LogRankAchieved(string levelId, string rank);
        void LogRankScreenDwellTime(string levelId, float seconds);
    }
    ```

- [ ] Task 3: Create `NullAnalyticsService` — null object fallback (AC: 1)
  - [ ] Create `Assets/_Project/Scripts/Analytics/NullAnalyticsService.cs`:
    ```csharp
    /// <summary>
    /// Registered immediately on Bootstrap. Replaced by GameAnalyticsService on successful SDK init.
    /// If SDK fails, this remains as a no-op implementation — zero crashes, zero analytics.
    /// </summary>
    public class NullAnalyticsService : IAnalyticsService
    {
        public void LogLevelStart(string levelId)                        { }
        public void LogLevelComplete(string levelId, string bestRank)    { }
        public void LogLevelFail(string levelId, int attemptCount)       { }
        public void LogGoldenBallCollected(string levelId)               { }
        public void LogRankAchieved(string levelId, string rank)         { }
        public void LogRankScreenDwellTime(string levelId, float seconds){ }
    }
    ```
  - [ ] `NullAnalyticsService` is a plain C# class (not MonoBehaviour) — no scene object needed

- [ ] Task 4: Create `GameAnalyticsService` MonoBehaviour (AC: 1–8)
  - [ ] Create `Assets/_Project/Scripts/Analytics/GameAnalyticsService.cs`:
    ```csharp
    using GameAnalyticsSDK; // GameAnalytics SDK namespace

    public class GameAnalyticsService : MonoBehaviour, IAnalyticsService
    {
        private bool _sdkReady;

        private void Awake()
        {
            try
            {
                GameAnalytics.Initialize();
                _sdkReady = true;
                // Override the NullAnalyticsService registered by BootstrapManager
                ServiceLocator.Register<IAnalyticsService>(this);
                Debug.Log("GameAnalyticsService: SDK initialised successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"GameAnalyticsService: SDK init failed — using NullAnalyticsService. Error: {e.Message}");
                // NullAnalyticsService remains registered — no crash, no analytics
            }
        }

        public void LogLevelStart(string levelId)
        {
            if (!_sdkReady) return;
            TryLog(() => GameAnalytics.NewProgressionEvent(
                GAProgressionStatus.Start, levelId));
        }

        public void LogLevelComplete(string levelId, string bestRank)
        {
            if (!_sdkReady) return;
            TryLog(() => GameAnalytics.NewProgressionEvent(
                GAProgressionStatus.Complete, levelId));
            TryLog(() => GameAnalytics.NewDesignEvent(
                $"rank:achieved:{bestRank}:{levelId}"));
        }

        public void LogLevelFail(string levelId, int attemptCount)
        {
            if (!_sdkReady) return;
            TryLog(() => GameAnalytics.NewProgressionEvent(
                GAProgressionStatus.Fail, levelId, attemptCount));
        }

        public void LogGoldenBallCollected(string levelId)
        {
            if (!_sdkReady) return;
            TryLog(() => GameAnalytics.NewDesignEvent(
                $"goldenBall:collected:{levelId}"));
        }

        public void LogRankAchieved(string levelId, string rank)
        {
            if (!_sdkReady) return;
            TryLog(() => GameAnalytics.NewDesignEvent(
                $"rank:session:{rank}:{levelId}"));
        }

        public void LogRankScreenDwellTime(string levelId, float seconds)
        {
            if (!_sdkReady) return;
            TryLog(() => GameAnalytics.NewDesignEvent(
                $"rankScreen:dwell:{levelId}", seconds));
        }

        private static void TryLog(System.Action analyticsCall)
        {
            try   { analyticsCall(); }
            catch (System.Exception e)
            {
                // Individual event failures never surface to player
                Debug.LogWarning($"GameAnalyticsService: event failed — {e.Message}");
            }
        }
    }
    ```
  - [ ] Add `GameAnalyticsService` component to Bootstrap GameObject
  - [ ] NOTE: `ServiceLocator.Register<IAnalyticsService>` must support overwriting — check that `Register<T>` allows re-registration (replacing `NullAnalyticsService`). Update `ServiceLocator.Register` if needed:
    ```csharp
    public static void Register<T>(T instance) where T : class
    {
        _services[typeof(T)] = instance; // overwrite — allows NullAnalyticsService → GameAnalyticsService
    }
    ```

- [ ] Task 5: Register `NullAnalyticsService` in `BootstrapManager` (AC: 1)
  - [ ] Open `Assets/_Project/Scripts/Core/BootstrapManager.cs`
  - [ ] In `Awake()`, register `NullAnalyticsService` BEFORE `GameAnalyticsService.Awake()` runs:
    ```csharp
    // Register null analytics first — GameAnalyticsService.Awake() will overwrite if SDK succeeds
    ServiceLocator.Register<IAnalyticsService>(new NullAnalyticsService());
    ```
  - [ ] Ensure `BootstrapManager.Awake()` runs before `GameAnalyticsService.Awake()` via Unity's Script Execution Order:
    - `Edit → Project Settings → Script Execution Order`
    - Add `BootstrapManager`: order `-100`
    - Add `GameAnalyticsService`: order `-50`
    - This guarantees null service is registered before SDK attempts override

- [ ] Task 6: Wire `levelStart` event (AC: 2)
  - [ ] Open `Assets/_Project/Scripts/Core/LevelManager.cs`
  - [ ] In `StartLevel()`, replace the existing TODO stub:
    ```csharp
    // BEFORE (stub from Story 2.2):
    // TODO: ServiceLocator.Get<IAnalyticsService>().LogLevelStart(CurrentLevel.levelId);

    // AFTER:
    ServiceLocator.Get<IAnalyticsService>().LogLevelStart(CurrentLevel.levelId);
    ```
  - [ ] `CurrentLevel.levelId` is set before `StartLevel()` is called — always valid and always from `LevelConfigSO`
  - [ ] Note: `levelStart` fires on EVERY call to `StartLevel()` — including retries. Retry analytics are correct behaviour: each attempt is a fresh start event. GameAnalytics progression events are designed to handle multiple starts per level.

- [ ] Task 7: Wire `levelComplete` event (AC: 3)
  - [ ] Open `Assets/_Project/Scripts/Core/LevelManager.cs`
  - [ ] In `HandleLevelCompleted()`, replace the TODO stub:
    ```csharp
    // AFTER saving levelData.completed = true:
    // BEFORE (stub from Story 2.5 / 3.4):
    // TODO: ServiceLocator.Get<IAnalyticsService>().LogLevelComplete(CurrentLevel.levelId);

    // AFTER:
    var levelData = save.GetLevelData(CurrentLevel.levelId); // already retrieved
    ServiceLocator.Get<IAnalyticsService>()
        .LogLevelComplete(CurrentLevel.levelId, levelData.bestRank);
    ```
  - [ ] `bestRank` is written by `StyleRankTracker.OnLevelCompleted()` which fires on the same `OnLevelCompleted` event. Execution order of SO event listeners is not guaranteed — retrieve `bestRank` AFTER saving in `LevelManager.HandleLevelCompleted()` where the save is already done
  - [ ] Tutorial level (`biome1_tutorial`): `levelComplete` still fires — useful to know tutorial completion rate. `bestRank` will be `"E"` (default) since `StyleRankTracker` skips tutorial scoring

- [ ] Task 8: Wire `levelFail` event (AC: 4)
  - [ ] Open `Assets/_Project/Scripts/Core/LevelManager.cs`
  - [ ] In `ShowOutOfLivesCoroutine()`, add analytics call after saving attempt data:
    ```csharp
    private IEnumerator ShowOutOfLivesCoroutine()
    {
        var save      = ServiceLocator.Get<ISaveService>();
        var levelData = save.GetLevelData(CurrentLevel.levelId);
        save.UpdateLevelData(CurrentLevel.levelId, levelData);

        // Fire levelFail analytics — attempt count is now saved
        ServiceLocator.Get<IAnalyticsService>()
            .LogLevelFail(CurrentLevel.levelId, levelData.attemptCount);

        yield return new WaitForSeconds(0.8f);
        _outOfLivesScreen.Show(...);
    }
    ```
  - [ ] `levelFail` fires AFTER save so attempt count is accurate
  - [ ] GameAnalytics `GAProgressionStatus.Fail` with attempt count maps neatly to their progression system

- [ ] Task 9: Wire `goldenBallCollected` event (AC: 5)
  - [ ] Open `Assets/_Project/Scripts/Progression/GoldenBallCollectionHandler.cs`
  - [ ] After saving `goldenBallCollected = true`, add:
    ```csharp
    private void HandleGoldenBallCollected()
    {
        // ... existing save code ...

        // Analytics — always sourced from CurrentLevel.levelId, never hardcoded
        ServiceLocator.Get<IAnalyticsService>().LogGoldenBallCollected(levelId);
    }
    ```
  - [ ] The idempotency guard `if (levelData.goldenBallCollected) return;` is already in place — analytics only fires once per collection (no duplicates on replay)

- [ ] Task 10: Wire `rankAchieved` and `rankScreenDwellTime` events (AC: 6, 7)
  - [ ] Open `Assets/_Project/Scripts/UI/RankRevealScreen.cs`
  - [ ] In `ShowRankReveal()`, after retrieving rank from `StyleRankTracker`, add:
    ```csharp
    private void ShowRankReveal()
    {
        // ... existing code retrieving rank, gotBall, hasSeen ...

        StyleRank rank  = tracker.LastRank;
        string levelId  = ServiceLocator.Get<ILevelManager>().CurrentLevel.levelId;

        // Fire rankAchieved immediately when reveal starts
        ServiceLocator.Get<IAnalyticsService>()
            .LogRankAchieved(levelId, rank.ToString()); // "A", "B", "C", "D", "E"

        // ... existing DOTween sequence ...
    }
    ```
  - [ ] In `OnTapToContinue()`, replace the TODO stub:
    ```csharp
    // BEFORE (stub from Story 3.4):
    // TODO: ServiceLocator.Get<IAnalyticsService>().LogRankScreenDwellTime(levelId, dwellTime);

    // AFTER:
    ServiceLocator.Get<IAnalyticsService>()
        .LogRankScreenDwellTime(levelId, dwellTime);
    ```
  - [ ] `rankAchieved` does NOT fire for tutorial level — `RankRevealScreen.Show()` routes tutorials to `ShowTutorialComplete()`, bypassing `ShowRankReveal()`. No guard needed.

- [ ] Task 11: Validate — no hardcoded levelId strings (AC: 8)
  - [ ] Run a project-wide search for hardcoded level IDs in analytics calls:
    ```
    Search pattern: LogLevel|LogGoldenBall|LogRank
    Expected: all calls pass levelId from CurrentLevel.levelId or local string variable
    Fail if: any string literal like "biome1_level01" appears in analytics call arguments
    ```
  - [ ] Add a comment to each analytics call site:
    ```csharp
    // Analytics: levelId always sourced from LevelConfigSO.levelId — see ADR-003
    ServiceLocator.Get<IAnalyticsService>().LogLevelStart(CurrentLevel.levelId);
    ```

- [ ] Task 12: Play Mode test checklist (AC: 1–8)
  - [ ] App launches → no crash even if SDK keys are invalid or blank
  - [ ] Level starts → verify `levelStart` call in console (add `Debug.Log` temporarily in `LogLevelStart`)
  - [ ] Level completed → `levelComplete` fires with correct levelId and bestRank
  - [ ] Out of lives → `levelFail` fires with correct attempt count
  - [ ] Golden ball collected → `goldenBallCollected` fires once; NOT on replay if already collected
  - [ ] Rank reveal shows → `rankAchieved` fires with session rank (may differ from best rank)
  - [ ] Player taps continue → `rankScreenDwellTime` fires with plausible duration (≥ sequence length)
  - [ ] Tutorial level complete → `levelComplete` fires; `rankAchieved` does NOT fire
  - [ ] Verify GameAnalytics dashboard receives events (requires real SDK keys and network access)

## Dev Notes

### Null Object Pattern — `NullAnalyticsService` Is the Default

The registration sequence guarantees analytics calls never throw:

```
BootstrapManager.Awake() (order -100):
  → ServiceLocator.Register<IAnalyticsService>(new NullAnalyticsService())

GameAnalyticsService.Awake() (order -50):
  → GameAnalytics.Initialize() succeeds
    → ServiceLocator.Register<IAnalyticsService>(this)  ← overwrite
  → GameAnalytics.Initialize() fails
    → NullAnalyticsService remains  ← no-op, no crash
```

This pattern eliminates `try/catch` from every call site. The implementation (GameAnalytics or Null) absorbs all failures internally.

### `ServiceLocator.Register` Must Allow Overwriting

```csharp
// Ensure ServiceLocator uses dictionary overwrite, not "throw if already registered":
public static void Register<T>(T instance) where T : class
{
    _services[typeof(T)] = instance; // Dictionary[] assignment overwrites silently ✓
}
```

If `Register` throws on duplicate type, the `NullAnalyticsService → GameAnalyticsService` replacement will crash. Verify this is not the case in `ServiceLocator.cs` from Story 1.1.

### GA Event Design — Naming Convention

```
Progression events (levelStart, levelComplete, levelFail):
  → GameAnalytics.NewProgressionEvent(status, levelId)
  → Tracked in GA dashboard under Progression funnel

Design events (goldenBall, rank, dwell):
  → GameAnalytics.NewDesignEvent("category:action:levelId", [value])
  → Naming: colon-separated hierarchy — GA groups by prefix

Examples:
  "goldenBall:collected:biome1_level01"
  "rank:session:A:biome1_level01"
  "rank:achieved:B:biome1_level01"      (bestRank written on levelComplete)
  "rankScreen:dwell:biome1_level01"     value = seconds
```

Do NOT use spaces in event strings — GameAnalytics event IDs are colon-separated hierarchies. Spaces are allowed but break dashboard grouping.

### `levelStart` Fires on Retry — This Is Correct

Each `StartLevel()` call (fresh or retry) fires `levelStart`. This is the correct analytics behaviour:
- Enables funnel analysis: how many starts does each level need before completion?
- Pairs with `levelComplete` and `levelFail` to compute per-level attempt distribution
- GameAnalytics progression events are designed for multiple starts per level

Do NOT add a guard to skip analytics on retry — you'd lose the most valuable data point (retry rate per level).

### `levelComplete` and `bestRank` — Post-Save Read

The `HandleLevelCompleted()` call order matters:

```
1. save.UpdateLevelData(levelId, levelData with completed=true)    ← save
2. ServiceLocator.Get<IAnalyticsService>().LogLevelComplete(...)   ← analytics reads bestRank
```

`StyleRankTracker.OnLevelCompleted()` also subscribes to `OnLevelCompleted` and writes `bestRank`. Since SO event listener order is not guaranteed, `LogLevelComplete` reads `levelData.bestRank` AFTER `HandleLevelCompleted()` has completed its own save. If `StyleRankTracker` fires AFTER `HandleLevelCompleted()` in the same event, `bestRank` in the analytics call might be the PRIOR session's best (not the just-calculated rank).

**Safer approach**: Use `StyleRankTracker.LastRank.ToString()` instead of reading from save:
```csharp
// In HandleLevelCompleted():
string sessionRank = "";
if (ServiceLocator.TryGet<IStyleRankTracker>(out var tracker))
    sessionRank = tracker.LastRank.ToString();

ServiceLocator.Get<IAnalyticsService>().LogLevelComplete(CurrentLevel.levelId, sessionRank);
```

This uses the in-memory rank from the current session — always accurate regardless of listener ordering. Note: tutorial level will have `StyleRank.E` (default) since tracker returns early. That's acceptable.

### `rankAchieved` vs `levelComplete.bestRank` — Two Different Events

| Event | Value | Meaning |
|-------|-------|---------|
| `levelComplete` | `bestRank` (historical best) | "What's the best rank ever on this level?" |
| `rankAchieved` | `tracker.LastRank` (session) | "What rank did the player get THIS attempt?" |

`rankAchieved` is more useful for rank distribution analysis — you want to know what ranks players are earning each session, not just their career best. Both are valuable; they answer different questions.

### SDK Keys — Never Commit to Version Control

```
❌ Do NOT commit actual gameKey / secretKey to Git
✅ Set them in the GameAnalytics SDK Inspector window (stored in .asset files)
✅ Add GameAnalytics-generated files to .gitignore if they contain keys
✅ Or: use environment variables via a build script
```

The `.gitignore` from Story 1.1 (Git LFS setup) should already exclude IDE-specific files. Verify that `GameAnalytics.prefab` or settings assets don't contain raw keys before pushing to a public repo.

### iOS Build — GameAnalytics Requires `NSUserTrackingUsageDescription`

For iOS 14+ App Tracking Transparency compliance:
- GameAnalytics SDK may require `NSUserTrackingUsageDescription` in `Info.plist`
- Unity iOS build settings: `Other Settings → Identification → iOS`
- Set the usage description: "This app uses analytics to improve gameplay."
- Or configure in the GameAnalytics SDK settings panel under "Privacy"

Check the GameAnalytics SDK documentation for the version imported — requirements vary by SDK version.

### Launch Readiness — All Analytics TODOs Resolved

This story closes the last set of `// TODO: Analytics` comments across the codebase:

| File | TODO Location | Resolved |
|------|--------------|---------|
| `LevelManager.cs` | `StartLevel()` | Task 6 ✓ |
| `LevelManager.cs` | `HandleLevelCompleted()` | Task 7 ✓ |
| `LevelManager.cs` | `ShowOutOfLivesCoroutine()` | Task 8 ✓ |
| `GoldenBallCollectionHandler.cs` | `HandleGoldenBallCollected()` | Task 9 ✓ |
| `RankRevealScreen.cs` | `ShowRankReveal()` | Task 10 ✓ |
| `RankRevealScreen.cs` | `OnTapToContinue()` | Task 10 ✓ |

After this story, all 21 stories in the sprint are `ready-for-dev` and the codebase architecture stubs are fully specified.

### Project Structure Notes

- `IAnalyticsService.cs` → `Assets/_Project/Scripts/Analytics/`
- `NullAnalyticsService.cs` → `Assets/_Project/Scripts/Analytics/`
- `GameAnalyticsService.cs` → `Assets/_Project/Scripts/Analytics/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `Register<T>` overwrite behaviour confirmed; `TryGet<T>` (6.2) used for `IStyleRankTracker`
- `LevelManager` (2.2/2.5) — `StartLevel()`, `HandleLevelCompleted()`, `ShowOutOfLivesCoroutine()` wired
- `GoldenBallCollectionHandler` (3.1) — `HandleGoldenBallCollected()` wired
- `StyleRankTracker` (3.2) — `LastRank.ToString()` used in `levelComplete` for session rank
- `RankRevealScreen` (3.4) — `ShowRankReveal()` and `OnTapToContinue()` wired; dwell time already captured

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md` (FR18: GameAnalytics events)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-003` (levelId sourcing rule)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 6.3`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
