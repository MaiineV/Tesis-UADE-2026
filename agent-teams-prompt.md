# AGENT TEAM PROMPT — Roguelite Dice Dungeon Prototype

> **How to use**: Make sure `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` is set to `1` in your settings.json or environment. Then paste this entire prompt into Claude Code. The lead agent will read it, create the task list, spawn teammates, and orchestrate the build.

---

Build a complete playable Unity prototype from the spec documents in `spec-docs/`. This is a roguelite dungeon crawler where dice are your inventory, weapon, movement, and defense. Combat uses Generala (Yahtzee) mechanics.

## SETUP — Read first, plan second

Before doing ANYTHING else:

1. Read `spec-docs/README.md` to understand the document index.
2. Read ALL spec docs (`00-game-overview.md` through `10-implementation-guide.md`). These contain exact C# class definitions, damage formulas, balance values, and architecture decisions. They are the source of truth.
3. Read `CLAUDE.md` at the repo root for project conventions all teammates must follow.
4. Verify the Unity project exists (check for `Assets/`, `ProjectSettings/`, etc.)

Then create the agent team and task list as described below.

## TEAM STRUCTURE

Create an agent team called `dice-dungeon` with the following specialist teammates. Each teammate should work on their own git branch (`us-{XX}-{short-name}`) to avoid conflicts.

Spawn teammates in waves based on task dependencies. Do NOT spawn all at once — wait for blocking tasks to complete before spawning teammates for dependent work.

### Teammate roles:

1. **foundation-engineer** — Sets up project structure, shared types, enums, ScriptableObjects, and asset instances. This is the first task and everything depends on it.

2. **dice-engineer** — Implements DiceInstance, DiceBag, SpeedDie, rolling mechanics, and face modification logic.

3. **grid-engineer** — Implements GridManager, TileData, grid generation, obstacle placement, tile highlighting, coordinate conversions.

4. **entity-engineer** — Implements PlayerEntity, PlayerState, EnemyEntity, EnemyState, spawning logic.

5. **combat-engineer** — Implements CombinationDetector (all Generala combo detection), AttackPhase (3-roll lock/unlock/commit), DefensePhase (remaining rolls → shield), DamageResolver.

6. **movement-engineer** — Implements MovementManager (BFS reachable tiles, pathfinding), player movement input, EnemyMovement, EnemyAI, collision detection.

7. **energy-craps-engineer** — Implements EnergyManager (energy gain from actions), CrapsMode (betting, resolve, bonus/penalty), enemy enrage mechanic.

8. **reward-engineer** — Implements RewardGenerator (face upgrade generation), upgrade application, mid-fight reward flow.

9. **ui-engineer** — Implements ALL UI: HUD (HP bar, energy bar, shield), CombatUI (dice display, lock/unlock, combo preview, buttons), CrapsUI (bet overlay, result popup), RewardUI (pick 1 of 2), GameOverUI, VictoryUI, CombatLogUI, floating damage numbers.

10. **integration-engineer** — Implements GameManager (master state machine, all phase transitions), TurnManager, wires everything together into the playable loop. Also uses Unity MCP to assemble the scene: Canvas, grid parent, managers on GameObjects, prefab instances, camera setup, all Inspector references.

11. **reviewer** — Does NOT implement features. Reviews completed work from other teammates. Checks: code compiles, follows conventions from CLAUDE.md, matches spec docs, no hardcoded values, correct folder structure. Merges branches to main after approval. Reports unblocked tasks to the lead.

## TASK LIST

Create these tasks with proper dependencies. The `blockedBy` field ensures teammates can't start work until prerequisites are merged.

### Wave 0: Foundation

