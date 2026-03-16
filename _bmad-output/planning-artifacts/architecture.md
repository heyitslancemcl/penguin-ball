---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
inputDocuments:
  - _bmad-output/planning-artifacts/gdd-PenguineBall-2026-03-14.md
  - _bmad-output/planning-artifacts/product-brief-PenguineBall-2026-03-14.md
  - _bmad-output/planning-artifacts/research/market-PenguineBall-research-2026-03-14.md
  - _bmad-output/planning-artifacts/prd.md
workflowType: 'architecture'
lastStep: 8
status: 'complete'
completedAt: '2026-03-15'
project_name: 'PenguineBall'
user_name: 'Lance'
date: '2026-03-14'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

---

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**

1. Gyroscope tilt input → ball physics movement (primary control scheme)
2. Accessibility alternative controls — virtual joystick + jump button with full gameplay parity
3. Ball physics simulation — rolling, bouncing, collision detection on ice/platform surfaces
4. Three-layer progression system: gold stars, golden ball collectibles, style rank scoring (E→A)
5. Style rank algorithm — deterministic scoring: speed + fire ability use + enemy defeats + hazards avoided
6. Enemy AI — Seal (patrol/sliding knock-off) and Walrus (stationary/slow, ranged ice spike projectiles)
7. Fire ability power-up — timed (20–30s), dual-use (offensive: defeats enemies, breaks walls; defensive: clears ice spikes); on-bounce radial blast wave
8. Boss level — 4-phase survival challenge; boss throws ice spikes, exploding ice blocks, and fire power-ups; player swipes to aim and shoot fireballs; 4 hits to defeat
9. Level system — tutorial level (Level 0), 9–12 main levels, 1 boss level (gated by golden ball count)
10. Obstacle system — static ice spikes, ground holes, moving platforms, stun obstacles (swipe-up recovery), fatal bounce obstacles, breakable walls (fire-only)
11. Ball state machine — Rolling, Stunned (1s swipe-up window), Recovery, Dead
12. Style rank reveal screen — sequential DOTween punch-in animation
13. In-game HUD — golden ball slot, lives/attempts counter, fire ability timer
14. Level select screen — simple grid with per-level completion state
15. Tutorial system — diegetic prompts via TutorialSequencer, no rank reveal on Level 0
16. Audio system — adaptive music, contextual SFX, character voice identity
17. Persistence — JSON save file with per-level data keyed by level ID string
18. Analytics — GameAnalytics SDK: levelStart, levelComplete, levelFail, goldenBallCollected, rankAchieved, rankScreenDwellTime

**Non-Functional Requirements:**

- **Performance:** 60 FPS target; level load < 3 seconds
- **Platform:** iOS 17, 18, 26 — portrait locked
- **Engine:** Unity (MVP)
- **Input:** CoreMotion gyroscope primary; Unity UI accessibility controls
- **Battery:** Optimised for mobile play sessions
- **App Store:** No ads, no IAP, age rating 4+/9+
- **Screen support:** iPhone SE (4th gen) through iPhone Pro Max

**Scale & Complexity:**

- Primary domain: iOS Mobile Game (Unity)
- Complexity level: **Medium** — well-scoped, single platform, clear mechanic set
- Major architectural systems: ~10

### Technical Constraints & Dependencies

- Unity engine — physics, rendering, scene management, audio
- iOS only at launch — no cross-platform abstraction needed
- CoreMotion — gyroscope framework; sensitivity variance across device generations
- Portrait locked — simplifies layout decisions
- No backend required at MVP — all state is local
- GameAnalytics SDK — free tier, anonymous telemetry, Unity SDK
- DOTween — Unity animation library for rank reveal sequencer

### Cross-Cutting Concerns Identified

1. **Input abstraction** — `IInputProvider` interface; tilt and joystick produce identical physics outcomes
2. **Physics consistency** — CoreMotion calibration at level load, delta-from-baseline, low-pass filter smoothing
3. **Scoring integrity** — deterministic style rank; per-session HashSet deduplication for score events
4. **State persistence** — JSON save, atomic writes, .bak backup, save on pause/focus-loss
5. **Audio management** — centralised audio bus, max 3 concurrent SFX, persistent AudioSource on Bootstrap
6. **Scene management** — Bootstrap (persistent) + GameScene (additive); LevelConfig ScriptableObjects
7. **Animation state machine** — DOTween sequencer for rank reveal; TutorialSequencer for Level 0 prompts
8. **Analytics reliability** — SDK wrapped in try/catch; never a hard dependency

---

## Architectural Decisions

### ADR-001: Input Abstraction Layer
**Decision:** `IInputProvider` interface with two concrete implementations — `GyroscopeInputProvider` and `JoystickInputProvider`. `BallController` depends only on the interface.

**CoreMotion Calibration (non-negotiable):**
- Baseline captured at every level load — not app launch
- Input expressed as delta-from-baseline
- Low-pass filter (configurable coefficient) for smooth, jitter-free feel
- Re-calibration via three-finger tap during play (haptic + toast) and pause menu
- Auto-suggest recalibration if drift exceeds threshold mid-level
- Device-specific sensitivity profiles tested on iPhone SE, 15, 16 Pro before launch
- `OnApplicationPause(false)` triggers recalibration after backgrounding
- `SystemInfo.supportsGyroscope` check on launch → auto-fallback to joystick if unavailable

**Rationale:** Tilt feel is the game's identity. Drift or miscalibration is fatal to reviews.

---

### ADR-002: Physics Approach
**Decision:** Unity `Rigidbody` + sphere collider. `PhysicsMaterial` near-zero friction for ice surface. `RigidbodyInterpolation.Interpolate` enabled. `collisionDetectionMode = Continuous` to prevent tunnelling. Max velocity clamped in `FixedUpdate`.

