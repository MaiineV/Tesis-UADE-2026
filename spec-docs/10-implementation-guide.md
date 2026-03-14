# 10 — Implementation Guide (Claude Code Quick Reference)

## Purpose
This document provides a step-by-step implementation order for building the prototype. Each step builds on the previous one and results in a testable milestone.

---

## Implementation Order

### Phase 1: Foundation (No Gameplay Yet)
**Goal**: Project setup, data structures, grid rendering.

1. **Create Unity project** (2D URP, TextMeshPro)
2. **Create ScriptableObjects**: `DiceData`, `CharacterData`, `EnemyData`
3. **Create instances**: d6, d8, d12 dice data; Warrior character; Goblin and Orc enemies
4. **Implement `GridManager`**: generate 8x8 grid, render tiles as sprites, place obstacles
5. **Implement `DiceInstance` and `DiceBag`**: create dice instances, test roll functionality
6. **Implement `PlayerEntity`**: spawn on grid, store state
7. **Implement `EnemyEntity`**: spawn on grid, store state

**Test milestone**: Run the scene, see an 8x8 grid with a blue square (player) and two colored shapes (enemies).

---

### Phase 2: Movement
**Goal**: Player and enemies can move on the grid by rolling dice.

1. **Implement `SpeedDie`** and roll logic
2. **Implement `MovementManager.GetReachableTiles`** (BFS)
3. **Implement `MovementManager.FindPath`** (BFS pathfinding)
4. **Implement player input**: click to select destination tile
5. **Implement tile highlighting**: reachable tiles in green, hover in yellow
6. **Implement player movement**: animate slide along path
7. **Implement `EnemyMovement`**: enemy rolls speed die, moves toward player
8. **Implement collision detection**: player reaches enemy tile → flag collision
9. **Implement basic turn loop**: player moves → enemies move → repeat

**Test milestone**: Player clicks to move, enemies chase. Moving to an enemy's tile triggers a console log "Combat started!".

---

### Phase 3: Combat — Attack
**Goal**: Full Generala attack system works.

1. **Implement `CombinationDetector.Evaluate`**: detect all combo types from an array of values
2. **Write unit tests** for CombinationDetector (test all combos: pair, three, straight, full house, poker, generala)
3. **Implement `AttackPhase`**: roll, lock, reroll, commit
4. **Build Combat UI**: dice display, lock/unlock, reroll button, commit button
5. **Show combo preview**: real-time display of best combo as player locks dice
6. **Implement `DamageResolver.ResolvePlayerAttack`**: apply damage + affinity bonus
7. **Wire up**: commit → damage → enemy HP decreases → check death

**Test milestone**: Enter combat, roll dice, lock/unlock, reroll, commit. See damage applied to enemy HP bar. Enemy dies after enough damage.

---

### Phase 4: Combat — Defense & Enemy Attack
**Goal**: Full combat round completes.

1. **Implement `DefensePhase`**: calculate available rolls, roll defense, compute shield
2. **Build Defense UI**: show defense rolls, shield value
3. **Implement enemy attack roll**: enemy rolls once, damage calculated
4. **Implement `DamageResolver.ResolveEnemyAttack`**: subtract shield from enemy damage
5. **Build enemy attack UI**: show roll, shield absorption, net damage
6. **Wire up full combat round**: attack → defense → enemy attack → check death → next round
7. **Implement round loop**: if nobody dies, start new attack phase

**Test milestone**: Full combat round works. Player attacks, defends, enemy attacks. Round repeats until someone dies.

---

### Phase 5: Energy & Craps
**Goal**: Energy builds during combat. Craps mode works when energy is full.

1. **Implement `EnergyManager`**: track energy, process combat actions for energy gain
2. **Build energy bar UI**: player and enemy bars
3. **Implement `CrapsMode`**: activate, place bet, resolve
4. **Build Craps bet UI**: overlay with combo options and risk/reward display
5. **Build Craps result UI**: success/failure popup
6. **Implement enemy enrage**: when enemy energy full, chance to double damage
7. **Wire up craps into combat flow**: energy full → show bet UI → normal attack → evaluate bet

**Test milestone**: After a few rounds of combat, energy fills. Craps mode activates, player places bet, sees bonus/penalty applied.

---

### Phase 6: Reward & Full Prototype Loop
**Goal**: Defeat enemy 1 → choose upgrade → fight enemy 2 → win/lose.

1. **Implement `RewardGenerator`**: generate 2 random face upgrades
2. **Implement `DiceUpgrader.ApplyUpgrade`**: modify dice face values
3. **Build Reward UI**: show 2 options, player picks one
4. **Implement prototype flow**: enemy 1 dies → reward → resume movement → fight enemy 2
5. **Build Game Over screen**: show stats, restart button
6. **Build Victory screen**: show stats, restart button
7. **Implement restart**: reset all state, regenerate room

**Test milestone**: Complete prototype loop. Player can move, fight goblin, pick upgrade, fight orc, win or die. Can restart.

---

### Phase 7: Polish & Playtest
**Goal**: Make it feel good enough to playtest.

1. **Add floating damage numbers** (rising text that fades)
2. **Add screen flash** for craps result
3. **Add die roll shake animation** (brief wobble before showing value)
4. **Add smooth HP bar transitions**
5. **Add phase transition labels** ("YOUR TURN", "ENEMY ATTACKS!")
6. **Add combat log** (scrolling text of actions)
7. **Tune balance**: adjust HP, damage formulas, energy rates
8. **Fix bugs** from playtesting

---

## Key Architecture Decisions

### Singleton Managers
For a prototype, singletons are fine. Use `Instance` pattern:
```csharp
public class MyManager : MonoBehaviour
{
    public static MyManager Instance;
    void Awake() { Instance = this; }
}
```
Managers: `GameManager`, `GridManager`, `MovementManager`, `CombatManager`, `EnergyManager`, `UIManager`

