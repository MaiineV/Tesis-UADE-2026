# 00 — Game Overview & Architecture

## Purpose
This document provides the high-level architecture and entry point for a roguelite dungeon crawler prototype built in Unity (C#). All other spec docs reference this as the central source of truth.

## Genre & Core Loop
- **Genre**: Roguelite dungeon crawler, turn-based combat
- **Perspective**: Top-down grid-based (placeholder art: geometric shapes)
- **Core Fantasy**: Dice ARE your inventory, weapon, movement, and defense

### Main Game Loop (Single Run)
```
Character Selection
       ↓
Enter Dungeon (procedural room matrix)
       ↓
┌─→ Enter Room
│      ↓
│   Room Type? ──→ Combat Room ──→ Movement Phase (dice roll) 
│      │                                    ↓
│      │                            Collision? ──→ Combat Phase (Generala)
│      │                                    ↓
│      │                            Room Cleared → Rewards
│      │
│      ├──→ Shop Room ──→ Buy/Enchant Dice
│      ├──→ Boss Room ──→ Boss Fight (unique mechanics)
│      ├──→ Sacrifice Room ──→ Trade HP for benefit
│      └──→ Craps Room ──→ Betting minigame
│      ↓
│   Choose Next Room (minimap)
└───────┘
       ↓
Run Ends (Win / Death)
       ↓
Meta-Progression (unlock check)
       ↓
Back to Character Selection
```

## Prototype Scope (Vertical Slice)
The prototype must prove the **core loop is fun**. It does NOT need all systems.

### IN SCOPE for Prototype
- 1 playable character (Warrior archetype)
- 3 types of dice (d6, d8, d12)
- 1 combat room with full Generala system
- Grid-based movement with dice rolls
- Craps/betting mechanic (energy bar + super roll)
- 2-enemy encounter: beat enemy 1 → choose 1 of 2 dice face upgrades → fight enemy 2 (more HP)
- Placeholder art (geometric shapes, solid colors)
- Basic UI: dice bag display, HP bar, energy bar, combat log

### OUT OF SCOPE for Prototype
- Multiple characters / class selection screen
- Procedural dungeon generation (full matrix)
- Shop / sacrifice / craps rooms
- Meta-progression / unlock system
- Enchantment system (beyond the mid-fight upgrade)
- Sound / music / animations
- Save system

## Architecture Overview (Unity)
```
Scenes:
  - MainMenu (placeholder)
  - GameScene (single room prototype)

Core Managers (Singletons):
  - GameManager          → Run state, turn flow, win/lose
  - CombatManager        → Generala combat logic, turn order
  - DiceManager          → Roll logic, dice pool, combinations
  - GridManager          → Room grid, tile data, pathfinding
  - MovementManager      → Player/enemy movement on grid
  - EnergyManager        → Energy bar, craps mode trigger
  - UIManager            → HUD updates, combat log, dice display

Data (ScriptableObjects):
  - DiceData             → Die type definitions (faces, power cost)
  - CharacterData        → Character stats, starting dice
  - EnemyData            → Enemy stats, movement die, behavior
  - CombinationData      → Generala combo definitions + damage

Entities (MonoBehaviour):
  - PlayerEntity         → Player on grid, stats, dice bag
  - EnemyEntity          → Enemy on grid, stats, AI
  - TileEntity           → Single grid tile
```

## Global Data Types (shared across systems)
```csharp
// Unique identifier for a die instance
// Each die in the player's bag is a unique instance
public class DiceInstance
{
    public string Id;           // unique GUID
    public DiceData BaseData;   // reference to ScriptableObject
    public int[] Faces;         // actual face values (can be modified by enchants)
    public int PowerCost;       // how many bag slots it uses
}

// Result of rolling a single die
public struct RollResult
{
    public string DiceId;       // which die was rolled
    public int FaceIndex;       // which face landed (0-based)
    public int Value;           // the numeric value
}

// A complete roll of all dice
public struct FullRollResult
{
    public RollResult[] Results;
    public int RollNumber;      // 1, 2, or 3
}

// Turn phases
public enum TurnPhase
{
    PlayerMovement,
    PlayerCombatRoll,
    PlayerDefenseRoll,
    EnemyCombatRoll,
    EnemyMovement,
    RoundEnd
}

// Room state
public enum RoomState
{
    Exploration,    // no enemies, free movement
    Combat,         // enemies present, dice movement
    Cleared,        // enemies defeated
    Shop,
    Boss,
    Sacrifice,
    Craps
}
```

## File Structure
```
Assets/
├── Scripts/
│   ├── Managers/
│   │   ├── GameManager.cs
│   │   ├── CombatManager.cs
│   │   ├── DiceManager.cs
│   │   ├── GridManager.cs
│   │   ├── MovementManager.cs
│   │   ├── EnergyManager.cs
│   │   └── UIManager.cs
│   ├── Data/
│   │   ├── DiceData.cs            (ScriptableObject)
│   │   ├── CharacterData.cs       (ScriptableObject)
│   │   ├── EnemyData.cs           (ScriptableObject)
│   │   └── CombinationData.cs     (ScriptableObject)
│   ├── Entities/
│   │   ├── PlayerEntity.cs
│   │   ├── EnemyEntity.cs
│   │   └── TileEntity.cs
│   ├── Combat/
│   │   ├── GeneralaCombat.cs      (combo detection + damage calc)
│   │   ├── DefenseResolver.cs
│   │   └── CrapsMode.cs
│   └── UI/
│       ├── DiceBagUI.cs
│       ├── CombatUI.cs
│       ├── HealthBarUI.cs
│       └── EnergyBarUI.cs
├── Data/
│   ├── Dice/                      (ScriptableObject instances)
│   ├── Characters/
│   └── Enemies/
├── Prefabs/
│   ├── Player.prefab
│   ├── Enemy.prefab
│   ├── Tile.prefab
│   └── DiceVisual.prefab
└── Scenes/
    ├── MainMenu.unity
    └── GameScene.unity
```

## Turn Flow (Prototype: Single Combat Room)
```
1. ROOM SETUP
   - Generate grid (e.g., 8x8)
   - Place player at starting tile
   - Place 2 enemies at random valid tiles

2. MOVEMENT PHASE (repeats until collision)
   a. Player rolls speed die → moves up to N tiles
   b. Check collision with any enemy → if yes, go to COMBAT
   c. Each alive enemy rolls their speed die → moves toward player
   d. Check collision → if yes, go to COMBAT
   e. Repeat from (a)

3. COMBAT PHASE
   a. Player Attack:
      - Roll all combat dice (roll 1 of 3)
      - Player selects dice to keep
      - Roll again (roll 2 of 3) — optional
      - Player selects dice to keep
      - Roll again (roll 3 of 3) — optional
      - Player commits to a combination → calculate damage
      - Apply damage to enemy
   b. Player Defense:
      - Remaining rolls (3 minus rolls used for attack) become defense rolls
      - Player rolls dice for each remaining roll
      - Best combination from defense rolls = shield value
   c. Enemy Attack:
      - Enemy rolls once → result = damage
      - Subtract player's shield → apply remaining damage to player
   d. Energy Update:
      - Add energy based on combat actions
      - If energy full → offer Craps mode next turn
   e. Check: enemy dead? → REWARD or next enemy
   f. Check: player dead? → GAME OVER

4. REWARD (after enemy 1 dies)
   - Offer 2 random dice face upgrades (pick 1)
   - Apply upgrade to chosen die face
   - Continue to enemy 2

5. ROOM CLEARED (after enemy 2 dies)
   - Show victory screen
   - Prototype ends (or restart)
```

## Cross-System Communication
Systems communicate via C# events (no complex event bus needed for prototype):

```csharp
// GameManager exposes events
public static event Action<TurnPhase> OnPhaseChanged;
public static event Action OnRunStarted;
public static event Action<bool> OnRunEnded; // true = win

// CombatManager exposes events  
public static event Action<FullRollResult> OnDiceRolled;
public static event Action<CombinationResult> OnCombinationSelected;
public static event Action<int> OnDamageDealt;
public static event Action<int> OnDamageReceived;
public static event Action OnEnemyDefeated;

// EnergyManager exposes events
public static event Action OnEnergyFull;
public static event Action<bool> OnCrapsResult; // true = success
```
