# Story 1.4: Ball State Machine

Status: ready-for-dev

## Story

As a developer,
I want a robust ball state machine that enforces valid state transitions,
so that all game systems can rely on the ball's current state without race conditions or ambiguity.

## Acceptance Criteria

1. **Given** any game system wants to change ball state **When** it calls `BallStateManager.TryTransitionTo(BallState.X)` **Then** the state machine evaluates validity and returns `true` (transitioned) or `false` (rejected) — direct state assignment is never permitted
2. **Given** the ball is Rolling **When** a stun obstacle is hit **Then** `TryTransitionTo(Stunned)` succeeds; a 1-second swipe-up window opens after the stun animation completes (not on collision)
3. **Given** the ball is Stunned **When** the player performs a swipe-up within the 1-second window **Then** `TryTransitionTo(Recovery)` succeeds, then `TryTransitionTo(Rolling)` completes the recovery
4. **Given** the ball is Stunned and the window expires **When** no swipe-up is detected **Then** `TryTransitionTo(Dead)` is called; Dead state takes priority over all other states
5. **Given** simultaneous Dead and Stunned triggers arrive **When** both `TryTransitionTo` calls are evaluated **Then** Dead always wins (priority: Dead > Stunned > Recovery > Rolling)
6. **Given** the `BallStateManager` is unit tested **When** Edit Mode tests run in Unity Test Framework **Then** all state transition rules and priority logic pass with 100% coverage of the state table

## Tasks / Subtasks

- [ ] Task 1: Implement `BallStateManager` (AC: 1, 2, 3, 4, 5)
  - [ ] Create `Assets/_Project/Scripts/Core/BallStateManager.cs` (MonoBehaviour implementing `IBallStateManager`)
  - [ ] `CurrentState` property (read-only public, private set)
  - [ ] `IsFireActive` property (public, set internally via fire ability events in Story 4.1)
  - [ ] Implement valid state transition table:
    ```
    Rolling   → can transition to: Stunned, Dead
    Stunned   → can transition to: Recovery, Dead
    Recovery  → can transition to: Rolling, Dead
    Dead      → no transitions out (terminal state — level restart resets the manager)
    ```
  - [ ] `TryTransitionTo(BallState newState)`:
    ```csharp
    public bool TryTransitionTo(BallState newState)
    {
        if (!IsValidTransition(CurrentState, newState)) return false;
        var previous = CurrentState;
        CurrentState = newState;
        OnBallStateChanged?.Raise(); // SO event
        HandleStateEntry(newState);
        return true;
    }
    ```
  - [ ] `IsValidTransition(BallState from, BallState to)` — pure method encoding the transition table above (used in unit tests)
  - [ ] `HandleStateEntry(BallState state)` — triggers state-specific behaviour:
    - `Stunned`: start stun window coroutine
    - `Recovery`: brief recovery animation trigger
    - `Dead`: fire `OnBallDead` SO event
    - `Rolling`: clear all timers

- [ ] Task 2: Implement stun window with swipe-up detection (AC: 2, 3, 4)
  - [ ] Add `[SerializeField] private float _stunWindowDuration = 1f;` to `BallStateManager` (also exposed via `LevelConfigSO` for per-obstacle tuning in Story 2.3)
  - [ ] Stun window coroutine:
    ```csharp
    private IEnumerator StunWindowCoroutine()
    {
        // Wait for stun animation to complete before opening window
        yield return new WaitForSeconds(_stunAnimationDuration); // e.g. 0.3f
        _swipeWindowOpen = true;
        _swipeWindowTimer = 0f;

        while (_swipeWindowTimer < _stunWindowDuration)
        {
            _swipeWindowTimer += Time.deltaTime;
            yield return null;
        }

        // Window expired — trigger death if still Stunned
        if (CurrentState == BallState.Stunned)
            TryTransitionTo(BallState.Dead);
    }
    ```
  - [ ] Swipe-up pulse prompt: activate UI arrow element when `_swipeWindowOpen = true`; deactivate on Recovery or Dead
  - [ ] `OnSwipeUp()` public method — only acts if `CurrentState == BallState.Stunned && _swipeWindowOpen`:
    ```csharp
    public void OnSwipeUp()
    {
        if (CurrentState != BallState.Stunned || !_swipeWindowOpen) return;
        StopAllCoroutines(); // Cancel stun window
        _swipeWindowOpen = false;
        TryTransitionTo(BallState.Recovery);
        StartCoroutine(RecoveryCoroutine());
    }
    ```
  - [ ] `RecoveryCoroutine()`: brief delay (0.2s) → `TryTransitionTo(BallState.Rolling)`

