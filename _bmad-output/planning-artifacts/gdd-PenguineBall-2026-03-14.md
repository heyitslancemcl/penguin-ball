---
stepsCompleted: [complete]
inputDocuments:
  - _bmad-output/planning-artifacts/product-brief-PenguineBall-2026-03-14.md
  - _bmad-output/planning-artifacts/research/market-PenguineBall-research-2026-03-14.md
  - _bmad-output/brainstorming/brainstorming-session-2026-03-12-2100.md
workflowType: 'gdd'
status: complete
---

# Game Design Document: PenguineBall

**Author:** Lance
**Date:** 2026-03-14
**Platform:** iOS
**Genre:** Premium Tilt-Ball Platformer
**Price:** $3.99 one-time purchase — no ads, no IAP

---

## 1. Game Overview

### Concept
PenguineBall is a premium iOS tilt-ball platformer where a chubby, charm-filled penguin rolled into a ball navigates icy platforming levels across themed biomes. The game is built around a single design philosophy: intrinsic replayability. Every level is crafted to be mastered, not just completed.

### Core Design Pillars
1. **Tilt-first controls** — the phone is the controller. Zero virtual buttons by default.
2. **Three-layer replayability** — every level rewards completion, collection, and mastery independently.
3. **Premium and honest** — $3.99 one-time purchase, no ads, no IAP, no energy timers.
4. **Accessible by design** — alternative controls available from day one for players with motor disabilities.

### Inspirations
- **Super Monkey Ball** — tilt physics and ball control feel
- **Spyro the Dragon** — collectible gating, world progression, completionist satisfaction
- **Crash Bandicoot** — level mastery, replayability, premium quality feel
- **Geometry Dash** — skill-based difficulty, replay-driven design
- **Fall Guys** — chonky character aesthetic, simple visual clarity
- **Wipeout Zone Mode** — inspiration for future "Don't Fall" mode

---

## 2. Core Gameplay

### Primary Game Loop
1. Player tilts the phone to roll the penguin ball through a level
2. Navigate obstacles, avoid enemies, collect optional golden ball detour
3. Reach the end of the level
4. Style rank reveal screen punches in — grade, score, golden ball status
5. Player returns to level select — motivated by progression, missed collection, or rank improvement

### Controls

#### Default: Tilt Controls
- **Primary input:** Gyroscope — tilt the physical device to roll the ball
- Zero virtual buttons during gameplay
- Tilt sensitivity calibrated at game start

#### Alternative: Accessibility Controls (opt-in via Settings)
- **Virtual joystick** — on-screen directional control replacing tilt
- **Jump button** — on-screen button for jump mechanics
- Full gameplay parity with tilt mode — not a degraded experience
- Designed alongside tilt from launch, not retrofitted

### Camera
Portrait orientation. Camera follows the ball with a slightly elevated fixed perspective, giving the player visibility of upcoming platforms and hazards.

---

## 3. Player Character

### The Penguin Ball

**Appearance:**
- A chubby, chonky, round penguin wearing:
  - Classic black and white tuxedo
  - A colourful knitted scarf
  - A matching beanie hat
- When rolled into ball form, the tuxedo, scarf and beanie are visible and animated
- Expressive face: reacts to game events (fearful when falling, triumphant on level complete, dazed when hit)

**Personality:** Joyful, warm, and resilient. The penguin never feels defeated — even getting hit produces a cheerful sound.

**Sound Identity:**
- Hit/damage: joyful owl-like hoot (surprised but happy)
- Level complete/win: excited rapid hooting
- Idle/rolling: soft ambient rolling sounds
- Fire ability active: crackle and whoosh layered over rolling

### Fire Ability
**Trigger:** Player rolls over a fire power-up pickup placed in the level.

**Duration:** 20–30 seconds (timer displayed on HUD during active state).

**Visual:**
- The penguin ball becomes a glowing, flaming ball — fire wraps around the exterior
- Orange/red glow with animated flame particles
- On bounce: a short radial fire blast wave emits from the point of impact

**Functionality:**
- **Offensive:** Defeats Seal and Walrus enemies on contact
- **Defensive:** Melts/clears ice spike hazards on contact
- Fire ability use contributes positively to the Style Rank score

---

## 4. Enemies

### Seal
**Appearance:** Chubby, round seal with a cheeky grin. Same chonky art style as the penguin.
**Behaviour:** Slides along platforms toward the ball attempting to knock it off the edge.
**Sound:** Playful barking.
**Defeat:** Fire ability on contact. Can also be avoided through precise movement.
**Threat level:** Light — agile but telegraphed.

