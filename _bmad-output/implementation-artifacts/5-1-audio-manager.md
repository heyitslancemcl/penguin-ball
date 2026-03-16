# Story 5.1: Audio Manager

Status: ready-for-dev

## Story

As a player,
I want the game to have adaptive music, contextual sound effects, and character voice that respond to what's happening,
so that audio enhances the feel of every moment without becoming repetitive or overwhelming.

## Acceptance Criteria

1. **Given** the app launches **When** the Bootstrap scene initialises **Then** a persistent `AudioManager` exists; it registers as `IAudioService` via `ServiceLocator`; all audio channels are on the Bootstrap object (never destroyed on load)
2. **Given** any game system needs to play audio **When** it calls `ServiceLocator.Get<IAudioService>()` **Then** the registered `AudioManager` is returned; no direct `AudioSource` references exist outside `AudioManager`
3. **Given** multiple SFX are triggered simultaneously **When** more than 3 concurrent SFX are requested **Then** only 3 play at once; the lowest priority SFX is interrupted; no Unity audio warnings
4. **Given** the player enters a level **When** the level starts **Then** the normal gameplay music track begins; when fire ability activates, music crossfades to the fire track; when lives reach 1, music crossfades to the near-death track; when fire expires or lives recover, music crossfades back
5. **Given** gameplay events occur **When** the corresponding SO event fires **Then** the correct SFX plays via the centralised audio bus (no direct `AudioSource.Play()` outside `AudioManager`)
6. **Given** a character voice line is triggered **When** the voice event fires **Then** voice plays on a dedicated channel without cutting off music; voice and SFX are independent

## Tasks / Subtasks

- [ ] Task 1: Create `IAudioService` interface (AC: 2)
  - [ ] Create `Assets/_Project/Scripts/Audio/IAudioService.cs`:
    ```csharp
    public interface IAudioService
    {
        void PlaySFX(AudioClip clip, float priority = 0.5f, float volume = 1f);
        void PlayVoice(AudioClip clip, float volume = 1f);
        void PlayMusic(AudioClip track, float fadeTime = 0.5f);
        void StopMusic(float fadeTime = 0.5f);
        float MasterVolume { get; set; }
        float MusicVolume  { get; set; }
        float SFXVolume    { get; set; }
    }
    ```

