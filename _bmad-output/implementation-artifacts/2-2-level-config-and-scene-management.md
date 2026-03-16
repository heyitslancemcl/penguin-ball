# Story 2.2: Level Config & Scene Management

Status: ready-for-dev

## Story

As a developer,
I want a robust level configuration and scene loading system,
so that each level loads reliably with its correct settings and any loading failure degrades gracefully.

## Acceptance Criteria

1. **Given** a level is defined **When** its `LevelConfigSO` is inspected **Then** it has a non-empty, unique `levelId` in `biome{N}_level{NN}` format; build-time validator rejects duplicates or empty values
2. **Given** a player starts a level **When** `LevelManager` loads the level **Then** the GameScene is loaded additively over the persistent Bootstrap scene using `LoadSceneAsync`
3. **Given** `LoadSceneAsync` fails **When** the error callback fires **Then** the player is returned to the level select screen with no crash
4. **Given** a `LevelConfigSO` is referenced in a scene **When** the Inspector is inspected **Then** it is assigned via serialised Inspector field — `Resources.Load` is never used at runtime
5. **Given** a level loads **When** `LevelManager.StartLevel()` is called **Then** the HUD is explicitly reset, the gyroscope baseline is recaptured, and the `levelStart` analytics event fires (stub for now)

## Tasks / Subtasks

