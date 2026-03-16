# Story 5.2: Tutorial Level (Level 0)

Status: ready-for-dev

## Story

As a new player,
I want to be guided through my first level with in-world prompts that teach me how to play,
so that I learn the controls and mechanics naturally without reading a manual.

## Acceptance Criteria

1. **Given** the player selects Level 0 (tutorial) **When** the level loads **Then** the `TutorialSequencer` initialises and subscribes to game events; prompt content is driven by `TutorialConfig` ScriptableObject
2. **Given** the tutorial is running **When** each game event fires **Then** the corresponding diegetic world-space prompt activates — prompts are 3D GameObjects in the level scene, NOT overlaid UI panels
3. **Given** a tutorial prompt is active **When** the player performs the prompted action **Then** the prompt deactivates; the step is marked complete in save data; the next prompt in the sequence is ready to trigger
4. **Given** the player completes Level 0 **When** the level complete trigger fires **Then** the rank reveal is NOT shown — the "Great start!" screen appears (handled by Story 3.4's tutorial check); `completed: true` is written; no `bestRank` or `bestScore` is stored
5. **Given** Level 0 is replayed **When** the level loads **Then** prompts for already-completed steps do not re-trigger; the player only sees prompts for steps they haven't done yet
6. **Given** the `TutorialSequencer` subscribes to events **When** its MonoBehaviour lifecycle is inspected **Then** all subscriptions are in `OnEnable()` and unsubscriptions in `OnDisable()` — no memory leaks

## Tasks / Subtasks

- [ ] Task 1: Add `completedTutorialStepIds` to `LevelSaveData` (AC: 3, 5)
  - [ ] Open `Assets/_Project/Scripts/Core/SaveData.cs` (Story 2.1)
  - [ ] Add to `LevelSaveData`:
    ```csharp
    public List<string> completedTutorialStepIds = new List<string>();
    ```
  - [ ] Defaults to empty list — safe for all non-tutorial levels (list is unused and negligible in save size)
  - [ ] Newtonsoft Json.NET handles `List<string>` serialisation correctly (Story 2.1 already confirmed Json.NET is used)

- [ ] Task 2: Create `TutorialStep` and `TutorialConfig` ScriptableObject (AC: 1)
  - [ ] Create `Assets/_Project/Scripts/Progression/TutorialStep.cs`:
    ```csharp
    [System.Serializable]
    public class TutorialStep
    {
        public string       stepId;          // unique ID — must match TutorialPrompt.stepId in scene
        public GameEventSO  triggerEvent;    // SO event that shows this prompt
        public GameEventSO  completionEvent; // SO event that marks this step done and hides prompt
        [TextArea(1, 3)]
        public string       devNote;         // dev reference — not shown in game
    }
    ```
  - [ ] Create `Assets/_Project/Scripts/Progression/TutorialConfig.cs`:
    ```csharp
    [CreateAssetMenu(menuName = "PenguineBall/TutorialConfig")]
    public class TutorialConfig : ScriptableObject
    {
        public TutorialStep[] steps;
    }
    ```
  - [ ] Create `Assets/_Project/ScriptableObjects/TutorialConfig.asset`:
    - Populate with the starter steps defined in Task 6 below
  - [ ] Wire trigger/completion events to existing SO event assets

- [ ] Task 3: Create `TutorialPrompt` MonoBehaviour (AC: 2, 3)
  - [ ] Create `Assets/_Project/Scripts/UI/TutorialPrompt.cs`:
    ```csharp
    /// <summary>
    /// Placed on world-space prompt GameObjects in the tutorial level scene.
    /// Matched to TutorialConfig steps by stepId.
    /// </summary>
    public class TutorialPrompt : MonoBehaviour
    {
        [SerializeField] public string stepId; // must match a TutorialConfig.steps[].stepId

        // Optional: pulse/animation while visible
        private Coroutine _pulseCoroutine;

        public void Show()
        {
            gameObject.SetActive(true);
            _pulseCoroutine = StartCoroutine(PulseCoroutine());
        }

        public void Hide()
        {
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            gameObject.SetActive(false);
        }

        private IEnumerator PulseCoroutine()
        {
            // Gentle scale pulse to draw attention
            Vector3 baseScale = transform.localScale;
            while (true)
            {
                float t = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f; // 0–1 oscillation
                transform.localScale = Vector3.Lerp(baseScale, baseScale * 1.15f, t);
                yield return null;
            }
        }
    }
    ```
  - [ ] Place `TutorialPrompt` components on world-space arrow/indicator GameObjects in the tutorial scene
  - [ ] World-space prompts are 3D objects (arrows, floating text meshes, particle emitters) — NOT Canvas UI
  - [ ] Each prompt is `SetActive(false)` by default in the scene; `TutorialSequencer` activates them

- [ ] Task 4: Create `TutorialSequencer` MonoBehaviour (AC: 1, 2, 3, 5, 6)
  - [ ] Create `Assets/_Project/Scripts/Progression/TutorialSequencer.cs`:
    ```csharp
    public class TutorialSequencer : MonoBehaviour
    {
        [SerializeField] private TutorialConfig  _config;
        [SerializeField] private TutorialPrompt[] _prompts; // must have stepId matching config

        private Dictionary<string, TutorialPrompt> _promptMap;
        private HashSet<string>                    _completedSteps;

        // Track subscriptions for proper cleanup
        private readonly List<(GameEventSO evt, System.Action handler)> _subscriptions
            = new List<(GameEventSO, System.Action)>();

        private void Awake()
        {
            // Build stepId → prompt lookup
            _promptMap = new Dictionary<string, TutorialPrompt>();
            foreach (var prompt in _prompts)
            {
                if (!string.IsNullOrEmpty(prompt.stepId))
                    _promptMap[prompt.stepId] = prompt;
            }
        }

        private void Start()
        {
            // Load already-completed steps from save
            var save    = ServiceLocator.Get<ISaveService>();
            var levelId = ServiceLocator.Get<ILevelManager>().CurrentLevel.levelId;
            var data    = save.GetLevelData(levelId);
            _completedSteps = new HashSet<string>(data.completedTutorialStepIds ?? new List<string>());
        }

        private void OnEnable()
        {
            if (_config == null) return;

            foreach (var step in _config.steps)
            {
                if (step.triggerEvent == null) continue;

                var s = step; // closure capture

                System.Action triggerHandler = () => OnStepTriggered(s);
                step.triggerEvent.AddListener(triggerHandler);
                _subscriptions.Add((step.triggerEvent, triggerHandler));

                if (step.completionEvent != null && step.completionEvent != step.triggerEvent)
                {
                    System.Action completionHandler = () => OnStepCompleted(s);
                    step.completionEvent.AddListener(completionHandler);
                    _subscriptions.Add((step.completionEvent, completionHandler));
                }
            }
        }

        private void OnDisable()
        {
            foreach (var (evt, handler) in _subscriptions)
                evt.RemoveListener(handler);
            _subscriptions.Clear();
        }

        private void OnStepTriggered(TutorialStep step)
        {
            if (_completedSteps.Contains(step.stepId)) return; // already done — skip

            if (_promptMap.TryGetValue(step.stepId, out var prompt))
                prompt.Show();
        }

        private void OnStepCompleted(TutorialStep step)
        {
            if (_completedSteps.Contains(step.stepId)) return; // idempotency guard

            // Hide prompt
            if (_promptMap.TryGetValue(step.stepId, out var prompt))
                prompt.Hide();

            // Mark complete in memory
            _completedSteps.Add(step.stepId);

            // Persist to save data
            var save    = ServiceLocator.Get<ISaveService>();
            var levelId = ServiceLocator.Get<ILevelManager>().CurrentLevel.levelId;
            var data    = save.GetLevelData(levelId);
            if (data.completedTutorialStepIds == null)
                data.completedTutorialStepIds = new List<string>();
            data.completedTutorialStepIds.Add(step.stepId);
            save.UpdateLevelData(levelId, data);
        }
    }
    ```
  - [ ] Place `TutorialSequencer` on a root GameObject in `biome1_tutorial.unity`
  - [ ] Wire `_config` → `TutorialConfig.asset` in Inspector
  - [ ] Wire `_prompts[]` array → all `TutorialPrompt` GameObjects in the scene

- [ ] Task 5: Guard `StyleRankTracker` against tutorial levels (AC: 4)
  - [ ] Open `Assets/_Project/Scripts/Progression/StyleRankTracker.cs` (Story 3.2)
  - [ ] In `OnLevelCompleted()`, add tutorial guard before scoring:
    ```csharp
    private void OnLevelCompleted()
    {
        if (_currentLevelConfig == null) return;

        // Tutorial level: unranked — no score written to save
        if (_currentLevelConfig.levelId.Contains("tutorial")) return;

        // ... existing scoring logic
    }
    ```
  - [ ] This prevents `bestRank` and `bestScore` being written for `biome1_tutorial` without needing a new `LevelConfigSO` field

- [ ] Task 6: Define starter tutorial steps in `TutorialConfig.asset` (AC: 1, 2)
  - [ ] Create the following steps (populate in Inspector after creating `TutorialConfig.asset`):

    | stepId | triggerEvent | completionEvent | devNote |
    |--------|-------------|-----------------|---------|
    | `tilt_to_move` | `OnLevelStarted` | `OnBallBounced` | Show arrow prompting tilt; completes on first roll/bounce |
    | `bounce_recovery` | `OnBallBounced` | `OnBallStateChanged` (Rolling after bounce) | Show "keep rolling" indicator; completes when ball exits first bounce cleanly |
    | `stun_obstacle` | `OnBallApproachingStun` (new — see below) | `OnBallStateChanged` (Recovery state) | Show swipe-up arrow above stun zone; completes on recovery |
    | `golden_ball_hint` | `OnGoldenBallNear` (new — see below) | `OnGoldenBallCollected` | Show sparkle above golden ball location; completes on collection |

  - [ ] Note: `OnBallApproachingStun` and `OnGoldenBallNear` are proximity events — see Task 7

- [ ] Task 7: Create proximity trigger events for tutorial (AC: 2)
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnBallApproachingStun.asset` (GameEventSO)
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnGoldenBallNear.asset` (GameEventSO)
  - [ ] Create `Assets/_Project/Scripts/Progression/TutorialProximityTrigger.cs`:
    ```csharp
    /// <summary>
    /// Invisible trigger volume placed near a hazard or collectible in the tutorial scene.
    /// Fires a SO event when the ball enters — used to activate contextual tutorial prompts.
    /// One-shot: fires once then disables itself.
    /// </summary>
    public class TutorialProximityTrigger : MonoBehaviour
    {
        [SerializeField] private GameEventSO _onBallEntered;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Ball")) return;
            _onBallEntered.Raise();
            enabled = false; // fire once only
        }
    }
    ```
  - [ ] Place `TutorialProximityTrigger` GameObjects in `biome1_tutorial.unity`:
    - Near the first stun obstacle → wire `_onBallEntered` → `OnBallApproachingStun.asset`
    - Near the golden ball → wire `_onBallEntered` → `OnGoldenBallNear.asset`
  - [ ] These trigger zones are invisible (no Mesh Renderer) — Collider with `IsTrigger = true`, box sized to fill the approach corridor

- [ ] Task 8: Build `biome1_tutorial.unity` scene (AC: 1–6)
  - [ ] Open `Assets/_Project/Scenes/Levels/biome1_tutorial.unity` (placeholder from Story 2.2)
  - [ ] Build a minimal tutorial layout:
    - Start platform (5×5 units)
    - Path leading to first stun obstacle
    - Path continuing past stun obstacle to golden ball location
    - `LevelCompleteZone` trigger at the far end (Story 2.5)
    - Kill plane coverage below all platforms
  - [ ] Place world-space prompt GameObjects (each needs `TutorialPrompt` component):
    - `Prompt_TiltToMove`: 3D arrow above start position pointing in movement direction
    - `Prompt_BounceRecovery`: floating indicator near first surface
    - `Prompt_StunObstacle`: swipe-up arrow floating above the stun zone approach
    - `Prompt_GoldenBall`: sparkle/glow effect above golden ball location
  - [ ] All prompts `SetActive(false)` in scene by default
  - [ ] Add `TutorialSequencer` GameObject with component wired per Task 4
  - [ ] Add `TutorialProximityTrigger` volumes per Task 7
  - [ ] Place one `StunObstacle` prefab (Story 2.3)
  - [ ] Place one `GoldenBall` prefab (Story 3.1)
  - [ ] Place `FireAbilityPickup` prefab near end (optional — teaches fire before LevelCompleteZone)

- [ ] Task 9: Play Mode test checklist (AC: 1–6)
  - [ ] Level loads → `TutorialSequencer` initialised, all prompts hidden
  - [ ] Ball starts moving → `tilt_to_move` prompt appears (triggered by `OnLevelStarted`, completed by first bounce)
  - [ ] Ball enters stun obstacle proximity → `stun_obstacle` prompt appears
  - [ ] Player completes swipe-up recovery → `stun_obstacle` prompt disappears; save data now has `"stun_obstacle"` in `completedTutorialStepIds`
  - [ ] Ball near golden ball → `golden_ball_hint` prompt appears; collecting it hides the prompt
  - [ ] Level complete → "Great start!" screen shown (not rank reveal); `completed: true` in save; `bestRank` field unchanged
  - [ ] Replay tutorial → completed prompts do NOT re-appear; `OnStepTriggered` returns early for completed steps

## Dev Notes

### Diegetic Prompts — No Canvas Overlays

Tutorial prompts are 3D world objects:

```
✅ CORRECT — diegetic world-space:
- 3D arrow mesh floating above start position
- World-space TextMesh (TextMeshPro 3D) saying "Tilt to roll!"
- Particle sparkle above golden ball location
- Billboard quad with swipe-up icon (using Billboard shader or LookAt in Update)

❌ WRONG — screen-space UI overlay:
- Canvas with Screen Space - Overlay mode
- UGUI Text/Image pinned to screen corners
- Semi-transparent fullscreen tutorial panel
```

World-space prompts integrate naturally into the game world and avoid breaking immersion. They scale and position naturally with the camera, and don't require safe-area handling.

### Same Trigger and Completion Event

If `triggerEvent == completionEvent` in a `TutorialStep`, the same event both shows AND immediately completes the prompt. This is handled by `OnEnable()` only registering the completion handler when the events differ:

```csharp
if (step.completionEvent != null && step.completionEvent != step.triggerEvent)
{
    // separate completion handler
}
```

For steps where trigger == completion (show-and-dismiss on same event), the trigger fires → `OnStepTriggered` shows the prompt → the NEXT time the same event fires, `OnStepCompleted` is called. But since only the trigger handler is registered (not completion), it just fires `OnStepTriggered` again, which returns early (`_completedSteps.Contains`). This is correct — the designer should use different trigger/completion events for a meaningful show/complete cycle.

For the `tilt_to_move` step: `triggerEvent = OnLevelStarted` (fires immediately on load) and `completionEvent = OnBallBounced` (fires on first roll). This gives a clean activate-on-load → dismiss-on-action pattern.

### `TutorialProximityTrigger` — One-Shot via `enabled = false`

```csharp
_onBallEntered.Raise();
enabled = false; // disables the MonoBehaviour — OnTriggerEnter won't fire again
```

Setting `enabled = false` on a MonoBehaviour disables its Update/trigger callbacks but leaves the Collider active. This is the correct one-shot pattern — `Destroy(gameObject)` would remove the trigger entirely (acceptable but heavier). If the designer wants the trigger to fire on replay, do NOT set `enabled = false` — remove it.

### Style Rank Guard — `Contains("tutorial")`

`StyleRankTracker.OnLevelCompleted()` returns early for tutorial levels:

```csharp
if (_currentLevelConfig.levelId.Contains("tutorial")) return;
```

This follows the same convention used in `RankRevealScreen.Show()` (Story 3.4). Both use the `levelId` string check rather than a new `LevelConfigSO.isTutorial` field — keeping the convention consistent throughout the codebase. Future biome tutorials (`biome2_tutorial`, etc.) are automatically excluded by this check.

### `TutorialPrompt._prompts[]` — Index Is Not Order

`TutorialSequencer._prompts[]` is a flat array of all prompts in the scene. The order in the array does NOT define trigger order — that's defined by `TutorialConfig.steps[]` with their events. The array is just a scene reference list that gets turned into a dictionary by `stepId`. Order is irrelevant.

### Save Data — `completedTutorialStepIds` Is Level-Agnostic

`completedTutorialStepIds` is on `LevelSaveData` (not a separate struct) so it uses the existing per-level save infrastructure. For non-tutorial levels, this list is always empty and ignored. The field adds a small memory overhead for every `LevelSaveData` entry — negligible for 12 levels.

### `OnBallApproachingStun` / `OnGoldenBallNear` — Local SO Events

These events are tutorial-specific proximity events. They are NOT reused by other systems. If the project grows and a "radar" or proximity system is needed globally, these could be generalised — but for MVP they're lightweight single-purpose triggers.

They live in `Assets/_Project/ScriptableObjects/Events/` alongside all other SO events for consistency.

### "Great start!" Screen — Already Handled

Story 3.4 implemented the tutorial completion path:
```csharp
bool isTutorial = levelManager.CurrentLevel.levelId.Contains("tutorial");
if (isTutorial) ShowTutorialComplete();
else ShowRankReveal();
```

No changes needed here. `biome1_tutorial` level completion automatically routes to the "Great start!" panel. This story does NOT modify `RankRevealScreen`.

### Minimal Level Design Scope

The tutorial scene layout (platforms, obstacles, visual art) is out of scope for the story — level art is not a programming task. Build the minimum functional layout:
- Start platform + clear path
- One `StunObstacle` on the path
- One `GoldenBall` reachable off the main path
- `LevelCompleteZone` at the end

The level designer iterates on layout after the mechanics are confirmed working.

### Project Structure Notes

- `TutorialStep.cs` → `Assets/_Project/Scripts/Progression/`
- `TutorialConfig.cs` → `Assets/_Project/Scripts/Progression/`
- `TutorialSequencer.cs` → `Assets/_Project/Scripts/Progression/`
- `TutorialPrompt.cs` → `Assets/_Project/Scripts/UI/`
- `TutorialProximityTrigger.cs` → `Assets/_Project/Scripts/Progression/`
- `TutorialConfig.asset` → `Assets/_Project/ScriptableObjects/`
- `OnBallApproachingStun.asset` → `Assets/_Project/ScriptableObjects/Events/`
- `OnGoldenBallNear.asset` → `Assets/_Project/ScriptableObjects/Events/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `TutorialSequencer.Start()` retrieves `ISaveService`, `ILevelManager`
- `GameEventSO` (1.3) — all tutorial trigger/completion events
- `ISaveService` (2.1) — `completedTutorialStepIds` persisted per level; `LevelSaveData` updated here
- `ILevelManager` (2.2) — `CurrentLevel.levelId` for save data access
- `LevelCompleteZone` (2.5) — already placed in tutorial scene; fires `OnLevelCompleted`
- `RankRevealScreen` (3.4) — handles "Great start!" screen via `isTutorial` check; no changes
- `StyleRankTracker` (3.2) — updated here to skip scoring for tutorial levels
- `OnLevelStarted.asset` (5.1) — used as trigger for `tilt_to_move` step
- `OnBallBounced.asset` (5.1) — used as completion for `tilt_to_move` step
- `OnGoldenBallCollected.asset` (3.1) — completion event for `golden_ball_hint` step

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md` (FR15: tutorial diegetic prompts)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 5.2`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
