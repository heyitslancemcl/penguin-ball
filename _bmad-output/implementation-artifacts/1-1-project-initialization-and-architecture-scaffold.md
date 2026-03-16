# Story 1.1: Project Initialization & Architecture Scaffold

Status: in-progress

## Story

As a developer,
I want a fully configured Unity project with the correct template, folder structure, and core architecture scaffolding,
so that all future stories have a consistent, well-organised foundation to build on.

## Acceptance Criteria

1. **Given** Unity Hub is installed **When** the project is created **Then** it uses the Unity 3D Mobile (URP) template with Unity 6 LTS (6000.x), platform set to iOS, portrait locked
2. **Given** the project is created **When** the folder structure is inspected **Then** `Assets/_Project/Scripts/Core|Input|Obstacles|Enemies|Progression|UI|Audio|Analytics`, `ScriptableObjects/LevelConfigs|Events`, `Prefabs/Ball|Enemies|Obstacles|Projectiles|UI`, `Scenes/`, `Audio/`, `Materials/`, `Textures/` all exist
3. **Given** the project is created **When** the Bootstrap scene is opened **Then** a persistent Bootstrap scene exists alongside a GameScene (additive), and neither is destroyed on load
4. **Given** a system requires a service **When** it calls `ServiceLocator.Get<IInterface>()` **Then** the registered implementation is returned; if unregistered, a clear error is thrown
5. **Given** gameplay requires pooled objects **When** `ObjectPool.Get<T>()` is called **Then** a pooled instance is returned; `ObjectPool.Release()` returns it to the pool; no `Instantiate`/`Destroy` occurs
6. **Given** the project is initialised **When** Git is inspected **Then** a `.gitattributes` file tracks `*.unity`, `*.prefab`, `*.asset`, `*.controller`, `*.png`, `*.jpg`, `*.mp3`, `*.wav`, `*.ogg`, `*.fbx`, `*.obj` via Git LFS; a `main` and `develop` branch exist

## Tasks / Subtasks

- [ ] Task 1: Create Unity project from template (AC: 1)
  - [ ] Open Unity Hub → New Project → 3D Mobile (URP) template
  - [ ] Select Unity 6 LTS (6000.x) as engine version
  - [ ] Set project name to `PenguineBall`
  - [ ] After creation: File → Build Settings → Switch Platform to iOS
  - [ ] Player Settings → set portrait locked (disable all but Portrait)
  - [ ] Verify URP asset is assigned in Graphics Settings

- [ ] Task 2: Install required packages via Package Manager (AC: 1)
  - [ ] Verify Input System package is installed (required for gyroscope + joystick)
  - [ ] Install DOTween: import from Asset Store or `Packages/manifest.json` (Demigiant)
  - [ ] Note: GameAnalytics SDK installed in a later story (6.3) — do NOT install now
  - [ ] Run DOTween Setup Utility (Tools → DOTween Utility Panel → Setup DOTween)

- [x] Task 3: Create full folder structure (AC: 2)
  - [x] Create `Assets/_Project/Scripts/Core/`
  - [x] Create `Assets/_Project/Scripts/Input/`
  - [x] Create `Assets/_Project/Scripts/Obstacles/`
  - [x] Create `Assets/_Project/Scripts/Enemies/`
  - [x] Create `Assets/_Project/Scripts/Progression/`
  - [x] Create `Assets/_Project/Scripts/UI/`
  - [x] Create `Assets/_Project/Scripts/Audio/`
  - [x] Create `Assets/_Project/Scripts/Analytics/`
  - [x] Create `Assets/_Project/ScriptableObjects/LevelConfigs/`
  - [x] Create `Assets/_Project/ScriptableObjects/Events/`
  - [x] Create `Assets/_Project/Prefabs/Ball/`
  - [x] Create `Assets/_Project/Prefabs/Enemies/`
  - [x] Create `Assets/_Project/Prefabs/Obstacles/`
  - [x] Create `Assets/_Project/Prefabs/Projectiles/`
  - [x] Create `Assets/_Project/Prefabs/UI/`
  - [x] Create `Assets/_Project/Scenes/`
  - [x] Create `Assets/_Project/Audio/`
  - [x] Create `Assets/_Project/Materials/`
  - [x] Create `Assets/_Project/Textures/`
  - [x] Create `Assets/_Project/Tests/Core/` — mirrors Scripts/ structure; empty now, populated by later stories
  - [x] Create `Assets/_Project/Tests/Input/`
  - [x] Create `Assets/_Project/Tests/Progression/`
  - [x] Add a `.gitkeep` to each empty folder so Git tracks them

