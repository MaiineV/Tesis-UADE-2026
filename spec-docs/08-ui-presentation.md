# 08 — UI & Presentation Layer

## Overview
This spec defines all UI screens, elements, and interactions for the prototype. All art is placeholder (geometric shapes, solid colors). The UI must be functional and clear enough to playtest the core loop.

## Dependencies
- References: All other specs
- Referenced by: None (UI is the top-level presentation layer)

---

## 1. Screen Flow

```
MAIN MENU (placeholder)
    │
    [Start Run]
    │
    ▼
GAME SCENE
    │
    ├── MOVEMENT MODE (grid visible, dice bag visible)
    │   Player rolls speed die, moves on grid
    │   Enemies move on grid
    │
    ├── COMBAT MODE (grid fades/shrinks, combat UI takes focus)
    │   Attack phase → Defense phase → Enemy attack
    │
    ├── REWARD OVERLAY (after enemy 1 dies)
    │   Pick 1 of 2 upgrades
    │
    ├── CRAPS OVERLAY (when energy full, before attack)
    │   Place bet on next combo
    │
    ├── GAME OVER OVERLAY
    │   "You died" + [Restart]
    │
    └── VICTORY OVERLAY
        "You won" + stats + [Restart]
```

---

## 2. HUD (Always Visible in Game Scene)

```
┌─────────────────────────────────────────────────────────────┐
│ TOP BAR                                                     │
│ ┌──────────────────┐  ┌──────────────────┐                  │
│ │ HP: ████████░░    │  │ ENERGY: ██████░░  │                │
│ │     80/100        │  │         60/100    │                 │
│ └──────────────────┘  └──────────────────┘                  │
│                                                             │
│                    ┌──────────────┐                          │
│                    │  GRID (8x8)  │                          │
│                    │              │                          │
│                    │  [P]    [E]  │                          │
│                    │              │                          │
│                    └──────────────┘                          │
│                                                             │
│ BOTTOM BAR                                                  │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │ DICE BAG: [🎲][🎲][🎲][🎲][🎲][🎲]  Power: 8/8       │ │
│ └─────────────────────────────────────────────────────────┘ │
│                                                             │
│ ┌──────────────────────────────────────────┐                │
│ │ COMBAT LOG                               │                │
│ │ > You moved 4 tiles                      │                │
│ │ > Goblin moves 2 tiles toward you        │                │
│ │ > Combat started!                        │                │
│ └──────────────────────────────────────────┘                │
└─────────────────────────────────────────────────────────────┘
```

### HUD Elements
| Element    | Type         | Behavior                                        |
|------------|--------------|-------------------------------------------------|
| HP Bar     | Fill bar     | Red fill, flashes when taking damage            |
| Energy Bar | Fill bar     | Blue→yellow→gold, pulses when full              |
| Dice Bag   | Icon strip   | Shows each die as a colored shape with type label|
| Combat Log | Scrolling text| Shows last 4-5 messages, auto-scrolls           |
| Phase Label| Text         | "MOVEMENT PHASE" / "YOUR ATTACK" / "DEFENSE" etc|

---

## 3. Movement Mode UI

### 3.1 Speed Roll Display
When player's movement turn begins:
```
┌───────────────────┐
│  🎲 Speed Roll: 4 │
│  Move up to 4 tiles│
└───────────────────┘
```
- Brief popup, then reachable tiles highlight on grid
- Disappears after 1.5 seconds, highlights remain

### 3.2 Grid Tile Highlighting
| State              | Color         |
|--------------------|---------------|
| Default walkable   | Light gray    |
| Obstacle           | Dark gray     |
| Player occupied    | Blue          |
| Enemy occupied     | Red           |
| Reachable (move)   | Green (alpha) |
| Hover (selectable) | Yellow        |
| Path preview       | Green dots    |

### 3.3 Enemy Turn Indicator
When enemies move:
```
GOBLIN's turn — rolled 2
[animated movement along path, 0.3s per tile]
```
- Show which enemy is moving
- Animate movement step by step (not instant)
- Brief pause between enemies (0.5s)

---

## 4. Combat Mode UI

