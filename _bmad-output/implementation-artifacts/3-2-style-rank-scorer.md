# Story 3.2: Style Rank Scorer

Status: ready-for-dev

## Story

As a player,
I want to receive a style rank (E through A) at the end of each level based on how I played,
so that skilled, expressive play is recognised and I'm motivated to improve.

## Acceptance Criteria

1. **Given** a level is completed **When** `StyleRankScorer.CalculateRank()` is called **Then** a deterministic rank (E, D, C, B, or A) is returned based on: speed score + fire ability uses + enemy defeats + hazards avoided — same inputs always produce the same rank
2. **Given** the player achieves a rank **When** it is compared to their previous best for that level **Then** only the higher rank is written to `bestRank` in save data; score is written to `bestScore` if higher
3. **Given** the scoring inputs are captured during play **When** the level runs **Then** speed is tracked continuously; fire ability activations, enemy defeats, and hazards avoided are each incremented via ScriptableObject events with per-session HashSet deduplication (no double-counting)
4. **Given** the `StyleRankScorer` is unit tested **When** Edit Mode tests run **Then** known input combinations produce the correct expected rank; boundary conditions for each rank threshold are covered

## Tasks / Subtasks

- [ ] Task 1: Add rank fields to `LevelSaveData` (AC: 2)
  - [ ] Open `Assets/_Project/Scripts/Core/SaveData.cs` (Story 2.1)
  - [ ] Add to `LevelSaveData`:
    ```csharp
    public string bestRank = "E";          // "E", "D", "C", "B", "A" — string for JSON compat
    public float bestScore = 0f;           // raw weighted score (for tiebreaker display)
    public bool hasSeenRankReveal = false; // controls cinematic vs condensed in Story 3.4
    ```
  - [ ] Defaults ensure new save data is valid without migration — new fields default to worst rank / unseen, which is correct

- [ ] Task 2: Create `StyleRank` enum and `ScoringInputs` struct (AC: 1)
  - [ ] Create `Assets/_Project/Scripts/Progression/StyleRank.cs`:
    ```csharp
    public enum StyleRank { E = 0, D = 1, C = 2, B = 3, A = 4 }
    ```
  - [ ] Create `Assets/_Project/Scripts/Progression/ScoringInputs.cs`:
    ```csharp
    public struct ScoringInputs
    {
        public float averageSpeed;          // avg linearVelocity.magnitude during session
        public float maxVelocityReference;  // from LevelConfigSO.maxVelocity — for normalisation
        public int   fireAbilityUses;       // count of OnFireAbilityActivated events this session
        public int   enemyDefeats;          // count of OnEnemyDefeated events this session
        public int   hazardsAvoided;        // count of unique HazardAvoidedZones exited alive
    }
    ```

- [ ] Task 3: Create `StyleRankScorer` — pure C# class (AC: 1, 4)
  - [ ] Create `Assets/_Project/Scripts/Progression/StyleRankScorer.cs`:
    ```csharp
    /// <summary>
    /// Pure scoring logic — no MonoBehaviour, no ServiceLocator. Edit Mode testable.
    /// </summary>
    public static class StyleRankScorer
    {
        // Score weights — designer-tunable constants
        private const float SpeedWeight  = 50f; // normalised 0–1 * 50 → 0–50 pts
        private const float FireWeight   = 20f; // min(uses, 1) * 20 → 0–20 pts (binary: used it or not)
        private const float EnemyWeight  = 10f; // min(defeats, 2) * 10 → 0–20 pts (cap at 2)
        private const float HazardWeight =  5f; // min(avoided, 2) *  5 → 0–10 pts (cap at 2)
        // Max total: 50 + 20 + 20 + 10 = 100

        // Rank thresholds (lower bound inclusive)
        private const float ThresholdD = 20f;
        private const float ThresholdC = 40f;
        private const float ThresholdB = 60f;
        private const float ThresholdA = 80f;

        public static StyleRank CalculateRank(ScoringInputs inputs)
        {
            float score = CalculateScore(inputs);

            if (score >= ThresholdA) return StyleRank.A;
            if (score >= ThresholdB) return StyleRank.B;
            if (score >= ThresholdC) return StyleRank.C;
            if (score >= ThresholdD) return StyleRank.D;
            return StyleRank.E;
        }

        public static float CalculateScore(ScoringInputs inputs)
        {
            float maxVel = inputs.maxVelocityReference > 0f ? inputs.maxVelocityReference : 1f;
            float normalisedSpeed = Mathf.Clamp01(inputs.averageSpeed / maxVel);

            float speedPts  = normalisedSpeed * SpeedWeight;
            float firePts   = Mathf.Min(inputs.fireAbilityUses, 1) * FireWeight;
            float enemyPts  = Mathf.Min(inputs.enemyDefeats, 2)    * EnemyWeight;
            float hazardPts = Mathf.Min(inputs.hazardsAvoided, 2)  * HazardWeight;

            return speedPts + firePts + enemyPts + hazardPts;
        }
    }
    ```
  - [ ] `CalculateScore` exposed separately so unit tests can verify raw score as well as rank
  - [ ] `Mathf.Clamp01` and `Mathf.Min` caps prevent any single factor dominating — no input can push score beyond max