**Task 1: Project Structure & Shared Types**
Teammate: foundation-engineer
Blocked by: nothing
Spec refs: `00-game-overview.md`, `01-dice-system.md`, `02-character-system.md`, `07-enemy-system.md`
Description:
Create the full folder structure under `Assets/Scripts/` (Managers, Core, Data, Entities, Grid, Combat, UI). Create all shared enums: `CombinationType` (HighDie, Pair, TwoPair, ThreeOfAKind, Straight, FullHouse, FourOfAKind, Generala, DoubleGenerala), `TurnPhase`, `RoomState`, `FaceUpgradeType` (ValueIncrease, ValueSet, FaceRemoval), `EnemyBehavior` (Aggressive, Cautious, Stationary), `CombatActionType` (DealtDamage, Defended, TookDamage, KilledEnemy). Create shared structs: `RollResult`, `FullRollResult`, `CombinationResult`, `RunStats`, `FaceUpgrade`, `FaceUpgradeOffer`, `CrapsResult`, `DiceLoadout`, `BagSummary`. Create ScriptableObject base classes: `DiceData`, `CharacterData`, `EnemyData`. Create ScriptableObject asset instances with values from specs: d6 (faces 1-6, cost 1), d8 (faces 1-8, cost 2), d12 (faces 1-12, cost 3), Warrior (100HP, budget 8, speed 2-5, 4×d6+2×d8, affinity Poker 1.25x), Goblin (40HP, 2×d6 attack, speed 1-3, energy 50/15), Orc (60HP, 2×d8 attack, speed 1-2, energy 40/12). Commit to branch `us-01-foundation`, then have the reviewer merge to main.
Acceptance criteria:
- All folders exist under Assets/Scripts/
- All enums compile correctly
- All structs compile with correct fields matching the spec docs
- All 3 ScriptableObject classes are created with [CreateAssetMenu] attributes
- All 6 ScriptableObject instances (.asset files) exist in Assets/Data/ with correct values
- Everything compiles with zero errors

### Wave 1: Core Systems (parallel, all depend only on Task 1)

**Task 2: Dice System**
Teammate: dice-engineer
Blocked by: Task 1
Spec refs: `01-dice-system.md`
Description:
Implement `DiceInstance` (unique ID, base data ref, mutable CurrentFaces array, PowerCost, Create factory, Roll method). Implement `DiceBag` (List of DiceInstance, MaxPower, CurrentPower, RemainingPower, CanAdd, TryAdd, Remove, RollAll). Implement `SpeedDie` (MinValue, MaxValue, Roll). Implement `DiceManager` singleton with RollDice and RerollDice methods. All in `Assets/Scripts/Core/`.
Acceptance criteria:
- DiceInstance.Create(DiceData) produces a valid instance with cloned faces
- DiceInstance.Roll() returns a random valid face
- DiceBag respects power budget (CanAdd returns false when over budget)
- DiceBag.RollAll() returns correct number of RollResults
- SpeedDie.Roll() returns value within min-max range
- All code compiles

**Task 3: Grid System**
Teammate: grid-engineer
Blocked by: Task 1
Spec refs: `04-movement-grid-system.md`
Description:
Implement `GridManager` singleton: configurable Width/Height (default 8×8), TileSize, TileData 2D array, GenerateGrid, PlaceRandomObstacles (4-6), GridToWorld, WorldToGrid, IsValidPosition, IsOccupied, GetTile, SetOccupant, ClearOccupant, HighlightTiles, ClearHighlights. Implement `TileData` (Position, IsWalkable, Occupant). Implement `TileVisual` MonoBehaviour for rendering tiles (SpriteRenderer, color changes for highlights). Use Unity MCP to create a Grid parent GameObject in the scene and verify tiles render correctly.
Acceptance criteria:
- 8×8 grid generates with 4-6 random obstacles
- Obstacles never block edges (spawn in interior)
- GridToWorld and WorldToGrid convert correctly
- Tiles render as colored squares matching the color palette in CLAUDE.md
- Highlight system works (can highlight tiles green, yellow, clear)
- Compiles and grid is visible when running the scene

**Task 4: Player & Enemy Entities**
Teammate: entity-engineer
Blocked by: Task 1
Spec refs: `02-character-system.md`, `07-enemy-system.md`
Description:
Implement `PlayerState` (BaseData, CurrentHP, MaxHP, DiceBag, SpeedDie, CurrentEnergy, MaxEnergy, GridPosition, ShieldValue, CrapsModeAvailable, Create factory from CharacterData, IsAlive, TakeDamage, Heal). Implement `PlayerEntity` MonoBehaviour (State, SpriteRenderer Visual, Initialize, MoveTo). Implement `EnemyState` (BaseData, CurrentHP, MaxHP, GridPosition, CurrentEnergy, SpeedDie, IsAlive, IsEnraged, Create factory, TakeDamage, GainEnergy). Implement `EnemyEntity` MonoBehaviour (State, Visual, Initialize, MoveTo, RollAttack with enrage logic). All in `Assets/Scripts/Entities/`.
Acceptance criteria:
- PlayerState.Create(Warrior data) produces correct initial state (100HP, bag with 4×d6+2×d8, speed 2-5)
- EnemyState.Create(Goblin data) produces correct initial state (40HP, speed 1-3)
- TakeDamage reduces HP, never goes below 0
- IsAlive returns false when HP reaches 0
- EnemyEntity.RollAttack returns damage in expected range (2-12 for Goblin, 2-16 for Orc)
- Enrage: 60% chance to double damage when energy is full, resets energy after use
- Compiles