- [ ] Task 2: Create `AudioManager` MonoBehaviour (AC: 1–6)
  - [ ] Create `Assets/_Project/Scripts/Audio/AudioManager.cs`:
    ```csharp
    public class AudioManager : MonoBehaviour, IAudioService
    {
        [Header("Volume (0–1)")]
        [SerializeField] private float _masterVolume = 1f;
        [SerializeField] private float _musicVolume  = 0.7f;
        [SerializeField] private float _sfxVolume    = 1f;

        // Music: two sources for A/B crossfade
        private AudioSource _musicSourceA;
        private AudioSource _musicSourceB;
        private Coroutine   _musicFadeCoroutine;

        // SFX: 3-slot priority pool
        private SFXSlot[] _sfxSlots;
        private const int SFXSlotCount = 3;

        // Voice: dedicated source
        private AudioSource _voiceSource;

        // IAudioService volume properties
        public float MasterVolume
        {
            get => _masterVolume;
            set { _masterVolume = Mathf.Clamp01(value); ApplyVolumes(); }
        }
        public float MusicVolume
        {
            get => _musicVolume;
            set { _musicVolume = Mathf.Clamp01(value); ApplyMusicVolume(); }
        }
        public float SFXVolume
        {
            get => _sfxVolume;
            set { _sfxVolume = Mathf.Clamp01(value); }
        }

        private void Awake()
        {
            ServiceLocator.Register<IAudioService>(this);
            InitAudioSources();
        }

        private void InitAudioSources()
        {
            // Music A/B
            _musicSourceA = gameObject.AddComponent<AudioSource>();
            _musicSourceB = gameObject.AddComponent<AudioSource>();
            foreach (var src in new[] { _musicSourceA, _musicSourceB })
            {
                src.loop        = true;
                src.playOnAwake = false;
                src.volume      = 0f;
            }

            // SFX slots
            _sfxSlots = new SFXSlot[SFXSlotCount];
            for (int i = 0; i < SFXSlotCount; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.loop        = false;
                src.playOnAwake = false;
                _sfxSlots[i] = new SFXSlot { Source = src };
            }

            // Voice
            _voiceSource            = gameObject.AddComponent<AudioSource>();
            _voiceSource.loop        = false;
            _voiceSource.playOnAwake = false;
        }

        // ─── IAudioService ────────────────────────────────────────────────────

        public void PlaySFX(AudioClip clip, float priority = 0.5f, float volume = 1f)
        {
            if (clip == null) return;

            // Find a free slot
            SFXSlot target = null;
            foreach (var slot in _sfxSlots)
            {
                if (!slot.Source.isPlaying)
                {
                    target = slot;
                    break;
                }
            }

            // No free slot — replace lowest priority if new clip is higher
            if (target == null)
            {
                SFXSlot lowest = _sfxSlots[0];
                foreach (var slot in _sfxSlots)
                    if (slot.Priority < lowest.Priority)
                        lowest = slot;

                if (priority <= lowest.Priority) return; // all slots are higher priority — drop
                target = lowest;
                target.Source.Stop();
            }

            target.Priority      = priority;
            target.Source.clip   = clip;
            target.Source.volume = volume * _sfxVolume * _masterVolume;
            target.Source.Play();
        }

        public void PlayVoice(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            _voiceSource.volume = volume * _masterVolume;
            _voiceSource.clip   = clip;
            _voiceSource.Play();
        }

        public void PlayMusic(AudioClip track, float fadeTime = 0.5f)
        {
            if (track == null) return;
            if (_musicFadeCoroutine != null) StopCoroutine(_musicFadeCoroutine);
            _musicFadeCoroutine = StartCoroutine(CrossfadeMusic(track, fadeTime));
        }

        public void StopMusic(float fadeTime = 0.5f)
        {
            if (_musicFadeCoroutine != null) StopCoroutine(_musicFadeCoroutine);
            _musicFadeCoroutine = StartCoroutine(FadeOutMusic(fadeTime));
        }

        // ─── Crossfade ────────────────────────────────────────────────────────

        private IEnumerator CrossfadeMusic(AudioClip newTrack, float fadeTime)
        {
            AudioSource outgoing = _musicSourceA.isPlaying ? _musicSourceA : _musicSourceB;
            AudioSource incoming = outgoing == _musicSourceA ? _musicSourceB : _musicSourceA;

            incoming.clip   = newTrack;
            incoming.volume = 0f;
            incoming.Play();

            float startVolume = outgoing.volume;
            float elapsed     = 0f;
            float targetVol   = _musicVolume * _masterVolume;

            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime; // use unscaled — music crossfades during pause
                float t = Mathf.Clamp01(elapsed / fadeTime);
                outgoing.volume = Mathf.Lerp(startVolume, 0f, t);
                incoming.volume = Mathf.Lerp(0f, targetVol, t);
                yield return null;
            }

            outgoing.Stop();
            outgoing.volume = targetVol;
        }

        private IEnumerator FadeOutMusic(float fadeTime)
        {
            AudioSource active = _musicSourceA.isPlaying ? _musicSourceA : _musicSourceB;
            float startVolume  = active.volume;
            float elapsed      = 0f;

            while (elapsed < fadeTime)
            {
                elapsed += Time.unscaledDeltaTime;
                active.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeTime);
                yield return null;
            }
            active.Stop();
        }

        private void ApplyVolumes()
        {
            ApplyMusicVolume();
            foreach (var slot in _sfxSlots)
                if (slot.Source.isPlaying)
                    slot.Source.volume = _sfxVolume * _masterVolume;
        }

        private void ApplyMusicVolume()
        {
            var activeMusic = _musicSourceA.isPlaying ? _musicSourceA : _musicSourceB;
            activeMusic.volume = _musicVolume * _masterVolume;
        }

        // ─── Inner class ──────────────────────────────────────────────────────

        private class SFXSlot
        {
            public AudioSource Source;
            public float       Priority;
        }
    }
    ```
  - [ ] Add `AudioManager` component to Bootstrap GameObject
  - [ ] `Awake()` registers `IAudioService` via `ServiceLocator` — must run before any gameplay scenes load