- [ ] Task 4: Create `IStyleRankTracker` interface and `StyleRankTracker` MonoBehaviour (AC: 2, 3)
  - [ ] Create `Assets/_Project/Scripts/Progression/IStyleRankTracker.cs`:
    ```csharp
    public interface IStyleRankTracker
    {
        StyleRank LastRank { get; }
        float LastScore { get; }
        void NotifyHazardAvoided(int zoneId); // called by HazardAvoidedZone — int for dedup
        void ResetSession();                  // called by LevelManager on fresh level load
    }
    ```
  - [ ] Create `Assets/_Project/Scripts/Progression/StyleRankTracker.cs` (MonoBehaviour on Bootstrap):
    ```csharp
    public class StyleRankTracker : MonoBehaviour, IStyleRankTracker
    {
        [SerializeField] private GameEventSO _onFireAbilityActivated;
        [SerializeField] private GameEventSO _onEnemyDefeated;
        [SerializeField] private GameEventSO _onLevelCompleted;
        [SerializeField] private LevelConfigSO _currentLevelConfig; // set by LevelManager on level load

        // Accumulation state
        private float _speedSampleTotal;
        private int   _speedSampleCount;
        private int   _fireAbilityUses;
        private int   _enemyDefeats;
        private readonly HashSet<int> _avoidedHazardIds = new HashSet<int>();

        public StyleRank LastRank  { get; private set; } = StyleRank.E;
        public float     LastScore { get; private set; }

        private void Awake() => ServiceLocator.Register<IStyleRankTracker>(this);

        private void OnEnable()
        {
            _onFireAbilityActivated.AddListener(OnFireAbilityActivated);
            _onEnemyDefeated.AddListener(OnEnemyDefeated);
            _onLevelCompleted.AddListener(OnLevelCompleted);
        }
        private void OnDisable()
        {
            _onFireAbilityActivated.RemoveListener(OnFireAbilityActivated);
            _onEnemyDefeated.RemoveListener(OnEnemyDefeated);
            _onLevelCompleted.RemoveListener(OnLevelCompleted);
        }

        // Called from LevelManager.StartLevel() to supply current config
        public void SetLevelConfig(LevelConfigSO config)
        {
            _currentLevelConfig = config;
            ResetSession();
        }

        // IStyleRankTracker
        public void NotifyHazardAvoided(int zoneId) => _avoidedHazardIds.Add(zoneId);

        public void ResetSession()
        {
            _speedSampleTotal  = 0f;
            _speedSampleCount  = 0;
            _fireAbilityUses   = 0;
            _enemyDefeats      = 0;
            _avoidedHazardIds.Clear();
        }

        // Speed sampling — called from FixedUpdate
        private void FixedUpdate()
        {
            // Only sample if ball is in a rolling state to avoid counting stunned/dead time
            var ball = ServiceLocator.Get<IBallStateManager>();
            if (ball.CurrentState == BallState.Rolling || ball.CurrentState == BallState.Recovery)
            {
                var ballController = ServiceLocator.Get<IBallController>();
                _speedSampleTotal += ballController.CurrentSpeed;
                _speedSampleCount++;
            }
        }

        private void OnFireAbilityActivated() => _fireAbilityUses++;
        private void OnEnemyDefeated()        => _enemyDefeats++;

        private void OnLevelCompleted()
        {
            if (_currentLevelConfig == null) return;

            var inputs = new ScoringInputs
            {
                averageSpeed         = _speedSampleCount > 0 ? _speedSampleTotal / _speedSampleCount : 0f,
                maxVelocityReference = _currentLevelConfig.maxVelocity,
                fireAbilityUses      = _fireAbilityUses,
                enemyDefeats         = _enemyDefeats,
                hazardsAvoided       = _avoidedHazardIds.Count
            };

            LastScore = StyleRankScorer.CalculateScore(inputs);
            LastRank  = StyleRankScorer.CalculateRank(inputs);

            SaveRankIfBetter();
        }

        private void SaveRankIfBetter()
        {
            var save    = ServiceLocator.Get<ISaveService>();
            var levelId = ServiceLocator.Get<ILevelManager>().CurrentLevel.levelId;
            var data    = save.GetLevelData(levelId);

            bool newScoreIsBetter = LastScore > data.bestScore;

            if (newScoreIsBetter)
            {
                data.bestScore = LastScore;
                data.bestRank  = LastRank.ToString(); // "A", "B", "C", "D", "E"
                save.UpdateLevelData(levelId, data);
            }
        }
    }
    ```
  - [ ] Add `StyleRankTracker` component to Bootstrap GameObject
  - [ ] Wire `[SerializeField]` SO event references in Inspector

