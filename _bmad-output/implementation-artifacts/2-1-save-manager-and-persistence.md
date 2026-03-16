# Story 2.1: Save Manager & Persistence

Status: ready-for-dev

## Story

As a player,
I want my progress saved automatically so I never lose it due to a crash, backgrounding, or closing the app,
so that I can pick up exactly where I left off every session.

## Acceptance Criteria

1. **Given** a level is completed or a golden ball is collected **When** the save trigger fires **Then** the save file is written atomically (temp file → rename) to `Application.persistentDataPath` with XOR obfuscation applied
2. **Given** a save is written successfully **When** the file system is inspected **Then** a `.bak` backup of the previous save exists alongside the current save file
3. **Given** the app is backgrounded or loses focus **When** `OnApplicationPause(true)` or `OnApplicationFocus(false)` fires **Then** the save is triggered automatically with no data loss
4. **Given** a save file exists at startup **When** `SaveManager` loads the file **Then** per-level data is correctly restored keyed by levelId string (e.g. `biome1_level01`)
5. **Given** the save file is corrupt or missing **When** `SaveManager` attempts to load **Then** the `.bak` file is used as fallback; if both fail, a fresh save is initialised with no crash
6. **Given** the `SaveManager` is unit tested **When** Edit Mode tests run **Then** save/load round-trip, corruption recovery, and atomic write behaviour all pass

## Tasks / Subtasks

- [ ] Task 1: Define save data models (AC: 4)
  - [ ] Create `Assets/_Project/Scripts/Progression/SaveData.cs`
    ```csharp
    [System.Serializable]
    public class SaveData
    {
        public Dictionary<string, LevelSaveData> levels = new();
        public GlobalStats globalStats = new();
    }

    [System.Serializable]
    public class LevelSaveData
    {
        public bool completed;
        public bool goldenBallCollected;
        public string bestRank;       // null, "E", "D", "C", "B", "A"
        public int bestScore;
        public int attemptCount;
        public bool hasSeenRankReveal;
    }

    [System.Serializable]
    public class GlobalStats
    {
        public int totalAttempts;
        public int totalCompletions;
        public int sessionCount;
    }
    ```
  - [ ] Note: `Dictionary<string, LevelSaveData>` requires a custom JSON converter OR use `JsonUtility`-compatible wrapper (see Dev Notes on serialisation choice)

- [ ] Task 2: Define `ISaveService` interface (AC: 1, 4, 5)
  - [ ] Create `Assets/_Project/Scripts/Progression/ISaveService.cs`
    ```csharp
    public interface ISaveService
    {
        SaveData Data { get; }
        void Save();
        void Load();
        LevelSaveData GetLevelData(string levelId);
        void UpdateLevelData(string levelId, LevelSaveData data);
    }
    ```

- [ ] Task 3: Implement `SaveManager` (AC: 1, 2, 3, 4, 5)
  - [ ] Create `Assets/_Project/Scripts/Progression/SaveManager.cs` (MonoBehaviour implementing `ISaveService`)
  - [ ] File paths:
    ```csharp
    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");
    private string BackupPath => Path.Combine(Application.persistentDataPath, "save.json.bak");
    private string TempPath => Path.Combine(Application.persistentDataPath, "save.json.tmp");
    ```
  - [ ] `Save()` — atomic write with XOR obfuscation:
    ```csharp
    public void Save()
    {
        string json = SerializeData(Data);
        byte[] bytes = XorObfuscate(System.Text.Encoding.UTF8.GetBytes(json));

        // Atomic write: write to temp, then rename
        File.WriteAllBytes(TempPath, bytes);

        // Backup existing save before overwriting
        if (File.Exists(SavePath))
            File.Copy(SavePath, BackupPath, overwrite: true);

        File.Move(TempPath, SavePath); // atomic on most file systems
    }
    ```
  - [ ] `Load()` — with fallback chain:
    ```csharp
    public void Load()
    {
        if (TryLoadFrom(SavePath, out var data))  { Data = data; return; }
        if (TryLoadFrom(BackupPath, out data))    { Data = data; return; }
        Data = new SaveData(); // fresh start
    }

    private bool TryLoadFrom(string path, out SaveData data)
    {
        data = null;
        try
        {
            if (!File.Exists(path)) return false;
            byte[] bytes = File.ReadAllBytes(path);
            string json = System.Text.Encoding.UTF8.GetString(XorObfuscate(bytes));
            data = DeserializeData(json);
            return data != null;
        }
        catch { return false; }
    }
    ```
  - [ ] XOR obfuscation — simple, symmetric (same method for encode/decode):
    ```csharp
    private static readonly byte _xorKey = 0x5A; // single-byte XOR — not encryption, just obfuscation
    private byte[] XorObfuscate(byte[] input)
    {
        var result = new byte[input.Length];
        for (int i = 0; i < input.Length; i++) result[i] = (byte)(input[i] ^ _xorKey);
        return result;
    }
    ```
  - [ ] `GetLevelData(string levelId)`: return `Data.levels.TryGetValue(levelId, out var d) ? d : new LevelSaveData()`
  - [ ] `UpdateLevelData(string levelId, LevelSaveData data)`: `Data.levels[levelId] = data; Save();`