- [ ] Task 3: Create `AdaptiveMusicController` MonoBehaviour (AC: 4)
  - [ ] Create `Assets/_Project/Scripts/Audio/AdaptiveMusicController.cs` (MonoBehaviour on Bootstrap):
    ```csharp
    public class AdaptiveMusicController : MonoBehaviour
    {
        [Header("Music Tracks (assign in Inspector)")]
        [SerializeField] private AudioClip _normalTrack;
        [SerializeField] private AudioClip _fireTrack;
        [SerializeField] private AudioClip _nearDeathTrack;

        [Header("SO Events")]
        [SerializeField] private GameEventSO _onLevelStarted;       // raised in LevelManager.StartLevel stub
        [SerializeField] private GameEventSO _onFireAbilityActivated;
        [SerializeField] private GameEventSO _onFireAbilityDeactivated;
        [SerializeField] private GameEventSO _onLivesChanged;
        [SerializeField] private GameEventSO _onLevelCompleted;

        private bool _fireActive;
        private bool _nearDeath;

        private void OnEnable()
        {
            _onLevelStarted.AddListener(OnLevelStarted);
            _onFireAbilityActivated.AddListener(OnFireActivated);
            _onFireAbilityDeactivated.AddListener(OnFireDeactivated);
            _onLivesChanged.AddListener(OnLivesChanged);
            _onLevelCompleted.AddListener(OnLevelCompleted);
        }

        private void OnDisable()
        {
            _onLevelStarted.RemoveListener(OnLevelStarted);
            _onFireAbilityActivated.RemoveListener(OnFireActivated);
            _onFireAbilityDeactivated.RemoveListener(OnFireDeactivated);
            _onLivesChanged.RemoveListener(OnLivesChanged);
            _onLevelCompleted.RemoveListener(OnLevelCompleted);
        }

        private void OnLevelStarted()
        {
            _fireActive = false;
            _nearDeath  = false;
            PlayCurrentTrack(fadeTime: 1f);
        }

        private void OnFireActivated()
        {
            _fireActive = true;
            ServiceLocator.Get<IAudioService>().PlayMusic(_fireTrack, fadeTime: 0.6f);
        }

        private void OnFireDeactivated()
        {
            _fireActive = false;
            PlayCurrentTrack(fadeTime: 0.8f);
        }

        private void OnLivesChanged()
        {
            int lives = ServiceLocator.Get<ILevelManager>().CurrentLives;
            _nearDeath = lives <= 1;
            if (!_fireActive) PlayCurrentTrack(fadeTime: 0.5f);
            // Don't interrupt fire track for near-death — fire takes priority
        }

        private void OnLevelCompleted()
        {
            ServiceLocator.Get<IAudioService>().StopMusic(fadeTime: 1f);
        }

        private void PlayCurrentTrack(float fadeTime)
        {
            var audio  = ServiceLocator.Get<IAudioService>();
            AudioClip track = _nearDeath && _nearDeathTrack != null ? _nearDeathTrack : _normalTrack;
            if (track != null)
                audio.PlayMusic(track, fadeTime);
        }
    }
    ```
  - [ ] Add `AdaptiveMusicController` to Bootstrap GameObject
  - [ ] Wire all `[SerializeField]` SO event and music clip references in Inspector
  - [ ] Assign placeholder `AudioClip` assets (Unity primitive or silent clip) — actual music assets are out of scope for this story