- [ ] Task 1: Expand `LevelConfigSO` with full fields (AC: 1, 4)
  - [ ] Open `Assets/_Project/Scripts/Progression/LevelConfigSO.cs` (created in Story 1.3 as stub)
  - [ ] Add all fields (preserve existing physics fields from Story 1.3):
    ```csharp
    [CreateAssetMenu(menuName = "PenguineBall/LevelConfig")]
    public class LevelConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string levelId;           // e.g. "biome1_level01" — validated at build time
        public string displayName;       // e.g. "Level 1"
        public string sceneName;         // Unity scene name to load (must match Build Settings)

        [Header("Physics (from Story 1.3)")]
        public float maxVelocity = 12f;
        public float goldenBallImpulseMagnitude = 3f;
        public float stuckVelocityThreshold = 0.1f;
        public float stuckDetectionWindow = 2f;

        [Header("Gameplay")]
        public float stunWindowDuration = 1f;
        public int livesCount = 5;
        public int goldenBallsRequiredForBoss = 4; // biome-wide gate

        [Header("Fire Ability")]
        public float fireAbilityDuration = 25f;    // 20-30s range

        [Header("Boss (populated in Story 6.1)")]
        // Phase data added in Story 6.1 — leave empty for now
    }
    ```
  - [ ] Create `LevelConfigSO` assets for all Biome 1 levels:
    - `Assets/_Project/ScriptableObjects/LevelConfigs/biome1_tutorial.asset` (update existing stub)
    - `Assets/_Project/ScriptableObjects/LevelConfigs/biome1_level01.asset`
    - `Assets/_Project/ScriptableObjects/LevelConfigs/biome1_level02.asset`
    - `Assets/_Project/ScriptableObjects/LevelConfigs/biome1_level03.asset`
    - `Assets/_Project/ScriptableObjects/LevelConfigs/biome1_boss.asset`
    - Create placeholder assets for levels 04–12 as needed (even if scenes don't exist yet)
  - [ ] CRITICAL: Every `levelId` field must be set in Inspector — do NOT leave any blank

- [ ] Task 2: Build-time `levelId` uniqueness validator (AC: 1)
  - [ ] Create `Assets/_Project/Scripts/Progression/Editor/LevelIdValidator.cs`
  - [ ] Use `IPreprocessBuildWithReport` Unity build callback:
    ```csharp
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;

    public class LevelIdValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var configs = AssetDatabase.FindAssets("t:LevelConfigSO")
                .Select(guid => AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .ToList();

            // Check for empty levelIds
            var empty = configs.Where(c => string.IsNullOrEmpty(c.levelId)).ToList();
            if (empty.Any())
                throw new BuildFailedException($"LevelConfigSO with empty levelId: {string.Join(", ", empty.Select(c => c.name))}");

            // Check for duplicates
            var duplicates = configs.GroupBy(c => c.levelId)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key).ToList();
            if (duplicates.Any())
                throw new BuildFailedException($"Duplicate levelIds found: {string.Join(", ", duplicates)}");
        }
    }
    ```
  - [ ] Also add a right-click context menu option: `[MenuItem("PenguineBall/Validate Level IDs")]` for manual validation during development without a full build

- [ ] Task 3: Create `ILevelManager` interface and `LevelManager` (AC: 2, 3, 5)
  - [ ] Create `Assets/_Project/Scripts/Core/ILevelManager.cs`:
    ```csharp
    public interface ILevelManager
    {
        void LoadLevel(LevelConfigSO config);
        void RestartLevel();
        void LoadLevelSelect();
        LevelConfigSO CurrentLevel { get; }
    }
    ```
  - [ ] Create `Assets/_Project/Scripts/Core/LevelManager.cs` (MonoBehaviour implementing `ILevelManager`)
  - [ ] `[SerializeField] private string _levelSelectSceneName = "LevelSelect";` — assigned in Inspector
  - [ ] `LoadLevel(LevelConfigSO config)`:
    ```csharp
    public void LoadLevel(LevelConfigSO config)
    {
        CurrentLevel = config;
        StartCoroutine(LoadLevelAsync(config));
    }

    private IEnumerator LoadLevelAsync(LevelConfigSO config)
    {
        // Unload previous GameScene if one is loaded
        if (_currentGameScene.IsValid())
        {
            yield return SceneManager.UnloadSceneAsync(_currentGameScene);
        }

        var op = SceneManager.LoadSceneAsync(config.sceneName, LoadSceneMode.Additive);
        op.completed += OnSceneLoadCompleted;

        while (!op.isDone) yield return null;
    }

    private void OnSceneLoadCompleted(AsyncOperation op)
    {
        if (!op.isDone) // load failed
        {
            Debug.LogError("LevelManager: Scene load failed, returning to level select");
            LoadLevelSelect();
            return;
        }
        StartLevel();
    }
    ```
  - [ ] `StartLevel()` — called after scene loads successfully:
    ```csharp
    private void StartLevel()
    {
        // 1. Recalibrate gyroscope baseline (wires TODO from Story 1.2)
        ServiceLocator.Get<IInputProvider>().Calibrate();

        // 2. Increment attempt count in save data
        var save = ServiceLocator.Get<ISaveService>();
        var levelData = save.GetLevelData(CurrentLevel.levelId);
        levelData.attemptCount++;
        save.UpdateLevelData(CurrentLevel.levelId, levelData);

        // 3. Reset HUD (stub — HUD not built until Story 3.3)
        // TODO: ServiceLocator.Get<IHUDService>().Reset(CurrentLevel);

        // 4. Reset BallStateManager to Rolling
        ServiceLocator.Get<IBallStateManager>().Reset();

        // 5. Fire analytics levelStart event (stub — wired in Story 6.3)
        // TODO: ServiceLocator.Get<IAnalyticsService>().LogLevelStart(CurrentLevel.levelId);

        Debug.Log($"LevelManager: Started level {CurrentLevel.levelId}");
    }
    ```
  - [ ] `RestartLevel()`: call `LoadLevel(CurrentLevel)` — reloads same level
  - [ ] `LoadLevelSelect()`: `SceneManager.LoadSceneAsync("LevelSelect", LoadSceneMode.Single)` — replaces everything

- [ ] Task 4: Add `Reset()` to `IBallStateManager` and `BallStateManager` (AC: 5)
  - [ ] Update `IBallStateManager.cs` (Story 1.4) to add:
    ```csharp
    void Reset(); // Force-resets to Rolling state — ONLY callable by LevelManager
    ```
  - [ ] Implement in `BallStateManager.cs`:
    ```csharp
    public void Reset()
    {
        StopAllCoroutines();
        _swipeWindowOpen = false;
        CurrentState = BallState.Rolling; // direct set ONLY here — LevelManager exception
    }
    ```

- [ ] Task 5: Create placeholder `LevelSelect` scene (AC: 3)
  - [ ] Create `Assets/_Project/Scenes/LevelSelect.unity` (empty scene with just a camera)
  - [ ] Add a temporary "Level Select Placeholder" UI text — full implementation in Story 2.4
  - [ ] Add this scene to Build Settings
  - [ ] Add all level scenes to Build Settings (even empty placeholder scenes are needed for `LoadSceneAsync` to work)

- [ ] Task 6: Create placeholder level scenes and add to Build Settings (AC: 2)
  - [ ] Create `Assets/_Project/Scenes/Levels/` subfolder
  - [ ] Create scene files: `biome1_tutorial.unity`, `biome1_level01.unity`, `biome1_boss.unity`
  - [ ] Each scene: empty except for a directional light and camera (art/level design is outside scope of this story)
  - [ ] Add ALL scenes to Build Settings in correct order:
    1. `Bootstrap` (index 0 — startup scene)
    2. `LevelSelect`
    3. `GameScene` (additive base)
    4. `biome1_tutorial`, `biome1_level01`, etc.
  - [ ] `sceneName` field in each `LevelConfigSO` must exactly match the scene name in Build Settings

- [ ] Task 7: Register `LevelManager` and wire Bootstrap (AC: 2, 5)
  - [ ] Add `LevelManager` component to Bootstrap GameObject
  - [ ] `LevelManager.Awake()`: `ServiceLocator.Register<ILevelManager>(this);`
  - [ ] Wire the Story 1.2 calibration TODO: open `BootstrapManager.cs` and remove the `// TODO: call Calibrate() on level load` comment — `LevelManager.StartLevel()` now handles this

## Dev Notes

### Scene Architecture — Additive Loading Pattern

```
Bootstrap.unity (always loaded, DontDestroyOnLoad)
  ├─ BootstrapManager (registers all services)
  ├─ LevelManager (controls scene loading)
  ├─ SaveManager (persists data)
  └─ InputCalibrationHandler (gyroscope)

GameScene.unity (additive base — loaded first, never unloaded during play)
  └─ [shared gameplay objects: camera, lighting rigs]

biome1_level01.unity (additive on top of GameScene)
  └─ [level-specific: platforms, obstacles, enemies, golden ball]
```

**CRITICAL**: When transitioning between levels, unload the previous level scene (`biome1_level01`) but keep `GameScene` loaded. Do NOT use `LoadSceneMode.Single` for level transitions — it would unload Bootstrap and destroy all persistent services.

`LoadSceneMode.Single` is ONLY used for `LoadLevelSelect()` to tear everything down and start fresh.

### LevelConfigSO — Inspector Assignment Rule (ADR Architecture)

```csharp
// ✅ CORRECT — assigned in Unity Inspector
[SerializeField] private LevelConfigSO _config;

// ❌ WRONG — runtime loading creates inconsistent scene setups
var config = Resources.Load<LevelConfigSO>("LevelConfigs/biome1_level01");
```

Every GameObject that needs a `LevelConfigSO` gets it assigned in the Unity Inspector, not loaded at runtime. This is non-negotiable per ADR.

### LevelId Format Enforcement

All `levelId` values must follow `biome{N}_level{NN}` format:
```
biome1_tutorial    ← tutorial level
biome1_level01     ← first main level (zero-padded)
biome1_level02
...
biome1_boss        ← boss level (not "level13" — explicit "boss" suffix)
```

The build-time validator enforces uniqueness but NOT format. The dev should manually verify format during asset creation.

### LoadSceneAsync Error Handling

Unity's `AsyncOperation` does not have a built-in failure callback for scene loading. The `op.completed` callback fires whether the scene loaded or not — check `op.isDone` inside it. If the scene name doesn't exist in Build Settings, Unity logs an error and the operation completes with `isDone = false`.

**Always add scenes to Build Settings before testing LoadSceneAsync.** A missing scene causes a silent failure that's hard to debug.

### `IBallStateManager.Reset()` — The Only Direct State Assignment Exception

The only place in the entire codebase where `CurrentState` is set directly (bypassing `TryTransitionTo`) is in `BallStateManager.Reset()`, called only by `LevelManager`. This is documented explicitly in Story 1.4's dev notes and is an intentional architectural exception for level restart flow.

### Build-Time Validator — Editor-Only Code

`LevelIdValidator.cs` must be in an `Editor/` subfolder — Unity strips editor-only code from builds automatically. Do NOT put editor scripts in a regular `Scripts/` folder or they will be compiled into the final build and cause errors.

```
Assets/_Project/Scripts/Progression/Editor/LevelIdValidator.cs  ✅
Assets/_Project/Scripts/Progression/LevelIdValidator.cs          ❌ (compiles into build)
```

### Project Structure Notes

- `ILevelManager.cs` → `Assets/_Project/Scripts/Core/`
- `LevelManager.cs` → `Assets/_Project/Scripts/Core/`
- `LevelIdValidator.cs` → `Assets/_Project/Scripts/Progression/Editor/`
- `LevelSelect.unity` → `Assets/_Project/Scenes/`
- `biome1_*.unity` → `Assets/_Project/Scenes/Levels/`
- All `LevelConfigSO` assets → `Assets/_Project/ScriptableObjects/LevelConfigs/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `LevelManager.Awake()` registers `ILevelManager`
- `IInputProvider` (1.2) — `StartLevel()` calls `Calibrate()`
- `LevelConfigSO` stub (1.3) — expanded here with full fields
- `IBallStateManager` (1.3/1.4) — `StartLevel()` calls `Reset()`; `Reset()` method added to interface here
- `ISaveService` (2.1) — `StartLevel()` increments `attemptCount`

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-009: Code Architecture Pattern` (scene management)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Starter Template Evaluation` (project structure, scene setup)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Naming Patterns` (LevelConfig loading rule)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (LoadSceneAsync error callback, HUD reset in RestartLevel)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-003` (levelId format)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 2.2`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