**Golden Ball Momentum Reward:**
- On collection: short `AddForce` impulse in current travel direction — satisfying speed burst
- Brief golden particle trail post-collection
- Impulse magnitude tunable via `LevelConfig`

**Fire Ability Physics:**
- Blast wave on bounce: `Physics.OverlapSphere` at impact point, radial force to enemies/hazards
- Fireball projectile (boss level): swipe direction vector → `FireballProjectile` prefab travels until boss hit or off-screen

**Stuck Detection:**
- If velocity < threshold for >2s and not in Stunned state → restart prompt

**Rationale:** Platform-native physics with momentum reward ties golden ball collection to a feel-good game moment.

---

### ADR-003: State Persistence & Analytics
**Decision:** JSON save file at `Application.persistentDataPath`. Per-level data keyed by level ID string. XOR obfuscation. Atomic writes (temp file → rename). `.bak` backup on every successful save. Save triggered on level complete, golden ball collected, `OnApplicationPause`, `OnApplicationFocus(false)`.

**Per-Level Save Structure:**
```json
{
  "levels": {
    "biome1_tutorial": { "completed": true, "goldenBallCollected": true,
      "bestRank": null, "bestScore": 0, "attemptCount": 1, "hasSeenRankReveal": false },
    "biome1_level01": { "completed": true, "goldenBallCollected": false,
      "bestRank": "C", "bestScore": 3100, "attemptCount": 7, "hasSeenRankReveal": true }
  },
  "globalStats": { "totalAttempts": 42, "totalCompletions": 18, "sessionCount": 6 }
}
```

**Analytics:** GameAnalytics SDK. Events: `levelStart`, `levelComplete`, `levelFail`, `goldenBallCollected`, `rankAchieved`, `rankScreenDwellTime`. SDK wrapped in try/catch — never a hard dependency. `LevelConfig` requires non-empty unique `levelId` validated at build time.

**Rationale:** String-keyed levels survive biome expansion; atomic writes prevent corruption; analytics surfaces drop-off data from day one.

---

### ADR-004: Ball State Machine
**Decision:** `BallStateManager` — states: Rolling → Stunned → Recovery → Rolling / Dead. Priority: Dead > Stunned. Single `TryTransitionTo()` gate prevents race conditions.

**Stun Mechanic:**
- 1-second window (tunable per obstacle via `LevelConfig`)
- Window opens after stun animation completes — not on collision
- Swipe-up prompt (arrow pulse) appears when window opens
- Swipe-up: touch delta detection (tilt mode) / jump button tap (accessibility)
- `OnSwipeUp` only processed if current state is `Stunned`

**Swipe-to-Aim (Boss Level):**
- Only processed if `IsFireActive && state == Rolling`
- Mutually exclusive with swipe-up recovery — no gesture conflict
- Touch start→end delta mapped to world direction → `FireballProjectile` launched

**Rationale:** State-gated gesture routing eliminates ambiguity; mutually exclusive states make swipe-up and swipe-to-aim conflict-free.

---

### ADR-005: Obstacle Classification
**Decision:** Six obstacle types via `ObstacleType` enum:

| Type | Behaviour | Death? |
|---|---|---|
| `StunObstacle` | Ball stops/slows; 1s swipe-up recovery window | No unless window missed |
| `FatalBounce` | Strong directional impulse off course → kill plane | Yes |
| `BreakableWall` | Passable only when fire active; shatters on contact | Blocks path if no fire |
| `IceSpike` | Death on contact unless fire active | Yes unless fire |
| `MovingPlatform` | Horizontal slide or 180° spin; movement paused during Stunned | Indirect |
| `GroundHole` | Fall through → kill plane | Yes |

**Kill Plane:** Y-threshold trigger below all platforms. Ball visibly arcs off platform before hitting plane — cinematic, not instant.
**BreakableWall:** Collider disabled same frame as shatter animation — no invisible blocking.
**BreakableWall as golden ball gate:** Fire timing becomes a core skill expression.

---

### ADR-006: Boss Level Architecture
**Decision:** Four-phase survival challenge. `BossController` phase state machine: Phase 1 → [fireball hit] → Phase 2 → [fireball hit] → Phase 3 → [fireball hit] → Phase 4 → [fireball hit] → Defeated. Phase data (duration, projectile patterns, spawn rates) in `LevelConfig`.

**Projectile Types:**
- `IceSpikeProjectile` — fast, thin, electric blue trail; dodge the path
- `ExplodingIceBlock` — chunky, tumbling, orange glow on impact; 0.5s flash → `Physics.OverlapSphere` blast radius → stun if in range
- `PowerUpThrow` — fire power-up; distinct boss wind-up animation; 2–3s collection window before disappears
- `FireballProjectile` — player-launched via swipe-to-aim; travels until boss hit or off-screen

**Golden Ball:** Accessible only during Phase 1 — calm window before platform escalates.
**Phase escalation:** Phase 1 slow/readable (teaching); Phase 4 simultaneous spikes + ice blocks + faster power-up window.

---

### ADR-007: Rank Reveal Screen
**Decision:** DOTween sequencer. First-play: full cinematic (~2s). Repeat plays: condensed (~1.2s). Sequence: rank letter slam (0.3s overshoot bounce) → score counter tick-up (0.8s) → golden ball result fade-in → fanfare. Final state holds 1.5s before "tap to continue." `hasSeenRankReveal` bool per level in SaveData.

---

### ADR-008: Tutorial System
**Decision:** Level 0 — identical engine to all other levels. `TutorialSequencer` subscribes to game events and activates diegetic prompts in sequence. Prompt triggers and content in `TutorialConfig` ScriptableObject. No style rank reveal on tutorial — replaced with "Great start!" completion screen. Golden ball collected status shown on tutorial completion.

---

### ADR-009: Code Architecture Pattern
**Decision:** Hybrid ScriptableObject Events + Service Locator.