### 4.1 Full Combat Layout
```
┌──────────────────────────────────────────────────────────────┐
│  PLAYER HP: ████████░░ 80/100         SHIELD: ██ 12 pts     │
│  ENERGY:    ██████░░░░ 60/100                                │
│                                                              │
│  ┌────────────────────────────────────────┐                  │
│  │         YOUR DICE (Roll 1/3)          │                   │
│  │                                        │                  │
│  │   ┌───┐  ┌───┐  ┌───┐  ┌───┐  ┌───┐  ┌───┐             │
│  │   │ 4 │  │ 6 │  │ 2 │  │ 6 │  │ 3 │  │ 8 │             │
│  │   │d6 │  │d6★│  │d8 │  │d6★│  │d6 │  │d8 │             │
│  │   └───┘  └───┘  └───┘  └───┘  └───┘  └───┘             │
│  │          (★ = locked — click to toggle)                   │
│  │                                        │                  │
│  │  BEST COMBO: Pair of 6s ──── 18 dmg   │                  │
│  │                                        │                  │
│  │  [ 🎲 REROLL ]     [ ⚔️ COMMIT ATTACK ] │                │
│  └────────────────────────────────────────┘                  │
│                                                              │
│  ┌────────────────────────┐                                  │
│  │  GOBLIN                │                                  │
│  │  HP: ██████░░ 25/40    │                                  │
│  │  Energy: ████░░ 30/50  │                                  │
│  └────────────────────────┘                                  │
│                                                              │
│  COMBAT LOG:                                                 │
│  > Roll 1: [4] [6] [2] [6] [3] [8]                         │
│  > Best combo: Pair of 6s (18 damage)                       │
└──────────────────────────────────────────────────────────────┘
```

### 4.2 Dice Interaction
- **Click a die** → toggle lock/unlock
- **Locked dice**: border glow (gold), marked with ★
- **Unlocked dice**: normal appearance
- Die shows: face value (large) + die type label (small, below)
- Die color matches die type (e.g., d6=blue, d8=green, d12=purple)

### 4.3 Combo Display
- Show detected combo name + damage in real-time as player locks/unlocks dice
- Update instantly on any lock change
- Highlight the dice that form the combo (matching border color)
- If no combo: "High Die: [value]"

### 4.4 Attack Buttons
| Button          | Enabled When              | Action                    |
|-----------------|---------------------------|---------------------------|
| REROLL          | Rolls remaining (< 3)     | Rerolls unlocked dice     |
| COMMIT ATTACK   | At least 1 roll done      | Finalizes combo, deals dmg|

After committing:
- Flash damage number on enemy
- Enemy HP bar decreases with animation
- Brief pause (0.5s)
- Transition to defense phase

### 4.5 Defense Phase UI
```
┌────────────────────────────────────────┐
│       DEFENSE PHASE (2 rolls left)     │
│                                        │
│   ┌───┐  ┌───┐  ┌───┐  ┌───┐  ┌───┐  ┌───┐
│   │ 3 │  │ 5 │  │ 5 │  │ 1 │  │ 7 │  │ 2 │
│   └───┘  └───┘  └───┘  └───┘  └───┘  └───┘
│                                        │
│   Shield combo: Pair of 5s → 7 shield  │
│                                        │
│   [🛡️ ROLL DEFENSE]  (1 of 2)         │
└────────────────────────────────────────┘
```
- No dice locking — each defense roll is a single throw
- Show best shield value found so far
- Auto-proceeds after all defense rolls used

### 4.6 Enemy Attack Display
```
┌────────────────────────────────────────┐
│         ENEMY ATTACKS!                 │
│                                        │
│   Goblin rolls: [4] [6] = 10 damage   │
│   Your shield absorbs: 7              │
│   You take: 3 damage                  │
│                                        │
│         [Continue]                     │
└────────────────────────────────────────┘
```
- Show enemy dice roll results
- Show shield absorption
- Show net damage
- Player HP bar decreases
- Click "Continue" or auto-advance after 2 seconds

---

## 5. Craps Mode Overlay

### 5.1 Bet Selection
```
┌──────────────────────────────────────────┐
│          🎰 CRAPS MODE ACTIVATED!        │
│                                          │
│   Your energy is maxed — place your bet! │
│   Predict what combo you'll roll next.   │
│                                          │
│   ┌──────────────────────────────────┐   │
│   │ Pair            +25%  / -10%    │   │
│   │ Three of a Kind +50%  / -15%    │   │
│   │ Straight        +50%  / -15%    │   │
│   │ Full House      +75%  / -20%    │   │
│   │ Four of a Kind  +100% / -25%    │   │
│   │ GENERALA        +200% / -50%    │   │
│   └──────────────────────────────────┘   │
│                                          │
│   Higher risk = bigger reward!           │
└──────────────────────────────────────────┘
```
- Each option is a button
- Hover shows tooltip with full bonus/penalty details
- Click to select → normal attack phase begins with bet active

