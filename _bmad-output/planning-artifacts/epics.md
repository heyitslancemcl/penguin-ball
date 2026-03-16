---
stepsCompleted: [step-01-validate-prerequisites, step-02-design-epics, step-03-create-stories, step-04-final-validation]
status: complete
completedAt: '2026-03-15'
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
---

# PenguineBall - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for PenguineBall, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

FR1: Gyroscope tilt input drives ball physics movement as the primary control scheme
FR2: Accessibility alternative controls — virtual joystick + jump button with full gameplay parity to tilt controls
FR3: Ball physics simulation — rolling, bouncing, collision detection on ice/platform surfaces
FR4: Three-layer progression system: gold stars, golden ball collectibles, and style rank scoring (E→A)
FR5: Style rank algorithm — deterministic scoring based on speed + fire ability use + enemy defeats + hazards avoided
FR6: Enemy AI — Seal (patrol/sliding knock-off behaviour) and Walrus (stationary/slow, ranged ice spike projectiles)
FR7: Fire ability power-up — timed (20–30s), dual-use (offensive: defeats enemies, breaks walls; defensive: clears ice spikes); on-bounce radial blast wave
FR8: Boss level — 4-phase survival challenge; boss throws ice spikes, exploding ice blocks, and fire power-ups; player swipes to aim and shoot fireballs; 4 hits to defeat boss
FR9: Level system — tutorial level (Level 0), 9–12 main levels, 1 boss level gated by golden ball count
FR10: Obstacle system — static ice spikes, ground holes, moving platforms, stun obstacles (swipe-up recovery), fatal bounce obstacles, breakable walls (fire-only)
FR11: Ball state machine — Rolling, Stunned (1s swipe-up window), Recovery, Dead states
FR12: Style rank reveal screen — sequential DOTween punch-in animation with first-play cinematic and condensed repeat
FR13: In-game HUD — golden ball slot, lives/attempts counter, fire ability timer
FR14: Level select screen — simple grid with per-level completion state display
FR15: Tutorial system — diegetic prompts via TutorialSequencer; no rank reveal on Level 0; "Great start!" completion screen
FR16: Audio system — adaptive music, contextual SFX, character voice identity
FR17: Persistence — JSON save file with per-level data keyed by level ID string; XOR obfuscation; atomic writes
FR18: Analytics — GameAnalytics SDK events: levelStart, levelComplete, levelFail, goldenBallCollected, rankAchieved, rankScreenDwellTime

### NonFunctional Requirements

NFR1: Performance — 60 FPS target; level load time < 3 seconds
NFR2: Platform — iOS 17, 18, 26; portrait locked
NFR3: Engine — Unity 6 LTS with URP (Universal Render Pipeline) for mobile optimisation
NFR4: Input — CoreMotion gyroscope primary with Unity UI accessibility fallback
NFR5: Battery — optimised for mobile play sessions (object pooling, LOD particles)
NFR6: App Store compliance — no ads, no IAP, age rating 4+/9+
NFR7: Screen support — iPhone SE (4th gen) through iPhone Pro Max
NFR8: Code standards — C# PascalCase classes/methods, _camelCase private fields, one class per file

### Additional Requirements

- **Starter Template (CRITICAL — Epic 1 Story 1):** Unity 3D Mobile (URP) template via Unity Hub; Unity 6 LTS; platform set to iOS; this must be the very first implementation story
- **Project folder structure:** `Assets/_Project/Scripts/Core|Input|Obstacles|Enemies|Progression|UI|Audio|Analytics` with matching `ScriptableObjects/`, `Prefabs/`, `Scenes/`, `Audio/`, `Materials/`, `Textures/` folders
- **Code architecture:** Hybrid ScriptableObject Events + Service Locator pattern (ADR-009); gameplay → systems via SO events; systems → services via ServiceLocator interfaces; only Bootstrap scene objects use persistent singletons
- **Input abstraction:** `IInputProvider` interface with `GyroscopeInputProvider` and `JoystickInputProvider`; `SystemInfo.supportsGyroscope` auto-fallback to joystick
- **CoreMotion calibration:** Baseline captured at every level load (not app launch); delta-from-baseline; low-pass filter; auto-recalibrate on drift; re-calibration via three-finger tap; `OnApplicationPause(false)` triggers recalibration
- **Save integrity:** Atomic writes (temp file → rename); `.bak` backup on every successful save; save triggered on level complete, golden ball collected, `OnApplicationPause`, `OnApplicationFocus(false)`
- **Object pooling:** Required for ice spike projectiles, fire particles, and enemy spawns — no `Instantiate`/`Destroy` during gameplay
- **Unit tests:** Unity Test Framework Edit Mode tests for `StyleRankScorer`, `SaveManager`, `BallStateManager`, `LevelConfig` (levelId uniqueness)
- **Build pipeline:** Local Xcode build → Archive → App Store Connect; TestFlight for pre-launch testing; GitHub + Git LFS for binary assets
- **Version control:** GitHub with Git LFS tracking `.unity`, `.prefab`, `.asset`, `.controller`, textures, audio, 3D models; `main` for stable/App Store, `develop` for active development
- **LevelConfig levelId:** `biome{N}_level{NN}` format; uniqueness validated at build time; never hardcoded in analytics calls