```
Gameplay layer    → ScriptableObject events → Systems layer
Systems layer     → ServiceLocator interfaces → Services
Bootstrap scene   → Persistent singletons (AudioSource, SceneLoader)
```

- **ScriptableObject events** for gameplay-to-system communication (score events, ability triggers, level complete, golden ball collected)
- **Service Locator + interfaces** for system-to-system communication (IInputProvider, ISaveService, IAudioService, IAnalyticsService)
- **Persistent singletons** only on Bootstrap scene objects that never need mocking

---

---

## Starter Template Evaluation

### Primary Technology Domain
iOS Mobile Game — Unity engine, portrait orientation, physics-driven tilt controls, particle effects, DOTween animation.

### Selected Starter: Unity 3D Mobile (URP)

**Rationale:** URP is Unity's recommended renderer for mobile — lower draw call overhead, mobile-optimised shaders, built-in GPU instancing. The Mobile template pre-configures quality settings for iOS performance targets, saving significant setup time.

**Initialization:**
```
Unity Hub → New Project → 3D Mobile (URP) template
Unity version: Unity 6 LTS (6000.x)
Platform: iOS
```

**Architectural Decisions Provided by Template:**
- **Renderer:** Universal Render Pipeline (URP) — mobile-optimised, compatible with DOTween and particle systems
- **Quality Settings:** Pre-configured mobile quality tiers — maps to A12 chip LOD requirement for fire particles
- **Physics:** Unity PhysX included — Rigidbody + sphere collider ready from project creation
- **Input System:** Unity's new Input System package — required for CoreMotion gyroscope and accessibility touch joystick

**Project Structure:**
```
Assets/
  _Project/
    Scripts/
      Core/           ← BallStateManager, BallController, LevelManager
      Input/          ← IInputProvider, GyroscopeInputProvider, JoystickInputProvider
      Obstacles/      ← ObstacleType enum, obstacle behaviours
      Enemies/        ← SealController, WalrusController, BossController
      Progression/    ← StyleRankScorer, SaveManager, LevelConfig SOs
      UI/             ← HUD, RankRevealScreen, TutorialSequencer
      Audio/          ← AudioManager, audio bus
      Analytics/      ← GameAnalyticsService, IAnalyticsService
    ScriptableObjects/
      LevelConfigs/   ← biome1_tutorial.asset, biome1_level01.asset...
      Events/         ← GameEventSO assets
    Prefabs/
      Ball/
      Enemies/
      Obstacles/
      Projectiles/
      UI/
    Scenes/
      Bootstrap.unity
      GameScene.unity
    Audio/
    Materials/
    Textures/
```

**Note:** Project initialization and folder structure setup should be the first implementation story (Epic 1, Story 1).

---

### Pre-mortem Hardening Checklist

- [ ] CoreMotion: baseline at level load, auto-recalibrate on drift threshold
- [ ] Device sensitivity profiles tested: iPhone SE, 15, 16 Pro
- [ ] Atomic save writes (temp → rename) + .bak backup
- [ ] Save on OnApplicationPause + OnApplicationFocus(false)
- [ ] Rigidbody.collisionDetectionMode = Continuous + max velocity clamp
- [ ] Stuck detection: velocity < threshold 2s → restart prompt
- [ ] Stun window opens after animation, not on collision
- [ ] Swipe threshold relative to Screen.height; min 44pt touch target
- [ ] Object pooling: ice spike projectiles, fire particles, enemy spawns
- [ ] Moving platforms: coroutine interpolation, not Update() polling
- [ ] Fire particle LOD on A12 and older chips
- [ ] LevelConfig required levelId + build-time uniqueness validator
- [ ] LoadSceneAsync error callback → level select fallback
- [ ] Per-session HashSet deduplication for score events
- [ ] Analytics SDK wrapped in try/catch — never a hard dependency
- [ ] HUD reset explicitly in RestartLevel()
- [ ] SystemInfo.supportsGyroscope → auto-fallback to joystick

---

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**
- Input abstraction (IInputProvider) — ADR-001
- Physics approach (Rigidbody + Continuous) — ADR-002
- Ball state machine (BallStateManager) — ADR-004
- Obstacle classification (ObstacleType enum) — ADR-005
- Save/persistence (JSON + atomic writes) — ADR-003
- Code architecture pattern (Hybrid SO Events + Service Locator) — ADR-009

**Important Decisions (Shape Architecture):**
- Analytics SDK (GameAnalytics) — ADR-003
- Boss architecture (4-phase, swipe-to-aim fireball) — ADR-006
- Rank reveal sequencer (DOTween) — ADR-007
- Tutorial system (TutorialSequencer + diegetic prompts) — ADR-008
- Unity 3D Mobile (URP) starter template

**Deferred Decisions (Post-MVP):**
- Play Mode automated tests — valuable but overhead for solo dev at MVP
- Unity Cloud Build — local Xcode sufficient at current scale
- Backend infrastructure — no server needed at MVP
- Social/leaderboard integration — roadmap Year 2

### Testing Strategy
**Decision:** Unity Test Framework — Edit Mode unit tests only for MVP.

**Scope:** Deterministic, pure C# logic that must be correct:
- `StyleRankScorer` — rank calculation algorithm
- `SaveManager` — save/load round-trip including corruption recovery
- `BallStateManager` — state transition rules and priority logic
- `LevelConfig` — levelId uniqueness validation

**Rationale:** Edit Mode tests run fast with no scene overhead. These four systems are the ones most likely to produce silent bugs that affect player experience. Play Mode tests deferred — valuable for physics integration but add significant setup overhead for a solo developer at MVP stage.

### Build Pipeline
**Decision:** Local Xcode build for iOS distribution.

**Workflow:**
1. Develop in Unity (VSCode for script editing)
2. Unity → Build → Export Xcode project
3. Open in Xcode → Archive → App Store Connect submission
4. TestFlight for pre-launch testing