- [ ] Task 4: Create Bootstrap and GameScene scenes (AC: 3)
  - [ ] Create `Assets/_Project/Scenes/Bootstrap.unity` — this is the persistent scene
  - [ ] Create `Assets/_Project/Scenes/GameScene.unity` — loaded additively
  - [ ] In Bootstrap scene: create an empty `Bootstrap` GameObject
  - [ ] Add a `BootstrapManager` MonoBehaviour to Bootstrap GameObject
  - [ ] Apply `[DefaultExecutionOrder(-100)]` attribute to `BootstrapManager` — ensures it runs before all other MonoBehaviours; Story 6.3 depends on this ordering for analytics service registration
  - [ ] `BootstrapManager.Awake()`: call `DontDestroyOnLoad(gameObject)`
  - [ ] Set Bootstrap as the startup scene in Build Settings

- [x] Task 5: Implement ServiceLocator (AC: 4)
  - [x] Create `Assets/_Project/Scripts/Core/ServiceLocator.cs`
  - [x] Implement static `Register<T>(T instance)` — uses `_services[typeof(T)] = instance` (dictionary assignment, NOT `.Add()`) — this allows later stories to overwrite registrations (e.g. Story 6.3 overwrites `NullAnalyticsService` with `GameAnalyticsService`)
  - [x] Implement static `Get<T>()` — returns cast instance; throws `InvalidOperationException` with message "ServiceLocator: No service registered for type {typeof(T).Name}" if missing
  - [x] Implement static `Clear()` — for test teardown only
  - [x] No MonoBehaviour dependency — pure static C# class

- [x] Task 6: Implement ObjectPool (AC: 5)
  - [x] Create `Assets/_Project/Scripts/Core/ObjectPool.cs`
  - [x] Implement a static pool registry: `Dictionary<Type, (Queue<MonoBehaviour> queue, MonoBehaviour prefab)>` — the prefab reference is stored at `Prewarm` time and used when the queue is empty
  - [x] Implement static `Prewarm<T>(T prefab, int count) where T : MonoBehaviour` — stores the prefab reference and pre-instantiates `count` inactive instances into the queue; this method MUST exist because Stories 4.1, 4.2, 4.3, 6.1, and 6.2 all call it
  - [x] Implement static `Get<T>() where T : MonoBehaviour` — dequeues an inactive instance, or instantiates a new one from the stored prefab if queue is empty; returns `null` if pool was never primed with `Prewarm`
  - [x] Implement static `Release<T>(T instance) where T : MonoBehaviour` — calls `SetActive(false)` and enqueues
  - [x] CRITICAL: `Instantiate`/`Destroy` only allowed in pool internals (`Prewarm` and empty-queue fallback), NEVER in gameplay code

- [x] Task 7: Set up Git repository and Git LFS (AC: 6)
  - [x] `git init` in project root
  - [x] `git lfs install` — Git LFS 3.7.1 installed and initialized in repo
  - [x] Create `.gitattributes` with LFS tracking for: `*.unity`, `*.prefab`, `*.asset`, `*.controller`, `*.png`, `*.jpg`, `*.psd`, `*.mp3`, `*.wav`, `*.ogg`, `*.fbx`, `*.obj`
  - [x] Create `.gitignore` using Unity's standard gitignore (Library/, Temp/, Logs/, UserSettings/, Builds/)
  - [x] Initial commit on `main` branch: "Initial Unity 6 LTS URP Mobile project setup"
  - [x] Create `develop` branch from `main`
  - [x] Push both branches to GitHub remote — origin: https://github.com/heyitslancemcl/penguin-ball.git

## Dev Notes

### Project Setup Critical Notes