### Wave 2: Game Mechanics (depend on Wave 1 tasks)

**Task 5: Combination Detector**
Teammate: combat-engineer
Blocked by: Task 2
Spec refs: `03-combat-system.md`
Description:
Implement `CombinationDetector` static class with `Evaluate(int[] diceValues, bool hasGeneralaThisRun)` method. Must detect ALL combinations: HighDie, Pair, TwoPair, ThreeOfAKind, Straight (5+ consecutive unique values), FullHouse, FourOfAKind, Generala, DoubleGenerala. Returns the BEST combination (highest damage). Damage formulas from the spec: Pair = sum×1.5, ThreeOfAKind = sum×2, Straight = 30+highest, FullHouse = 35+sum, FourOfAKind = sum×3, Generala = sum×5, DoubleGenerala = sum×10, HighDie = highest value. Use helper methods: frequency map, straight detection, GetRemaining. Place in `Assets/Scripts/Combat/CombinationDetector.cs`.
Acceptance criteria:
- Evaluate([6,6,3,2,1], false) → Pair of 6s, damage = 18
- Evaluate([4,4,4,2,1], false) → ThreeOfAKind, damage = 24
- Evaluate([1,2,3,4,5], false) → Straight, damage = 35
- Evaluate([3,3,3,5,5], false) → FullHouse, damage = 54
- Evaluate([6,6,6,6,2], false) → FourOfAKind, damage = 72
- Evaluate([6,6,6,6,6], false) → Generala, damage = 150
- Evaluate([6,6,6,6,6], true) → DoubleGenerala, damage = 300
- Evaluate([1,3,7,2,8], false) → HighDie 8, damage = 8
- Always returns the highest-damage combo when multiple are possible
- Compiles

**Task 6: Movement System**
Teammate: movement-engineer
Blocked by: Task 3, Task 4
Spec refs: `04-movement-grid-system.md`
Description:
Implement `MovementManager` singleton: `GetReachableTiles(start, maxSteps)` using BFS (4 cardinal directions, respects obstacles), `FindPath(start, target)` using BFS, `MovePlayerAlongPath` (step by step, checks for enemy collision at each step). Implement `EnemyMovement` static class: `MoveEnemyTowardPlayer(enemy, player)` — rolls enemy speed die, finds path, moves up to N steps, returns true on collision. Implement `EnemyAI` static class: `DecideMovement` based on behavior type (Aggressive always moves toward player). Implement `PlayerMovementInput`: on mouse click, convert to grid pos, check if reachable, execute move. Events: `OnMovementCompleted`, `OnCollisionWithEnemy`.
Acceptance criteria:
- GetReachableTiles returns correct tiles within N steps, excluding obstacles
- FindPath returns shortest path around obstacles
- Player can click a highlighted tile to move there
- Enemy moves toward player using shortest path
- Collision detected when player/enemy reach same tile
- Compiles

**Task 7: Attack Phase**
Teammate: combat-engineer (same teammate as Task 5, sequential)
Blocked by: Task 5
Spec refs: `03-combat-system.md`
Description:
Implement `AttackPhase` class: MaxRolls=3, CurrentRoll tracking, RollResult array, LockedDiceIds HashSet, PerformRoll (first roll = all dice, subsequent = only unlocked), ToggleLock, CanRollAgain, Commit (calls CombinationDetector.Evaluate). Implement `DamageResolver` static class: `ResolvePlayerAttack(combo, characterData)` — applies affinity bonus, `ResolveEnemyAttack(rawDamage, shield)` — subtracts shield. Place in `Assets/Scripts/Combat/`.
Acceptance criteria:
- First roll rolls all dice in bag
- Locked dice keep their values on reroll
- CanRollAgain is false after 3 rolls
- Commit returns the best CombinationResult
- Affinity bonus applies when combo matches character affinity (Warrior + Poker = 1.25x)
- ResolveEnemyAttack correctly subtracts shield, never goes negative
- Compiles