- [ ] Task 4: Register `SaveManager` via ServiceLocator and handle app lifecycle (AC: 3)
  - [ ] Add `SaveManager` component to Bootstrap GameObject
  - [ ] `Awake()`: `ServiceLocator.Register<ISaveService>(this); Load();`
  - [ ] `OnApplicationPause(bool paused)`: `if (paused) Save();`
  - [ ] `OnApplicationFocus(bool hasFocus)`: `if (!hasFocus) Save();`
  - [ ] `Data.globalStats.sessionCount++` on each `Load()` call (increment session count at startup)

- [ ] Task 5: Wire save triggers to SO events (AC: 1)
  - [ ] `SaveManager` subscribes to `OnGoldenBallCollected` SO event (created in Story 1.3) in `OnEnable()`/`OnDisable()`
  - [ ] On `OnGoldenBallCollected`: mark current level's `goldenBallCollected = true` and call `Save()` — level ID passed via event payload (upgrade `GameEventSO` to carry a string payload, or use a separate `GameEventSOString`)
  - [ ] On `OnLevelCompleted` (create new SO event asset): mark `completed = true`, save
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnLevelCompleted.asset` (GameEventSO)
  - [ ] Note: `OnLevelCompleted` is raised by `LevelManager` in Story 2.2 — create the asset now, wire the raise in 2.2

- [ ] Task 6: Write Edit Mode unit tests (AC: 6)
  - [ ] Create `Assets/_Project/Tests/Progression/SaveManagerTests.cs`
  - [ ] Test cases:
    - `Save_And_Load_RoundTrip()` → save data with known values, load back, assert all fields match
    - `Load_WithCorruptFile_FallsBackToBackup()` → write garbage bytes to save path, verify `.bak` is used
    - `Load_WithBothFilesCorrupt_InitialisesEmpty()` → corrupt both files, verify `Data != null` and no exception
    - `Save_WritesToTempFirst()` → verify `.tmp` file is created then removed during save
    - `Save_CreatesBackup()` → save twice, verify `.bak` exists and contains first save's data
    - `GetLevelData_UnknownLevel_ReturnsDefault()` → get data for levelId not in save → returns default LevelSaveData (not null, not exception)
    - `UpdateLevelData_PersistsCorrectly()` → update a level, reload, assert change persisted
    - `XorObfuscate_IsSymmetric()` → XorObfuscate(XorObfuscate(input)) == input
  - [ ] Tests use a temp directory (`Path.GetTempPath()`) for file I/O — clean up in `TearDown`

## Dev Notes

### JSON Serialisation Choice — Critical Decision

Unity's `JsonUtility` does NOT support `Dictionary<string, T>`. Two options:

**Option A (Recommended): Newtonsoft Json.NET**
- Available via Unity Package Manager: `com.unity.nuget.newtonsoft-json`
- Supports Dictionary natively, handles null fields, more robust
- Import: Package Manager → Add package by name → `com.unity.nuget.newtonsoft-json`
- Usage: `JsonConvert.SerializeObject(data)` / `JsonConvert.DeserializeObject<SaveData>(json)`

**Option B: JsonUtility with wrapper**
- Use a serialisable list of key-value pairs instead of Dictionary
- More boilerplate, less readable, harder to debug
- Not recommended for this project

**Decision: Use Option A (Newtonsoft Json.NET).** Add it to the project in this story.

### Atomic Write Pattern — Why It Matters

```
❌ BAD: Direct write to save.json
   → App crash mid-write = corrupt save file = player loses all progress