### Walrus
**Appearance:** Large, round, grumpy walrus. Chonky and imposing but still cartoonish.
**Behaviour:** Stationary or slow-moving. Throws ice spikes at the player at regular intervals.
**Sound:** Deep grumbly grunt when throwing. Heavier impact sound when defeated.
**Defeat:** Fire ability on contact. Can also be dodged through timing and navigation.
**Threat level:** Medium — ranged attack requires awareness and positioning.

---

## 5. Biome 1: The Penguin's Biome

### Setting
An icy arctic world with Frozen-palette aesthetics — cool blues, crisp whites, snow-dusted surfaces. Environment has Fall Guys visual simplicity with detailed ice and snow texture work. Snow particles, icy surface reflections, and ambient cold atmosphere.

### Level Structure
- **Tutorial level:** 1 dedicated opening level — teaches all core mechanics through show-don't-tell. Visual cues and contextual in-world arrows. No text walls. Player in full control within 15 seconds.
- **Main levels:** 9–12 platforming levels with progressively increasing difficulty
- **Boss level:** 1 final boss level, gated by golden ball collection count

**Total levels at launch:** 11–14 (including tutorial and boss)

### Obstacles

| Obstacle | Behaviour | Counter |
|---|---|---|
| Ice spikes | Stationary hazard — contact = death | Navigate around, or fire ability to melt |
| Ground holes | Fall through = death, core tension mechanic | Precision tilt navigation |
| Moving platforms (horizontal) | Slide left/right periodically | Timing + tilt precision |
| Moving platforms (rotating) | Spin 180° periodically | Timing + tilt precision |
| Walrus ice spike projectiles | Thrown at intervals from walrus position | Dodge timing or fire ability |

### Biome Progression
- Each level contains one golden ball, always placed off the main path via a deliberate detour
- Collecting golden balls across levels accumulates toward the boss gate threshold
- Players can progress forward and backtrack — no frustration lock, just motivated return visits

---

## 6. Progression Systems

### Three-Layer Replayability Architecture
Every level contains three independent completion layers — each serving a different player motivation:

#### Layer 1: Gold Stars (Completion)
- Awarded for finishing a level
- Immediate, satisfying milestone
- Serves the casual player and youngest audience

#### Layer 2: Golden Balls (Collection)
- One per level, always off the main path
- Optional per run — no penalty for skipping
- Aggregate count gates access to the final boss level (Spyro-style)
- Creates named, specific backtrack goals: "I know exactly which level I missed it in"
- Solves Day 30 retention without live-ops infrastructure

#### Layer 3: Style Rank (Mastery)
- Revealed only at end of level as a surprise — never telegraphed during play
- Grades: **E → D → C → B → A** (Capcom-inspired)
- Scored on four factors:
  1. **Speed** — time to complete the level
  2. **Fire ability use** — effective activation of the power-up
  3. **Enemy defeats** — seals and walruses eliminated
  4. **Hazards avoided** — clean navigation through obstacle sections
- The reveal surprise creates variable reward: pride at high rank, motivation at low rank — both drive return

### Golden Ball World Gate
- Minimum golden ball count required to access the final boss level
- Specific threshold TBD in level design phase (suggested: 70–80% of available golden balls)
- Players must backtrack deliberately to unlock — this is a feature, not a frustration

---

## 7. Style Rank Reveal Screen

The end-of-level overlay is a key moment of delight and motivation. Elements punch in sequentially with satisfying impact animations:

1. **STYLE** — large letter grade slams onto screen (E/D/C/B/A) with a rank-appropriate sound
2. **SCORE** — total score counter ticks up
3. **GOLDEN BALL** — collected ✅ or missed ❌
4. **Rank-appropriate fanfare** — high ranks (A/B) trigger a celebratory jingle; lower ranks (C/D/E) play a lighter "try again" sound that still feels encouraging, not punishing

**Screen feel:** Full overlay above the paused game world. Bold, punchy typography. Brief but impactful — players should want to tap through quickly on a bad rank to replay.

---

## 8. UI & HUD

### In-Game HUD (minimal)
| Element | Position | Detail |
|---|---|---|
| Golden ball slot | Top centre or top right | Empty circle slot — fills when collected in current run |
| Lives/attempts | Top left | Simple number counter, minimal styling |
| Fire ability timer | Bottom or near ball | Appears only when fire ability is active — countdown bar |

Design principle: screen stays as uncluttered as possible. Only essential information visible during play.

### Level Select Screen (MVP)
- Simple grid layout showing all levels in the current biome
- Each level tile shows: completion star (earned/empty), golden ball status (collected/missing), best style rank achieved
- Clean, readable — can be evolved into a more visually rich world map in a future update

### Menus
- Main menu: game title, play button, settings
- Settings: control scheme toggle (tilt / alternative controls), audio on/off, sensitivity calibration
- Pause menu: accessible during gameplay — resume, restart level, quit to level select