- [ ] Task 4: Create `OnLevelStarted` SO event and wire to `LevelManager` (AC: 4)
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnLevelStarted.asset` (GameEventSO)
  - [ ] Open `Assets/_Project/Scripts/Core/LevelManager.cs` — in `StartLevel()`, add:
    ```csharp
    [SerializeField] private GameEventSO _onLevelStarted;
    // ...in StartLevel():
    _onLevelStarted.Raise();
    ```
  - [ ] Wire `_onLevelStarted` → `OnLevelStarted.asset` in Bootstrap's `LevelManager` Inspector

- [ ] Task 5: Create `SFXEventBinder` MonoBehaviour (AC: 5)
  - [ ] Create `Assets/_Project/Scripts/Audio/SFXEventBinder.cs` (MonoBehaviour on Bootstrap):
    ```csharp
    [System.Serializable]
    public struct SFXBinding
    {
        public GameEventSO trigger;
        public AudioClip   clip;
        [Range(0f, 1f)] public float priority;
        [Range(0f, 1f)] public float volume;
    }

    public class SFXEventBinder : MonoBehaviour
    {
        [SerializeField] private SFXBinding[] _bindings;

        private readonly List<System.Action> _unsubscribeActions = new List<System.Action>();

        private void OnEnable()
        {
            foreach (var binding in _bindings)
            {
                if (binding.trigger == null || binding.clip == null) continue;

                var b = binding; // capture for closure
                System.Action handler = () =>
                    ServiceLocator.Get<IAudioService>().PlaySFX(b.clip, b.priority, b.volume);

                binding.trigger.AddListener(handler);
                _unsubscribeActions.Add(() => b.trigger.RemoveListener(handler));
            }
        }

        private void OnDisable()
        {
            foreach (var unsub in _unsubscribeActions) unsub();
            _unsubscribeActions.Clear();
        }
    }
    ```
  - [ ] Add `SFXEventBinder` to Bootstrap GameObject
  - [ ] Configure `_bindings` array in Inspector with placeholder AudioClips. Suggested bindings:

    | Trigger Event | Priority | Notes |
    |---|---|---|
    | `OnBallBounced` | 0.3 | Low — happens frequently |
    | `OnGoldenBallCollected` | 0.9 | High — important moment |
    | `OnEnemyDefeated` | 0.8 | High |
    | `OnFireAbilityActivated` | 0.9 | High |
    | `OnFireAbilityDeactivated` | 0.6 | Medium |
    | `OnBallDead` (from `OnBallStateChanged`) | 0.7 | Medium |

  - [ ] Assign placeholder silent `AudioClip` assets for now — real SFX provided by audio designer later

- [ ] Task 6: Add `OnBallBounced` event to `BallController` (AC: 5)
  - [ ] Create `Assets/_Project/ScriptableObjects/Events/OnBallBounced.asset` (GameEventSO)
  - [ ] Open `Assets/_Project/Scripts/Core/BallController.cs`
  - [ ] Add:
    ```csharp
    [SerializeField] private GameEventSO _onBallBounced;

    private float _lastBounceTime;
    private const float BounceCooldown = 0.12f; // min seconds between bounce events

    private void OnCollisionEnter(Collision col)
    {
        // Bounce event with cooldown — prevents audio spam on rapid multi-contact
        if (Time.time - _lastBounceTime >= BounceCooldown)
        {
            _lastBounceTime = Time.time;
            _onBallBounced?.Raise();
        }
        // Existing collision logic (FireBlastWave is a separate component — no duplication)
    }
    ```
  - [ ] Wire `_onBallBounced` → `OnBallBounced.asset` in Ball prefab Inspector

- [ ] Task 7: Create `VoiceEventBinder` for character voice (AC: 6)
  - [ ] Create `Assets/_Project/Scripts/Audio/VoiceEventBinder.cs` (MonoBehaviour on Bootstrap):
    ```csharp
    [System.Serializable]
    public struct VoiceBinding
    {
        public GameEventSO trigger;
        public AudioClip[] clips; // randomly selected for variety
        [Range(0f, 1f)] public float volume;
    }

    public class VoiceEventBinder : MonoBehaviour
    {
        [SerializeField] private VoiceBinding[] _bindings;
        private readonly List<System.Action> _unsubscribeActions = new List<System.Action>();

        private void OnEnable()
        {
            foreach (var binding in _bindings)
            {
                if (binding.trigger == null || binding.clips == null || binding.clips.Length == 0) continue;

                var b = binding;
                System.Action handler = () =>
                {
                    var clip = b.clips[Random.Range(0, b.clips.Length)];
                    ServiceLocator.Get<IAudioService>().PlayVoice(clip, b.volume);
                };

                binding.trigger.AddListener(handler);
                _unsubscribeActions.Add(() => b.trigger.RemoveListener(handler));
            }
        }

        private void OnDisable()
        {
            foreach (var unsub in _unsubscribeActions) unsub();
            _unsubscribeActions.Clear();
        }
    }
    ```
  - [ ] Add `VoiceEventBinder` to Bootstrap GameObject
  - [ ] Configure voice bindings for key moments (placeholder clips for now):

    | Trigger | Clip variety |
    |---|---|
    | `OnGoldenBallCollected` | 1–2 exclamation clips |
    | `OnEnemyDefeated` | 1–2 victory clips |
    | `OnFireAbilityActivated` | 1 fire activation line |

- [ ] Task 8: Play Mode test checklist (AC: 1–6)
  - [ ] Bootstrap loads → `AudioManager` registered, no errors
  - [ ] Level starts → `OnLevelStarted` fires → normal music plays
  - [ ] Fire ability activated → music crossfades to fire track (even with placeholder clips)
  - [ ] Fire expires → music crossfades back to normal track
  - [ ] Lives drop to 1 → music crossfades to near-death track (if fire not active)
  - [ ] Rapid ball bounces → only 3 simultaneous SFX play; 4th is dropped; no Unity audio warnings
  - [ ] Golden ball collected → SFX plays immediately on dedicated slot
  - [ ] Voice line plays → music continues uninterrupted
  - [ ] Level complete → music fades out

## Dev Notes

### `Time.unscaledDeltaTime` for Music Crossfades

```csharp
elapsed += Time.unscaledDeltaTime; // ← in CrossfadeMusic and FadeOutMusic
```

Music crossfades use `Time.unscaledDeltaTime` rather than `Time.deltaTime`. This ensures music transitions complete even when `Time.timeScale = 0` (pause menu). If scaled time is used, crossfades freeze during pause — the music appears to hang.

SFX and voice play via `AudioSource.Play()` which is unaffected by `timeScale`. Only the duration calculation for crossfades needs unscaled time.

### 3-Slot SFX Priority System

```
Priority guide (0.0 – 1.0):
0.0 – 0.3 : Ambient / frequent (bouncing, footsteps) — replaceable
0.4 – 0.6 : Standard gameplay (obstacles, general events)
0.7 – 0.8 : Important moments (death, enemy defeat)
0.9 – 1.0 : Critical / irreplaceable (golden ball, fire activation)
```

When all 3 slots are occupied and a new SFX is requested:
- New priority > lowest active priority → replace it
- New priority ≤ lowest active priority → silently drop the new SFX

The dropped SFX produces no error — this is intentional. A rapid-fire bounce spam filling all 3 slots won't block a golden ball collection sound (priority 0.9) from playing.

### `SFXEventBinder` — Closure Capture Pattern

```csharp
var b = binding; // capture local copy — CRITICAL
System.Action handler = () => ServiceLocator.Get<IAudioService>().PlaySFX(b.clip, b.priority, b.volume);
```

Without `var b = binding`, the lambda captures the loop variable by reference — all handlers would use the last binding's values. `var b = binding` creates a fresh value copy per iteration. This is a classic C# closure-in-loop gotcha.

### `AdaptiveMusicController` — Fire Takes Priority Over Near-Death

```csharp
private void OnLivesChanged()
{
    if (!_fireActive) PlayCurrentTrack(fadeTime: 0.5f);
    // Don't change music while fire track is playing
}
```

If the player has 1 life AND fire active, the fire track plays — the near-death tension is overridden by the excitement of fire. When fire expires, `OnFireDeactivated` calls `PlayCurrentTrack()` which evaluates `_nearDeath` and switches to the near-death track at that point.

### `OnBallBounced` — 0.12s Cooldown

Without the cooldown, the ball generates 3–5 collision events per second during normal rolling on uneven geometry. At 5 events/second, 3 SFX slots fill with bounce sounds every 0.6s — no room for anything else.

The 0.12s cooldown (~8 events per second max) prevents slot exhaustion while still making the ball feel responsive. Adjust based on playtesting feel.

### Placeholder AudioClips — Mandatory Before Testing

Unity will throw warnings if `AudioSource.Play()` is called with `clip = null`. For testing, assign any valid `AudioClip`:
- Use Unity's built-in test clips from the URP template
- Or create a 0.1s silent clip via `AudioClip.Create("Silent", 4410, 1, 44100, false)`
- Or import any royalty-free SFX temporarily

All bindings in `SFXEventBinder` and `VoiceEventBinder` that have `clip == null` are silently skipped — no crash, but no audio. The null guard `if (binding.clip == null) continue;` handles this.

### `AudioSource` Creation in `Awake` — No `[SerializeField]` Sources

`AudioManager` creates its `AudioSource` components programmatically in `Awake()` via `AddComponent`. This is intentional — it prevents accidental misconfiguration in the Inspector and ensures all audio sources are properly initialised with the correct settings before any game code runs.

### Project Structure Notes

- `IAudioService.cs` → `Assets/_Project/Scripts/Audio/`
- `AudioManager.cs` → `Assets/_Project/Scripts/Audio/`
- `AdaptiveMusicController.cs` → `Assets/_Project/Scripts/Audio/`
- `SFXEventBinder.cs` → `Assets/_Project/Scripts/Audio/`
- `VoiceEventBinder.cs` → `Assets/_Project/Scripts/Audio/`
- `OnLevelStarted.asset` → `Assets/_Project/ScriptableObjects/Events/`
- `OnBallBounced.asset` → `Assets/_Project/ScriptableObjects/Events/`

### Previous Story Dependencies

- `ServiceLocator` (1.1) — `AudioManager.Awake()` registers `IAudioService`
- `GameEventSO` (1.3) — all SO event subscriptions
- `BallController` (1.3) — `OnBallBounced` event added here
- `IBallStateManager` (1.4) — `OnBallDead` available for SFX binding
- `ILevelManager` (2.2/2.5) — `AdaptiveMusicController` reads `CurrentLives` in `OnLivesChanged`
- `OnBallDead.asset` (1.4), `OnGoldenBallCollected.asset` (3.1) — bound in `SFXEventBinder`
- `OnLivesChanged.asset` (2.5) — music track selection
- `OnFireAbilityActivated.asset`, `OnFireAbilityDeactivated.asset` (3.2) — music + SFX bindings
- `OnLevelCompleted.asset` (2.1) — music fade-out on level complete
- `OnEnemyDefeated.asset` (3.2) — SFX + voice binding

### References

- Architecture: `_bmad-output/planning-artifacts/architecture.md` (FR16: adaptive music, SFX, character voice)
- Epics: `_bmad-output/planning-artifacts/epics.md#Story 5.1`

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

### File List