### FR Coverage Map

FR1: Epic 1 — Gyroscope tilt → ball physics movement (primary control)
FR2: Epic 1 — Accessibility joystick + jump button (full gameplay parity)
FR3: Epic 1 — Ball physics simulation (rolling, bouncing, collision)
FR11: Epic 1 — Ball state machine (Rolling → Stunned → Recovery → Dead)
FR9: Epic 2 — Level system (tutorial L0, 9–12 main levels, 1 boss per biome)
FR10: Epic 2 — Obstacle system (6 types)
FR14: Epic 2 — Level select screen with per-level completion state
FR17: Epic 2 — JSON persistence, atomic writes, .bak backup
FR4: Epic 3 — Three-layer progression (gold stars, golden balls, style rank E→A)
FR5: Epic 3 — Style rank algorithm (deterministic scoring)
FR12: Epic 3 — Rank reveal screen (DOTween cinematic sequence)
FR13: Epic 3 — In-game HUD (golden ball slot, lives counter, fire timer)
FR6: Epic 4 — Enemy AI (Seal patrol/knock-off, Walrus stationary/ice spikes)
FR7: Epic 4 — Fire ability power-up (timed, offensive/defensive, blast wave)
FR15: Epic 5 — Tutorial system (diegetic prompts, no rank reveal on L0)
FR16: Epic 5 — Audio system (adaptive music, SFX, character voice)
FR8: Epic 6 — Boss level (4-phase survival, swipe-to-aim fireballs, 4 hits)
FR18: Epic 6 — Analytics (GameAnalytics SDK, 6 defined events)

## Epic List

### Epic 1: Foundation — Playable Ball on Device
A player can launch the app, see a ball on screen, and control it with gyroscope tilt or the accessibility joystick. The ball rolls, bounces, and responds to physics on ice surfaces. The Bootstrap + GameScene structure is in place. This is the core identity of the game running end-to-end.
**FRs covered:** FR1, FR2, FR3, FR11
**Note:** Project init (Unity 3D Mobile URP template, Git LFS, folder structure, Service Locator scaffold, object pooling foundation) is Story 1.1.

### Epic 2: Levels, Obstacles & Save System
A player can select a level from a grid, navigate through it encountering all obstacle types, die, and retry. Progress persists across sessions. The per-biome boss level is gated by collecting golden balls in at least 4 levels of that biome (derived count from save data — no stored flag).
**FRs covered:** FR9, FR10, FR14, FR17
**Note:** MVP is Biome 1 only. Multi-biome and ultimate final boss are post-MVP roadmap. Boss gate check: count of `goldenBallCollected: true` across `biome1_*` level entries ≥ 4.

### Epic 3: Progression — Golden Balls, Style Ranks & HUD
A player earns a style rank at the end of each level and sees it revealed through a cinematic DOTween animation. Golden balls are tracked and displayed. The HUD shows live fire timer, lives, and golden ball slot. Progress carries meaning across sessions.
**FRs covered:** FR4, FR5, FR12, FR13

### Epic 4: Enemies & Fire Ability
A player encounters Seal and Walrus enemies with distinct AI behaviours. The fire ability power-up enables offensive and defensive tactics. Breakable walls become skill-expression gates. Object pooling handles projectiles and particles.
**FRs covered:** FR6, FR7

### Epic 5: Tutorial, Audio & Polish
A new player is onboarded through Level 0 with in-world diegetic prompts — no rank reveal, just a "Great start!" completion screen. All levels have adaptive music, contextual SFX, and character voice identity.
**FRs covered:** FR15, FR16

### Epic 6: Boss Level & Launch Readiness (Post-MVP Roadmap Note: Multi-biome final boss is deferred — Biome 1 only at launch)
A player who has collected golden balls in at least 4 Biome 1 levels can face the 4-phase boss — dodging ice spikes, exploding ice blocks, catching fire power-ups, and landing 4 swipe-aimed fireballs to win. Analytics telemetry fires throughout all sessions. The game is App Store ready.
**FRs covered:** FR8, FR18

---

## Epic 1: Foundation — Playable Ball on Device

A player can launch the app, see a ball on screen, and control it with gyroscope tilt or the accessibility joystick. The ball rolls, bounces, and responds to physics on ice surfaces. The Bootstrap + GameScene structure is in place. This is the core identity of the game running end-to-end.

### Story 1.1: Project Initialization & Architecture Scaffold

As a developer,
I want a fully configured Unity project with the correct template, folder structure, and core architecture scaffolding,
So that all future stories have a consistent, well-organised foundation to build on.

**Acceptance Criteria:**

**Given** Unity Hub is installed
**When** the project is created
**Then** it uses the Unity 3D Mobile (URP) template with Unity 6 LTS, platform set to iOS, portrait locked

