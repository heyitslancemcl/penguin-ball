# Story 2.4: Level Select Screen

Status: ready-for-dev

## Story

As a player,
I want to see all levels in a grid and know which ones I've completed and whether the boss is unlocked,
so that I can choose where to play and feel motivated by my progress.

## Acceptance Criteria

1. **Given** the player opens the level select screen **When** the grid renders **Then** all Biome 1 levels are displayed including the tutorial (L0), 9–12 main levels, and the boss level
2. **Given** a level has been completed **When** the level select grid renders **Then** that level's tile shows its completion state (completed indicator visible)
3. **Given** a level has had its golden ball collected **When** the level select grid renders **Then** that level's tile shows the golden ball as collected
4. **Given** fewer than 4 Biome 1 levels have `goldenBallCollected: true` in save data **When** the level select grid renders **Then** the boss level tile is locked and non-tappable; a counter shows e.g. "2/4 golden balls"
5. **Given** 4 or more Biome 1 levels have `goldenBallCollected: true` **When** the level select grid renders **Then** the boss level tile is unlocked and tappable; the golden ball counter shows "4+/4"
6. **Given** the player taps an unlocked level tile **When** the tap registers **Then** that level loads via `LevelManager`

## Tasks / Subtasks

- [ ] Task 1: Create `LevelSelectController` and scene setup (AC: 1)
  - [ ] Open `Assets/_Project/Scenes/LevelSelect.unity` (placeholder created in Story 2.2)
  - [ ] Replace placeholder content with proper scene setup:
    - Main Camera (portrait, orthographic or perspective — your choice)
    - Canvas (Screen Space — Overlay, reference resolution 390×844 for iPhone portrait)
    - `LevelSelectController` MonoBehaviour on a root GameObject
  - [ ] Create `Assets/_Project/Scripts/UI/LevelSelectController.cs`
  - [ ] `[SerializeField] private LevelConfigSO[] _levelConfigs;` — assign ALL level configs in order in Inspector (tutorial first, boss last)
  - [ ] `[SerializeField] private LevelTile _tilePrefab;`
  - [ ] `[SerializeField] private Transform _gridContainer;`
  - [ ] `[SerializeField] private Text _goldenBallCounterText;` (or `TMP_Text` if TextMeshPro is available)
  - [ ] `[SerializeField] private int _goldenBallsRequiredForBoss = 4;`

- [ ] Task 2: Create `LevelTile` prefab and script (AC: 1, 2, 3, 4, 5, 6)
  - [ ] Create `Assets/_Project/Scripts/UI/LevelTile.cs` (MonoBehaviour)
  - [ ] Tile displays:
    - Level display name (e.g. "Level 1", "Tutorial", "Boss")
    - Completion checkmark/star (active when `completed == true`)
    - Golden ball icon (active when `goldenBallCollected == true`)
    - Lock overlay (active when tile is locked)
  - [ ] `LevelTile` fields:
    ```csharp
    [SerializeField] private Text _levelNameText;
    [SerializeField] private GameObject _completedIndicator;
    [SerializeField] private GameObject _goldenBallIndicator;
    [SerializeField] private GameObject _lockOverlay;
    [SerializeField] private Button _button;
    ```
  - [ ] `public void Setup(LevelConfigSO config, LevelSaveData saveData, bool isLocked)`:
    ```csharp
    public void Setup(LevelConfigSO config, LevelSaveData saveData, bool isLocked)
    {
        _config = config;
        _levelNameText.text = config.displayName;
        _completedIndicator.SetActive(saveData.completed);
        _goldenBallIndicator.SetActive(saveData.goldenBallCollected);
        _lockOverlay.SetActive(isLocked);
        _button.interactable = !isLocked;
        _button.onClick.AddListener(OnTilePressed);
    }

    private void OnTilePressed()
    {
        ServiceLocator.Get<ILevelManager>().LoadLevel(_config);
    }
    ```
  - [ ] Create `LevelTile.prefab`: `Assets/_Project/Prefabs/UI/LevelTile.prefab`
  - [ ] Tile dimensions: ~160×160px for portrait grid (3 tiles per row fits comfortably)

- [ ] Task 3: Implement grid population logic (AC: 1, 2, 3, 4, 5)
  - [ ] In `LevelSelectController.Start()`:
    ```csharp
    void Start()
    {
        var save = ServiceLocator.Get<ISaveService>();
        int goldenBallCount = CountBiomeGoldenBalls(save);

        foreach (var config in _levelConfigs)
        {
            var saveData = save.GetLevelData(config.levelId);
            bool isBoss = config.levelId.Contains("boss");
            bool isLocked = isBoss && goldenBallCount < _goldenBallsRequiredForBoss;

            var tile = Instantiate(_tilePrefab, _gridContainer);
            tile.Setup(config, saveData, isLocked);
        }

        // Update golden ball counter
        UpdateGoldenBallCounter(goldenBallCount);
    }
    ```
  - [ ] `CountBiomeGoldenBalls(ISaveService save)` — derived count, no stored flag:
    ```csharp
    private int CountBiomeGoldenBalls(ISaveService save)
    {
        return _levelConfigs
            .Where(c => !c.levelId.Contains("boss") && !c.levelId.Contains("tutorial"))
            .Count(c => save.GetLevelData(c.levelId).goldenBallCollected);
    }
    ```
  - [ ] `UpdateGoldenBallCounter(int count)`:
    ```csharp
    int display = Mathf.Min(count, _goldenBallsRequiredForBoss);
    string suffix = count >= _goldenBallsRequiredForBoss ? "+" : "";
    _goldenBallCounterText.text = $"{display}{suffix}/{_goldenBallsRequiredForBoss} golden balls";
    ```