- [ ] Task 3: Implement swipe-up input detection (AC: 3)
  - [ ] Create `Assets/_Project/Scripts/Input/SwipeDetector.cs` (MonoBehaviour)
  - [ ] In `Update()`: detect upward swipe gesture:
    - Touch mode (tilt): `touch.deltaPosition.y > _swipeThreshold` where `_swipeThreshold = Screen.height * 0.05f` (5% of screen height — relative, not hardcoded pixels)
    - Accessibility mode (joystick): jump button tap fires the same `OnSwipeUp()` path
  - [ ] On swipe detected: call `ServiceLocator.Get<IBallStateManager>()` as `BallStateManager` and invoke `OnSwipeUp()`
  - [ ] CRITICAL: Minimum touch target 44pt — ensure swipe detection zone covers at minimum 44pt vertical distance (architecture pre-mortem checklist requirement)
  - [ ] Attach `SwipeDetector` to the Ball GameObject or a persistent Input Handler object in Bootstrap

- [ ] Task 4: Dead state priority enforcement (AC: 5)
  - [ ] The transition table in `IsValidTransition` naturally enforces Dead priority — Dead is always a valid target from any non-Dead state
  - [ ] Add explicit test: if `TryTransitionTo(Stunned)` and `TryTransitionTo(Dead)` are called in same frame, Dead wins because it is processed via the transition gate, not a parallel flag
  - [ ] Add `[SerializeField] private float _stunAnimationDuration = 0.3f;` — stun window must NOT open during this period

- [ ] Task 5: Register `BallStateManager` via `ServiceLocator` (AC: 1)
  - [ ] Add `BallStateManager` component to the Ball prefab
  - [ ] In `BootstrapManager.Awake()`, add after input registration:
    ```csharp
    // Note: BallStateManager is on the Ball prefab, not Bootstrap
    // It self-registers in its own Awake() after Bootstrap is done
    ```
  - [ ] In `BallStateManager.Awake()`:
    ```csharp
    void Awake() => ServiceLocator.Register<IBallStateManager>(this);
    ```
  - [ ] CRITICAL: Verify `BallController.Start()` retrieves `IBallStateManager` AFTER this Awake runs — Unity guarantees all Awake() calls complete before any Start() calls, so order is safe

- [ ] Task 6: Create required SO events (AC: 1)
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnBallStateChanged.asset` (GameEventSO)
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnBallDead.asset` (GameEventSO)
  - [ ] Wire `BallStateManager` to raise these events in `TryTransitionTo()`
  - [ ] `BallStateManager` holds `[SerializeField] private GameEventSO _onBallStateChanged;` and `[SerializeField] private GameEventSO _onBallDead;` — assigned in Ball prefab Inspector

- [ ] Task 7: Write Edit Mode unit tests (AC: 6)
  - [ ] Create `Assets/_Project/Tests/Core/BallStateManagerTests.cs`
  - [ ] Add `Tests` assembly definition if not yet created: `Assets/_Project/Tests/PenguineBall.Tests.asmdef`
  - [ ] Test cases (minimum — cover full state table):
    - `Rolling_To_Stunned_IsValid()` → `TryTransitionTo(Stunned)` from Rolling returns true
    - `Rolling_To_Dead_IsValid()` → returns true
    - `Rolling_To_Recovery_IsInvalid()` → returns false
    - `Stunned_To_Recovery_IsValid()` → returns true
    - `Stunned_To_Dead_IsValid()` → returns true
    - `Stunned_To_Rolling_IsInvalid()` → returns false
    - `Recovery_To_Rolling_IsValid()` → returns true
    - `Recovery_To_Dead_IsValid()` → returns true
    - `Dead_To_Any_IsInvalid()` → Dead → Rolling, Stunned, Recovery all return false
    - `Dead_Priority_Over_Stunned()` → after transitioning to Dead, TryTransitionTo(Stunned) returns false
    - `StunWindow_Expires_Causes_Dead()` → mock time advance past stun window → state = Dead
    - `OnSwipeUp_During_Window_Causes_Recovery()` → OnSwipeUp() while Stunned and window open → state = Recovery
    - `OnSwipeUp_Outside_Window_IsIgnored()` → OnSwipeUp() while Rolling → state unchanged
  - [ ] Tests use a plain C# `BallStateManager` instance (not MonoBehaviour in scene) — extract `IsValidTransition()` as a pure static method or test via interface for Edit Mode compatibility

## Dev Notes

### Architecture Compliance (ADR-004)

`TryTransitionTo()` is the ONLY valid state transition mechanism — no exceptions:

```csharp
// ✅ CORRECT — every caller uses TryTransitionTo
if (!_ballStateManager.TryTransitionTo(BallState.Stunned)) return;

// ❌ WRONG — direct assignment is forbidden
_ballStateManager.CurrentState = BallState.Stunned;
_ballStateManager.SetState(BallState.Stunned);
```