**Given** the project is created
**When** the folder structure is inspected
**Then** `Assets/_Project/Scripts/Core|Input|Obstacles|Enemies|Progression|UI|Audio|Analytics`, `ScriptableObjects/LevelConfigs|Events`, `Prefabs/Ball|Enemies|Obstacles|Projectiles|UI`, `Scenes/`, `Audio/`, `Materials/`, `Textures/` all exist

**Given** the project is created
**When** the Bootstrap scene is opened
**Then** a persistent Bootstrap scene exists alongside a GameScene (additive), and neither is destroyed on load

**Given** a system requires a service
**When** it calls `ServiceLocator.Get<IInterface>()`
**Then** the registered implementation is returned; if unregistered, a clear error is thrown

**Given** gameplay requires pooled objects
**When** `ObjectPool.Get<T>()` is called
**Then** a pooled instance is returned; `ObjectPool.Release()` returns it to the pool; no `Instantiate`/`Destroy` occurs

**Given** the project is initialised
**When** Git is inspected
**Then** a `.gitattributes` file tracks `*.unity`, `*.prefab`, `*.asset`, `*.controller`, `*.png`, `*.jpg`, `*.mp3`, `*.wav`, `*.ogg`, `*.fbx`, `*.obj` via Git LFS; a `main` and `develop` branch exist

### Story 1.2: Input Abstraction Layer

As a player,
I want to control the ball using my device's gyroscope tilt — or a virtual joystick if tilt isn't available —
So that I have a responsive, consistent control experience regardless of my device.

**Acceptance Criteria:**

**Given** the game starts on a device with a gyroscope
**When** the input system initialises
**Then** `GyroscopeInputProvider` is registered with `ServiceLocator` and `BallController` receives tilt input via `IInputProvider`

**Given** the game starts on a device without a gyroscope
**When** `SystemInfo.supportsGyroscope` returns false
**Then** `JoystickInputProvider` is automatically registered instead, with no error or crash

**Given** the gyroscope is active
**When** a level loads
**Then** a new CoreMotion baseline is captured (not at app launch) and input is expressed as delta-from-baseline with a configurable low-pass filter applied

**Given** the player has been playing for a while and tilt drift is detected
**When** drift exceeds the threshold mid-level
**Then** a recalibration suggestion toast is shown; three-finger tap triggers haptic feedback and immediate recalibration

**Given** the app is backgrounded and then resumed
**When** `OnApplicationPause(false)` fires
**Then** the gyroscope baseline is recaptured automatically

**Given** `JoystickInputProvider` is active
**When** the player moves the virtual joystick
**Then** the ball receives identical physics input as a tilt equivalent — full gameplay parity confirmed

### Story 1.3: Ball Physics & BallController

As a player,
I want the ball to roll, bounce, and glide realistically across ice surfaces,
So that movement feels satisfying and physically grounded.

**Acceptance Criteria:**

**Given** the ball is on an ice surface
**When** the player tilts the device
**Then** the ball rolls with near-zero friction (`PhysicsMaterial` configured), `RigidbodyInterpolation.Interpolate` enabled for smooth visuals

**Given** the ball is moving at high speed
**When** `FixedUpdate` runs
**Then** velocity is clamped to the configured max value in `LevelConfig` — no unbounded acceleration

**Given** fast-moving gameplay
**When** the ball approaches a thin obstacle
**Then** `collisionDetectionMode = Continuous` prevents tunnelling

**Given** the ball collects a golden ball (stub event for now)
**When** the collection event fires
**Then** a short `AddForce` impulse in the current travel direction is applied (magnitude tunable via `LevelConfig`)

**Given** the ball hasn't moved
**When** velocity stays below the threshold for more than 2 seconds and ball is not in Stunned state
**Then** a "Stuck?" restart prompt is displayed

**Given** the ball goes below the kill plane Y-threshold
**When** the kill plane trigger fires
**Then** the ball visibly arcs off the platform before the death is registered — not an instant disappear

### Story 1.4: Ball State Machine

As a developer,
I want a robust ball state machine that enforces valid state transitions,
So that all game systems can rely on the ball's current state without race conditions or ambiguity.

**Acceptance Criteria:**

**Given** any game system wants to change ball state
**When** it calls `BallStateManager.TryTransitionTo(BallState.X)`
**Then** the state machine evaluates validity and returns `true` (transitioned) or `false` (rejected) — direct state assignment is never permitted

**Given** the ball is Rolling
**When** a stun obstacle is hit
**Then** `TryTransitionTo(Stunned)` succeeds; a 1-second swipe-up window opens after the stun animation completes (not on collision)

**Given** the ball is Stunned
**When** the player performs a swipe-up within the 1-second window
**Then** `TryTransitionTo(Recovery)` succeeds, then `TryTransitionTo(Rolling)` completes the recovery

**Given** the ball is Stunned and the window expires
**When** no swipe-up is detected
**Then** `TryTransitionTo(Dead)` is called; Dead state takes priority over all other states