- **Unity Version**: Unity 6 LTS — use the `6000.x` LTS stream specifically. Do NOT use a non-LTS Unity 6 version. Check Unity Hub to confirm the LTS badge.
- **Template**: Must be **3D Mobile (URP)** — NOT "3D (URP)" or "3D". The Mobile template pre-configures quality tiers for iOS performance that the standard template does not include.
- **Input System**: The new Input System package should already be included in the Mobile template. If prompted to enable the new Input System and restart, accept. The legacy Input Manager must be disabled or set to "Both" — confirm in Project Settings → Player → Active Input Handling = "Input System Package (New)".
- **DOTween**: After import, MUST run the DOTween Setup Utility (Tools → DOTween Utility Panel → Setup DOTween) to generate the required `DOTweenModules` assembly. Skipping this causes compile errors in later stories.
- **Portrait Lock**: In Player Settings → Resolution and Presentation → Default Orientation = Portrait; uncheck Landscape Left, Landscape Right, Portrait Upside Down.

### ServiceLocator Pattern (ADR-009)

```csharp
// Registration — ONLY in Bootstrap Awake()
ServiceLocator.Register<IInputProvider>(new GyroscopeInputProvider());

// Retrieval — ONLY in Start(), NEVER in Awake()
var input = ServiceLocator.Get<IInputProvider>();
```

**Why register in Awake, retrieve in Start?** Unity does not guarantee Awake() execution order across objects. Registering in Awake() and retrieving in Start() ensures all registrations are complete before any retrieval.

### ObjectPool Pattern

```csharp
// ✅ CORRECT — prewarm once in BootstrapManager.Awake() when each type is introduced
ObjectPool.Prewarm<IceSpikeProjectile>(_iceSpikePrefab, count: 8);

// ✅ CORRECT — get/release during gameplay (no Instantiate/Destroy)
var spike = ObjectPool.Get<IceSpikeProjectile>();
ObjectPool.Release(spike);

// ❌ WRONG — never call these during gameplay
Instantiate(spikePrefab);
Destroy(spikeInstance);
```

**Design note:** The static pool registry stores the prefab reference provided at `Prewarm` time. When `Get<T>()` is called and the queue is empty, it instantiates a new instance from the stored prefab as a fallback. `Get<T>()` returns `null` if the type was never primed — callers must guard against this.

The pool scaffold created in this story only needs to be the infrastructure — it does not need pre-configured pools for specific prefabs yet. `Prewarm` calls are added in the stories that introduce each pooled type: Stories 4.1 (`BlastWaveEffect`), 4.2 (`SealController`), 4.3 (`IceSpikeProjectile`), 6.1 (`ExplodingIceBlock`, `ThrownFirePickup`), 6.2 (`FireballProjectile`).

### Scene Architecture (ADR-009)

```
Bootstrap.unity (persistent, DontDestroyOnLoad)
  └─ BootstrapManager (registers services in Awake)
  └─ [Future: AudioSource, persistent singletons]

GameScene.unity (loaded additively over Bootstrap)
  └─ [Level content loaded here]
```

**CRITICAL**: GameScene is ALWAYS loaded additively — never as a standalone replacement scene. `LoadSceneMode.Additive` is mandatory. This pattern is established now and must be followed in all future stories.

### Folder Naming Convention

- All project assets live under `Assets/_Project/` — the underscore prefix ensures it sorts first in the Project window and visually separates project code from Unity packages/third-party assets.
- Never put scripts directly in `Assets/` root — this is a common mistake that causes namespace and organisation issues.

### Project Structure Notes

- Alignment: This story establishes the canonical `Assets/_Project/` structure. All subsequent stories MUST place files in the correct subfolder — deviations will cause story review failures.
- `Assets/_Project/Tests/` mirrors the `Scripts/` structure (`Tests/Core/`, `Tests/Input/`, `Tests/Progression/`). Created now, populated by Stories 2.1 (`SaveManager`), 3.2 (`StyleRankScorer`), and 1.4 (`BallStateManager`).
- One class per file — filename must match class name exactly. No exceptions.

### Latest Tech Notes (verify versions at time of implementation)

**Unity 6 LTS (6000.0.x)**
- Stream is `6000.0.x` — confirm the current LTS patch in Unity Hub (look for the LTS badge)
- Latest patch as of mid-2025 was 6000.0.40+; check: https://unity.com/releases/editor/lts-releases