**Rationale:** Standard indie iOS pipeline. No cloud build cost at MVP scale. Unity Cloud Build deferred until build frequency warrants it.

### Version Control
**Decision:** GitHub + Git LFS for Unity binary assets.

**Git LFS tracked file types:**
```
*.png, *.jpg, *.psd          ← Textures
*.mp3, *.wav, *.ogg          ← Audio
*.fbx, *.obj                 ← 3D models
*.unity                      ← Scenes
*.prefab                     ← Prefabs
*.asset                      ← ScriptableObjects
*.controller                 ← Animator controllers
```

**Branch strategy:**
- `main` — stable, App Store submission only
- `develop` — active development
- Feature branches from `develop`

### Code Standards & Naming Conventions

**C# Naming:**
```csharp
public class BallStateManager { }              // PascalCase classes
private BallController _ballController;        // _camelCase private fields
public bool IsFireActive { get; private set; } // PascalCase properties
private void HandleStateTransition() { }       // PascalCase methods
```

**ScriptableObjects:** `[TypeName]SO` suffix — e.g. `LevelConfigSO`, `GameEventSO`, `IntVariableSO`

**File organisation:** One class per file, filename matches class name exactly.

**Level IDs:** `biome{N}_level{NN}` format (e.g. `biome1_level03`, `biome1_boss`, `biome1_tutorial`) — enforced by build-time validator.

**Unity folder convention:** All project assets under `Assets/_Project/` to separate from third-party packages.

### Decision Impact Analysis

**Implementation Sequence:**
1. Unity project init (URP Mobile template) + folder structure + Git LFS
2. Input system (IInputProvider + GyroscopeInputProvider)
3. Ball physics + BallController
4. BallStateManager (state machine)
5. LevelManager + SceneManager (Bootstrap + GameScene)
6. SaveManager + JSON persistence
7. Obstacle system (ObstacleType enum + behaviours)
8. Enemy AI (Seal, Walrus)
9. Progression systems (StyleRankScorer, golden ball tracking)
10. UI/HUD + RankRevealScreen (DOTween)
11. AudioManager
12. GameAnalytics integration
13. Tutorial level (TutorialSequencer)
14. Boss level (BossController + projectile system)

**Cross-Component Dependencies:**
- `BallController` depends on `IInputProvider` and `BallStateManager`
- `StyleRankScorer` depends on ScriptableObject events from gameplay layer
- `SaveManager` depends on `LevelConfig` levelId strings
- `HUD` driven by `BallStateManager.IsFireActive` and SO events
- `BossController` depends on `BallStateManager` (swipe-to-aim state gate)
- `GameAnalyticsService` depends on `LevelConfig` levelId (never hardcoded)

---

## Implementation Patterns & Consistency Rules

**Critical Conflict Points Identified:** 8 areas where AI agents could make different choices.

---

### Naming Patterns

**ScriptableObject Events:**
```csharp
// ✅ CORRECT — verb + noun, past tense, PascalCase
OnGoldenBallCollected, OnLevelCompleted, OnFireAbilityActivated,
OnBallStateChanged, OnStyleRankCalculated

// ❌ WRONG
GoldenBallCollect, goldenBallCollected, GoldenBallEvent
```

**Service Locator:**
```csharp
// ✅ CORRECT — register in Bootstrap Awake(), retrieve in Start()
ServiceLocator.Register<IInputProvider>(new GyroscopeInputProvider()); // Awake
var input = ServiceLocator.Get<IInputProvider>();                       // Start

// ❌ WRONG — never retrieve in Awake() (registration order not guaranteed)
```

**Object Pooling:**
```csharp
// ✅ CORRECT
ObjectPool.Get<IceSpikeProjectile>();
ObjectPool.Release(projectile);

// ❌ WRONG — never Instantiate/Destroy during gameplay
```

**LevelConfig Loading — serialised Inspector field, never Resources.Load:**
```csharp
// ✅ CORRECT — assigned in Unity Inspector
[SerializeField] private LevelConfigSO _config;

// ❌ WRONG — runtime loading creates inconsistent scene setups
var config = Resources.Load<LevelConfigSO>("LevelConfigs/biome1_level01");
```

---

### Structure Patterns

**MonoBehaviour Lifecycle Rules:**
```
Awake()       → Self-init only. ServiceLocator.Register() (Bootstrap only).
Start()       → ServiceLocator.Get() calls. Cross-object references.
OnEnable()    → Subscribe to ScriptableObject events.
OnDisable()   → Unsubscribe from SO events. ALWAYS paired with OnEnable.
FixedUpdate() → Physics forces, Rigidbody interactions ONLY.
Update()      → Input reading, non-physics timers ONLY.
```

**ScriptableObject Event Subscription:**
```csharp
// ✅ CORRECT — always paired
private void OnEnable()  => _onGoldenBallCollected.AddListener(Handle);
private void OnDisable() => _onGoldenBallCollected.RemoveListener(Handle);

// ❌ WRONG — Start() subscription without OnDestroy unsubscribe = memory leak
```

**State Transitions — TryTransitionTo() is the ONLY valid pattern:**
```csharp
// ✅ CORRECT — caller requests, state machine decides; bool return for rejection
if (!_ballStateManager.TryTransitionTo(BallState.Stunned)) return;

// ❌ WRONG — callers must not set state directly
_ballStateManager.CurrentState = BallState.Stunned;
_ballStateManager.SetState(BallState.Stunned);
```

**Test File Location — mirror Scripts/ structure under Tests/:**
```
Assets/_Project/Scripts/Core/BallStateManager.cs
Assets/_Project/Tests/Core/BallStateManagerTests.cs
```

---

### Format Patterns