- [ ] Task 4: Set up Grid Layout on Canvas (AC: 1)
  - [ ] Add a `ScrollView` to the Canvas for the level grid (levels may exceed screen height)
  - [ ] Inside ScrollView content: add `GridLayoutGroup` component:
    - `Cell Size`: (160, 160)
    - `Spacing`: (16, 16)
    - `Constraint`: Fixed Column Count = 3
    - `Start Corner`: Upper Left
  - [ ] Anchor the golden ball counter text to the top of the Canvas (above the grid)
  - [ ] Boss tile: placed last in the grid, visually distinct (darker background or gold border)

- [ ] Task 5: Handle return from level (AC: 6)
  - [ ] When `LoadLevelSelect()` is called from `LevelManager` (Story 2.2), this scene loads via `LoadSceneMode.Single`
  - [ ] `LevelSelectController.Start()` refreshes all tile states from current save data — no stale state issues
  - [ ] No additional "back" button needed for this story — navigation is handled by `LevelManager`

- [ ] Task 6: Add `LevelSelectController` to `LevelSelect.unity` and test (AC: 1–6)
  - [ ] Wire all `[SerializeField]` references in Inspector
  - [ ] Assign `_levelConfigs` array in correct order: tutorial, level01, level02, ..., boss
  - [ ] Play Mode test checklist:
    - [ ] All level tiles appear
    - [ ] Boss tile locked when golden ball count < 4
    - [ ] Boss tile unlocked when golden ball count ≥ 4 (manually set save data for testing)
    - [ ] Completed indicator appears for completed levels
    - [ ] Golden ball icon appears for collected levels
    - [ ] Tapping unlocked tile loads the level
    - [ ] Tapping locked boss tile does nothing

## Dev Notes

### Golden Ball Boss Gate — Derived Count Only

Per architecture (confirmed in party mode discussion): the boss gate is a **derived count** from save data, NOT a stored flag.

```csharp
// ✅ CORRECT — count derived at runtime from level save data
int count = _levelConfigs
    .Where(c => !c.levelId.Contains("boss") && !c.levelId.Contains("tutorial"))
    .Count(c => save.GetLevelData(c.levelId).goldenBallCollected);

// ❌ WRONG — stored flag that could get out of sync
bool bossUnlocked = save.Data.globalStats.bossUnlocked;
```

This ensures the count is always accurate even if save data is partially corrupted or the player somehow loses a golden ball record — the derived count self-corrects.

### No UX Design Document — Keep It Functional

No UX design was created for this project. Build a clean, functional grid UI:
- Portrait orientation, safe area respected
- 3 tiles per row works well on all supported iPhone sizes (SE 4th gen through Pro Max)
- Placeholder art is fine — visual polish is out of scope for MVP stories
- Use Unity's built-in UI components (Button, Text, Image) — no third-party UI framework

### TextMeshPro vs Legacy Text

Unity 6 projects include TextMeshPro by default. Prefer `TMP_Text` over legacy `Text` for all UI text in this story:

```csharp
using TMPro;
[SerializeField] private TMP_Text _levelNameText;
[SerializeField] private TMP_Text _goldenBallCounterText;
```

If legacy `Text` was used in any earlier story UI, migrate to TMP now and be consistent going forward.

### ServiceLocator Access in UI

`LevelSelectController` loads via `LoadSceneMode.Single` — Bootstrap is destroyed when this happens. This means `ServiceLocator` registrations from Bootstrap are gone.

**Solution**: `LevelSelectController` is its own scene with its own Bootstrap-like setup, OR use a persistent `GameManager` singleton.

**Recommended for MVP**: Add a `LevelSelectBootstrap` component to the LevelSelect scene that registers `ISaveService` and `ILevelManager` fresh:

```csharp
// LevelSelectBootstrap.Awake():
ServiceLocator.Clear(); // clear old registrations
var saveManager = gameObject.AddComponent<SaveManager>();
ServiceLocator.Register<ISaveService>(saveManager);
var levelManager = gameObject.AddComponent<LevelManager>();
ServiceLocator.Register<ILevelManager>(levelManager);
```

**OR**: Refactor Bootstrap to use `DontDestroyOnLoad` more robustly so it persists through `LoadSceneMode.Single`. This is the cleaner long-term solution but requires updating `BootstrapManager` to survive scene transitions.

Choose one approach and be consistent — document the decision in `BootstrapManager.cs` comments.

### `LevelTile` Button Cleanup

`_button.onClick.AddListener(OnTilePressed)` in `Setup()` — ensure the listener is removed if the tile is destroyed or re-used:

```csharp
private void OnDestroy() => _button.onClick.RemoveListener(OnTilePressed);
```

### Project Structure Notes

- `LevelSelectController.cs` → `Assets/_Project/Scripts/UI/`
- `LevelTile.cs` → `Assets/_Project/Scripts/UI/`
- `LevelTile.prefab` → `Assets/_Project/Prefabs/UI/`
- `LevelSelect.unity` → `Assets/_Project/Scenes/` (replaces placeholder from Story 2.2)

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `LevelTile` retrieves `ILevelManager`
- `ISaveService` (2.1) — `LevelSelectController` reads all level save data
- `LevelConfigSO` (2.2) — all config assets assigned in `_levelConfigs` array
- `ILevelManager` (2.2) — `LevelTile.OnTilePressed()` calls `LoadLevel()`
- `LevelSelect.unity` placeholder (2.2) — expanded here

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-003` (save data structure, levelId keying)
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Requirements Overview` (FR14: Level select screen)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 2.4`
- Epics: `_bmad-output/planning-artifacts/epics.md#Epic 2` (boss gate: ≥4 golden balls, derived count)

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
