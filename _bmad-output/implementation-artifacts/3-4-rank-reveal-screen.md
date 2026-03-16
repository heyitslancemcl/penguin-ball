# Story 3.4: Rank Reveal Screen

Status: ready-for-dev

## Story

As a player,
I want a satisfying cinematic rank reveal at the end of each level,
so that completing a level feels like a rewarding moment worth replaying.

## Acceptance Criteria

1. **Given** a level is completed for the first time **When** the rank reveal screen appears **Then** the full cinematic plays (~2s): rank letter slams in with 0.3s overshoot bounce → score ticks up over 0.8s → golden ball result fades in → fanfare plays; final state holds 1.5s before "tap to continue"
2. **Given** a level is completed on a subsequent play **When** `hasSeenRankReveal` is `true` for that level **Then** the condensed sequence plays (~1.2s) instead of the full cinematic
3. **Given** the rank reveal completes **When** the player taps to continue **Then** `hasSeenRankReveal` is set to `true` in save data; the level select screen loads
4. **Given** the tutorial (Level 0) is completed **When** the completion screen appears **Then** the rank reveal is NOT shown — a "Great start!" screen is shown instead; golden ball collected status is displayed
5. **Given** the rank reveal DOTween sequence runs **When** the sequence is inspected **Then** it uses a `DOTween` Sequence (not coroutines); first-play and repeat sequences are driven by `hasSeenRankReveal` from save data

## Tasks / Subtasks

- [ ] Task 1: Import and configure DOTween (AC: 1, 2, 5)
  - [ ] Import DOTween from Unity Asset Store or Package Manager (free version)
  - [ ] CRITICAL: After import, run the DOTween Setup Utility: `Tools → DOTween Utility Panel → Setup DOTween!` — without this step, DOTween will not compile correctly in Unity 6
  - [ ] Add `using DG.Tweening;` to any script that uses DOTween
  - [ ] DOTween is used ONLY for this screen — do not add DOTween calls to other scripts yet

- [ ] Task 2: Create `IRankRevealService` interface (AC: 1–4)
  - [ ] Create `Assets/_Project/Scripts/UI/IRankRevealService.cs`:
    ```csharp
    public interface IRankRevealService
    {
        /// <summary>
        /// Called by LevelManager after level complete save. Pulls rank data from
        /// IStyleRankTracker and save state via ServiceLocator — caller provides nothing.
        /// </summary>
        void Show();
    }
    ```
  - [ ] Open `Assets/_Project/Scripts/Core/LevelManager.cs` (Story 2.5)
  - [ ] Replace the stub coroutine in `HandleLevelCompleted()`:
    ```csharp
    private void HandleLevelCompleted()
    {
        // 1. Update save data (already present from Story 2.5)
        var save = ServiceLocator.Get<ISaveService>();
        var levelData = save.GetLevelData(CurrentLevel.levelId);
        levelData.completed = true;
        save.UpdateLevelData(CurrentLevel.levelId, levelData);

        // 2. Fire analytics levelComplete event (stub — Story 6.3)
        // TODO: ServiceLocator.Get<IAnalyticsService>().LogLevelComplete(CurrentLevel.levelId);

        // 3. Show rank reveal screen (replaces stub from Story 2.5)
        ServiceLocator.Get<IRankRevealService>().Show();

        // Story 2.5 stub removed:
        // StartCoroutine(LevelCompleteStubCoroutine()); ← DELETE THIS
    }
    ```
  - [ ] Remove `LevelCompleteStubCoroutine()` method entirely from `LevelManager`
  - [ ] Remove `[SerializeField] private GameObject _levelCompleteStubText;` field from `LevelManager`
  - [ ] Remove `_levelCompleteStubText` assignment from the GameScene Inspector