**Save Data — SaveManager is the ONLY entry point:**
```csharp
// ✅ CORRECT
SaveManager.Instance.SetLevelCompleted("biome1_level03");
bool collected = SaveManager.Instance.IsGoldenBallCollected("biome1_level03");

// ❌ WRONG — never access SaveData fields directly from outside SaveManager
_saveData.levels["biome1_level03"].goldenBallCollected = true;
```

**Level IDs — always use LevelIds constants, never raw strings:**
```csharp
// ✅ CORRECT
public static class LevelIds
{
    public const string Tutorial = "biome1_tutorial";
    public const string Level01  = "biome1_level01";
    public const string Boss     = "biome1_boss";
}

// ❌ WRONG — raw string literals are typo-prone and unchecked at compile time
SaveManager.Instance.SetLevelCompleted("biome1level1");
```

---

### Communication Patterns

**Physics Forces — FixedUpdate only, staged from Update:**
```csharp
// ✅ CORRECT
private Vector2 _pendingForce;
void Update()      => _pendingForce = _inputProvider.GetDirection();
void FixedUpdate() => _rigidbody.AddForce(_pendingForce * _forceMultiplier);

// ❌ WRONG — AddForce in Update() = frame-rate dependent physics
```

**Async Unity Work — Coroutines only, never async/await:**
```csharp
// ✅ CORRECT
StartCoroutine(PlayRankRevealSequence());
private IEnumerator PlayRankRevealSequence() { yield return ...; }

// ❌ WRONG — async/await does not integrate with Unity scene lifecycle
async Task PlayRankRevealSequence() { await Task.Delay(300); }
```

**Analytics — always through IAnalyticsService:**
```csharp
// ✅ CORRECT
_analyticsService.LogLevelComplete(LevelIds.Level01, rank, score);

// ❌ WRONG — direct SDK call bypasses try/catch safety wrapper
GA_SDK.NewProgressionEvent(GAProgressionStatus.Complete, "biome1_level01");
```

---

### Process Patterns

**Error Handling:**
```csharp
// ✅ CORRECT — log + graceful fallback
try { _saveData = JsonUtility.FromJson<SaveData>(json); }
catch (Exception e) { Debug.LogError($"[SaveManager] {e.Message}"); LoadBackup(); }

// ❌ WRONG — silent catch
catch { }
```

**Loading States — always reset, even on early return:**
```csharp
// ✅ CORRECT
_isLoading = true;
yield return SceneManager.LoadSceneAsync("GameScene", LoadSceneMode.Additive);
_isLoading = false;
```

---

### Enforcement Guidelines

**All AI Agents MUST:**
- Subscribe/unsubscribe SO events in `OnEnable`/`OnDisable` — never `Start`/`OnDestroy`
- Apply physics forces in `FixedUpdate` only — never `Update`
- Access save data through `SaveManager` only — never touch `SaveData` directly
- Use `LevelIds` constants — never raw level ID strings
- Use Coroutines for async Unity work — never `async/await`
- Wrap all analytics calls in `IAnalyticsService` — never call SDK directly
- Use `ObjectPool.Get/Release` for projectiles and particles — never `Instantiate/Destroy`
- Register services in `Awake`, retrieve in `Start`
- Use `TryTransitionTo(state) : bool` for all state transitions — never set state directly
- Assign `LevelConfigSO` via `[SerializeField]` Inspector field — never `Resources.Load`

**Cross-System Integration Checklist (mandatory for any gameplay story):**
```
[ ] ScriptableObject events wired (OnEnable/OnDisable)
[ ] ObjectPool used for any spawned objects
[ ] Analytics event logged via IAnalyticsService
[ ] SaveManager notified if state changes persistence
[ ] HUD updated via SO event (not direct reference)
[ ] AudioManager called via IAudioService
[ ] FixedUpdate used for any physics forces
[ ] LevelIds constant used (no raw strings)
[ ] TryTransitionTo() used for any state changes
[ ] LevelConfigSO assigned in Inspector (not loaded at runtime)
```

**Anti-Pattern Quick Reference:**
| ❌ Never | ✅ Instead |
|---|---|
| `Instantiate(prefab)` in gameplay | `ObjectPool.Get<T>()` |
| `AddForce()` in `Update()` | `AddForce()` in `FixedUpdate()` |
| `async Task` in MonoBehaviour | `IEnumerator` + `StartCoroutine` |
| Raw level ID strings | `LevelIds.LevelXX` constants |
| Direct `_saveData` field access | `SaveManager.Instance` methods |
| SO event subscribe in `Start` | Subscribe `OnEnable`, unsub `OnDisable` |
| Direct `GA_SDK` calls | `_analyticsService.LogXxx()` |
| `_stateManager.CurrentState = x` | `_stateManager.TryTransitionTo(x)` |
| `Resources.Load<LevelConfigSO>` | `[SerializeField] LevelConfigSO _config` |

---

## Project Structure & Boundaries

### Complete Project Directory Structure