### 5.2 Craps Result
After attack commits:
```
SUCCESS:                              FAILURE:
┌─────────────────────────┐          ┌─────────────────────────┐
│  🎰 BET WON! ✅         │          │  🎰 BET LOST! ❌        │
│                         │          │                         │
│  You bet: Full House    │          │  You bet: Full House    │
│  You got: Full House!   │          │  You got: Pair          │
│                         │          │                         │
│  Damage: +75% bonus!    │          │  Damage: -20% penalty   │
│  42 → 73 damage         │          │  42 → 33 damage         │
└─────────────────────────┘          └─────────────────────────┘
```
- Success: green background, celebratory animation
- Failure: red background, shake animation
- Auto-dismiss after 2 seconds

---

## 6. Reward Overlay

```
┌──────────────────────────────────────────────┐
│          🎁 ENEMY DEFEATED!                   │
│             Choose a reward:                  │
│                                               │
│  ┌───────────────────────┐ ┌────────────────────────┐
│  │    OPTION A           │ │    OPTION B             │
│  │                       │ │                         │
│  │  d6: face [2] → [5]  │ │  d8: REMOVE face [1]   │
│  │  (+3 to this face)    │ │  (8→7 faces, better    │
│  │                       │ │   odds on remaining)   │
│  │                       │ │                         │
│  │  [Choose A]           │ │  [Choose B]             │
│  └───────────────────────┘ └────────────────────────┘
│                                               │
└──────────────────────────────────────────────┘
```
- Shows 2 cards side by side
- Each card shows: which die, which face, what changes, and a plain-language explanation
- Click to choose → brief animation of upgrade applying → overlay closes

---

## 7. Game Over / Victory

### Game Over
```
┌──────────────────────────────┐
│       💀 GAME OVER           │
│                              │
│   You were defeated by Orc   │
│   Rounds fought: 7           │
│   Damage dealt: 156          │
│   Best combo: Poker (72 dmg) │
│                              │
│       [🔄 Try Again]        │
└──────────────────────────────┘
```

### Victory
```
┌──────────────────────────────┐
│       🏆 VICTORY!            │
│                              │
│   Room cleared!              │
│   Rounds fought: 5           │
│   Damage dealt: 234          │
│   Best combo: Full House     │
│   Craps bets won: 1/1       │
│                              │
│       [🔄 Play Again]       │
└──────────────────────────────┘
```

---

## 8. Visual Style (Prototype Placeholder)

### Color Palette
| Element       | Color (Hex)  |
|---------------|-------------|
| Background    | #1a1a2e     |
| Grid tile     | #16213e     |
| Obstacle      | #0f0f23     |
| Player        | #4fc3f7     |
| Enemy (Goblin)| #66bb6a     |
| Enemy (Orc)   | #ef5350     |
| d6 die        | #42a5f5     |
| d8 die        | #66bb6a     |
| d12 die       | #ab47bc     |
| HP bar fill   | #e53935     |
| Energy bar    | #ffb300     |
| Shield bar    | #78909c     |
| UI panel bg   | #1e1e3a     |
| UI text       | #e0e0e0     |
| Accent/Gold   | #ffd54f     |

### Fonts
- Use Unity's default TextMeshPro font
- Combat numbers: Bold, large (24-32pt)
- UI labels: Regular, medium (14-18pt)
- Combat log: Regular, small (12pt)

### Shapes (Placeholder Art)
| Entity   | Shape                           |
|----------|---------------------------------|
| Player   | Blue square with white border   |
| Goblin   | Green triangle                  |
| Orc      | Red pentagon                    |
| Obstacle | Dark square (no border)         |
| Die (d6) | Blue rounded rectangle          |
| Die (d8) | Green octagon                   |
| Die (d12)| Purple dodecagon (or circle)    |
| Tile     | Square with thin border         |

---

## 9. Animation Guidelines (Minimal for Prototype)

| Action           | Animation                                      | Duration |
|------------------|------------------------------------------------|----------|
| Die roll         | Dice icons shake briefly then show result      | 0.3s     |
| Lock/unlock die  | Border appears/disappears + subtle scale pulse | 0.15s    |
| Damage dealt     | Floating number rises and fades                | 0.8s     |
| HP bar change    | Smooth fill decrease/increase                  | 0.4s     |
| Entity move      | Slide between tiles                            | 0.2s/tile|
| Phase transition | Brief text flash ("YOUR TURN" / "ENEMY TURN")  | 1.0s     |
| Craps success    | Green screen flash                             | 0.5s     |
| Craps failure    | Red screen shake                               | 0.5s     |
| Enemy death      | Fade out + shrink                              | 0.5s     |
