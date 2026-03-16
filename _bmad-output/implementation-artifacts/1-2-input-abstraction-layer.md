# Story 1.2: Input Abstraction Layer

Status: ready-for-dev

## Story

As a player,
I want to control the ball using my device's gyroscope tilt — or a virtual joystick if tilt isn't available —
so that I have a responsive, consistent control experience regardless of my device.

## Acceptance Criteria

1. **Given** the game starts on a device with a gyroscope **When** the input system initialises **Then** `GyroscopeInputProvider` is registered with `ServiceLocator` and `BallController` receives tilt input via `IInputProvider`
2. **Given** the game starts on a device without a gyroscope **When** `SystemInfo.supportsGyroscope` returns false **Then** `JoystickInputProvider` is automatically registered instead, with no error or crash
3. **Given** the gyroscope is active **When** a level loads **Then** a new CoreMotion baseline is captured (not at app launch) and input is expressed as delta-from-baseline with a configurable low-pass filter applied
4. **Given** the player has been playing for a while and tilt drift is detected **When** drift exceeds the threshold mid-level **Then** a recalibration suggestion toast is shown; three-finger tap triggers haptic feedback and immediate recalibration
5. **Given** the app is backgrounded and then resumed **When** `OnApplicationPause(false)` fires **Then** the gyroscope baseline is recaptured automatically
6. **Given** `JoystickInputProvider` is active **When** the player moves the virtual joystick **Then** the ball receives identical physics input as a tilt equivalent — full gameplay parity confirmed

## Tasks / Subtasks

- [ ] Task 1: Define `IInputProvider` interface (AC: 1, 2, 6)
  - [ ] Create `Assets/_Project/Scripts/Input/IInputProvider.cs`
  - [ ] Define `Vector2 GetMovementInput()` — returns normalised 2D movement vector (x = lateral, y = forward/back)
  - [ ] Define `void Calibrate()` — triggers recalibration (baseline reset for gyroscope; no-op for joystick)
  - [ ] Define `bool IsAvailable` property — returns true if this provider can operate on current device
  - [ ] No MonoBehaviour inheritance — pure C# interface

- [ ] Task 2: Implement `GyroscopeInputProvider` (AC: 1, 3, 4, 5)
  - [ ] Create `Assets/_Project/Scripts/Input/GyroscopeInputProvider.cs`
  - [ ] Implements `IInputProvider`
  - [ ] Constructor: call `Input.gyro.enabled = true`
  - [ ] `IsAvailable`: return `SystemInfo.supportsGyroscope`
  - [ ] `Calibrate()`: capture current `Input.gyro.attitude` as `_baseline` quaternion
  - [ ] `GetMovementInput()`:
    - Compute delta from baseline: `Quaternion delta = Quaternion.Inverse(_baseline) * Input.gyro.attitude`
    - Extract tilt angles from delta
    - Apply low-pass filter: `_smoothed = Vector2.Lerp(_smoothed, rawInput, _filterCoefficient * Time.deltaTime)`
    - Return `_smoothed` clamped to [-1, 1] range
  - [ ] `_filterCoefficient` is configurable (expose as `[SerializeField]` or inject via constructor — keep it tunable without recompile)
  - [ ] Drift detection: track delta magnitude over time; if exceeds `_driftThreshold` for `_driftWindowSeconds` → fire drift event
  - [ ] Expose `OnDriftDetected` event for UI toast to subscribe to

- [ ] Task 3: Implement `JoystickInputProvider` (AC: 2, 6)
  - [ ] Create `Assets/_Project/Scripts/Input/JoystickInputProvider.cs`
  - [ ] Implements `IInputProvider`
  - [ ] `IsAvailable`: always returns `true` (fallback provider)
  - [ ] `Calibrate()`: no-op (nothing to calibrate on a virtual joystick)
  - [ ] `GetMovementInput()`: return current joystick axis values from the on-screen joystick component
  - [ ] Create virtual joystick UI prefab: `Assets/_Project/Prefabs/UI/VirtualJoystick.prefab`
    - Use Unity UI (Canvas-based), anchored to bottom-left of screen
    - Joystick handle follows touch within a defined radius
    - Outputs normalised Vector2 matching gyroscope output range
  - [ ] Jump button: separate UI button anchored bottom-right (for stun recovery swipe-up parity)
  - [ ] CRITICAL: Output vector must produce identical ball physics response as gyroscope input of same magnitude — test side-by-side

- [ ] Task 4: Wire input registration in `BootstrapManager` (AC: 1, 2)
  - [ ] Open `Assets/_Project/Scripts/Core/BootstrapManager.cs` (created in Story 1.1)
  - [ ] In `Awake()`, add input provider registration:
    ```csharp
    if (SystemInfo.supportsGyroscope)
        ServiceLocator.Register<IInputProvider>(new GyroscopeInputProvider());
    else
        ServiceLocator.Register<IInputProvider>(new JoystickInputProvider());
    ```
  - [ ] If joystick provider registered: instantiate `VirtualJoystick` prefab and activate it
  - [ ] If gyroscope provider registered: ensure VirtualJoystick UI is NOT shown