```
PenguineBall/
├── .github/
│   └── workflows/
│       └── unity-tests.yml               ← Edit Mode test runner on push
├── .gitattributes                         ← Git LFS tracked file types
├── .gitignore
├── README.md
│
├── Assets/
│   ├── _Project/
│   │   ├── Scripts/
│   │   │   ├── Core/
│   │   │   │   ├── Bootstrap.cs           ← Persistent scene init, ServiceLocator.Register()
│   │   │   │   ├── ServiceLocator.cs      ← Service registration/retrieval
│   │   │   │   ├── LevelManager.cs        ← Scene loading, level lifecycle
│   │   │   │   ├── ObjectPool.cs          ← Generic object pool
│   │   │   │   └── LevelIds.cs            ← Level ID string constants
│   │   │   ├── Input/
│   │   │   │   ├── IInputProvider.cs      ← Interface: GetDirection(), OnSwipeUp event
│   │   │   │   ├── GyroscopeInputProvider.cs  ← CoreMotion, baseline calibration
│   │   │   │   └── JoystickInputProvider.cs   ← Virtual joystick + jump button
│   │   │   ├── Ball/
│   │   │   │   ├── BallController.cs      ← Rigidbody physics, FixedUpdate forces
│   │   │   │   ├── BallStateManager.cs    ← Rolling/Stunned/Recovery/Dead
│   │   │   │   ├── BallState.cs           ← BallState enum
│   │   │   │   └── FireAbilityHandler.cs  ← Timed fire ability, blast wave
│   │   │   ├── Obstacles/
│   │   │   │   ├── ObstacleType.cs
│   │   │   │   ├── StunObstacle.cs
│   │   │   │   ├── FatalBounceObstacle.cs
│   │   │   │   ├── BreakableWall.cs
│   │   │   │   ├── IceSpikeObstacle.cs
│   │   │   │   ├── MovingPlatform.cs
│   │   │   │   └── KillPlane.cs
│   │   │   ├── Enemies/
│   │   │   │   ├── SealController.cs
│   │   │   │   ├── WalrusController.cs
│   │   │   │   └── BossController.cs      ← 4-phase state machine
│   │   │   ├── Projectiles/
│   │   │   │   ├── IceSpikeProjectile.cs  ← Pooled
│   │   │   │   ├── ExplodingIceBlock.cs   ← Pooled, blast radius stun
│   │   │   │   ├── FireballProjectile.cs  ← Pooled, player swipe-to-aim
│   │   │   │   └── PowerUpThrow.cs        ← Boss-thrown fire power-up
│   │   │   ├── Progression/
│   │   │   │   ├── StyleRankScorer.cs     ← Deterministic E→A algorithm
│   │   │   │   ├── GoldenBallTracker.cs
│   │   │   │   └── WorldGateChecker.cs    ← Boss unlock threshold
│   │   │   ├── Persistence/
│   │   │   │   ├── SaveManager.cs         ← JSON, atomic writes, .bak
│   │   │   │   ├── SaveData.cs
│   │   │   │   └── LevelSaveData.cs
│   │   │   ├── Analytics/
│   │   │   │   ├── IAnalyticsService.cs
│   │   │   │   └── GameAnalyticsService.cs ← GA SDK wrapper, try/catch
│   │   │   ├── Audio/
│   │   │   │   ├── IAudioService.cs
│   │   │   │   ├── AudioManager.cs        ← Bus, max 3 SFX, persistent
│   │   │   │   └── AudioClipId.cs         ← Enum
│   │   │   ├── UI/
│   │   │   │   ├── HUD.cs                 ← SO-driven, reset in RestartLevel()
│   │   │   │   ├── RankRevealScreen.cs    ← DOTween sequence
│   │   │   │   ├── LevelSelectGrid.cs
│   │   │   │   ├── PauseMenu.cs
│   │   │   │   ├── MainMenu.cs
│   │   │   │   └── SettingsMenu.cs
│   │   │   └── Tutorial/
│   │   │       ├── TutorialSequencer.cs
│   │   │       └── TutorialPrompt.cs
│   │   │
│   │   ├── ScriptableObjects/
│   │   │   ├── Events/
│   │   │   │   ├── GameEventSO.cs
│   │   │   │   ├── IntGameEventSO.cs
│   │   │   │   ├── StringGameEventSO.cs
│   │   │   │   ├── OnGoldenBallCollected.asset
│   │   │   │   ├── OnLevelCompleted.asset
│   │   │   │   ├── OnFireAbilityActivated.asset
│   │   │   │   ├── OnBallStateChanged.asset
│   │   │   │   └── OnStyleRankCalculated.asset
│   │   │   └── LevelConfigs/
│   │   │       ├── LevelConfigSO.cs       ← [Required] levelId, obstacles, config
│   │   │       ├── biome1_tutorial.asset
│   │   │       ├── biome1_level01.asset → biome1_level12.asset
│   │   │       └── biome1_boss.asset
│   │   │
│   │   ├── Prefabs/
│   │   │   ├── Ball/PenguinBall.prefab
│   │   │   ├── Enemies/Seal.prefab, Walrus.prefab, Boss.prefab
│   │   │   ├── Obstacles/ ← one prefab per obstacle type
│   │   │   ├── Projectiles/ ← pooled prefabs
│   │   │   └── UI/HUD.prefab, RankRevealScreen.prefab, TutorialPrompt.prefab
│   │   │
│   │   ├── Scenes/
│   │   │   ├── Bootstrap.unity            ← Persistent: AudioManager, ServiceLocator
│   │   │   ├── MainMenu.unity
│   │   │   ├── LevelSelect.unity
│   │   │   └── GameScene.unity            ← Additively loaded per level
│   │   │
│   │   ├── Audio/
│   │   │   ├── Music/biome1_theme.mp3
│   │   │   └── SFX/ ← penguin_hoot, fire, seal_bark, walrus_grunt, golden_ball, fanfare
│   │   │
│   │   ├── Materials/
│   │   │   ├── Ice_Surface.mat            ← Near-zero friction PhysicsMaterial
│   │   │   ├── Ball_Default.mat
│   │   │   └── Ball_Fire.mat
│   │   │
│   │   └── Textures/Characters/, Environment/, UI/
│   │
│   └── Tests/
│       └── EditMode/
│           ├── StyleRankScorerTests.cs
│           ├── SaveManagerTests.cs
│           ├── BallStateManagerTests.cs
│           └── LevelConfigValidatorTests.cs
│
├── Packages/manifest.json                 ← Unity Package Manager
└── ProjectSettings/ProjectVersion.txt    ← Unity 6 LTS (6000.x)
```

### Architectural Boundaries

**Input Boundary:** `IInputProvider` is the hard wall between hardware and gameplay. Nothing outside `Input/` reads CoreMotion or touch events directly.

**Physics Boundary:** `BallController` is the only class that calls `Rigidbody.AddForce()`. All physics interactions go through the ball's public API or SO events.