**Given** simultaneous Dead and Stunned triggers arrive
**When** both `TryTransitionTo` calls are evaluated
**Then** Dead always wins (priority: Dead > Stunned > Recovery > Rolling)

**Given** the `BallStateManager` is unit tested
**When** Edit Mode tests run in Unity Test Framework
**Then** all state transition rules and priority logic pass with 100% coverage of the state table

---

## Epic 2: Levels, Obstacles & Save System

A player can select a level from a grid, navigate through it encountering all obstacle types, die, and retry. Progress persists across sessions. The Biome 1 boss is gated by collecting golden balls in at least 4 levels.

### Story 2.1: Save Manager & Persistence

As a player,
I want my progress saved automatically so I never lose it due to a crash, backgrounding, or closing the app,
So that I can pick up exactly where I left off every session.

**Acceptance Criteria:**

**Given** a level is completed or a golden ball is collected
**When** the save trigger fires
**Then** the save file is written atomically (temp file → rename) to `Application.persistentDataPath` with XOR obfuscation applied

**Given** a save is written successfully
**When** the file system is inspected
**Then** a `.bak` backup of the previous save exists alongside the current save file

**Given** the app is backgrounded or loses focus
**When** `OnApplicationPause(true)` or `OnApplicationFocus(false)` fires
**Then** the save is triggered automatically with no data loss

**Given** a save file exists at startup
**When** `SaveManager` loads the file
**Then** per-level data is correctly restored keyed by levelId string (e.g. `biome1_level01`)

**Given** the save file is corrupt or missing
**When** `SaveManager` attempts to load
**Then** the `.bak` file is used as fallback; if both fail, a fresh save is initialised with no crash

**Given** the `SaveManager` is unit tested
**When** Edit Mode tests run
**Then** save/load round-trip, corruption recovery, and atomic write behaviour all pass

### Story 2.2: Level Config & Scene Management

As a developer,
I want a robust level configuration and scene loading system,
So that each level loads reliably with its correct settings and any loading failure degrades gracefully.

**Acceptance Criteria:**

**Given** a level is defined
**When** its `LevelConfigSO` is inspected
**Then** it has a non-empty, unique `levelId` in `biome{N}_level{NN}` format; build-time validator rejects duplicates or empty values

**Given** a player starts a level
**When** `LevelManager` loads the level
**Then** the GameScene is loaded additively over the persistent Bootstrap scene using `LoadSceneAsync`

**Given** `LoadSceneAsync` fails
**When** the error callback fires
**Then** the player is returned to the level select screen with no crash

**Given** a `LevelConfigSO` is referenced in a scene
**When** the Inspector is inspected
**Then** it is assigned via serialised Inspector field — `Resources.Load` is never used at runtime

**Given** a level loads
**When** `LevelManager.StartLevel()` is called
**Then** the HUD is explicitly reset, the gyroscope baseline is recaptured, and the `levelStart` analytics event fires (stub for now)

### Story 2.3: Obstacle System

As a player,
I want to encounter a variety of obstacles that challenge me in different ways,
So that each level feels distinct and requires different skills to navigate.

**Acceptance Criteria:**

**Given** the ball contacts a `StunObstacle`
**When** the collision registers
**Then** `BallStateManager.TryTransitionTo(Stunned)` is called; a 1-second swipe-up window opens after the stun animation completes; a swipe-up arrow pulse prompt appears

**Given** the ball contacts a `FatalBounce` obstacle
**When** the collision registers
**Then** a strong directional impulse sends the ball off course toward the kill plane; death registers when the kill plane is reached

**Given** the ball contacts a `BreakableWall` without fire active
**When** the collision registers
**Then** the wall blocks the ball's path; no shattering occurs

**Given** the ball contacts a `BreakableWall` with fire active
**When** the collision registers
**Then** the wall shatter animation plays and the collider is disabled the same frame — no invisible blocking

**Given** the ball contacts an `IceSpike` without fire active
**When** the collision registers
**Then** death is triggered

**Given** the ball contacts an `IceSpike` with fire active
**When** the collision registers
**Then** the ice spike is destroyed and the ball continues

**Given** a `MovingPlatform` is in the scene
**When** the level runs
**Then** the platform moves via coroutine interpolation (not `Update()` polling); movement pauses while ball is in Stunned state

**Given** the ball falls through a `GroundHole`
**When** the kill plane trigger fires
**Then** death is registered with the same cinematic arc as a normal kill plane death

### Story 2.4: Level Select Screen

As a player,
I want to see all levels in a grid and know which ones I've completed and whether the boss is unlocked,
So that I can choose where to play and feel motivated by my progress.

**Acceptance Criteria:**

**Given** the player opens the level select screen
**When** the grid renders
**Then** all Biome 1 levels are displayed including the tutorial (L0), 9–12 main levels, and the boss level

**Given** a level has been completed
**When** the level select grid renders
**Then** that level's tile shows its completion state (completed indicator visible)

**Given** a level has had its golden ball collected
**When** the level select grid renders
**Then** that level's tile shows the golden ball as collected