- [ ] Task 5: Implement recalibration triggers (AC: 3, 4, 5)
  - [ ] Create `Assets/_Project/Scripts/Input/InputCalibrationHandler.cs` (MonoBehaviour)
  - [ ] `OnApplicationPause(bool paused)`: if `!paused` → call `ServiceLocator.Get<IInputProvider>().Calibrate()`
  - [ ] Three-finger tap detection in `Update()`: detect `Input.touchCount == 3` → call `Calibrate()` + trigger haptic (`Handheld.Vibrate()`) + show toast
  - [ ] Subscribe to `GyroscopeInputProvider.OnDriftDetected`: show "Tap with 3 fingers to recalibrate" toast
  - [ ] Toast is a simple fade-in/fade-out UI Text element (no DOTween needed — use a coroutine)
  - [ ] Attach `InputCalibrationHandler` to the Bootstrap GameObject

- [ ] Task 6: Level-load recalibration (AC: 3)
  - [ ] Note: `LevelManager` does not exist yet — add a `// TODO: call Calibrate() on level load` comment placeholder in `BootstrapManager`
  - [ ] This will be wired properly in Story 2.2 (Level Config & Scene Management)
  - [ ] For now, call `Calibrate()` once in `Start()` as the initial baseline capture

## Dev Notes

### Architecture Compliance (ADR-001)

This story implements ADR-001 exactly. The `BallController` (Story 1.3) will depend ONLY on `IInputProvider` — it must never reference `GyroscopeInputProvider` or `JoystickInputProvider` directly.

```csharp
// ✅ CORRECT — BallController only knows IInputProvider
private IInputProvider _input;
void Start() => _input = ServiceLocator.Get<IInputProvider>();
void FixedUpdate() => ApplyForce(_input.GetMovementInput());

// ❌ WRONG — never reference concrete implementation
private GyroscopeInputProvider _gyro;
```

### CoreMotion Calibration — Non-Negotiable Rules (ADR-001)

Per architecture: "Tilt feel is the game's identity. Drift or miscalibration is fatal to reviews."

- Baseline captured at **every level load** — NOT at app launch. A player who picks up their phone at an angle should not have a broken baseline.
- Input is always **delta-from-baseline** — never raw gyroscope attitude.
- Low-pass filter is **mandatory** — raw gyroscope data is too jittery for smooth ball movement.
- `OnApplicationPause(false)` recalibration is **mandatory** — backgrounding the app causes gyroscope drift.

### Low-Pass Filter Implementation Note

```csharp
// Configurable coefficient: lower = smoother but more lag, higher = more responsive but jittery
// Recommended starting value: 8-12f (tune during playtesting on device)
_smoothed = Vector2.Lerp(_smoothed, rawTiltInput, _filterCoefficient * Time.deltaTime);
```

Do NOT use a fixed `t` value — multiply by `Time.deltaTime` to make the filter frame-rate independent.

### Gameplay Parity Requirement (AC: 6)

The joystick provider must output the same Vector2 range and magnitude as the gyroscope for equivalent physical inputs. Test this by:
1. Tilt device to produce a specific ball velocity with gyroscope
2. Move joystick to full extent — ball should reach same velocity

If parity is not achieved, the joystick is not a true accessibility alternative.

### Unity Input System vs Legacy Input

This project uses the **new Input System** (`com.unity.inputsystem`). However, for gyroscope access in Unity 6, use the legacy `Input.gyro` API via the **Input System's `Gyroscope` sensor**:

```csharp
// New Input System gyroscope access:
using UnityEngine.InputSystem;
var gyro = Gyroscope.current;
if (gyro != null) InputSystem.EnableDevice(gyro);
var angularVelocity = gyro.angularVelocity.ReadValue();
```

Alternatively, the legacy `Input.gyro` is still accessible if "Both" is set in Player Settings → Active Input Handling. **Confirm which approach is used and be consistent** — do not mix legacy `Input.gyro` and new Input System sensor APIs in the same provider.

### Device Sensitivity Profiles

Architecture requires testing on iPhone SE (4th gen), iPhone 15, iPhone 16 Pro before launch. These have different gyroscope sensitivities. The `_filterCoefficient` and sensitivity scalar should be tunable via a `[SerializeField]` on the Bootstrap object or a `InputSettingsSO` ScriptableObject — do not hardcode.

### Project Structure Notes

- `IInputProvider.cs` → `Assets/_Project/Scripts/Input/`
- `GyroscopeInputProvider.cs` → `Assets/_Project/Scripts/Input/`
- `JoystickInputProvider.cs` → `Assets/_Project/Scripts/Input/`
- `InputCalibrationHandler.cs` → `Assets/_Project/Scripts/Input/`
- `VirtualJoystick.prefab` → `Assets/_Project/Prefabs/UI/`
- All files follow one-class-per-file rule; filename matches class name exactly

### Previous Story Foundation (Story 1.1)

This story depends on Story 1.1 being complete:
- `ServiceLocator` must exist and be functional before `BootstrapManager` can register `IInputProvider`
- `BootstrapManager` on the Bootstrap scene GameObject is the registration point
- `Assets/_Project/Scripts/Input/` folder already exists from Story 1.1 folder setup

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-001: Input Abstraction Layer`
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Cross-Cutting Concerns` (Input abstraction, Physics consistency)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (CoreMotion, swipe threshold, SystemInfo.supportsGyroscope)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 1.2`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