**Task 8: Defense Phase**
Teammate: combat-engineer (sequential after Task 7)
Blocked by: Task 7
Spec refs: `03-combat-system.md`
Description:
Implement `DefensePhase` class: AvailableRolls = 3 minus attack rolls used, PerformDefenseRoll (rolls all dice once per defense roll, no locking), CalculateShield (best combo from all defense rolls, uses shield value table). Shield values: HighDie = value×0.5, Pair = sum×0.75, TwoPair = sum×0.75, ThreeOfAKind = sum×1, Straight = 15 flat, FullHouse = 20 flat, FourOfAKind = sum×1.5, Generala = sum×2.5. Place in `Assets/Scripts/Combat/DefensePhase.cs`.
Acceptance criteria:
- If attack used 1 roll, defense has 2 rolls
- If attack used 3 rolls, defense has 0 rolls (shield = 0)
- Each defense roll is a single throw (no locking within defense)
- CalculateShield returns the best shield value across all defense rolls
- Shield values match the table in the spec
- Compiles

### Wave 3: Secondary Systems

**Task 9: Energy System**
Teammate: energy-craps-engineer
Blocked by: Task 7
Spec refs: `05-energy-craps-system.md`
Description:
Implement `EnergyManager` singleton: Initialize, AddPlayerEnergy, ResetPlayerEnergy, ProcessCombatAction (energy gain table: DealtDamage +10, ThreeOfAKind+ gives more, Generala +50, Defended +5, TookDamage +5, KilledEnemy +10). Events: OnPlayerEnergyChanged, OnPlayerEnergyFull. Max energy = 100. When full, sets CrapsModeAvailable on PlayerState.
Acceptance criteria:
- Energy starts at 0
- ProcessCombatAction adds correct amounts per action type
- Energy caps at MaxEnergy (100)
- OnPlayerEnergyFull fires when energy reaches 100
- ResetPlayerEnergy sets to 0 and CrapsModeAvailable to false
- Compiles

**Task 10: Craps Mode**
Teammate: energy-craps-engineer (sequential after Task 9)
Blocked by: Task 9
Spec refs: `05-energy-craps-system.md`
Description:
Implement `CrapsMode` class: Activate, PlaceBet(CombinationType), Resolve(actualCombo, baseDamage) → CrapsResult. Success = exact match OR "better" within N-of-a-kind line (bet Pair, got Poker = win). Bonus/penalty tables from spec: Pair +25%/-10%, ThreeOfAKind +50%/-15%, Straight +50%/-15%, FullHouse +75%/-20%, FourOfAKind +100%/-25%, Generala +200%/-50%. HP changes on some bets. Events: OnCrapsModeStarted, OnBetPlaced, OnCrapsResolved.
Acceptance criteria:
- Betting Pair and rolling FourOfAKind = success (or better rule)
- Betting Straight and rolling Pair = failure (different line)
- Damage multipliers match spec table
- HP changes apply (FourOfAKind fail = -5 HP, Generala success = +20 HP heal)
- Energy resets after resolve regardless of outcome
- Compiles

**Task 11: Reward System**
Teammate: reward-engineer
Blocked by: Task 2
Spec refs: `06-inventory-system.md`, `01-dice-system.md`
Description:
Implement `RewardGenerator` static class: `GenerateOffers(DiceBag, count)` → generates N unique FaceUpgradeOffer. Upgrade types: ValueIncrease (50% chance, +2 to +4), ValueSet (30% chance, set to max+1 to max+3), FaceRemoval (20% chance, removes a face, min 3 faces). Implement `DiceUpgrader` static class: `ApplyUpgrade(DiceInstance, FaceUpgrade)` — modifies CurrentFaces array. Safety: FaceRemoval can't reduce below 2 faces.
Acceptance criteria:
- GenerateOffers returns 2 unique offers (different die/face targets)
- ValueIncrease correctly adds to face value
- ValueSet correctly replaces face value
- FaceRemoval correctly removes a face from the array
- FaceRemoval refuses if die has ≤2 faces
- Compiles