**Given** fewer than 4 Biome 1 levels have `goldenBallCollected: true` in save data
**When** the level select grid renders
**Then** the boss level tile is locked and non-tappable; a counter shows e.g. "2/4 golden balls"

**Given** 4 or more Biome 1 levels have `goldenBallCollected: true`
**When** the level select grid renders
**Then** the boss level tile is unlocked and tappable; the golden ball counter shows "4+/4"

**Given** the player taps an unlocked level tile
**When** the tap registers
**Then** that level loads via `LevelManager`

### Story 2.5: Level Flow & Retry

As a player,
I want a complete loop of starting a level with 5 lives, dying, retrying, and either completing the level or being sent back to level select when all lives are spent,
So that the core game loop feels complete and there is meaningful consequence to failure.

**Acceptance Criteria:**

**Given** a level starts
**When** `LevelManager.StartLevel()` is called
**Then** the player begins with 5 lives; the HUD displays the lives counter at 5; the attempt counter increments in save data

**Given** the ball enters the Dead state and lives remain
**When** death is confirmed
**Then** the death animation plays, lives decrement by 1, and a retry prompt is shown with the updated lives count visible

**Given** the player taps retry with lives remaining
**When** `RestartLevel()` is called
**Then** the HUD resets explicitly (lives count preserved), the ball respawns at the level start position, and the gyroscope baseline is recaptured

**Given** the ball enters the Dead state and lives reach 0
**When** death is confirmed
**Then** an "Out of lives" screen is shown with no retry option; the player can only return to level select; total attempts are saved

**Given** the player completes a level
**When** the level complete trigger fires
**Then** `completed: true` and remaining lives count are written to save data, save triggers, and the rank reveal screen stub is shown

**Given** the player exits to level select mid-level
**When** the exit is confirmed
**Then** the current attempt is recorded, save triggers, and the level select screen loads cleanly with no scene leaks

---

## Epic 3: Progression — Golden Balls, Style Ranks & HUD

A player earns a style rank at the end of each level and sees it revealed through a cinematic DOTween animation. Golden balls are tracked and displayed. The HUD shows live fire timer, lives, and golden ball slot. Progress carries meaning across sessions.

### Story 3.1: Golden Ball Collection & Tracking

As a player,
I want to collect a golden ball hidden in each level and have it tracked permanently,
So that my skill and exploration are rewarded and contribute toward unlocking the boss.

**Acceptance Criteria:**

**Given** a golden ball exists in a level
**When** the ball rolls over it
**Then** the `OnGoldenBallCollected` ScriptableObject event fires; `goldenBallCollected: true` is written to save data for that level; save triggers immediately

**Given** the golden ball is collected
**When** the collection event fires
**Then** a short `AddForce` impulse is applied in the current travel direction (magnitude from `LevelConfig`); a brief golden particle trail plays

**Given** a level is replayed and the golden ball was already collected
**When** the level loads
**Then** the golden ball collectible is not present in the scene — it cannot be double-collected

**Given** the golden ball HUD slot is visible
**When** the golden ball is collected mid-level
**Then** the HUD slot updates immediately to show collected state

**Given** save data is inspected after collection
**When** `goldenBallCollected` is queried for that levelId
**Then** it returns `true` and persists across app restarts

### Story 3.2: Style Rank Scorer

As a player,
I want to receive a style rank (E through A) at the end of each level based on how I played,
So that skilled, expressive play is recognised and I'm motivated to improve.

**Acceptance Criteria:**

**Given** a level is completed
**When** `StyleRankScorer.CalculateRank()` is called
**Then** a deterministic rank (E, D, C, B, or A) is returned based on: speed score + fire ability uses + enemy defeats + hazards avoided — same inputs always produce the same rank

**Given** the player achieves a rank
**When** it is compared to their previous best for that level
**Then** only the higher rank is written to `bestRank` in save data; score is written to `bestScore` if higher

**Given** the scoring inputs are captured during play
**When** the level runs
**Then** speed is tracked continuously; fire ability activations, enemy defeats, and hazards avoided are each incremented via ScriptableObject events with per-session HashSet deduplication (no double-counting)

**Given** the `StyleRankScorer` is unit tested
**When** Edit Mode tests run
**Then** known input combinations produce the correct expected rank; boundary conditions for each rank threshold are covered

### Story 3.3: In-Game HUD

As a player,
I want a clean HUD showing my lives, fire timer, and golden ball status while I play,
So that I always know my current state without breaking immersion.

**Acceptance Criteria:**

**Given** a level starts
**When** the HUD initialises
**Then** lives counter shows 5, golden ball slot shows uncollected (or collected if already saved), fire timer is hidden

**Given** the player loses a life
**When** `OnBallStateChanged` fires with Dead state
**Then** the lives counter decrements immediately and visibly

**Given** the fire ability is activated
**When** `OnFireAbilityActivated` SO event fires
**Then** the fire timer appears and counts down for the configured duration (20–30s from `LevelConfig`); it disappears when the ability expires

**Given** the golden ball is collected in-level
**When** `OnGoldenBallCollected` fires
**Then** the golden ball slot updates to collected state immediately