- [ ] Task 3: Create `RankRevealScreen` MonoBehaviour (AC: 1–5)
  - [ ] Create `Assets/_Project/Scripts/UI/RankRevealScreen.cs`:
    ```csharp
    using DG.Tweening;

    public class RankRevealScreen : MonoBehaviour, IRankRevealService
    {
        [Header("Shared Elements")]
        [SerializeField] private CanvasGroup _screenGroup;
        [SerializeField] private Button      _tapToContinueButton;

        [Header("Rank Reveal Elements")]
        [SerializeField] private GameObject  _rankRevealPanel;
        [SerializeField] private TMP_Text    _rankLetterText;
        [SerializeField] private TMP_Text    _scoreText;
        [SerializeField] private CanvasGroup _goldenBallResultGroup; // fades in
        [SerializeField] private TMP_Text    _goldenBallResultText;  // "Golden Ball: YES / NO"

        [Header("Tutorial Complete Elements")]
        [SerializeField] private GameObject  _tutorialCompletePanel;
        [SerializeField] private TMP_Text    _goldenBallTutorialText;

        private float _revealShownTime;

        private void Awake()
        {
            ServiceLocator.Register<IRankRevealService>(this);
            gameObject.SetActive(false);
        }

        private void Start()
        {
            _tapToContinueButton.onClick.AddListener(OnTapToContinue);
        }

        private void OnDestroy()
        {
            _tapToContinueButton.onClick.RemoveListener(OnTapToContinue);
        }

        // IRankRevealService
        public void Show()
        {
            gameObject.SetActive(true);
            _screenGroup.alpha = 0f;
            _tapToContinueButton.gameObject.SetActive(false);

            var levelManager = ServiceLocator.Get<ILevelManager>();
            bool isTutorial = levelManager.CurrentLevel.levelId.Contains("tutorial");

            if (isTutorial)
                ShowTutorialComplete();
            else
                ShowRankReveal();
        }

        // ─── Tutorial path ───────────────────────────────────────────────────

        private void ShowTutorialComplete()
        {
            _rankRevealPanel.SetActive(false);
            _tutorialCompletePanel.SetActive(true);

            var save    = ServiceLocator.Get<ISaveService>();
            var levelId = ServiceLocator.Get<ILevelManager>().CurrentLevel.levelId;
            bool gotBall = save.GetLevelData(levelId).goldenBallCollected;
            _goldenBallTutorialText.text = gotBall ? "Golden Ball: Collected!" : "Golden Ball: Not found";

            // Fade in the panel, then show tap-to-continue after 1s
            _screenGroup.DOFade(1f, 0.3f).OnComplete(() =>
            {
                DOVirtual.DelayedCall(1f, () => ShowTapToContinue());
            });
        }

        // ─── Rank reveal path ────────────────────────────────────────────────

        private void ShowRankReveal()
        {
            _rankRevealPanel.SetActive(true);
            _tutorialCompletePanel.SetActive(false);

            var tracker = ServiceLocator.Get<IStyleRankTracker>();
            var save    = ServiceLocator.Get<ISaveService>();
            var levelId = ServiceLocator.Get<ILevelManager>().CurrentLevel.levelId;

            StyleRank rank    = tracker.LastRank;
            float     score   = tracker.LastScore;
            bool      gotBall = save.GetLevelData(levelId).goldenBallCollected;
            bool      hasSeen = save.GetLevelData(levelId).hasSeenRankReveal;

            _rankLetterText.text       = rank.ToString();
            _goldenBallResultText.text = gotBall ? "Golden Ball: YES" : "Golden Ball: NO";
            _goldenBallResultGroup.alpha = 0f;

            // Prepare score display
            float displayScore = 0f;
            _scoreText.text = "0";

            _screenGroup.DOFade(1f, 0.2f).OnComplete(() =>
            {
                if (hasSeen)
                    PlayCondensedSequence(score, ref displayScore);
                else
                    PlayFullCinematic(score, ref displayScore);
            });
        }

        private void PlayFullCinematic(float targetScore, ref float displayScore)
        {
            // Capture ref into local for closure
            float localDisplay = displayScore;

            Sequence seq = DOTween.Sequence();

            // 1. Rank letter slams in (0.3s punch-scale overshoot)
            _rankLetterText.transform.localScale = Vector3.zero;
            seq.Append(_rankLetterText.transform.DOScale(Vector3.one, 0.15f));
            seq.Append(_rankLetterText.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 10, 0.5f));

            // 2. Score ticks up (0.8s)
            seq.Append(DOTween.To(
                () => localDisplay,
                x  => { localDisplay = x; _scoreText.text = Mathf.RoundToInt(x).ToString(); },
                targetScore, 0.8f
            ).SetEase(Ease.OutQuad));

            // 3. Golden ball result fades in (0.3s)
            seq.Append(_goldenBallResultGroup.DOFade(1f, 0.3f));

            // 4. Hold 1.5s, then show tap-to-continue
            seq.AppendInterval(1.5f);
            seq.AppendCallback(() =>
            {
                // TODO: ServiceLocator.Get<IAudioService>().PlayFanfare(); (Story 5.1)
                ShowTapToContinue();
            });

            seq.Play();
        }

        private void PlayCondensedSequence(float targetScore, ref float displayScore)
        {
            float localDisplay = displayScore;

            Sequence seq = DOTween.Sequence();

            // Quick rank appearance (0.1s)
            _rankLetterText.transform.localScale = Vector3.zero;
            seq.Append(_rankLetterText.transform.DOScale(Vector3.one, 0.1f));

            // Score ticks up quickly (0.4s)
            seq.Append(DOTween.To(
                () => localDisplay,
                x  => { localDisplay = x; _scoreText.text = Mathf.RoundToInt(x).ToString(); },
                targetScore, 0.4f
            ).SetEase(Ease.OutQuad));

            // Golden ball appears immediately
            seq.Join(_goldenBallResultGroup.DOFade(1f, 0.2f));

            // Hold 0.3s, show tap-to-continue
            seq.AppendInterval(0.3f);
            seq.AppendCallback(() => ShowTapToContinue());

            seq.Play();
        }

        private void ShowTapToContinue()
        {
            _revealShownTime = Time.realtimeSinceStartup;
            _tapToContinueButton.gameObject.SetActive(true);
        }

        // ─── Navigation ──────────────────────────────────────────────────────

        private void OnTapToContinue()
        {
            // Mark rank reveal as seen
            var save    = ServiceLocator.Get<ISaveService>();
            var levelId = ServiceLocator.Get<ILevelManager>().CurrentLevel.levelId;
            var data    = save.GetLevelData(levelId);

            // Fire dwell-time analytics (Story 6.3)
            float dwellTime = Time.realtimeSinceStartup - _revealShownTime;
            // TODO: ServiceLocator.Get<IAnalyticsService>().LogRankScreenDwellTime(levelId, dwellTime);

            data.hasSeenRankReveal = true;
            save.UpdateLevelData(levelId, data);

            // Kill any running DOTween sequences on this object
            DOTween.Kill(gameObject);

            // Return to level select
            ServiceLocator.Get<ILevelManager>().LoadLevelSelect();
        }
    }
    ```