This is the single most critical architecture rule for the ball system. All story reviewers must check that no code bypasses `TryTransitionTo()`.

### State Transition Table (Complete)

```
FROM        → TO          VALID?
─────────────────────────────────
Rolling     → Stunned     ✅
Rolling     → Dead        ✅
Rolling     → Recovery    ❌
Rolling     → Rolling     ❌ (no self-transitions)
Stunned     → Recovery    ✅
Stunned     → Dead        ✅
Stunned     → Rolling     ❌
Stunned     → Stunned     ❌
Recovery    → Rolling     ✅
Recovery    → Dead        ✅
Recovery    → Stunned     ❌
Recovery    → Recovery    ❌
Dead        → (anything)  ❌ (terminal — requires level restart to reset)
```

Level restart (Story 2.5) will reset `BallStateManager` by calling a `Reset()` method that force-sets `CurrentState = BallState.Rolling` without going through `TryTransitionTo()` — this is the ONLY permitted exception to the rule, and only callable by `LevelManager`.

### Stun Window Timing — Critical

Architecture states: "Window opens after stun animation completes — not on collision"

This is deliberate UX design. The player must see the stun animation finish before the swipe-up window activates. If the window opens on collision, players get confused about why their swipe didn't register (they swiped during the animation). The `_stunAnimationDuration` delay before `_swipeWindowOpen = true` is mandatory.

### Swipe-Up vs Swipe-to-Aim — Mutual Exclusion

Swipe-to-aim (Boss Level, Story 6.2) uses the same swipe gesture but is only active when `IsFireActive && CurrentState == Rolling`. Swipe-up recovery is only active when `CurrentState == Stunned`. These are mutually exclusive by state — no additional disambiguation logic is needed.

```csharp
// SwipeDetector routing logic (wired in Story 6.2):
if (CurrentState == BallState.Stunned && _swipeWindowOpen)
    OnSwipeUp(); // recovery
else if (IsFireActive && CurrentState == BallState.Rolling)
    OnSwipeToAim(direction); // boss fireball (Story 6.2)
```

### Unit Test Architecture Note

`BallStateManager` is a `MonoBehaviour`, which normally requires a scene for Edit Mode tests. To make it testable:
- Extract `IsValidTransition(BallState from, BallState to)` as a `public static` method — pure logic, no MonoBehaviour dependency
- Test the transition table via this static method in Edit Mode
- Test `OnSwipeUp()` behaviour via integration in Play Mode tests (deferred to post-MVP per architecture decision)

```csharp
// Testable static method approach:
public static bool IsValidTransition(BallState from, BallState to) { ... }

// In test:
Assert.IsTrue(BallStateManager.IsValidTransition(BallState.Rolling, BallState.Stunned));
Assert.IsFalse(BallStateManager.IsValidTransition(BallState.Dead, BallState.Rolling));
```

### ServiceLocator Registration Order

```
Frame 1 (Awake calls, all complete before any Start):
  BootstrapManager.Awake() → registers IInputProvider
  BallStateManager.Awake() → registers IBallStateManager

Frame 1 (Start calls, after all Awakes):
  BallController.Start() → retrieves IInputProvider ✅
  BallController.Start() → retrieves IBallStateManager ✅ (now registered)
```

The null-safety guard added in Story 1.3 (`try { ServiceLocator.Get<IBallStateManager>(); } catch { }`) can be removed once this story is complete and BallStateManager is always registered.

### Project Structure Notes

- `BallStateManager.cs` → `Assets/_Project/Scripts/Core/`
- `SwipeDetector.cs` → `Assets/_Project/Scripts/Input/`
- `BallStateManagerTests.cs` → `Assets/_Project/Tests/Core/`
- `PenguineBall.Tests.asmdef` → `Assets/_Project/Tests/`
- `OnBallStateChanged.asset` → `Assets/_Project/ScriptableObjects/Events/`
- `OnBallDead.asset` → `Assets/_Project/ScriptableObjects/Events/`

### Previous Story Dependencies (Stories 1.1, 1.2, 1.3)

- `ServiceLocator` (1.1) — used for `IBallStateManager` registration
- `BallState` enum (1.3) — defined there, used here
- `IBallStateManager` interface (1.3) — defined there, implemented here
- `GameEventSO` (1.3) — used for `OnBallStateChanged` and `OnBallDead` events
- `IInputProvider` (1.2) — `SwipeDetector` retrieves it to distinguish tilt vs joystick mode

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-004: Ball State Machine`
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Naming Patterns` (TryTransitionTo pattern)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Testing Strategy`
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (swipe threshold, stun window)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 1.4`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