**Given** `RestartLevel()` is called
**When** the HUD resets
**Then** lives counter returns to its current value (not 5 — preserved from prior deaths), fire timer is hidden, golden ball slot reflects save state

**Given** the HUD is driven by events
**When** its MonoBehaviour lifecycle is inspected
**Then** all SO event subscriptions are in `OnEnable()` and unsubscriptions are in `OnDisable()` — no memory leaks

### Story 3.4: Rank Reveal Screen

As a player,
I want a satisfying cinematic rank reveal at the end of each level,
So that completing a level feels like a rewarding moment worth replaying.

**Acceptance Criteria:**

**Given** a level is completed for the first time
**When** the rank reveal screen appears
**Then** the full cinematic plays (~2s): rank letter slams in with 0.3s overshoot bounce → score ticks up over 0.8s → golden ball result fades in → fanfare plays; final state holds 1.5s before "tap to continue"

**Given** a level is completed on a subsequent play
**When** `hasSeenRankReveal` is `true` for that level
**Then** the condensed sequence plays (~1.2s) instead of the full cinematic

**Given** the rank reveal completes
**When** the player taps to continue
**Then** `hasSeenRankReveal` is set to `true` in save data; best rank and score are updated if improved; the level select screen loads

**Given** the tutorial (Level 0) is completed
**When** the completion screen appears
**Then** the rank reveal is NOT shown — a "Great start!" screen is shown instead; golden ball collected status is displayed

**Given** the rank reveal DOTween sequence runs
**When** the sequence is inspected
**Then** it uses a `DOTween` sequencer (not coroutines); first-play and repeat sequences are driven by `hasSeenRankReveal` bool from save data

---

## Epic 4: Enemies & Fire Ability

A player encounters Seal and Walrus enemies with distinct AI behaviours. The fire ability power-up enables offensive and defensive tactics. Breakable walls become skill-expression gates. Object pooling handles projectiles and particles.

### Story 4.1: Fire Ability Power-Up

As a player,
I want to collect a fire ability power-up that lets me defeat enemies, break walls, and clear ice spikes for a limited time,
So that timing and positioning the fire ability becomes a core skill expression.

**Acceptance Criteria:**

**Given** a fire ability power-up exists in a level
**When** the ball rolls over it
**Then** `OnFireAbilityActivated` SO event fires; `IsFireActive` is set to `true` on `BallStateManager`; the timer starts for the configured duration (20–30s from `LevelConfig`)

**Given** the fire ability is active
**When** the timer expires
**Then** `IsFireActive` is set to `false`; `OnFireAbilityDeactivated` SO event fires; fire visual effects are removed

**Given** the fire ability is active and the ball bounces
**When** the ball makes contact with a surface
**Then** `Physics.OverlapSphere` fires at the impact point; radial force is applied to all enemies and hazards within range; ice spikes in range are destroyed

**Given** the fire ability is active and the ball contacts an enemy
**When** the collision registers
**Then** the enemy is defeated (handled per enemy type in Stories 4.2 and 4.3)

**Given** the fire ability is active and the ball contacts a `BreakableWall`
**When** the collision registers
**Then** the wall shatters (AC covered in Story 2.3 — confirmed still valid with `IsFireActive` flag)

**Given** fire particles are active
**When** running on an A12 chip or older
**Then** the fire particle system uses the LOD quality tier configured in URP mobile quality settings — no frame rate drop below 60 FPS target

**Given** fire particles and blast wave effects are spawned
**When** `ObjectPool` is inspected
**Then** all fire particle instances and blast effects are pooled — no `Instantiate`/`Destroy` calls during gameplay

### Story 4.2: Seal Enemy AI

As a player,
I want to encounter patrolling Seal enemies that slide into me and knock me off course,
So that I must time my movement to avoid or defeat them.

**Acceptance Criteria:**

**Given** a Seal is placed in a level
**When** the level starts
**Then** the Seal patrols its configured waypoint path continuously at its configured speed (from `LevelConfig`)

**Given** the Seal is patrolling and reaches a waypoint
**When** the waypoint is reached
**Then** the Seal reverses direction smoothly — no snapping or teleporting

**Given** the ball contacts a Seal without fire active
**When** the collision registers
**Then** the Seal applies a directional knock-off impulse to the ball; if the ball is knocked off the platform to the kill plane, death is registered

**Given** the ball contacts a Seal with fire active
**When** the collision registers
**Then** the Seal is defeated; a defeat animation plays; `OnEnemyDefeated` SO event fires (used by `StyleRankScorer`)

**Given** the fire ability blast wave fires near a Seal
**When** `Physics.OverlapSphere` includes the Seal
**Then** the Seal is defeated via the same defeat path as direct contact

**Given** Seal instances are needed in a level
**When** `ObjectPool.Get<SealController>()` is called
**Then** a pooled Seal instance is returned; `ObjectPool.Release()` returns it on defeat — no `Instantiate`/`Destroy`

### Story 4.3: Walrus Enemy AI