---

## 9. Audio Direction

### Music
**Tone:** Upbeat, bouncy, warm — classic platformer energy with a cold-weather personality.
**Biome 1 style:** Cheerful orchestral/chiptune hybrid with icy instruments (music box, bells, light strings). Energetic without being frenetic — suits 2–5 minute level sessions.
**Adaptive audio:** Music intensity can subtly increase as player approaches level end or activates fire ability.

### Sound Effects

| Event | Sound |
|---|---|
| Penguin hit | Joyful owl hoot (surprised, not pained) |
| Penguin level complete | Excited rapid hooting |
| Fire ability activated | Whoosh + crackle ignition |
| Fire ability active (loop) | Soft fire crackling |
| Fire blast wave (bounce) | Short radial whomp/explosion |
| Enemy defeated (Seal) | Playful bark cut short |
| Enemy defeated (Walrus) | Deep grunt + thud |
| Seal idle/moving | Occasional bark |
| Walrus throwing | Grumbly grunt + ice projectile whoosh |
| Golden ball collected | Bright chime/sparkle |
| Level complete | Short triumphant jingle |
| Style rank reveal — A/B | Celebratory fanfare |
| Style rank reveal — C/D/E | Light encouraging chime |
| Platform rolling | Soft textured rolling sound |
| Falling into hole | Short descending whistle |
| Ice spike contact | Sharp crack |

---

## 10. Art Direction

### Visual Style
**Reference:** Fall Guys character simplicity + Frozen colour world + ice/snow texture detail.

**Overall aesthetic:**
- Clean, bold, rounded shapes — no sharp edges on characters
- Characters are chubby, expressive, and immediately readable at small mobile screen sizes
- Environment uses simple geometry with high-quality texture passes for ice, snow, and frost
- Lighting: cool blue ambient with warm accent lighting (fire ability, golden ball glow, level complete effects)

### Colour Palette — Biome 1
| Element | Colour Direction |
|---|---|
| Sky/background | Deep cool blue to pale icy blue gradient |
| Platforms | White/light blue ice with snow dusting on top surfaces |
| Penguin | Black/white tuxedo, coloured scarf + beanie (warm contrast accent) |
| Fire ability active | Orange/amber flame against the cool ice palette — high visual contrast |
| Golden ball | Warm gold — stands out immediately against cool biome palette |
| Seal | Grey/white with bright eyes |
| Walrus | Brown/tan with ivory tusks |
| UI | Clean white with cool blue accents, bold typography |

### Character Design Principle
All characters — penguin, seal, walrus, and future biome characters — share the same chonky, round design language. They should feel like they belong to the same toy set.

---

## 11. Technical Requirements

### Platform
- **Primary:** iOS (iPhone), portrait orientation
- **Target OS versions:** iOS 17, iOS 18, iOS 26
- **No Android support at launch** (post-launch roadmap consideration)

### Game Engine
- **Unity** (MVP)
- Portrait-locked orientation
- Physics engine: Unity's built-in Rigidbody physics for ball movement, with gyroscope input layer for tilt controls

### Performance Targets
- **Frame rate:** 60 FPS target on supported devices
- **Load times:** Level loads under 3 seconds
- **Battery:** Optimised for mobile sessions — no excessive background processing

### Input
- Gyroscope (CoreMotion) as primary tilt input
- On-screen virtual joystick + button as accessibility alternative (Unity UI layer)
- Tilt sensitivity configurable in Settings

### iOS-Specific
- App Store submission compliant (no ads, no IAP, age rating appropriate for 4+/9+)
- Supports iPhone screen sizes from iPhone SE (4th gen) through iPhone Pro Max
- Portrait orientation locked

---

## 12. Monetisation

- **Price:** $3.99 one-time purchase
- **No ads**
- **No in-app purchases**
- **No energy timers or paywalls**
- **No subscription**

The premium pricing is a brand statement and a trust signal — particularly to parents purchasing for children and nostalgic adult players who have abandoned F2P mobile games.

---

## 13. Post-Launch Roadmap

| Phase | Feature | Description |
|---|---|---|
| Year 1 | Biome 2+ | New biome with distinct enemy set, obstacles, biome power-up, and visual identity |
| Year 1 | Ice coins | Temple Run-style collectible coins along the level path — adds score depth |
| Year 1 | Time trial mode | Beat the level under a time limit with maximum boosts — replay layer |
| Year 2 | "Don't Fall" mode | Wipeout Zone Mode-inspired endless escalating difficulty — distinct mode |
| Year 2 | Social features | Leaderboards, style rank sharing, Game Center integration |
| Year 2 | Platform expansion | iPad optimisation, Apple Arcade consideration |