- [ ] Task 4: Build RankRevealScreen Canvas layout in GameScene (AC: 1–4)
  - [ ] Add `RankRevealScreen` as a full-screen overlay on the GameScene Canvas (above other HUD elements)
  - [ ] Add `CanvasGroup` component to the root `RankRevealScreen` GameObject (`_screenGroup`)
  - [ ] **Rank reveal panel** (child, `_rankRevealPanel`):
    - `TMP_Text: RankLetterText` — large centred letter (120pt, bold) — `_rankLetterText`
    - `TMP_Text: ScoreText` — smaller score number below rank — `_scoreText`
    - `Container: GoldenBallResultContainer` with `CanvasGroup` — `_goldenBallResultGroup`
      - `TMP_Text: GoldenBallResultText` inside — `_goldenBallResultText`
  - [ ] **Tutorial complete panel** (child, `_tutorialCompletePanel`):
    - `TMP_Text: "Great start!"` — large centred header
    - `TMP_Text: GoldenBallTutorialText` — `_goldenBallTutorialText`
  - [ ] **Tap to continue button** (`_tapToContinueButton`):
    - Anchored bottom-centre, above home indicator safe area
    - `TMP_Text` label: "Tap to continue"
    - Inactive by default (`gameObject.SetActive(false)`)
  - [ ] `RankRevealScreen` root: `SetActive(false)` in Awake — hidden until `Show()` is called
  - [ ] Wire all `[SerializeField]` references in Inspector

- [ ] Task 5: `DOTween.Kill` on scene transitions (AC: 5)
  - [ ] `OnTapToContinue()` calls `DOTween.Kill(gameObject)` before `LoadLevelSelect()`
  - [ ] This prevents any in-flight tweens from updating destroyed GameObjects after the scene changes
  - [ ] DOTween sequences reference the `gameObject` target via `.SetTarget(gameObject)` — note the `PlayFullCinematic` and `PlayCondensedSequence` sequences do NOT automatically target `gameObject`; call `seq.SetTarget(gameObject)` at sequence creation:
    ```csharp
    Sequence seq = DOTween.Sequence().SetTarget(gameObject);
    ```
    This ensures `DOTween.Kill(gameObject)` correctly cleans them up.

- [ ] Task 6: Play Mode test checklist (AC: 1–5)
  - [ ] First completion of a level → full cinematic plays with punch-scale rank slam, score tick, golden ball fade, tap button appears after hold
  - [ ] Replay same level → condensed sequence plays (verify `hasSeenRankReveal == true` in save)
  - [ ] Complete tutorial level → "Great start!" panel shown, no rank letter, golden ball status displayed
  - [ ] Golden ball collected → "Golden Ball: YES" shown; not collected → "NO"
  - [ ] Tap to continue → `hasSeenRankReveal` saved, returns to level select with no scene leaks
  - [ ] Verify DOTween sequences are killed cleanly (no console errors after scene transition)

## Dev Notes

### DOTween Setup — Critical First Step

DOTween MUST be configured before it will work:

```
1. Import DOTween from Asset Store
2. Tools → DOTween Utility Panel → Setup DOTween!
3. In setup dialog: leave defaults, click "Apply"
4. Restart Unity if prompted
```

Without running Setup, DOTween's `DOTweenAnimation` and scripting APIs will throw `NullReferenceException` at runtime. This is the most common DOTween issue in Unity 6.