As a player,
I want to encounter stationary Walrus enemies that fire ice spike projectiles at me,
So that I must dodge ranged attacks while navigating the level — or use fire to clear them.

**Acceptance Criteria:**

**Given** a Walrus is placed in a level
**When** the level starts
**Then** the Walrus is stationary at its configured position; it begins firing ice spike projectiles at its configured interval (from `LevelConfig`)

**Given** the Walrus fires a projectile
**When** the fire interval triggers
**Then** a pooled `IceSpikeProjectile` is launched toward the ball's current position; it travels until it hits the ball, a surface, or exits the level bounds

**Given** an ice spike projectile hits the ball without fire active
**When** the collision registers
**Then** death is triggered (same path as `IceSpike` obstacle contact in Story 2.3)

**Given** an ice spike projectile hits the ball with fire active
**When** the collision registers
**Then** the projectile is destroyed; the ball continues unharmed

**Given** the ball contacts a Walrus directly without fire active
**When** the collision registers
**Then** the Walrus applies a knock-off impulse; the ball is redirected

**Given** the ball contacts a Walrus with fire active
**When** the collision registers
**Then** the Walrus is defeated; `OnEnemyDefeated` SO event fires

**Given** the fire ability blast wave fires near a Walrus
**When** `Physics.OverlapSphere` includes the Walrus
**Then** the Walrus is defeated via the same defeat path as direct fire contact

**Given** ice spike projectiles are spawned during gameplay
**When** `ObjectPool` is inspected
**Then** all `IceSpikeProjectile` instances are pooled — no `Instantiate`/`Destroy` during gameplay

---

## Epic 5: Tutorial, Audio & Polish

A new player is onboarded through Level 0 with in-world diegetic prompts — no rank reveal, just a "Great start!" completion screen. All levels have adaptive music, contextual SFX, and character voice identity.

### Story 5.1: Audio Manager

As a player,
I want the game to have adaptive music, contextual sound effects, and character voice that respond to what's happening,
So that audio enhances the feel of every moment without becoming repetitive or overwhelming.

**Acceptance Criteria:**

**Given** the app launches
**When** the Bootstrap scene initialises
**Then** a persistent `AudioSource` exists on the Bootstrap scene object; it is never destroyed on load; `AudioManager` registers as `IAudioService` via `ServiceLocator`

**Given** any game system needs to play audio
**When** it calls `ServiceLocator.Get<IAudioService>()`
**Then** the registered `AudioManager` is returned; no direct `AudioSource` references exist outside `AudioManager`

**Given** multiple SFX are triggered simultaneously
**When** more than 3 concurrent SFX are requested
**Then** only 3 play at once; the lowest priority SFX is dropped — no audio clipping or Unity warnings

**Given** the player enters a level
**When** the level starts
**Then** the appropriate adaptive music track begins; music transitions smoothly based on gameplay state (e.g. normal play, fire active, near death)

**Given** gameplay events occur (ball bounce, enemy defeat, golden ball collect, stun)
**When** the corresponding SO event fires
**Then** the correct contextual SFX plays at the appropriate volume via the centralised audio bus

**Given** a character voice line is triggered
**When** the voice event fires
**Then** the character voice plays without cutting off music; voice and music are on separate audio channels

### Story 5.2: Tutorial Level (Level 0)

As a new player,
I want to be guided through my first level with in-world prompts that teach me how to play,
So that I learn the controls and mechanics naturally without reading a manual.

**Acceptance Criteria:**

**Given** the player selects Level 0 (tutorial)
**When** the level loads
**Then** the `TutorialSequencer` initialises and subscribes to game events; prompt content is loaded from `TutorialConfig` ScriptableObject

**Given** the tutorial is running
**When** each game event fires (first tilt movement, first bounce, first stun obstacle approach, etc.)
**Then** the corresponding diegetic prompt activates in sequence — prompts are world-space, not overlaid UI panels

**Given** a tutorial prompt is active
**When** the player performs the prompted action
**Then** the prompt deactivates and the next prompt in the sequence becomes ready to trigger

**Given** the player completes Level 0
**When** the level complete trigger fires
**Then** the rank reveal screen is NOT shown; a "Great start!" completion screen is shown instead with golden ball collected status displayed

**Given** Level 0 is completed
**When** save data is inspected
**Then** `completed: true` is written for `biome1_tutorial`; no `bestRank` or `bestScore` is stored for the tutorial

**Given** the player replays Level 0
**When** the level loads
**Then** the `TutorialSequencer` does not re-trigger prompts for actions the player has already completed in a prior session — prompts respect prior completion state from `TutorialConfig`

**Given** the `TutorialSequencer` subscribes to events
**When** its MonoBehaviour lifecycle is inspected
**Then** all subscriptions are in `OnEnable()` and unsubscriptions are in `OnDisable()` — no memory leaks

---

## Epic 6: Boss Level & Launch Readiness