**Persistence Boundary:** `SaveManager` is the only class that reads/writes `SaveData`. All other classes call `SaveManager.Instance` methods only.

**Analytics Boundary:** `IAnalyticsService` is the only entry point to the GA SDK. Wrapped in try/catch — never a hard dependency.

**State Boundary:** `BallStateManager.TryTransitionTo()` is the only valid state transition path.

### Requirements to Structure Mapping

| GDD Requirement | Primary File(s) |
|---|---|
| Tilt controls | `GyroscopeInputProvider.cs` |
| Accessibility controls | `JoystickInputProvider.cs` |
| Ball physics + momentum reward | `BallController.cs` |
| State machine (stun/recovery/dead) | `BallStateManager.cs` |
| Fire ability (timed, blast wave) | `FireAbilityHandler.cs` |
| Seal enemy | `SealController.cs` |
| Walrus enemy | `WalrusController.cs` |
| Boss (4-phase, fireball) | `BossController.cs`, `FireballProjectile.cs` |
| All obstacle types | `Obstacles/` folder |
| Style rank algorithm | `StyleRankScorer.cs` |
| Golden ball tracking | `GoldenBallTracker.cs` |
| World gate validation | `WorldGateChecker.cs` |
| Save/load | `SaveManager.cs` |
| Analytics | `GameAnalyticsService.cs` |
| HUD | `HUD.cs` |
| Rank reveal screen | `RankRevealScreen.cs` |
| Level select grid | `LevelSelectGrid.cs` |
| Tutorial prompts | `TutorialSequencer.cs`, `TutorialPrompt.cs` |
| Audio | `AudioManager.cs`, `IAudioService.cs` |
| Level data | `LevelConfigSO.cs` + `biome1_*.asset` |

### Data Flow

```
Device gyroscope
  → GyroscopeInputProvider (delta-from-baseline, low-pass filter)
  → IInputProvider.GetDirection()
  → BallController._pendingForce (Update)
  → Rigidbody.AddForce() (FixedUpdate)
  → Collision detected → ObstacleType handler
  → BallStateManager.TryTransitionTo()
  → SO event raised (OnBallStateChanged)
  → HUD updated, AudioManager triggered, StyleRankScorer notified

Level complete
  → StyleRankScorer calculates rank
  → OnStyleRankCalculated SO event raised
  → SaveManager.SetLevelRank() called
  → IAnalyticsService.LogLevelComplete() called
  → RankRevealScreen DOTween sequence starts
```

---

## Architecture Validation Results

### Coherence Validation ✅