### Wave 4: UI Layer (parallel, each depends on relevant system tasks)

**Task 12: HUD & Bars**
Teammate: ui-engineer
Blocked by: Task 9
Spec refs: `08-ui-presentation.md`
Description:
Create Canvas with UI elements using Unity MCP. Implement `HealthBarUI` (fill image, color changes at thresholds: green>50%, yellow 25-50%, red<25%). Implement `EnergyBarUI` (fill image, color ramp blue→yellow→gold, pulsing when full, "CRAPS MODE READY" text). Implement `ShieldDisplay` (text showing current shield points). Implement phase label text ("YOUR MOVE", "YOUR ATTACK", "DEFENSE", "ENEMY ATTACKS"). Implement `UIManager` singleton that controls panel visibility based on game state. All in `Assets/Scripts/UI/`.
Acceptance criteria:
- HP bar fills/depletes smoothly
- HP bar changes color at thresholds
- Energy bar shows progress with color ramp
- Energy bar pulses/glows when full
- Phase label updates text correctly
- UIManager can show/hide panels by game state
- Uses Unity MCP to set up Canvas and UI hierarchy in the scene
- Compiles

**Task 13: Combat UI**
Teammate: ui-engineer (sequential after Task 12)
Blocked by: Task 12, Task 7
Spec refs: `08-ui-presentation.md`
Description:
Implement `CombatUI`: dice display area showing each die as a colored rectangle with face value (large) and type label (small). Click a die to toggle lock (locked = gold border + ★ marker). Real-time combo preview text ("Best combo: Pair of 6s → 18 dmg"). Roll counter ("Roll 2/3"). Buttons: "REROLL" (disabled when no rolls left), "COMMIT ATTACK" (disabled before first roll). Defense UI: "ROLL DEFENSE" button, defense roll results display, shield value. Enemy attack result overlay: enemy roll, shield absorption, net damage, "Continue" button.
Acceptance criteria:
- Each die in the bag is displayed with correct value and type
- Clicking a die toggles lock visually (border + icon change)
- Combo preview updates when dice are locked/unlocked
- Roll counter shows current/max rolls
- Buttons enable/disable at correct times
- Defense phase shows available rolls and results
- Enemy attack shows roll, shield, and net damage
- Compiles

**Task 14: Overlay UIs**
Teammate: ui-engineer (sequential after Task 13)
Blocked by: Task 13, Task 10, Task 11
Spec refs: `08-ui-presentation.md`
Description:
Implement `CrapsUI`: full-screen overlay with bet options (Pair, ThreeOfAKind, Straight, FullHouse, FourOfAKind, Generala), each showing bonus/penalty percentages. Result popup: green flash for success, red flash for failure, shows bet vs actual + damage modifier. Implement `RewardUI`: 2 cards side-by-side showing upgrade options (die name, face change, plain-language description). Click to choose. Implement `GameOverUI`: "GAME OVER", stats (rounds, damage dealt, best combo), restart button. Implement `VictoryUI`: "VICTORY!", same stats, restart button. Implement `CombatLogUI`: scrolling text list showing last 5 messages.
Acceptance criteria:
- Craps overlay shows all 6 bet options with correct percentages
- Craps result popup shows success/failure with details
- Reward UI shows 2 distinct upgrade options
- Clicking a reward option triggers selection callback
- Game Over and Victory screens show stats
- Restart button works (calls GameManager.RestartRun or similar)
- Combat log scrolls and shows recent messages
- All overlays can be shown/hidden via UIManager
- Compiles

### Wave 5: Integration