A player who has collected golden balls in at least 4 Biome 1 levels can face the 4-phase boss — dodging ice spikes, exploding ice blocks, catching fire power-ups, and landing 4 swipe-aimed fireballs to win. Analytics telemetry fires throughout all sessions. The game is App Store ready.

### Story 6.1: Boss Controller & Phase System

As a player,
I want to face a challenging multi-phase boss that escalates in difficulty,
So that the final battle feels like a climactic test of everything I've learned.

**Acceptance Criteria:**

**Given** the player has 4+ golden balls collected in Biome 1 and selects the boss level
**When** the level loads
**Then** `BossController` initialises in Phase 1; phase data (duration, projectile patterns, spawn rates) is loaded from `LevelConfig`

**Given** Phase 1 is active
**When** the level starts
**Then** the boss fires at a slow, readable rate — teaching the player the dodge patterns; the golden ball is accessible in Phase 1 only

**Given** the player lands a fireball hit on the boss
**When** the hit registers
**Then** `BossController` transitions to the next phase; each subsequent phase increases projectile speed, spawn rate, and pattern complexity

**Given** Phase 4 is active
**When** the boss attacks
**Then** ice spikes, exploding ice blocks, and fire power-up throws all occur simultaneously at the fastest configured rate

**Given** an `IceSpikeProjectile` is thrown by the boss
**When** it is launched
**Then** a pooled projectile with a fast, thin, electric blue trail travels toward the ball; it is dodgeable by moving out of its path

**Given** an exploding ice block is thrown
**When** it lands
**Then** it flashes for 0.5s then `Physics.OverlapSphere` applies a blast radius stun to the ball if in range

**Given** the boss throws a fire power-up
**When** it is thrown
**Then** a distinct wind-up animation plays; the power-up has a 2–3s collection window before it disappears; collecting it activates the fire ability

**Given** the ball enters the Dead state during the boss level
**When** death is confirmed
**Then** the standard 5-lives retry flow applies (from Story 2.5); the boss phase resets to Phase 1 on retry

### Story 6.2: Swipe-to-Aim Fireball System

As a player,
I want to aim and shoot fireballs at the boss by swiping the screen,
So that defeating the boss requires both dodging and precise offensive targeting.

**Acceptance Criteria:**

**Given** the fire ability is active and the ball is in Rolling state
**When** the player swipes the screen
**Then** the swipe start→end delta is mapped to a world direction vector; a pooled `FireballProjectile` is launched in that direction

**Given** a swipe-to-aim gesture is in progress
**When** `BallStateManager` is queried
**Then** swipe-to-aim is only processed if `IsFireActive && state == Rolling` — it is mutually exclusive with swipe-up recovery (Stunned state); no gesture conflict occurs

**Given** a `FireballProjectile` is launched
**When** it travels
**Then** it continues until it hits the boss or exits the screen bounds; it is then returned to the pool

**Given** a fireball hits the boss
**When** the hit registers
**Then** the boss hit count increments; a hit reaction animation plays on `BossController`

**Given** the boss has been hit 4 times
**When** the 4th fireball connects
**Then** `BossController` transitions to Defeated state; a defeat sequence plays; the level complete trigger fires

**Given** `FireballProjectile` instances are spawned
**When** `ObjectPool` is inspected
**Then** all fireball instances are pooled — no `Instantiate`/`Destroy` during gameplay

**Given** the swipe gesture is evaluated
**When** the touch delta is calculated
**Then** the swipe threshold is relative to `Screen.height`; minimum touch target is 44pt — no accidental triggers from small touches

### Story 6.3: GameAnalytics Integration

As a developer,
I want analytics events firing throughout all gameplay sessions,
So that I can identify level drop-off, rank distribution, and player behaviour from day one of launch.

**Acceptance Criteria:**

**Given** the app launches
**When** the Bootstrap scene initialises
**Then** the GameAnalytics SDK initialises; `GameAnalyticsService` registers as `IAnalyticsService` via `ServiceLocator`; SDK is wrapped in try/catch — a SDK failure never crashes the game

**Given** a level starts
**When** `LevelManager.StartLevel()` is called
**Then** `levelStart` event fires with the level's `levelId` (from `LevelConfigSO` — never hardcoded)

**Given** a level is completed
**When** the level complete trigger fires
**Then** `levelComplete` event fires with `levelId` and `bestRank`

**Given** the ball enters Dead state and lives reach 0
**When** the "Out of lives" screen is shown
**Then** `levelFail` event fires with `levelId` and attempt count

**Given** a golden ball is collected
**When** `OnGoldenBallCollected` SO event fires
**Then** `goldenBallCollected` event fires with `levelId`

**Given** the rank reveal screen displays a rank
**When** the rank is shown
**Then** `rankAchieved` event fires with `levelId` and rank value

**Given** the rank reveal screen is visible
**When** the player taps to continue
**Then** `rankScreenDwellTime` event fires with the duration in seconds the screen was visible

**Given** any analytics call is made
**When** the `IAnalyticsService` implementation is inspected
**Then** `levelId` is always sourced from `LevelConfigSO` — no hardcoded strings anywhere in the analytics layer
