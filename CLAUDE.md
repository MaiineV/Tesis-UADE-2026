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
- `Assets/8.Documents/Game Design Document.md` — Full game design. Covers: concept, glossary, core loop, combat system, dice bag, combos, exploration, enemies, dungeon structure, shop, economy, formulas.

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
│   ├── Core/           → DiceInstance, DiceBag, SpeedDie, CombinationDetector, etc.
│   ├── Data/           → ScriptableObject class definitions (DiceData, CharacterData, etc.)
│   ├── Entities/       → PlayerEntity, PlayerState, EnemyEntity, EnemyState, EnemyAI
│   ├── Grid/           → TileData, TileVisual
│   ├── Combat/         → AttackPhase, DefensePhase, CrapsMode, DamageResolver
│   └── UI/             → All UI scripts (CombatUI, DiceBagUI, HealthBarUI, etc.)
├── Data/               → ScriptableObject INSTANCES (.asset files)
│   ├── Dice/           → d6.asset, d8.asset, d12.asset
│   ├── Characters/     → Warrior.asset
│   └── Enemies/        → Goblin.asset, Orc.asset
├── Prefabs/            → Player.prefab, Enemy.prefab, Tile.prefab, etc.
└── Scenes/
    └── GameScene.unity
```

### Shared Types (defined in US-01, used by everyone)
These types are in `Assets/Scripts/Core/` and `Assets/Scripts/Data/`. Do NOT redefine them:
- `DiceData` (ScriptableObject)
- `CharacterData` (ScriptableObject)
- `EnemyData` (ScriptableObject)
- `DiceInstance`, `DiceBag`, `SpeedDie`
- `RollResult`, `FullRollResult`, `CombinationResult`
- Enums: `CombinationType`, `TurnPhase`, `RoomState`, `FaceUpgradeType`, `EnemyBehavior`, `CombatActionType`

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
| Parameter            | Value |
|----------------------|-------|
| Player HP            | 100   |
| Player power budget  | 8     |
| Player speed die     | 2–5   |
| Starting dice        | 4×d6 + 2×d8 |
| Goblin HP            | 40    |
| Goblin attack        | 2×d6  |
| Goblin speed         | 1–3   |
| Orc HP               | 60    |
| Orc attack           | 2×d8  |
| Orc speed            | 1–2   |
| Grid size            | 8×8   |
| Obstacles            | 4–6   |
| Energy max (player)  | 100   |
| Energy max (goblin)  | 50    |
| Energy max (orc)     | 40    |

---

# Game Design Rules

## Game Identity

1. **Dice are EVERYTHING** — They're not RNG, they're the build, the inventory, the identity.
2. **Simple core, items break rules** — Base = roll to move, roll to hit. Items add combos, defense, ranged, healing. Isaac philosophy.
3. **2 AP per turn** — Move + Attack. Order matters. Unused AP are lost.
4. **Threshold tension** — Every attack is a bet: hit the threshold or deal zero. Crits reward risk.
5. **Strategy over reflexes** — Pure turn-based. The difficulty is thinking.
6. **Roguelite progression** — Each run is unique: dice, items, passives, rooms.

## Official Terminology

| Term | Meaning | DO NOT use |
|---|---|---|
| **Dice Bag** | The player's loadout/inventory | Backpack, inventory |
| **Hit Threshold** | Minimum die result to deal damage | Accuracy, difficulty |
| **AP (Action Points)** | 2 per turn: move + attack | Turn points, actions |
| **Floor** | One procedural dungeon level | Level, stage |
| **Room** | A single space in the floor grid | Chamber, area |
| **Run** | Complete playthrough from start to victory/defeat | Match, session |
| **Craps Mode** | The super bet mechanic (item/unlock) | Special attack, ultimate |
| **Face Enchanting** | Modifying a specific face of a die | Upgrading, leveling |
| **Miss/Hit/Crit** | Attack results based on threshold | Fail/success/bonus |
| **Dice Power Budget** | The loadout cost limit | Slots, capacity |

## Combat Flow

```
Run
 └─ Floor (procedural dungeon)
     └─ Room (isometric grid)
         └─ Combat (triggered on adjacency)
             ├─ Player Turn (2 AP)
             │   ├─ Move (1 AP) — roll movement die, move tiles
             │   └─ Attack (1 AP) — roll weapon die vs threshold
             └─ Enemy Turn
                 └─ Roll attack die → result = damage (always hits)
```

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
- Change the combat flow (2 AP → Move → Attack vs Threshold) without team discussion
- Add base mechanics that should be items (defense, ranged, healing, flee are all item territory)
- Remove or rename existing ScriptableObject fields (breaks asset references)

---

# Performance Standards

## Hard Rules
- NO LINQ in hot paths
- NO per-frame allocations
- NO `Instantiate`/`Destroy` in gameplay — use Object Pooling
- Dice physics can use Rigidbody but must pool dice objects
- Structs over classes where possible for data
- Enum/int keys over strings