✅ GOOD: Write to temp → copy existing to .bak → rename temp to save.json
   → If crash during write, .tmp is incomplete, .json is still intact
   → If crash between backup and rename, .bak is the safety net
```

`File.Move()` is atomic on most file systems (including iOS APFS) — it either completes or doesn't. It does NOT partially write the destination.

### XOR Obfuscation — Not Encryption

XOR obfuscation prevents casual hex editing of the save file. It is NOT cryptographic security. The architecture does not require real encryption — this is a single-player mobile game with no server validation. The goal is to prevent accidental corruption from partial reads and deter casual save editing.

### Save Trigger Architecture

Do NOT save on every frame or every physics tick. Save triggers are:
1. `OnGoldenBallCollected` SO event
2. `OnLevelCompleted` SO event
3. `OnApplicationPause(true)`
4. `OnApplicationFocus(false)`
5. Level restart (Story 2.5 will call `SaveManager.Save()` in `RestartLevel()`)

Story 2.2 (LevelManager) will call `SaveManager.UpdateLevelData()` for `attemptCount` on level start.

### GameEventSO String Payload

The `OnGoldenBallCollected` event needs to carry the `levelId` so `SaveManager` knows which level to mark. Upgrade `GameEventSO` (from Story 1.3) to a generic version OR create a new `GameEventSOString`:

```csharp
[CreateAssetMenu(menuName = "PenguineBall/GameEvent (String)")]
public class GameEventSOString : ScriptableObject
{
    private readonly List<Action<string>> _listeners = new();
    public void AddListener(Action<string> l) => _listeners.Add(l);
    public void RemoveListener(Action<string> l) => _listeners.Remove(l);
    public void Raise(string value) => _listeners.ForEach(l => l?.Invoke(value));
}
```

Replace the `OnGoldenBallCollected` asset with a `GameEventSOString` asset and update `BallController` (Story 1.3) to raise it with the levelId.

### MonoBehaviour Lifecycle

```
Awake()              → Register ISaveService, Load() data, increment sessionCount
OnEnable()           → Subscribe to OnGoldenBallCollected, OnLevelCompleted SO events
OnDisable()          → Unsubscribe from SO events
OnApplicationPause() → Save() if pausing
OnApplicationFocus() → Save() if losing focus
```

`SaveManager` is on the Bootstrap GameObject — it persists across all scenes.

### Project Structure Notes

- `SaveData.cs` → `Assets/_Project/Scripts/Progression/`
- `ISaveService.cs` → `Assets/_Project/Scripts/Progression/`
- `SaveManager.cs` → `Assets/_Project/Scripts/Progression/`
- `GameEventSOString.cs` → `Assets/_Project/Scripts/Core/`
- `SaveManagerTests.cs` → `Assets/_Project/Tests/Progression/`
- `OnLevelCompleted.asset` → `Assets/_Project/ScriptableObjects/Events/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `SaveManager.Awake()` registers `ISaveService`
- `GameEventSO` (1.3) — `OnGoldenBallCollected` subscribed here; `OnLevelCompleted` created here
- Bootstrap GameObject (1.1) — `SaveManager` MonoBehaviour attached here

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md#ADR-003: State Persistence & Analytics`
- Architecture: `_bmad-output/planning-artifacts/architecture.md#Pre-mortem Hardening Checklist` (atomic saves, OnApplicationPause)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 2.1`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