**Task 15: GameManager & Scene Assembly**
Teammate: integration-engineer
Blocked by: Task 6, Task 8, Task 10, Task 11, Task 14
Spec refs: `09-game-state-flow.md`, `00-game-overview.md`
Description:
Implement `GameManager` singleton with the full state machine from spec 09: MainMenu, RoomSetup, MovementPhase, PreCombat, CrapsBet, AttackPhase, DefensePhase, EnemyAttack, RoundEnd, RewardSelection, GameOver, Victory. Implement all transitions: StartRun → SetupRoom (generate grid, spawn player at [1,1], spawn Goblin at [6,5], spawn Orc at [5,3]) → MovementPhase (player rolls speed die, clicks to move, enemies move, check collision) → Combat (craps check → attack → defense → enemy attack → round end → loop or death) → after enemy 1 dies: RewardSelection → after enemy 2 dies: Victory. Wire up ALL events from all systems. Track run stats (rounds, damage, best combo, craps attempts). Use Unity MCP to assemble the final scene: create manager GameObjects (GameManager, GridManager, MovementManager, EnergyManager, UIManager on a "Managers" parent), set up camera (orthographic, centered on grid), create player and enemy prefabs, wire all serialized references in the Inspector. Implement RestartRun that resets everything.
Acceptance criteria:
- Full state machine works: every transition fires correctly
- Player can move → collide → combat → defend → survive enemy → next round
- Craps mode triggers when energy is full
- Killing enemy 1 shows reward selection
- Picking a reward applies it and returns to movement
- Killing enemy 2 shows victory screen
- Player dying shows game over screen
- Restart resets everything and starts fresh
- Scene is fully assembled via Unity MCP — no manual editor work needed
- Compiles and runs as a playable prototype

### Wave 6: Polish

**Task 16: Visual Feedback & Final Testing**
Teammate: integration-engineer (same as Task 15)
Blocked by: Task 15
Spec refs: `08-ui-presentation.md`, `10-implementation-guide.md`
Description:
Add `FloatingDamageUI`: numbers that rise and fade when damage is dealt (white normal, yellow affinity, red craps bonus). Add screen flash for craps results (green success, red failure). Add smooth HP bar transitions (lerp over 0.4s instead of instant). Add entity movement animation (slide between tiles at 0.2s per tile instead of teleport). Add die roll visual feedback (brief shake/wobble for 0.3s before showing value). Play through the ENTIRE loop start to finish. Fix any bugs. Verify: move → fight goblin → reward → fight orc → win. Also verify: die to goblin → game over → restart works. Tune balance if needed (adjust HP, damage, energy rates via ScriptableObjects).
Acceptance criteria:
- Floating damage numbers appear and fade
- HP bars transition smoothly
- Entities slide between tiles
- Dice shake briefly on roll
- Full play loop works without crashes
- Can win AND lose and restart both scenarios
- Game feels playable (3-5 rounds per enemy)

## COORDINATION RULES FOR THE LEAD

1. **Create all tasks first** using the Task system with correct `blockedBy` dependencies.
2. **Spawn teammates in waves**: start with foundation-engineer alone. Once Task 1 is merged, spawn Wave 1 teammates (dice, grid, entity engineers) in parallel. Continue wave by wave.
3. **The reviewer teammate** should be spawned early and persist throughout. When a worker completes a task, message the reviewer to review and merge the branch.
4. **After each merge to main**, check which blocked tasks are now unblocked and either message idle teammates to claim them or spawn new teammates if needed.
5. **Combat-engineer handles Tasks 5, 7, 8 sequentially** — these are tightly coupled and should be one teammate working through them in order.
6. **Energy-craps-engineer handles Tasks 9, 10 sequentially** — same reason.
7. **UI-engineer handles Tasks 12, 13, 14 sequentially** — UI layers build on each other.
8. **Integration-engineer handles Tasks 15, 16 sequentially** — final assembly then polish.
9. **Maximize parallelism**: In Wave 1, three teammates work simultaneously. In Wave 2-3, up to 4 can work in parallel.
10. **Every teammate must read the relevant spec docs** before writing code. Remind them in the spawn prompt.

## FINAL GOAL

When all tasks are complete, a player should be able to:
1. See a grid room with a blue square (player) and two colored shapes (enemies)
2. Roll a speed die and click to move on the grid
3. Collide with an enemy to start Generala combat
4. Roll dice with 3 rolls, lock/unlock dice, commit to a combination
5. See damage dealt to the enemy
6. Roll defense dice with remaining rolls
7. Survive the enemy's counter-attack (reduced by shield)
8. Build up energy over multiple rounds
9. Use Craps mode when energy is full (bet on next combo)
10. Kill the Goblin, pick 1 of 2 dice face upgrades
11. Fight and kill the Orc (or die trying)
12. See Victory or Game Over screen with run statistics
13. Restart and play again

Start now. Read the specs, create the tasks, and begin orchestrating.
