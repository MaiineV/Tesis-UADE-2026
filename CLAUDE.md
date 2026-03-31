# Roguelite Dice Dungeon — Project Rules

## Project Overview
**[Untitled] Dice Roguelite** — A turn-based roguelite dungeon crawler built in Unity 6.3 for PC. Dice are the core mechanic: the player builds a dice loadout, explores procedural isometric dungeons, and uses a Yahtzee/Generala-style combo system for combat. Casino aesthetic with deep build-crafting through face enchanting, special dice, and passives.

## Team
- 6-person development team — Game Development B.S. (UADE 2026)
- Members: Gabriel Guerrero, Franco Delocca, Franco N., Santiago Bocco + Maine, Sebiche

## Tech Stack
- **Engine**: Unity 6.3 (C#)
- **Render Pipeline**: 2D URP
- **Input**: New Input System
- **UI**: TextMeshPro + uGUI (Canvas-based)
- **Target platform**: PC (itch.io / Steam)
- **Perspective**: Fixed isometric

---

# Design Documents

## GDD
- `Assets/Documents/Game Design Document.md` — Full game design. Covers: concept, glossary, core loop, combat system (Pick & Roll), dice bag, combos, Generala phase, Craps Mode, exploration, enemies, dungeon structure, shop, economy, formulas.

## Spec Documents
All design specs are in `spec-docs/`. Read `spec-docs/README.md` for the index. Each spec contains C# code examples, data structures, and acceptance criteria.

**IMPORTANT**: Before implementing ANY system, read its corresponding spec doc AND the overview (`spec-docs/00-game-overview.md`). The specs contain exact class definitions, damage formulas, and balance values you MUST follow.

## Architecture Rules (ALL teammates must follow)

### Code Style
- **Prototype quality** — working code over perfect architecture. No SOLID patterns, no DI frameworks.
- **No namespaces** — keep everything flat for simplicity.
- **C# 9+** features are fine (pattern matching, records, etc.)

### Unity Patterns
- **Singletons** for all managers:
  ```csharp
  public static MyManager Instance;
  void Awake() { Instance = this; }
  ```
- **ScriptableObjects** for ALL tunable data (dice stats, character stats, enemy stats, balance values). Never hardcode gameplay numbers.
- **C# events** (`System.Action<T>`) for cross-system communication. No custom event bus.
- **TextMeshPro** for all text. Always `using TMPro;`
- **2D URP** — the project is already configured for 2D.

### Unity MCP
You have the Unity MCP server available. **Use it** to:
- Create and configure GameObjects in scenes
- Add components and set Inspector values
- Build UI (Canvas, panels, buttons, text elements)
- Create prefab instances
- Wire up serialized field references
- **Do NOT leave TODOs for manual Unity Editor work** — if something needs to be set up in the scene, use the MCP.

### Folder Structure
```
Assets/
├── Scripts/
│   ├── Managers/       → GameManager, GridManager, CombatManager, etc.
│   ├── Core/           → DiceInstance, DiceBag, CombinationDetector, GeneralaPhase, etc.
│   ├── Data/           → ScriptableObject class definitions (DiceData, CharacterData, etc.)
│   ├── Entities/       → PlayerEntity, PlayerState, EnemyEntity, EnemyState, EnemyAI
│   ├── Grid/           → TileData, TileVisual
│   ├── Combat/         → PickAndRoll, GeneralaPhase, CrapsMode, DamageResolver, OpportunityAttack
│   └── UI/             → All UI scripts (CombatUI, DiceBagUI, HealthBarUI, EnergyBarUI, etc.)
├── Data/               → ScriptableObject INSTANCES (.asset files)
│   ├── Dice/           → d6.asset, d8.asset, d10.asset, d12.asset
│   ├── Characters/     → Warrior.asset, Mage.asset, Rogue.asset
│   └── Enemies/        → Goblin.asset, Orc.asset, CardArcher.asset
├── Prefabs/            → Player.prefab, Enemy.prefab, Tile.prefab, etc.
└── Scenes/
    └── GameScene.unity
```

### Shared Types (defined in US-01, used by everyone)
These types are in `Assets/Scripts/Core/` and `Assets/Scripts/Data/`. Do NOT redefine them:
- `DiceData` (ScriptableObject)
- `CharacterData` (ScriptableObject)
- `EnemyData` (ScriptableObject)
- `DiceInstance`, `DiceBag`
- `RollResult`, `FullRollResult`, `CombinationResult`
- Enums: `CombinationType`, `TurnPhase`, `RoomState`, `FaceUpgradeType`, `EnemyBehavior`, `CombatActionType`

> **NOTE:** `SpeedDie` is ELIMINATED. Movement comes from combat dice (face value = tiles). Do NOT create or use `SpeedDie`.

### Git Rules
- Work on a feature branch: `us-{XX}-{short-name}`
- Commit often with messages: `US-{XX}: {what you did}`
- Before starting work, make sure you're on the correct branch
- Do NOT modify files owned by other tasks unless they are shared types from US-01
- If you need something from another task that isn't done yet, create a stub/interface and leave a `// TODO: Depends on US-XX` comment

### Placeholder Art
- Player: blue square
- Goblin: green triangle
- Orc: red pentagon
- Obstacles: dark gray squares
- Tiles: light gray squares with thin borders
- Use `SpriteRenderer` with simple sprites or Unity primitives. Generate sprites programmatically if needed.

### Color Palette
| Element        | Hex     |
|----------------|---------|
| Background     | #1a1a2e |
| Grid tile      | #16213e |
| Obstacle       | #0f0f23 |
| Player         | #4fc3f7 |
| Goblin         | #66bb6a |
| Orc            | #ef5350 |
| d6             | #42a5f5 |
| d8             | #66bb6a |
| d12            | #ab47bc |
| HP bar         | #e53935 |
| Energy bar     | #ffb300 |
| Shield         | #78909c |
| UI panel bg    | #1e1e3a |
| UI text        | #e0e0e0 |
| Accent/Gold    | #ffd54f |

### Balance Values (use ScriptableObjects, but these are the defaults)

**Player (Warrior)**
| Parameter              | Value       |
|------------------------|-------------|
| HP                     | 100         |
| Power Budget           | 8           |
| Starting dice          | 4×d6 + 2×d8 |
| Energy max             | 100         |
| Affinity combo         | Full House (+20% dmg) |

**Enemies**
| Parameter              | Goblin (Croupier) | Orc (Chip Golem) |
|------------------------|-------------------|------------------|
| HP                     | 40                | 60               |
| Attack dice            | 2×d6              | 2×d8             |
| Movement tiles         | 1–3               | 1–2              |
| Energy max             | 50                | 40               |
| Energy per round       | +15               | +12              |
| Enrage (at max energy) | 60% × 2 dmg, reset | 60% × 2 dmg, reset |

**Power Budget costs per die type**
| Die | Cost | Face range |
|-----|------|------------|
| d6  | 1    | 1–6        |
| d8  | 2    | 1–8        |
| d10 | 3    | 1–10       |
| d12 | 4    | 1–12       |

**Dungeon**
| Parameter  | Value |
|------------|-------|
| Grid size  | 8×8   |
| Obstacles  | 4–6   |
| Rooms/floor | 8–14 |

**Opportunity Attack (base mechanic)**
| Parameter | Value |
|-----------|-------|
| Damage when leaving adjacency | 1d6 (both sides) |

---

# Game Design Rules

## Game Identity

1. **Dice are EVERYTHING** — They're not RNG, they're the build, the inventory, the identity.
2. **Simple core, items break rules** — Base = roll to move + Generala combo for damage. Items add defense, ranged, healing, rule-breaking. Isaac philosophy.
3. **Pick & Roll** — Every turn: roll all dice, pick some for movement (face = tiles), use the rest for Generala. No AP system.
4. **Combo tension** — Sacrifice dice for movement or keep them for a better Generala hand? Every split is a decision.
5. **Bet the moment** — Craps Mode (when energy = 100): call your combo before rolling for bonus damage, or pay the penalty.
6. **Strategy over reflexes** — Pure turn-based. The difficulty is thinking.
7. **Roguelite progression** — Each run is unique: dice, items, passives, rooms.

## Official Terminology

| Term | Meaning | DO NOT use |
|---|---|---|
| **Dice Bag** | The player's loadout/inventory | Backpack, inventory |
| **Power Budget** | The loadout cost limit (charm notch system) | Slots, capacity |
| **Pick & Roll** | The turn system: pick movement dice, then Generala | AP system, action points |
| **Movement Dice** | Dice chosen from the roll for movement (face = tiles) | Speed dice, movement roll |
| **Generala Phase** | The lock/reroll/commit attack phase (Yahtzee rules) | Attack phase, combat roll |
| **Combo** | The Generala hand result (Pair, Straight, etc.) | Hand, combination |
| **Floor** | One procedural dungeon level | Level, stage |
| **Room** | A single space in the floor grid | Chamber, area |
| **Run** | Complete playthrough from start to victory/defeat | Match, session |
| **Craps Mode** | Core bet mechanic — call combo before rolling when energy = 100 | Special attack, ultimate |
| **Energy Bar** | Builds during combat, enables Craps Mode at 100/100 | Mana, charge |
| **Opportunity Attack** | 1d6 mutual damage when leaving adjacency | Escape penalty |
| **Face Enchanting** | Modifying a specific face of a die (item/shop service) | Upgrading, leveling |
| **Enraged** | Enemy state at max energy — 60% chance ×2 damage | Powered up, berserk |

## Combat Flow

```
Run (3-4 floors)
 └─ Floor (8-14 rooms, procedural)
     └─ Room (8×8 isometric grid, 4-6 obstacles)
         └─ Combat (triggered on adjacency — NO scene transition)
             ├─ [Optional] CRAPS BET — if energy = 100, call your combo before rolling
             ├─ Player Turn — Pick & Roll
             │   ├─ 1. Roll ALL dice from Dice Bag
             │   ├─ 2. Pick movement dice (face value = tiles, sum = total movement)
             │   ├─ 3. Move on grid (BFS pathfinding, single continuous path)
             │   ├─ 4. Generala Phase — lock/reroll up to 3 total rolls
             │   └─ 5. Commit — best combo → damage formula → apply to adjacent enemy
             │         (Craps Mode bonus/penalty applies here if active)
             └─ Enemy Turn (each enemy in sequence)
                 ├─ Roll movement → move toward player (BFS)
                 ├─ If adjacent → roll attack dice → result = direct damage (always hits)
                 └─ If energy full → Enraged: 60% chance to deal ×2 damage, energy resets
```

### Opportunity Attack
When either the player OR an enemy **leaves adjacency range**:
- Both roll 1d6 → result = direct damage to the one escaping
- This is a base mechanic — escaping always has a cost
- Smoke Bomb item negates the enemy's 1d6 (player still deals theirs)

### Generala Combo Damage Table
| Combo | Requirement | Formula | Example (d6s) |
|-------|-------------|---------|----------------|
| High Die | No combo | highest × 1 | [6] = 6 |
| Pair | 2 equal | sum of pair × 1.5 | [4,4] = 12 |
| Two Pair | 2+2 equal | sum of both × 1.2 | [3,3,5,5] = 19 |
| Three of a Kind | 3 equal | sum × 2 | [5,5,5] = 30 |
| Straight | 4+ consecutive | 30 + highest | [3,4,5,6] = 36 |
| Full House | 3+2 equal | 35 + sum all | [4,4,4,6,6] = 59 |
| Four of a Kind | 4 equal | sum × 3 | [5,5,5,5] = 60 |
| Generala | 5 equal | sum × 5 | [5,5,5,5,5] = 125 |
| Double Generala | 6 equal | sum × 8 | [5,5,5,5,5,5] = 240 |

### Craps Mode Energy Gain Table
| Action | Energy |
|--------|--------|
| Deal damage (any combo) | +10 |
| Three of a Kind or better | +15 |
| Full House | +20 |
| Four of a Kind | +25 |
| Generala / Double Generala | +50 |
| Take damage | +5 |
| Kill an enemy | +10 |

### Craps Bet Outcomes
| Bet | Hit | Miss |
|-----|-----|------|
| Pair | +25% dmg | −10% dmg |
| Three of a Kind | +50% dmg | −15% dmg |
| Straight | +50% dmg + heal 10 HP | −15% dmg |
| Full House | +75% dmg | −20% dmg |
| Four of a Kind | +100% dmg (×2) | −25% dmg + 5 HP lost |
| Generala | +200% dmg (×3) + heal 20 HP | −50% dmg + 10 HP lost |
"Or better" counts as a hit. After resolving (hit or miss), energy resets to 0.

---

# Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Classes/Structs | `PascalCase` | `DiceBag`, `ComboResolver` |
| ScriptableObjects | `PascalCase` + Data suffix | `DiceData`, `EnemyData` |
| Interfaces | `I` prefix | `IService`, `IComboScorer` |
| Public methods | `PascalCase` | `RollDice()`, `BuildCombo()` |
| Private methods | `camelCase` | `evaluateCombo()` |
| Private fields | `_camelCase` | `_currentDice` |
| Enums | `PascalCase` | `CombinationType.FullHouse` |
| Constants | `PascalCase` | `MaxDiceCount` |

---

# Behavioral Rules

## Do
- Read existing code before modifying
- Follow the ScriptableObject data pattern for any new game data
- Use C# events (`System.Action<T>`) for cross-system communication
- Mark suggestions with `[SUGGESTION]` when proposing new mechanics
- Mark unresolved design questions with `[TBD]`
- Validate new mechanics against the game identity pillars
- Consider dice physics feel in all combat-related code

## Don't
- Hardcode gameplay numbers — always use ScriptableObjects
- Add new enums without explicit int values and spacing
- Change the Pick & Roll flow without team discussion
- Add base mechanics that should be items: defense, ranged, healing, hit & run, cleave, split movement are ALL item territory
- Remove or rename existing ScriptableObject fields (breaks asset references)
- Use or create `SpeedDie` — it is eliminated. Movement comes from combat dice.
- Implement a "Hit Threshold" for the player — damage comes from the Generala combo formula, not a threshold check
- Implement AP (Action Points) — the system is Pick & Roll, not AP-based

---

# Performance Standards

## Hard Rules
- NO LINQ in hot paths
- NO per-frame allocations
- NO `Instantiate`/`Destroy` in gameplay — use Object Pooling
- Dice physics can use Rigidbody but must pool dice objects
- Structs over classes where possible for data
- Enum/int keys over strings