- [ ] Task 5: Expose `CurrentSpeed` on `IBallController` / `BallController` (AC: 3)
  - [ ] Update `Assets/_Project/Scripts/Core/IBallController.cs` (create if it doesn't exist as a separate interface — or add to `BallController` if no interface exists):
    ```csharp
    public interface IBallController
    {
        float CurrentSpeed { get; }
    }
    ```
  - [ ] In `BallController.cs`, add:
    ```csharp
    public float CurrentSpeed => _rigidbody.linearVelocity.magnitude;
    ```
  - [ ] Register in `BallController.Awake()`:
    ```csharp
    ServiceLocator.Register<IBallController>(this);
    ```
  - [ ] `IBallController` may already be partially present from earlier stories — check before creating a new file
  - [ ] Note: If `BallController` already implements an interface or is registered differently, adapt accordingly

- [ ] Task 6: Create `HazardAvoidedZone` MonoBehaviour (AC: 3)
  - [ ] Create `Assets/_Project/Scripts/Obstacles/HazardAvoidedZone.cs`:
    ```csharp
    /// <summary>
    /// Place as a trigger volume near/around a hazard. When the ball exits alive, credits
    /// one hazard-avoided point. HashSet deduplication in StyleRankTracker prevents double-count.
    /// </summary>
    public class HazardAvoidedZone : MonoBehaviour
    {
        [SerializeField] private int _zoneId; // Set unique int in Inspector per scene

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Ball")) return;

            var stateManager = ServiceLocator.Get<IBallStateManager>();
            if (stateManager.CurrentState == BallState.Dead) return; // ball died in zone, not avoided

            ServiceLocator.Get<IStyleRankTracker>().NotifyHazardAvoided(_zoneId);
        }
    }
    ```
  - [ ] Place one `HazardAvoidedZone` in `biome1_tutorial.unity` for testing — encompass a StunObstacle or IceSpike
  - [ ] Assign a unique `_zoneId` (e.g. 1, 2, 3…) per zone in the Inspector
  - [ ] `HashSet<int>.Add()` is idempotent — if ball passes through same zone twice, only counted once

- [ ] Task 7: Wire `StyleRankTracker` into `LevelManager` (AC: 2, 3)
  - [ ] In `LevelManager.StartLevel()` (Story 2.2), add:
    ```csharp
    // Reset rank tracker for the new session
    ServiceLocator.Get<IStyleRankTracker>().ResetSession();
    // Also set the config so tracker knows maxVelocity:
    (ServiceLocator.Get<IStyleRankTracker>() as StyleRankTracker)?.SetLevelConfig(CurrentLevel);
    ```
  - [ ] Note: Casting to `StyleRankTracker` for `SetLevelConfig()` is a pragmatic shortcut; alternatively add `SetLevelConfig` to `IStyleRankTracker` interface (preferred):
    ```csharp
    // Better: add to IStyleRankTracker
    void SetLevelConfig(LevelConfigSO config);
    ```
  - [ ] On `RestartLevel()` — `ResetSession()` fires automatically because `StartLevel()` is called again after scene reload (lives preserved, session scoring resets)

- [ ] Task 8: Create required SO event assets (AC: 1, 3)
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnFireAbilityActivated.asset` (GameEventSO)
    - Will be raised by `FireAbilityPickup.cs` in Story 4.1 — create asset now, raise later
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnFireAbilityDeactivated.asset` (GameEventSO)
    - Needed by HUD timer in Story 3.3 — create now
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnEnemyDefeated.asset` (GameEventSO)
    - Will be raised by `SealController` (4.2) and `WalrusController` (4.3) — create asset now
  - [ ] `OnLevelCompleted.asset` — already created in Story 2.1; reference it; do NOT create a duplicate

- [ ] Task 9: Write Edit Mode unit tests (AC: 4)
  - [ ] Create `Assets/_Project/Tests/EditMode/StyleRankScorerTests.cs`:
    ```csharp
    using NUnit.Framework;

    public class StyleRankScorerTests
    {
        // --- Rank boundary tests ---

        [Test]
        public void Score_Below20_ReturnsE()
        {
            var inputs = new ScoringInputs { averageSpeed = 0f, maxVelocityReference = 12f };
            Assert.AreEqual(StyleRank.E, StyleRankScorer.CalculateRank(inputs));
        }

        [Test]
        public void Score_Exactly20_ReturnsD()
        {
            // 20/50 speed = 0.4 normalised * 50 = 20 pts
            var inputs = new ScoringInputs { averageSpeed = 4.8f, maxVelocityReference = 12f };
            float score = StyleRankScorer.CalculateScore(inputs);
            Assert.AreEqual(20f, score, 0.01f);
            Assert.AreEqual(StyleRank.D, StyleRankScorer.CalculateRank(inputs));
        }

        [Test]
        public void Score_Exactly40_ReturnsC()
        {
            // 40/50 speed = 0.8 normalised * 50 = 40 pts
            var inputs = new ScoringInputs { averageSpeed = 9.6f, maxVelocityReference = 12f };
            float score = StyleRankScorer.CalculateScore(inputs);
            Assert.AreEqual(40f, score, 0.01f);
            Assert.AreEqual(StyleRank.C, StyleRankScorer.CalculateRank(inputs));
        }

        [Test]
        public void Score_Exactly60_ReturnsB()
        {
            // Max speed (50 pts) + 1 fire use (20 pts) = 70 — needs tuning to hit 60 exactly
            // Full speed + no fire + 2 enemies (20 pts) = 70; use partial speed:
            // 0.8 normalised * 50 = 40 + 20 fire = 60 pts
            var inputs = new ScoringInputs
            {
                averageSpeed = 9.6f, maxVelocityReference = 12f,
                fireAbilityUses = 1
            };
            float score = StyleRankScorer.CalculateScore(inputs);
            Assert.AreEqual(60f, score, 0.01f);
            Assert.AreEqual(StyleRank.B, StyleRankScorer.CalculateRank(inputs));
        }

        [Test]
        public void Score_Exactly80_ReturnsA()
        {
            // Max speed 50 + fire 20 + 1 enemy 10 = 80 pts
            var inputs = new ScoringInputs
            {
                averageSpeed = 12f, maxVelocityReference = 12f,
                fireAbilityUses = 1, enemyDefeats = 1
            };
            float score = StyleRankScorer.CalculateScore(inputs);
            Assert.AreEqual(80f, score, 0.01f);
            Assert.AreEqual(StyleRank.A, StyleRankScorer.CalculateRank(inputs));
        }

        // --- Cap/clamp tests ---

        [Test]
        public void MultipleFireUses_CappedAt1Use_20pts()
        {
            var inputs = new ScoringInputs { fireAbilityUses = 5, maxVelocityReference = 12f };
            float score = StyleRankScorer.CalculateScore(inputs);
            Assert.AreEqual(20f, score, 0.01f); // fire cap at 1 * 20 = 20
        }

        [Test]
        public void MoreThan2Enemies_CappedAt2_20pts()
        {
            var inputs = new ScoringInputs { enemyDefeats = 10, maxVelocityReference = 12f };
            float score = StyleRankScorer.CalculateScore(inputs);
            Assert.AreEqual(20f, score, 0.01f); // enemy cap at 2 * 10 = 20
        }

        [Test]
        public void SpeedAboveMax_NormalisedTo1_50pts()
        {
            var inputs = new ScoringInputs { averageSpeed = 999f, maxVelocityReference = 12f };
            float score = StyleRankScorer.CalculateScore(inputs);
            Assert.AreEqual(50f, score, 0.01f); // clamped to 1.0 normalised
        }

        [Test]
        public void ZeroMaxVelocityReference_DoesNotThrow()
        {
            var inputs = new ScoringInputs { averageSpeed = 5f, maxVelocityReference = 0f };
            Assert.DoesNotThrow(() => StyleRankScorer.CalculateRank(inputs));
        }

        // --- Determinism test ---

        [Test]
        public void SameInputs_AlwaysProduceSameRank()
        {
            var inputs = new ScoringInputs
            {
                averageSpeed = 8f, maxVelocityReference = 12f,
                fireAbilityUses = 1, enemyDefeats = 1, hazardsAvoided = 2
            };
            StyleRank r1 = StyleRankScorer.CalculateRank(inputs);
            StyleRank r2 = StyleRankScorer.CalculateRank(inputs);
            Assert.AreEqual(r1, r2);
        }
    }
    ```
  - [ ] Run tests via Unity Test Runner: Window → General → Test Runner → EditMode → Run All
  - [ ] All 9 tests must pass before marking story complete

## Dev Notes

### Scoring Algorithm — Designer Tuning

The weights and thresholds in `StyleRankScorer` are placeholder balance values. The level designer should tune after playtesting:

```
Speed factor (0–50 pts):   normalised avgSpeed / maxVelocity * 50
Fire factor (0–20 pts):    binary — used fire once? Full 20 pts
Enemy factor (0–20 pts):   10 pts per defeat, cap at 2
Hazard factor (0–10 pts):  5 pts per avoided zone, cap at 2
────────────────────────
Max achievable: 100 pts

E: 0–19    (slow, no extras)
D: 20–39   (moderate speed or one small extra)
C: 40–59   (decent speed OR fire + some speed)
B: 60–79   (good speed + fire OR enemies + speed)
A: 80+     (fast + fire + defeating enemies)
```

Levels with no enemies or fire pickups: max achievable is 50 pts (C). This is intentional — A rank requires all factors.

If the designer wants A-rank achievable in enemy-free levels, increase `SpeedWeight` to 80 and adjust thresholds. The unit tests will need updating to match.

### `bestRank` Stored as String — Not Enum

```csharp
data.bestRank = LastRank.ToString(); // "A", "B", "C", "D", "E"
```

Stored as string for JSON serialisation safety. `Enum.ToString()` is stable — these values will never be renamed. When reading back:

```csharp
// If you need to compare:
if (Enum.TryParse<StyleRank>(data.bestRank, out var savedRank))
    bool isNewBetter = LastRank > savedRank; // enum int comparison works
```

The current implementation uses `bestScore` (float) for the "is new better?" comparison rather than comparing rank enums directly. This is correct — score is more granular than rank (two B-rank scores may have different point totals).

### HashSet Deduplication — Zone IDs Must Be Unique Per Scene

`HazardAvoidedZone._zoneId` values must be unique within a scene. Convention: assign sequential integers (1, 2, 3…) when placing zones in the level.

No runtime check enforces uniqueness — this is a level authoring responsibility. If two zones share the same ID, the second pass through either zone won't double-count (which is actually fine — it just means that specific zone pair can only contribute one point together).

Do NOT use `GameObject.GetInstanceID()` — this changes every session, breaking the HashSet dedup guarantee across retries.

### Speed Sampling — `FixedUpdate` Only During Rolling/Recovery

Speed is sampled only when the ball is in `Rolling` or `Recovery` state. This prevents:
- Stunned time inflating sample count with zero velocity
- Dead animation time skewing average speed down

If the ball spends most of the level stunned (rare but possible), the average speed may be higher than the true average. This is acceptable — stunned time is already penalised by losing lives.

### `OnFireAbilityActivated` and `OnEnemyDefeated` — Forward Declarations

These SO event assets are created here but raised in Stories 4.1, 4.2, 4.3. Until those stories are implemented:
- `_fireAbilityUses` and `_enemyDefeats` will always be 0
- Score will be purely speed-based
- This is correct and expected for intermediate builds

Do NOT stub fake raises of these events — let the scoring naturally produce low ranks until the systems that raise them exist.

### `IBallController` Interface — Check for Existing Registration

Story 1.3 created `BallController.cs`. If no `IBallController` interface exists yet, create it. If one does exist (check `Assets/_Project/Scripts/Core/`), add `CurrentSpeed` to it rather than creating a duplicate interface.

### Project Structure Notes

- `StyleRank.cs` → `Assets/_Project/Scripts/Progression/`
- `ScoringInputs.cs` → `Assets/_Project/Scripts/Progression/`
- `StyleRankScorer.cs` → `Assets/_Project/Scripts/Progression/`
- `StyleRankTracker.cs` → `Assets/_Project/Scripts/Progression/`
- `IStyleRankTracker.cs` → `Assets/_Project/Scripts/Progression/`
- `HazardAvoidedZone.cs` → `Assets/_Project/Scripts/Obstacles/`
- `StyleRankScorerTests.cs` → `Assets/_Project/Tests/EditMode/`
- `OnFireAbilityActivated.asset` → `Assets/_Project/ScriptableObjects/Events/`
- `OnFireAbilityDeactivated.asset` → `Assets/_Project/ScriptableObjects/Events/`
- `OnEnemyDefeated.asset` → `Assets/_Project/ScriptableObjects/Events/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `StyleRankTracker` registers `IStyleRankTracker`; `HazardAvoidedZone` retrieves it
- `GameEventSO` (1.3) — `OnFireAbilityActivated`, `OnEnemyDefeated`, `OnLevelCompleted` events
- `BallState` enum (1.3/1.4) — speed sampling guards on `BallState.Rolling`/`Recovery`
- `IBallStateManager` (1.4) — `StyleRankTracker.FixedUpdate()` checks current state
- `ISaveService` (2.1) — `bestRank`, `bestScore`, `hasSeenRankReveal` fields added to `LevelSaveData` here
- `LevelConfigSO` (2.2) — `maxVelocity` used as normalisation reference in scorer
- `ILevelManager` (2.2) — `CurrentLevel.levelId` for save data lookup on level complete
- `OnLevelCompleted.asset` (2.1) — subscribed by `StyleRankTracker` to trigger scoring

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-009` (SO events + ServiceLocator)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Unit Tests` (Edit Mode tests for StyleRankScorer)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 3.2`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