**URP 17 — CRITICAL Breaking Changes**
- 🚨 **Render Graph is mandatory in URP 17+**: The old `ScriptableRendererFeature` pattern using `AddRenderPasses`/`Execute` is deprecated. Any custom renderer features must use the new `RecordRenderGraph` API. At MVP this project does not require custom renderer features, but do NOT add any using the old pattern.
- **GPU Resident Drawer** is enabled by default in the Mobile template — can cause issues with dynamic batching. If you see unexpected batching bugs, check Project Settings → URP → GPU Resident Drawer.
- **Shader stripping** is more aggressive in URP 17 — if particles or materials render pink/missing at runtime, check that required shader variants are not stripped. Add them to `Always Included Shaders` in Graphics Settings if needed.
- **HDR Display Output** may be on by default — disable in Player Settings if targeting low-end devices.

**DOTween**
- Latest stable: ~1.2.765 (free). No breaking API changes for Unity 6.
- MUST run Setup Utility after import (Tools → DOTween Utility Panel → Setup DOTween) — skipping causes compile errors.
- Import from Asset Store or: http://dotween.demigiant.com/download.php

**Unity Input System**
- Latest stable: ~1.11.x (`com.unity.inputsystem`). Fully compatible with Unity 6 LTS.
- `PlayerInput`, `InputAction`, `InputActionAsset` APIs are stable from 1.8+.
- Unity 6 Mobile template defaults to new Input System — confirm Player Settings → Active Input Handling = "Input System Package (New)".

**GameAnalytics SDK**
- NOT installed in this story — deferred to Story 6.3.
- When needed: UPM package `com.gameanalytics.sdk`, import via Git URL or Asset Store.

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#Starter Template Evaluation`
- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-009: Code Architecture Pattern`
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Version Control`
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Code Standards & Naming Conventions`
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 1.1`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

- Git LFS not installed on host machine; `git lfs install` step is pending manual action. Workaround: `.gitattributes` is already committed with correct LFS tracking rules — installing LFS + running `git lfs install` in the repo will activate it without further changes.

### Completion Notes List

- ✅ Task 3: Full `Assets/_Project/` folder hierarchy created (22 directories, `.gitkeep` in each)
- ✅ Task 5: `ServiceLocator.cs` — pure static C# class, `Register<T>`/`Get<T>`/`Clear()`. Dictionary assignment (not `.Add()`) enables Story 6.3 service overwrite pattern. `Get<T>` throws `InvalidOperationException` with type name in message.
- ✅ Task 6: `ObjectPool.cs` — static pool registry using inner `PoolEntry` class. `Prewarm/Get/Release` all implemented. `Instantiate`/`Destroy` confined to pool internals only.
- ✅ Task 4 (code only): `BootstrapManager.cs` — `[DefaultExecutionOrder(-100)]`, `DontDestroyOnLoad`, `RegisterServices()` stub ready for Stories 1.2, 2.1, 5.1, 6.3.
- ✅ Task 7 (partial): `git init`, `.gitattributes`, `.gitignore`, initial commit on `main`, `develop` branch created. Git LFS install and GitHub remote push are pending manual steps.
- ⏳ Tasks 1, 2, 4 (Unity Editor): Require Unity Hub/Editor interaction — detailed instructions provided to user.
- Tests: `ServiceLocatorTests.cs` (EditMode, 5 tests), `ObjectPoolTests.cs` (PlayMode, 6 tests) written and ready to run in Unity Test Runner once project is created.

### File List

Assets/_Project/Scripts/Core/ServiceLocator.cs
Assets/_Project/Scripts/Core/ObjectPool.cs
Assets/_Project/Scripts/Core/BootstrapManager.cs
Assets/_Project/Tests/Core/ServiceLocatorTests.cs
Assets/_Project/Tests/Core/ObjectPoolTests.cs
.gitattributes
.gitignore
Assets/_Project/ (all subdirectories with .gitkeep)

## Change Log

- 2026-03-16: Implemented Tasks 3, 5, 6, 7 (partial) and Task 4 code. Full folder structure created, ServiceLocator and ObjectPool implemented with tests, BootstrapManager scaffolded, git repo initialized. Tasks 1, 2, 4 (Unity Hub/Editor) remain for manual completion.