### ScriptableObjects for Data
All tunable values go in ScriptableObjects so designers can tweak without touching code:
- Dice types (d6, d8, d12) with face values and power costs
- Character data (HP, speed, starting dice)
- Enemy data (HP, attack dice, speed, energy)
- Damage formulas could be in a central BalanceConfig ScriptableObject

### Event-Driven Communication
Use C# events/Actions for loose coupling:
```csharp
// In sender:
public static event Action<int> OnDamageDealt;
OnDamageDealt?.Invoke(damage);

// In receiver:
void OnEnable() => GameManager.OnDamageDealt += HandleDamage;
void OnDisable() => GameManager.OnDamageDealt -= HandleDamage;
```

### UI Architecture
- One Canvas in the scene
- UI panels as children, toggled on/off with `SetActive`
- `UIManager` controls which panels are visible based on game state
- Each panel has its own script for internal logic (button handlers, etc.)

---

## File Naming Convention
```
Scripts/
├── Managers/GameManager.cs
├── Managers/GridManager.cs
├── Managers/MovementManager.cs
├── Managers/EnergyManager.cs
├── Data/DiceData.cs
├── Data/CharacterData.cs
├── Data/EnemyData.cs
├── Core/DiceInstance.cs
├── Core/DiceBag.cs
├── Core/SpeedDie.cs
├── Core/CombinationDetector.cs
├── Core/AttackPhase.cs
├── Core/DefensePhase.cs
├── Core/CrapsMode.cs
├── Core/DamageResolver.cs
├── Core/RewardGenerator.cs
├── Entities/PlayerEntity.cs
├── Entities/PlayerState.cs
├── Entities/EnemyEntity.cs
├── Entities/EnemyState.cs
├── Entities/EnemyAI.cs
├── Entities/EnemyMovement.cs
├── Grid/TileData.cs
├── Grid/TileVisual.cs
├── UI/UIManager.cs
├── UI/CombatUI.cs
├── UI/DiceBagUI.cs
├── UI/HealthBarUI.cs
├── UI/EnergyBarUI.cs
├── UI/CrapsUI.cs
├── UI/RewardUI.cs
├── UI/CombatLogUI.cs
├── UI/GameOverUI.cs
├── UI/VictoryUI.cs
└── UI/FloatingDamageUI.cs
```

---

## Balance Tuning Cheatsheet

All these values should be editable in the Unity Inspector (ScriptableObjects or serialized fields):

| Value                    | Location            | Default | Adjust If...                    |
|--------------------------|---------------------|---------|---------------------------------|
| Player HP                | CharacterData       | 100     | Player dies too fast/slow       |
| Goblin HP                | EnemyData           | 40      | First fight too long/short      |
| Orc HP                   | EnemyData           | 60      | Second fight too long/short     |
| Player speed die         | CharacterData       | 2-5     | Grid feels too slow/fast        |
| Goblin speed die         | EnemyData           | 1-3     | Enemy reaches player too fast   |
| Goblin attack dice       | EnemyData           | 2d6     | Enemy too strong/weak           |
| Orc attack dice          | EnemyData           | 2d8     | Enemy too strong/weak           |
| Affinity bonus           | CharacterData       | 1.25x   | Affinity feels meaningless      |
| Energy per action        | EnergyManager       | varies  | Craps triggers too rarely/often |
| Enemy energy per round   | EnemyData           | 12-15   | Enemy enrage too frequent/rare  |
| Defense multipliers      | DefensePhase        | varies  | Shield blocks too much/little   |
| Craps bonuses/penalties  | CrapsMode           | varies  | Risk/reward feels off           |
| Grid size                | GridManager         | 8x8     | Room feels too big/small        |
| Obstacle count           | GridManager         | 4-6     | Grid too open/cramped           |
| Face upgrade amounts     | RewardGenerator     | +2 to +4| Upgrade feels too weak/strong   |

---

## Testing Checklist

After each phase, verify:

- [ ] **Phase 1**: Grid renders, entities visible on grid
- [ ] **Phase 2**: Player moves by clicking, enemies chase, collision detected
- [ ] **Phase 3**: All 9 combination types detected correctly. Damage applied. Enemy can die.
- [ ] **Phase 4**: Full combat round loops. Player can defend. Enemy deals damage.
- [ ] **Phase 5**: Energy bar fills. Craps mode activates. Bet bonuses/penalties apply.
- [ ] **Phase 6**: Kill goblin → pick reward → fight orc. Win and lose screens work. Restart works.
- [ ] **Phase 7**: Animations play. Numbers feel right. 3-5 rounds per enemy.

---

## Common Pitfalls

1. **CombinationDetector**: Make sure "Straight" requires 5+ consecutive UNIQUE values, not 5 consecutive dice. Player might have duplicate values.
2. **Defense rolls**: Defense rolls do NOT have locking. Each defense roll is a single throw of all dice.
3. **Craps "or better"**: Betting on Pair and rolling Four of a Kind should count as a win (within the N-of-a-kind line).
4. **Face removal**: Don't allow removing faces if the die would have fewer than 2 faces.
5. **Energy reset**: Energy resets to 0 after Craps Mode resolves, not after the combat ends.
6. **Generala tracking**: "Double Generala" is a RUN-level flag, not per-combat. If player gets Generala against Goblin, getting it again against Orc counts as Double.
7. **Enemy energy**: Each enemy has SEPARATE energy. One enemy's enrage doesn't affect the other.
8. **Grid occupancy**: When an entity moves, clear old tile occupancy and set new tile. Don't forget to clear on death.
