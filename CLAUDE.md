# Roguelite Dice Dungeon — Project Rules

## Project Overview
Roguelite dungeon crawler prototype in Unity (C#). Dice are everything: inventory, weapon, movement, defense. Combat uses Generala (Yahtzee) mechanics. Single-room prototype with 2 enemies.

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
