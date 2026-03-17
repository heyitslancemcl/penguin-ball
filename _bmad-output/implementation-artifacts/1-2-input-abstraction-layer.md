# Story 1.2: Input Abstraction Layer

Status: review

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

- [x] Task 1: Define `IInputProvider` interface (AC: 1, 2, 6)
  - [x] Create `Assets/_Project/Scripts/Input/IInputProvider.cs`
  - [x] Define `Vector2 GetMovementInput()` — returns normalised 2D movement vector (x = lateral, y = forward/back)
  - [x] Define `void Calibrate()` — triggers recalibration (baseline reset for gyroscope; no-op for joystick)
  - [x] Define `bool IsAvailable` property — returns true if this provider can operate on current device
  - [x] No MonoBehaviour inheritance — pure C# interface

- [x] Task 2: Implement `GyroscopeInputProvider` (AC: 1, 3, 4, 5)
  - [x] Create `Assets/_Project/Scripts/Input/GyroscopeInputProvider.cs`
  - [x] Implements `IInputProvider`
  - [x] Constructor: enable AttitudeSensor via new Input System (activeInputHandler=1)
  - [x] `IsAvailable`: return `SystemInfo.supportsGyroscope && AttitudeSensor.current != null`
  - [x] `Calibrate()`: capture current `AttitudeSensor.current.attitude` as `_baseline` quaternion
  - [x] `GetMovementInput()`: delta-from-baseline, euler extraction, low-pass filter, clamp [-1,1]
  - [x] `_filterCoefficient` injected via constructor (default 10f, tunable)
  - [x] Drift detection: accumulates delta magnitude; fires `OnDriftDetected` after threshold window
  - [x] Expose `OnDriftDetected` event for UI toast to subscribe to

- [x] Task 3: Implement `JoystickInputProvider` (AC: 2, 6)
  - [x] Create `Assets/_Project/Scripts/Input/JoystickInputProvider.cs`
  - [x] Implements `IInputProvider`
  - [x] `IsAvailable`: always returns `true` (fallback provider)
  - [x] `Calibrate()`: no-op
  - [x] `GetMovementInput()`: reads from `VirtualJoystickController.Input`
  - [x] Create `Assets/_Project/Scripts/Input/VirtualJoystickController.cs` — Canvas touch drag, normalised Vector2 output
  - [x] MANUAL: VirtualJoystick prefab to be built in Unity Editor (`Assets/_Project/Prefabs/UI/VirtualJoystick.prefab`)
  - [x] MANUAL: Jump button prefab — separate UI button anchored bottom-right

- [x] Task 4: Wire input registration (AC: 1, 2)
  - [x] Created `InputBootstrapper.cs` (separate component, order -99) to avoid Core↔Input circular asmdef dependency
  - [x] Gyroscope path: registers `GyroscopeInputProvider`, no joystick shown
  - [x] Fallback path: registers `JoystickInputProvider`, instantiates VirtualJoystick prefab
  - [x] MANUAL: Add `InputBootstrapper` component to Bootstrap GameObject in Unity Editor
  - [x] MANUAL: Assign VirtualJoystick prefab reference in Inspector

- [x] Task 5: Implement recalibration triggers (AC: 3, 4, 5)
  - [x] Create `Assets/_Project/Scripts/Input/InputCalibrationHandler.cs`
  - [x] `OnApplicationPause(false)` → `Calibrate()`
  - [x] Three-finger tap → `Calibrate()` + `Handheld.Vibrate()` + toast
  - [x] Subscribe to `GyroscopeInputProvider.OnDriftDetected` → show toast
  - [x] Toast: fade-in/hold/fade-out coroutine (no DOTween)
  - [x] MANUAL: Add `InputCalibrationHandler` to Bootstrap GameObject, assign Toast Text reference

- [x] Task 6: Level-load recalibration (AC: 3)
  - [x] `InputBootstrapper.Start()` calls initial `Calibrate()`
  - [x] TODO comment in `InputBootstrapper` for Story 2.2 LevelManager wiring

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

- Avoided Core↔Input circular asmdef dependency by extracting input registration into `InputBootstrapper.cs` (Input assembly, order -99) rather than placing it in `BootstrapManager` (Core assembly).
- Used new Input System `AttitudeSensor` instead of legacy `Input.gyro.attitude` — consistent with `activeInputHandler: 1`.

### Completion Notes List

- ✅ Task 1: `IInputProvider` — pure C# interface, `GetMovementInput/Calibrate/IsAvailable`
- ✅ Task 2: `GyroscopeInputProvider` — AttitudeSensor, delta-from-baseline, low-pass filter (frame-rate independent), drift detection with configurable threshold/window
- ✅ Task 3: `JoystickInputProvider` + `VirtualJoystickController` — canvas touch drag, normalised [-1,1] output matching gyroscope range; VirtualJoystick prefab is a manual Unity Editor step
- ✅ Task 4: `InputBootstrapper` (order -99) handles ServiceLocator registration, VirtualJoystick instantiation
- ✅ Task 5: `InputCalibrationHandler` — app-resume recalibration, 3-finger tap, drift toast
- ✅ Task 6: Initial `Calibrate()` in `InputBootstrapper.Start()`, TODO comment for Story 2.2
- ⏳ MANUAL: Add `InputBootstrapper` + `InputCalibrationHandler` components to Bootstrap GameObject; create VirtualJoystick prefab; assign Inspector references

### File List

unity/New Unity Project/Assets/_Project/Scripts/Input/IInputProvider.cs
unity/New Unity Project/Assets/_Project/Scripts/Input/GyroscopeInputProvider.cs
unity/New Unity Project/Assets/_Project/Scripts/Input/JoystickInputProvider.cs
unity/New Unity Project/Assets/_Project/Scripts/Input/VirtualJoystickController.cs
unity/New Unity Project/Assets/_Project/Scripts/Input/InputBootstrapper.cs
unity/New Unity Project/Assets/_Project/Scripts/Input/InputCalibrationHandler.cs
unity/New Unity Project/Assets/_Project/Scripts/Input/PenguineBall.Input.asmdef
unity/New Unity Project/Assets/_Project/Scripts/Core/BootstrapManager.cs (updated)
unity/New Unity Project/Assets/_Project/Tests/EditMode/InputProviderTests.cs
unity/New Unity Project/Assets/_Project/Tests/EditMode/PenguineBall.Tests.EditMode.asmdef (updated)