**Decision Compatibility:**
All 9 ADRs are mutually reinforcing. Unity 6 LTS + URP is compatible with CoreMotion (via Unity's new Input System), DOTween, and GameAnalytics SDK — no package conflicts. The Hybrid SO Events + Service Locator pattern (ADR-009) is correctly scoped: SO events for gameplay-to-systems, Service Locator for system-to-system, Bootstrap singletons for infrastructure. PhysicsMaterial near-zero friction (ADR-002) is consistent with the icy biome aesthetic. `RigidbodyInterpolation.Interpolate` + `Continuous` collision detection are compatible with the 60 FPS target.

**Pattern Consistency:**
The stun/swipe-up recovery (ADR-004) and swipe-to-aim fireball (ADR-006) are state-gated correctly — mutually exclusive by design, no gesture ambiguity. The `TryTransitionTo()` bool return pattern is consistent with the Dead > Stunned priority rule. DOTween (ADR-007) for rank reveal and `IEnumerator` coroutines everywhere else is consistent — no mixed async patterns. LevelIds constants + build-time validator + `[SerializeField]` Inspector assignment form a coherent level identity system.

**Structure Alignment:**
All 9 ADRs map cleanly to named files in the project structure. `BossController.cs`, `FireballProjectile.cs`, and `PowerUpThrow.cs` support the 4-phase boss (ADR-006). `GoldenBallTracker.cs` + `WorldGateChecker.cs` + SaveManager combine correctly for the golden ball world gate. `Bootstrap.unity` hosts the persistent AudioManager and ServiceLocator — nothing else persists across scenes.

---

### Requirements Coverage Validation ✅

**Functional Requirements Coverage:**

| FR | Architectural Support |
|---|---|
| FR1: Gyroscope tilt → ball physics | `GyroscopeInputProvider` → `IInputProvider` → `BallController` ✅ |
| FR2: Accessibility controls | `JoystickInputProvider` (identical `IInputProvider` interface) ✅ |
| FR3: Ball physics | Rigidbody + sphere collider + `PhysicsMaterial` + `BallController` ✅ |
| FR4: Three-layer progression | `StyleRankScorer`, `GoldenBallTracker`, `SaveManager` + SO events ✅ |
| FR5: Style rank algorithm | `StyleRankScorer.cs` + deterministic scoring + Edit Mode test ✅ |
| FR6: Enemy AI (Seal/Walrus) | `SealController`, `WalrusController` ✅ |
| FR7: Fire ability | `FireAbilityHandler` + `Physics.OverlapSphere` blast wave + `BallStateManager.IsFireActive` ✅ |
| FR8: Boss level | `BossController` (4-phase), `FireballProjectile`, swipe-to-aim state gate ✅ |
| FR9: Level system | `LevelManager` + `LevelConfigSO` assets + `WorldGateChecker` ✅ |
| FR10: Obstacle system | All 6 obstacle types in `Obstacles/` ✅ |
| FR11: Ball state machine | `BallStateManager` Rolling/Stunned/Recovery/Dead ✅ |
| FR12: Style rank reveal | `RankRevealScreen` + DOTween sequencer + `hasSeenRankReveal` ✅ |
| FR13: In-game HUD | `HUD.cs` SO-event driven, reset in `RestartLevel()` ✅ |
| FR14: Level select screen | `LevelSelectGrid.cs` ✅ |
| FR15: Tutorial system | `TutorialSequencer` + `TutorialConfig` SO + no rank on Level 0 ✅ |
| FR16: Audio system | `AudioManager` + `IAudioService` + persistent Bootstrap + max 3 SFX ✅ |
| FR17: Persistence | `SaveManager` + JSON + atomic write + `.bak` + per-level dictionary ✅ |
| FR18: Analytics | `GameAnalyticsService` wrapping GA SDK + 6 events + try/catch ✅ |

**Non-Functional Requirements Coverage:**
- **60 FPS:** URP mobile-optimised renderer, object pooling for projectiles/particles, fire particle LOD on A12 ✅
- **Load < 3s:** `LoadSceneAsync` + Bootstrap persistent (no reload overhead) ✅
- **iOS 17/18/26:** Unity 6 LTS target, `SystemInfo.supportsGyroscope` fallback ✅
- **Portrait locked:** Configured at project level ✅
- **Battery:** Object pooling, coroutine-based platform movement (no per-frame instantiation) ✅
- **App Store compliance:** No ads/IAP, age-appropriate — no architectural violations ✅
- **Screen support SE → Pro Max:** Swipe threshold relative to `Screen.height`, 44pt touch targets ✅

---

### Implementation Readiness Validation ✅

**Decision Completeness:**
All 9 ADRs include concrete C# patterns, not just principles. Package versions are locked (Unity 6 LTS 6000.x). The pre-mortem hardening checklist has 17 specific items — each maps to a concrete implementation requirement.

**Structure Completeness:**
Every file in the project tree is named, described, and mapped to a requirement. The Requirements-to-Structure mapping table covers all 18 FRs. Bootstrap → persistent services → GameScene data flow is fully traced.

**Pattern Completeness:**
8 conflict point categories covered with ✅/❌ examples. Anti-pattern quick reference table. Cross-system integration checklist (10 items) — mandatory for every gameplay story. All enforcement guidelines written as actionable "MUST" rules for AI agents.

---

### Gap Analysis Results

**Critical Gaps:** None. All implementation-blocking decisions are defined.

**Important Gaps (non-blocking):**
1. **`LevelConfigSO` field schema** — exact fields (obstacle placements, enemy positions, stun window duration per obstacle, golden ball position, boss phase data) are not yet defined. These will be populated during Epic 1 Story 1 when assets are first created. The architecture establishes the pattern; the schema emerges from implementation.
2. **`StyleRankScorer` thresholds** — exact score boundaries for E/D/C/B/A ranks are tuning values not yet assigned. These should live in `LevelConfigSO` (or a `RankConfigSO`) — not hardcoded — and be defined during the first playtest tuning pass.

**Nice-to-Have:**
- `GameEventSO<T>` generic base class pattern not explicitly shown — first implementor should confirm `UnityEvent<T>` vs custom delegate on first SO event implementation.

---

### Architecture Completeness Checklist

**✅ Requirements Analysis**
- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed (Medium — ~10 systems, single platform)
- [x] Technical constraints identified (CoreMotion variance, A12 LOD, SE screen sizes)
- [x] Cross-cutting concerns mapped (8 concerns)

**✅ Architectural Decisions**
- [x] 9 ADRs documented with concrete code examples
- [x] Technology stack fully specified (Unity 6 LTS, URP, DOTween, GameAnalytics, CoreMotion)
- [x] Integration patterns defined (SO events + Service Locator)
- [x] Performance considerations addressed (pooling, FixedUpdate, LOD, coroutines)

**✅ Implementation Patterns**
- [x] Naming conventions established (PascalCase, `_camelCase`, `SO` suffix, `On` prefix events)
- [x] Structure patterns defined (MonoBehaviour lifecycle rules)
- [x] Communication patterns specified (SO events, Service Locator, physics staging)
- [x] Process patterns documented (error handling, loading states, async via coroutine)

**✅ Project Structure**
- [x] Complete directory structure defined (every file named)
- [x] Component boundaries established (5 hard boundaries)
- [x] Integration points mapped (data flow diagrams for input and level complete paths)
- [x] Requirements-to-structure mapping complete (all 18 FRs)

---

### Architecture Readiness Assessment

**Overall Status:** READY FOR IMPLEMENTATION

**Confidence Level:** High

**Key Strengths:**
- Tilt input identity is architecturally protected — `IInputProvider` interface + calibration rigour means feel can be tuned without touching gameplay code
- Three-layer replayability fully wired: collection, scoring, persistence, and analytics all have named owners
- State machine architecture eliminates ambiguous gesture routing — the stun/swipe-up vs swipe-to-aim conflict is solved at the type-system level
- Pre-mortem checklist converts 17 known failure modes into first-pass implementation requirements
- Save corruption architecturally handled (atomic write + .bak) from day one

**Areas for Future Enhancement:**
- `LevelConfigSO` field schema to be populated during Epic 1 Story 1
- `StyleRankScorer` threshold values to be tuned and extracted to config asset post-first-playtest
- `GameEventSO<T>` base pattern to be confirmed on first SO event implementation
- Play Mode integration tests deferred — add post-MVP when physics integration issues surface

---

### Implementation Handoff

**AI Agent Guidelines:**
- Follow all architectural decisions exactly as documented
- Use implementation patterns consistently across all components
- Respect the 5 hard architectural boundaries (Input, Physics, Persistence, Analytics, State)
- Refer to the Anti-Pattern Quick Reference and Cross-System Integration Checklist for every gameplay story

**First Implementation Priority:**
```
Unity Hub → New Project → 3D Mobile (URP) template
Unity version: Unity 6 LTS (6000.x)
Platform: iOS
→ Set up folder structure per Project Structure section
→ Configure Git LFS with .gitattributes
→ This is Epic 1, Story 1
```