### `DOTween.Sequence().SetTarget(gameObject)` — Memory Leak Prevention

```csharp
// ✅ CORRECT — sequence is linked to this gameObject for Kill cleanup
Sequence seq = DOTween.Sequence().SetTarget(gameObject);

// Then on navigation:
DOTween.Kill(gameObject); // kills ALL sequences targeting this gameObject
```

Without `.SetTarget()`, `DOTween.Kill(gameObject)` won't find the sequence and it will continue running against a destroyed object, causing `NullReferenceException` after scene load.

### `hasSeenRankReveal` — Set on Tap, Not on Show

`hasSeenRankReveal` is set to `true` when the player **taps to continue** — not when the screen first appears. This mirrors real game UX: if the player quits mid-reveal (app killed), they see the full cinematic again on next completion. Only after they've tapped through is the seen state recorded.

```csharp
// ✅ Set in OnTapToContinue — player has seen and acknowledged the reveal
data.hasSeenRankReveal = true;

// ❌ Do NOT set in Show() — player might not see it if they quit immediately
```

### Tutorial Detection — levelId String Check

```csharp
bool isTutorial = levelManager.CurrentLevel.levelId.Contains("tutorial");
```

`biome1_tutorial` is the only tutorial level at launch. This check is sufficient for MVP. If a future biome adds a tutorial (e.g. `biome2_tutorial`), this check covers it automatically.

Do NOT add a `bool isTutorial` field to `LevelConfigSO` for this — it would need to be manually checked for every new level config. The string convention is self-enforcing.

### `LevelCompleteStubCoroutine` Removal — Clean Up Story 2.5 Code

Story 2.5 added `LevelCompleteStubCoroutine()` and `_levelCompleteStubText` as temporary scaffolding. Story 3.4 replaces them. The dev must:

1. Delete `LevelCompleteStubCoroutine()` from `LevelManager.cs`
2. Delete `[SerializeField] private GameObject _levelCompleteStubText;`
3. Remove the "Level Complete!" placeholder text GameObject from the GameScene Canvas
4. Remove the stub TODO comment

After this, `HandleLevelCompleted()` in `LevelManager` is clean.

### Dwell Time — `Time.realtimeSinceStartup` Not `Time.time`

```csharp
// ✅ CORRECT — measures real elapsed time; not affected by Time.timeScale
_revealShownTime = Time.realtimeSinceStartup;
// ...
float dwellTime = Time.realtimeSinceStartup - _revealShownTime;

// ❌ WRONG for dwell time — affected by timeScale (set to 0 by pause menu)
_revealShownTime = Time.time;
```

The rank reveal screen is shown after level complete when gameplay is no longer running — but `Time.timeScale` might still be in an odd state. Using `realtimeSinceStartup` gives accurate user-facing dwell time for analytics.

### Analytics Stub — Story 6.3

The `OnTapToContinue()` method contains:
```csharp
// TODO: ServiceLocator.Get<IAnalyticsService>().LogRankScreenDwellTime(levelId, dwellTime);
```

`_revealShownTime` must be set when `ShowTapToContinue()` is called (when the button becomes interactive) — this is the correct start of the "dwell" window. Story 6.3 will fill in the analytics call.

### Score Display — `Mathf.RoundToInt` Avoids Floating Point Text

```csharp
_scoreText.text = Mathf.RoundToInt(x).ToString();
```

The raw `float` score (e.g. `67.3`) would display as "67.3" or "67.300003" without rounding. Round to int for clean display — the fractional component is a scoring artefact with no player-facing meaning.

### Project Structure Notes

- `IRankRevealService.cs` → `Assets/_Project/Scripts/UI/`
- `RankRevealScreen.cs` → `Assets/_Project/Scripts/UI/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `RankRevealScreen.Awake()` registers `IRankRevealService`
- `ISaveService` (2.1) — reads/writes `hasSeenRankReveal`, `goldenBallCollected` from `LevelSaveData`
- `ILevelManager` (2.2) — `CurrentLevel.levelId` for save lookups; `LoadLevelSelect()` for navigation
- `LevelManager.HandleLevelCompleted()` (2.5) — stub coroutine removed; `IRankRevealService.Show()` wired
- `StyleRank` enum (3.2) — `LastRank` from `IStyleRankTracker`
- `IStyleRankTracker` (3.2) — `LastRank` and `LastScore` read in `Show()`
- `LevelSaveData.hasSeenRankReveal` (3.2) — bool added to save model in Story 3.2
- `OnLevelCompleted.asset` (2.1) — NOT subscribed by `RankRevealScreen` — `LevelManager` calls `Show()` directly

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (DOTween setup, object lifecycle)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 3.4`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
