# Game Design Document

## Game Summary
* **Working Title:** [Untitled] — Dice-Based Roguelite Dungeon Crawler
* **Genre:** Roguelite Dungeon Crawler, Turn-Based
* **Perspective:** Fixed Isometric
* **Engine:** Unity 6.3 (C#)
* **Platform:** PC (itch.io / Steam)
* **Controls:** Mouse (grid click + dice drag)
* **Target Audience:** Strategy gamers, roguelite fans, tabletop gaming community
* **Run Duration:** ~1 hour 40 minutes (3-4 floors, ~25 min each)
* **Team:** 6 — Game Development B.S. (UADE 2026)
* **Stage:** Concept / Pre-Prototype

## Concept
A turn-based roguelite where dice are your inventory, your weapon, your movement, and your identity. Build your dice loadout, explore procedural dungeons, and use a Generala/Yahtzee combo system for combat where every roll is a bet on how far you move, how hard you hit, and whether your build pays off.

The base system is deliberately simple: roll all your dice, pick which ones move you, and use the rest to build combos for damage. Items, passives, and special dice break those rules in interesting ways — that's where the depth comes from. Inspired by The Binding of Isaac's philosophy (clean core that items transform), Balatro's combo escalation (base hands are weak, builds make them devastating), and classic Generala/Yahtzee (lock, reroll, chase the combo).

## Gameplay
* Grid-based turn system — roll dice, split between movement and combat
* Every die has dual purpose: move OR attack (never both)
* Generala/Yahtzee combo system: lock dice, reroll up to 3 times, combos determine damage
* Base combos deal moderate damage — items and face enchanting make them devastating
* No base defense — items grant shields, lifesteal, evasion, and damage reduction
* Enemies always hit — their roll IS the damage
* Items and passives layer complexity on top of a simple core (Isaac model)
* Procedural isometric dungeons with room-based exploration
* Deep build-crafting via dice enchanting, items, passives, and special dice
* D&D-inspired character classes with unique playstyles
* Meta-progression with unlockable content across runs

## Art Direction
* **Style:** Pixel-Poly (3D Low Poly + Pixel Art Shader)
* **Perspective:** Fixed isometric, no camera rotation
* **Palette:** Dark tones + casino neons (table green, dice red, chip gold)
* **Visual Exception:** Dice rotate with real physics when rolled — they are the visual protagonist
* **Visual Hierarchy:** Dice > Player > Enemies > Environment
* Each floor has a distinct casino-themed aesthetic [TBD by art team]

## Game Feel & Visual Feedback
* Every roll has impact — dice hit the table with physics, bounce, settle. The screen reacts.
* Combos explode visually — Generala fills the screen with effects, Poker shakes it.
* The split decision (move vs attack) creates visible tension — dice drag from pool to movement slots.
* Numbers must feel satisfying — damage numbers scale visually with their value.
* The gambling tension never stops — every Generala phase is a bet on whether to keep or reroll.

---

# Glossary

## The World

### **Grid (Grilla)**
The entire game world is built on a permanent tile grid. No free movement — all displacement happens tile by tile. The grid is active both inside and outside combat. There is no scene transition when combat begins. The grid is always visible during combat.

### **Adjacency (Adyacencia)**
Prototype: 4-tile cardinal adjacency (up, down, left, right). No diagonals.
Future: migrate to hexagonal grid for optimal tactical gameplay.

### **Obstacles (Obstaculos)**
Rooms contain fixed blocked tiles (pillars, tables, casino furniture, etc.). They cannot be passed through and add tactical depth to movement and positioning.

## Dice Terms

### **Dice Bag (Bolsa de Dados)**
The player's dice loadout — their inventory, build, and identity. Contains all combat dice (e.g., 4×d6 + 2×d8). All dice are rolled every turn and split between movement and Generala. Managed by a Power Budget system.

### **Power Budget (Presupuesto de Poder)**
The loadout cost limit. Each die has a power cost (d6=1, d8=2, d10=3, d12=4). The player's total dice cannot exceed their budget. Hollow Knight charm notch system — forces meaningful choices about which dice to carry.

### **Movement Dice (Dados de Movimiento)**
Dice the player picks from their roll to use for movement. Face value = tiles. No multiplier. These dice are removed from the Generala pool for that turn. The sum of all movement dice = total tiles available.

### **Generala Phase (Fase de Generala)**
After picking movement dice, remaining dice enter the Generala phase. The player can lock dice and reroll up to 3 times total, then commits to evaluate the best combo for damage. Full Yahtzee/Generala rules.

### **Special Dice**
Dice with built-in effects that trigger on specific face results. Follow power budget rules but add synergy layers.

## Combat Results

### **Combo (Combinación)**
The Generala hand formed by the dice after the player commits. Combo type determines the damage formula. Higher combos = more damage. See Combo Table.

## Player Stats

### **Life Points (HP/Vida)**
Core health resource. Reaching 0 HP ends the run.

### **Speed (Velocidad)**
[ELIMINATED — movement comes from combat dice. "Dice are everything."]

### **Dexterity (Destreza)**
[TBD — may modify item effectiveness via items]

## Economy

### **Gold (Oro)**
Currency dropped by enemies on defeat. Spent in Shop rooms. Always visible in HUD.

## Official Terminology

| Term | Meaning | DO NOT use |
|---|---|---|
| **Dice Bag** | The player's dice loadout/inventory | Backpack, inventory |
| **Power Budget** | The loadout cost limit (notch system) | Slots, capacity |
| **Generala Phase** | The lock/reroll/combo attack phase | Attack phase, combat roll |
| **Movement Dice** | Dice picked for movement (face=tiles) | Speed dice, movement roll |
| **Floor** | One procedural dungeon level | Level, stage |
| **Room** | A single space in the floor grid | Chamber, area |
| **Run** | Complete playthrough from start to victory/defeat | Match, session |
| **Craps Mode** | Core bet mechanic — bet on combo before rolling when energy is full | Special attack, ultimate |
| **Energy Bar** | Builds during combat, enables Craps Mode at 100/100 | Mana, charge |
| **Face Enchanting** | Modifying a specific face of a die | Upgrading, leveling |
| **Combo** | The Generala hand (Pair, Straight, etc.) | Hand, combination |

---

# Game Core Loop

## 3 Layers

```
+==============================================================+
|  MICRO LOOP — Turn                                            |
|  Roll all dice -> Pick movement -> Move -> Generala -> Damage |
+==============================================================+
|  MACRO LOOP — The Floor                                       |
|  Explore rooms -> Fight -> Loot -> Shop -> Boss               |
+==============================================================+
|  META LOOP (between runs) — Progression                       |
|  Unlock characters -> New dice -> New items -> New passives   |
+==============================================================+
```

## Micro Loop — Turn (Pick & Roll)
Roll all your dice. Pick which ones move you (face = tiles). Use the rest for Generala (lock, reroll, combos = damage). Every die has dual purpose. The tension: sacrifice dice for movement or keep them for a better combo?

## Macro Loop — Floor
Explore procedural dungeon on isometric grid. Room types: combat (gold/loot), shop (upgrade build), sacrifice (risk/reward), boss (unique mechanics). Floor ends with a boss.

## Meta Loop — Progression
Isaac model. Each run unlocks new content via milestones: defeating bosses, completing constraints, reaching achievements. Every run has value — win or lose.

---

# Core Combat System — Pick & Roll

> This section defines the base combat mechanics.
> Everything here is the foundation on which items, builds, and modifiers are added (Isaac-style).
> The system must be fun and functional with zero items equipped.

## 1. The Grid
* The entire world is built on a permanent tile grid
* No free movement — all displacement happens tile by tile
* The grid is active both inside and outside combat
* **No scene transition when combat begins** — combat happens on the same grid
* **The grid is always visible during the Generala phase** — no separate combat screen
* Rooms contain **fixed obstacles** (blocked tiles) that affect movement and positioning
* Adjacency is **4-tile cardinal** (up/down/left/right) — no diagonals in prototype
* [FUTURE] Migrate to hexagonal grid for better tactical depth

## 2. Turn Structure

```
Player Turn → Enemy Turn → Player Turn → Enemy Turn → ...
```

Each player turn follows the **Pick & Roll** flow:

```
┌─────────────────────────────────────────────────┐
│ 0. CRAPS BET?   If energy=100, option to bet    │
│                 on combo (optional, opt-in)      │
│ 1. ROLL         Roll all dice from Dice Bag     │
│ 2. PICK MOVE    Select 0+ dice for movement     │
│                 (face value = tiles, summed)     │
│ 3. MOVE         Execute movement on grid        │
│ 4. GENERALA     Lock/reroll remaining dice      │
│                 (up to 3 rolls total)            │
│ 5. COMMIT       Best combo → damage to target   │
│                 (Craps bonus/penalty if active)  │
│ 6. ENEMY TURNS  Each enemy moves and attacks    │
└─────────────────────────────────────────────────┘
```

**The AP system is replaced by Pick & Roll.** There are no separate action points. The player's entire turn is: pick movement, move, then Generala with the rest.

## 3. Step 0 — Craps Mode (Optional)

### Energy Bar
* Starts at 0 at the beginning of each combat encounter
* Builds based on combat actions:

| Action | Energy Gained |
|--------|---------------|
| Deal damage (any combo) | +10 |
| Three of a Kind or better | +15 |
| Full House | +20 |
| Four of a Kind | +25 |
| Generala / Double Generala | +50 |
| Take damage | +5 |
| Kill an enemy | +10 |

* Max energy = 100
* When energy reaches 100 → **Craps Mode becomes available**

### Craps Bet
When energy is full, **before rolling dice**, the player can choose:
* **Skip** — ignore Craps Mode, play normally. Energy stays at 100.
* **Bet** — select a combo type they think they'll roll this turn

### Bet Outcomes

| Bet | If you hit it | If you miss |
|-----|--------------|-------------|
| Pair | +25% daño | -10% daño |
| Three of a Kind | +50% daño | -15% daño |
| Straight | +50% daño + heal 10 HP | -15% daño |
| Full House | +75% daño | -20% daño |
| Four of a Kind | +100% daño (×2) | -25% daño + 5 HP perdidos |
| Generala | +200% daño (×3) + heal 20 HP | -50% daño + 10 HP perdidos |

* "Or better" counts — bet Pair but roll Four of a Kind → success
* After resolving (hit or miss), energy resets to 0
* **Key tension:** dice used for movement are removed from Generala pool → betting high combos while needing to move is a big gamble

> **Design note:** Craps Mode is a core mechanic, available to all players in all runs. It's opt-in — you never have to use it. This reinforces the casino identity: every combat has a potential "double or nothing" moment.

## 4. Step 1 — Roll All Dice

* All dice in the Dice Bag are rolled simultaneously at the start of each turn
* The player sees ALL results before making any decisions
* This is the only roll for movement — there is no separate movement die
* **"Dice are everything"** — combat dice ARE movement dice

**Example (6 dice: 4×d6 + 2×d8):**
Roll result: `[4] [2] [5] [3] [6] [3]`
The player now decides how to split these 6 dice.

## 5. Step 2 — Pick Movement Dice

* The player selects **0 or more dice** from the roll to use for movement
* **Face value = tiles.** A die showing 5 = 5 tiles of movement.
* **Multiple movement dice are SUMMED.** Picking [3] and [2] = 5 tiles total.
* Selected dice are **removed from the Generala pool** — they cannot be used for attack
* **The player can choose to pick 0 dice** — skip movement entirely, keep all dice for Generala. This is optimal when already adjacent to an enemy.
* Movement is always a **single continuous path** (no splitting). Split movement = item territory.
* Unused movement tiles are lost — they do not carry over.

**Strategic decision:** Identify which dice don't contribute to a good combo and sacrifice them for movement. A [2] that breaks your Straight is better used as 2 tiles of movement.

## 6. Step 3 — Move

* The player moves on the grid using BFS pathfinding
* Total tiles = sum of all movement dice face values
* Movement is always a single continuous path around obstacles
* The player must end movement on a valid (non-occupied, non-obstacle) tile
* **Move FIRST, attack SECOND — always.** The player cannot attack and then move. Hit & run = item territory.

## 7. Step 4 — Generala Phase

After movement, the remaining dice enter the **Generala phase**. This is full Yahtzee/Generala rules:

### Roll Sequence
* **Roll 1:** The initial values from Step 1 (already rolled)
* **Lock:** The player selects which dice to keep
* **Reroll:** Unlocked dice are rerolled
* **Repeat** for up to **3 total rolls** (initial + 2 rerolls)
* **Commit:** The player confirms their final hand

### Rules
* The player can lock/unlock any die between rolls
* The player can commit early (after roll 1 or 2) if satisfied
* Fewer dice = harder to form combos, but still possible (a Pair needs only 2 dice)
* **If the player is NOT adjacent to any enemy after moving, the Generala phase is skipped** — dice are wasted. This punishes bad positioning.

## 8. Combo Table — Damage Formulas

| Combo | Requirement | Damage Formula | Example (d6s) |
|-------|-------------|----------------|----------------|
| **High Die** | No combo | Highest face × 1 | [6] = 6 dmg |
| **Pair** | 2 equal | Sum of pair × 1.5 | [4,4] = 12 dmg |
| **Two Pair** | 2 + 2 equal | Sum of both pairs × 1.2 | [3,3,5,5] = 19 dmg |
| **Three of a Kind** | 3 equal | Sum of trio × 2 | [5,5,5] = 30 dmg |
| **Straight** | 4+ consecutive | 30 + highest die | [3,4,5,6] = 36 dmg |
| **Full House** | 3 + 2 equal | 35 + sum of all | [4,4,4,6,6] = 59 dmg |
| **Four of a Kind** | 4 equal | Sum of four × 3 | [5,5,5,5] = 60 dmg |
| **Generala** | 5 equal | Sum × 5 | [5,5,5,5,5] = 125 dmg |
| **Double Generala** | 6 equal | Sum × 8 | [5,5,5,5,5,5] = 240 dmg |

> **Design note (Balatro philosophy):** Base combo damage is moderate. Items, face enchanting, and passives make combos devastating. A base Pair does 12 damage; an enchanted Pair with the right passive might do 40+.

## 9. Step 5 — Damage Resolution

* The best combo from the committed dice is automatically detected
* Damage is applied to the **chosen adjacent enemy**
* If multiple enemies are adjacent, the player chooses the target
* Overkill damage is **wasted** — it does not splash to other enemies (cleave = item territory)
* Character affinity bonus applies if the combo matches the character's affinity type

## 10. Step 6 — Enemy Turns

After the player's turn, each living enemy acts:

* **Movement:** Each enemy rolls its own movement dice and moves toward the player (BFS pathfinding)
* **Attack:** If adjacent to the player, the enemy rolls its attack dice. The result IS the damage (enemies always hit)
* **AI Behavior:** Each enemy type has a behavior pattern (Aggressive, Ranged, Stationary, etc.)

## 11. Combat Lock & Opportunity Attack

* If the player is **adjacent to an enemy**, they are **in combat**
* They can still move away by picking movement dice — **but they cannot skip the Generala phase** (must commit or waste dice)
* Enemies that are adjacent attack on their turn regardless

### Opportunity Attack (Ataque de Oportunidad)
When either the player or an enemy **leaves the adjacency range** of the other:
* **Both roll 1d6** — the result is direct damage to the one escaping
* This is a **base mechanic**, not an item — escaping always has a cost
* Creates a real decision: stay and combo, or escape and eat mutual 1d6 damage
* **Smoke Bomb item** negates the enemy's opportunity attack (player still deals theirs)

## 12. What is NOT in the Core (Added via Items)

The following mechanics are NOT part of the base system.
They exist as items, passives, and build modifiers:

| Mechanic | How it enters the game |
|----------|----------------------|
| **Defense / Damage reduction** | Items: Shield, Armor, etc. |
| **Lifesteal / Healing** | Items: Vampiric Dice, Potion, etc. |
| **Evasion** | Items: Dodge Cloak, etc. |
| **Ranged attack** | Items: Bow, Wand, etc. |
| **Hit & Run** (move after attack) | Items: Escape Boots, etc. |
| **Negate Opportunity Attack** | Items: Smoke Bomb (enemy doesn't roll 1d6 on escape) |
| **Cleave** (damage all adjacent) | Items: Whirlwind Blade, etc. |
| **Split Movement** (move in 2 paths) | Items: Shadow Step, etc. |
| **Extra dice** | Items: add dice to bag beyond starting set |
| **Combo modifiers** | Items: change combo multipliers, add effects to combos |
| **Face Enchanting** | Shop service / item |
| ~~**Craps Mode**~~ | **MOVED TO CORE** — base mechanic, available when energy bar is full |
| **Safe flee from combat** | Items: Smoke Bomb (negate enemy opportunity attack) |

> **Design philosophy:** The base game is "roll all dice, split between move and attack, Generala for damage." Items BREAK these rules in interesting ways.

---

# Game Flow

## Run Structure
```
Run (3-4 floors)
 └─ Floor 1 (8-14 rooms, 1-2 enemies per room, no archers)
 └─ Floor 2 (8-14 rooms, 1-3 enemies per room, archers introduced)
 └─ Floor 3 (8-14 rooms, 1-3 enemies per room, harder tiers)
 └─ Floor 4 [optional] (8-14 rooms, 2-3 enemies, elites)
 Each floor ends with Boss → Reward screen → Next floor
```

## 1. Pre-Run Setup

### Character Selection [TBD]
* D&D-inspired classes: Warrior, Mage, Rogue
* Each defines: base HP, starting Dice Bag, power budget, class passive, affinity combo
* Prototype: single base character with fixed stats

## 2. Floor Generation
* Procedural room matrix (Isaac-style)
* 8-14 rooms connected in a grid
* Room types: Combat (majority), Shop (1), Boss (1), Sacrifice (rare)
* Minimap visible in HUD showing discovered rooms and adjacent outlines
* Only Shop (T) and Boss (B) show icons on minimap

## 3. Exploration Phase
Outside combat, the same Pick & Roll turn system applies:
* Roll all dice at turn start → pick movement dice → move on grid
* If no enemy is adjacent after moving, Generala phase is skipped (nothing to attack)
* Enemies in the room move toward the player each turn (each has its own movement dice)
* **Enemies only activate when the player is in the same room** — enemies in other rooms are frozen
* Combat triggers naturally when player ends adjacent to an enemy (or enemy moves adjacent)
* Player can move toward doors freely as long as no enemy is adjacent

## 4. Combat State
* Player enters combat when an enemy is adjacent
* **While in combat**, the player can still move away using movement dice, but **Opportunity Attack triggers** (both roll 1d6 — mutual damage)
* Enemies that are adjacent attack on their turn regardless
* Smoke Bomb item negates the enemy's opportunity attack on exit

## 5. Multi-Enemy Combat
When the player is adjacent to multiple enemies:
* Player chooses which enemy to target with their Generala combo
* ALL adjacent enemies attack the player on their respective turns
* Combat ends when all enemies in the room are defeated
* Priority decision: kill the highest-DPS enemy first, or the lowest-HP one?

## 6. Enemy Persistence
* Leaving a room without clearing it: enemies retain their HP on return
* Enemy positions randomize on re-entry
* Dead enemies don't respawn

## 7. Floor Transition (Post-Boss)
After defeating a floor's boss:
1. **Boss drop** — 1 random item from the boss loot pool
2. **Floor stats screen** — rooms cleared, enemies killed, gold earned, items acquired
3. **Automatic transition** to next floor
4. Run ends after defeating the final boss (floor 3 or 4)

---

# Dice Bag — The Inventory

## Overview
The Dice Bag is the player's loadout — their build, their identity, their toolkit. All dice in the bag are rolled every turn and split between movement and Generala.

## Power Budget System
Each die has a **power cost**. The player's total dice cannot exceed their **Power Budget** (Hollow Knight charm notch system).

| Die Type | Power Cost | Face Range |
|----------|-----------|------------|
| d6 | 1 | 1-6 |
| d8 | 2 | 1-8 |
| d10 | 3 | 1-10 |
| d12 | 4 | 1-12 |

**Starting budget: 8** (Warrior default: 4×d6 + 2×d8 = 4×1 + 2×2 = 8)

## Dice Bag Rules
* All dice in the bag are rolled every turn — no "equipping" individual dice
* More dice = more options for movement AND Generala
* Higher dice (d10, d12) = higher face values for movement AND higher combo numbers
* Items can add dice beyond the starting set (increasing the pool)
* Items can increase the Power Budget (allowing bigger dice or more dice)
* The bag is built during the run through shops, loot, and rewards

## Build Examples
| Build | Dice | Budget | Strategy |
|-------|------|--------|----------|
| Starter | 4×d6 + 2×d8 | 8 | Balanced, good for Pairs and Three of a Kind |
| Speed | 6×d6 | 6 | Many low dice = easy to find movement + still form combos |
| Power | 2×d6 + 2×d10 | 8 | Fewer dice but higher values = massive combos, harder to move |
| Full | 4×d6 + 2×d8 + 1×d10 | 11 | Needs budget increase item, 7 dice = very flexible |

---

# Special Dice
Beyond standard numbered dice, some dice found during a run have built-in special effects that trigger on specific results. Examples:
* A die that deals fire damage on its highest face
* A die that heals the player when it rolls a 1
* A die that boosts adjacent dice values when used in a combo

Special dice follow the same Power Budget rules and are rolled with all other dice. They can be used for movement OR Generala. Specific special dice types [TBD].

---

# Face Enchanting (Item/Shop Territory)

Face enchanting has been moved from a core mechanic to an **item/shop service**. Available when the player finds/buys the appropriate item or visits a shop that offers the service.

| Enchantment Type | Effect |
|-----------------|--------|
| Value increase | +1 to +3 to a specific face |
| Value set | Set a face to a specific value |
| Face removal | Remove a face (fewer faces = higher avg values) |
| Damage multiplier | That face deals increased damage in combos |
| Special effect | Unique effect on that face (poison, lifesteal, shield, etc.) |

> **Balatro parallel:** Face enchanting is how base combos become devastating. An enchanted trio of 7s does far more than a base trio of 5s.

Specific enchanting costs, availability, and balance [TBD].

---

# Items System (Isaac Model)

## Overview
Items are the complexity layer. The base game is intentionally simple — items BREAK the rules in interesting ways. Every item should make the player feel like they're "cheating" the system.

**Critical design note:** Since Pick & Roll has **no base defense**, items that provide defense, healing, and damage reduction are ESSENTIAL to survive multi-room floors. This creates strong demand for the shop and makes every item drop meaningful.

## Item Sources
* Shop rooms (purchased with Gold)
* Combat rewards (random drop on enemy defeat — not guaranteed)
* Sacrifice rooms (trade HP for random item — **blind**, player doesn't see what they'll get)
* Boss drops (1 random item from boss loot pool — guaranteed)

## Item Categories

### Dice Items
Modify the dice themselves or add new ones.
* Example: Extra d6 — add a d6 to your bag (+1 power cost)
* Example: Loaded Die — one die always shows its max face (but costs 2× power)
* Example: Chameleon Die — this die copies the value of any adjacent die in your combo

### Defense Items
Add damage reduction or blocking that doesn't exist in base.
* Example: Wooden Shield — reduce incoming damage by 2
* Example: Iron Armor — first hit each room deals 0 damage
* Example: Dodge Cloak — 25% chance to evade any attack entirely
* Example: Pair Shield — when you roll a Pair, gain shield equal to pair value

### Healing Items
Restore HP — the only way to heal.
* Example: Potion — heal 15 HP (consumable, limited uses per floor)
* Example: Vampiric Dice — Generala combos heal 10% of damage dealt
* Example: Lucky Clover — heal 5 HP when you roll a Straight or better

### Movement Items
Break movement restrictions.
* Example: Escape Boots — after attacking, move 2 tiles free
* Example: Shadow Step — split movement into 2 separate paths
* Example: Magnetic Boots — +1 tile to movement total each turn

### Combat Items
Modify attack rules.
* Example: Bow — attack enemies up to 3 tiles away (ranged)
* Example: Whirlwind Blade — Generala damage hits ALL adjacent enemies (cleave)
* Example: Shadow Dagger — on kill, teleport to an adjacent tile of the killed enemy
* Example: Berserker Axe — +50% combo damage when below 30% HP

### Combo Modifier Items
Change how Generala combos work.
* Example: Full House Ring — Full House multiplier increased from 35+sum to 50+sum
* Example: Pair Specialist — Pairs deal ×2 instead of ×1.5
* Example: Straight Flush — if your Straight uses all same-type dice, double damage
* Example: Lucky Seven — any die showing 7 counts as wild (matches any value)

### Passive Items
Always-on modifiers.
* Example: Gold Magnet — enemies drop 50% more gold
* Example: Scout — see enemy HP and intended action before your turn
* Example: Thick Skin — +10 max HP

### Special / Rule-Breaking Items
Items that fundamentally alter the game's rules.
* Example: Time Loop — if your Generala phase results in High Die only, reroll all free
* Example: Battle Frenzy — roll your dice twice this turn, pick the better set
* Example: Combo Chain — if you get Three of a Kind or better, gain a bonus attack

### Prototype Items (5 — Confirmed)

These 5 items are the minimum for the prototype. All live in the dice/combo space (Balatro joker philosophy):

| Item | Category | Effect | Price | Why it exists |
|------|----------|--------|-------|---------------|
| **Pair Specialist** | Combo Modifier | Pair multiplier ×2.5 instead of ×1.5 | 30g | Makes the most common combo viable. Build-around: many same-type dice |
| **Loaded Die** | Dice Modifier | One die always shows its max face. Costs 2× power budget | 35g | Guarantees a high die for movement OR combo. Trade-off: expensive in power budget |
| **Reroll Token** | Dice Modifier | +1 extra reroll in Generala phase (4 rerolls instead of 3) | 25g | Pure control. More chances to build combos. Simple, always useful, never broken |
| **Combo Chain** | Combo Modifier | If you roll Three of a Kind or better → roll 1 bonus die as extra damage (face value = bonus damage) | 40g | Rewards strong combos with bonus damage. Moderate snowball — it's 1 die, not a full Generala |
| **Smoke Bomb** | Escape Modifier | When leaving adjacency, negate the enemy's Opportunity Attack 1d6 (you still deal yours) | 20g | Makes escape a viable tactic. Without it, fleeing always costs HP |

**Synergy example:** Pair Specialist + Loaded Die in a 6×d6 bag → Loaded Die always shows 6, remaining 5 dice have high Pair chance → consistent ×2.5 Pair damage.

Full item list [TBD — need at least 20 items for vertical slice].

---

# Passives System

## Overview
Throughout the run, the player acquires passive abilities from shops, combat rewards, or special rooms. Passives are always active and modify how dice, combos, movement, or defense behaves.

## Examples
* Increase combo damage multipliers
* Grant extra gold per fight
* Add effects when certain conditions are met (on Pair, on Generala, on kill)
* Modify dice pool or Power Budget

Passives stack and interact with each other and with special dice, creating potential for powerful synergy chains. Specific passives [TBD].

---

# Characters — Classes and Builds

## Overview
D&D-inspired classes, each pushing toward a different playstyle without forcing a single build.

## Classes

| Class | Starting Dice | Power Budget | HP | Affinity Combo | Class Passive |
|-------|--------------|-------------|-----|----------------|--------------|
| Warrior | 4×d6 + 2×d8 | 8 | 100 | Full House (+20% dmg) | [TBD] |
| Mage | 2×d6 + 3×d8 | 8 | 80 | Straight (+20% dmg) | [TBD — e.g. reroll one extra time per Generala] |
| Rogue | 6×d6 | 6 | 90 | Pair (+30% dmg) | [TBD — e.g. +2 tiles when moving] |

Characters unlock via meta-progression milestones.

**Prototype:** Warrior with fixed stats to test all mechanics.

---

# Enemies and Bosses

## Enemy Design
Every enemy blends classic fantasy + casino theming.

### Known Enemy Archetypes

| Enemy | Tier | Attack Dice | Movement | HP | Behavior | First Appears |
|-------|------|------------|----------|-----|----------|--------------|
| **Croupier Goblin** | Weak | 2×d6 (2-12 dmg) | 1-3 tiles | 40 | Aggressive: moves toward player | Floor 1 |
| **Chip Golem (Orc)** | Strong | 2×d8 (2-16 dmg) | 1-2 tiles | 60 | Aggressive: moves toward player, slow | Floor 1 |
| **Card Archer** | Normal | 1×d6 ranged | 1-2 tiles | 30 | Flee: runs if player is adjacent, attacks from range | Floor 2+ |
| **Living Die** | Normal | [TBD] | [TBD] | [TBD] | [TBD] | Floor 1 |

### Enemy Attack
* Enemies **always hit** — no threshold, no miss chance
* They roll their attack dice and the result is direct damage to the player
* **No damage reduction in base** — the full roll is the full damage (items add defense)

### Enemy Movement
* Each enemy rolls for movement on their turn (1-N tiles based on enemy type)
* Enemies always move toward the player using BFS pathfinding around obstacles
* **Enemies only activate when the player is in the same room** — other rooms are frozen

### Enemy Energy & Enrage
* Each enemy has a separate energy bar (Goblin: max 50, +15/round. Orc: max 40, +12/round)
* When energy reaches max → **Enraged** state
* Enraged attack: **60% chance to deal ×2 damage**, then energy resets to 0
* Creates urgency to kill enemies quickly — leaving them alive is punished
* Already implemented in code (`EnemyState.IsEnraged`, `EnemyEntity.RollAttack`)

### Enemy Count Per Room
* **Floor 1:** 1-2 enemies per combat room
* **Floor 2:** 1-3 enemies per combat room
* **Floor 3+:** 1-3 enemies per combat room (harder tiers, potential elites)

## Boss Design
Bosses are NOT enemies with more health. They have unique mechanics that change combat rules:
* Modifying the player's dice (cursing faces, reducing values)
* Starting with adds (extra enemies already in the room)
* Changing their own stats between phases
* Applying debuffs (reduced Power Budget, cursed dice faces)

Each boss forces the player to adapt their strategy.

### The Dealer (Floor 1 Boss)

**Stats:** 100 HP, 2×d8 attack, speed 1-2
**Room setup:** The Dealer + 2 Croupier Goblins (40 HP each, 2×d6, speed 1-3) already in the room

| Phase | HP Threshold | Mechanic |
|-------|-------------|----------|
| **1** | 100→40 | Normal behavior. The Goblins do the dirty work while The Dealer hits hard. |
| **2** | 40→0 | **Curses 2 player dice:** -2 value for Generala (but full value for movement). Curse lasts 2 turns. Speed increases to 2-3. |

**Tactical decision:** Kill Goblins first (safer but more turns = more Dealer enrage accumulation) or rush The Dealer (risky but ends the fight faster, eating damage from 3 sources).

**Phase 2 curse interaction with Pick & Roll:** Cursed dice are worth -2 in Generala but full value for movement → player is incentivized to use cursed dice for movement and keep clean dice for combos. This is a dice-space mechanic, consistent with the game identity.

Additional boss designs [TBD — need at least 1 more for Floor 2].

---

# Dungeon Structure

## Overview
Procedural dungeons organized in a room matrix (Isaac-style). Each floor has a different casino aesthetic.

## Room Types

| Icon | Type | Description | Frequency |
|------|------|-------------|-----------|
| SWD | Combat | Enemies on grid, turn-based combat | Common |
| BOS | Boss | Unique mechanics, guaranteed item drop | 1 per floor |
| SHP | Shop | Buy items, dice, passives, enchanting | 1-2 per floor |
| SKL | Sacrifice | Lose max HP in exchange for random item (**blind**) | Rare |

## Floor Generation
* 8-14 rooms connected in a grid
* Required rooms: Combat (majority), Shop (1), Boss (1)
* Each room = 8×8 grid with 4-6 fixed obstacles
* Player moves freely between rooms using doors
* Floor ends when boss is defeated

## Minimap
Always visible in HUD. Shows:
* Discovered rooms as filled tiles
* Adjacent undiscovered rooms as outlines
* Door connections as openings between tiles
* Only 2 room types show icon labels: **T** (Shop), **B** (Boss)
* All other rooms appear as blank tiles

---

# Shop System

## Overview
Functional shop room with items distributed on the floor.

## Shop Mechanics
* Items have visible name and price text
* Approaching an item shows full description + buy button
* Purchase with accumulated Gold
* Items not purchased persist if the player leaves and returns
* Shop offers: items, extra dice, face enchanting service, passives

## Price Calibration
* Standard item ≈ reward from 3-4 normal enemies
* Premium item ≈ reward from 5 enemies
* Defense items are slightly more expensive (high demand due to no base defense)

---

# Gold Economy

## Overview
* Enemies drop Gold on defeat — amount varies by tier
* Gold auto-collects to inventory after combat
* Gold total always visible in HUD
* Spent in Shop rooms for items, dice, enchantments, and passives

## Gold Drops by Enemy Tier

| Enemy Tier | Gold Drop Range |
|------------|----------------|
| Weak | 3-7 gold |
| Normal | 7-13 gold |
| Strong | 12-18 gold |
| Boss | 40-60 gold |

---

# Life System

## Overview
Life Points (HP) are the core health resource. Reaching 0 HP ends the run immediately.

## Damage Sources (Base)
* Enemy attacks (die result = direct damage, **no reduction in base**)
* Sacrifice rooms (trade HP for power)

## Healing Sources
* Items only (Potion, lifesteal effects, passive abilities)
* Cannot exceed maximum HP
* **There is no base healing** — the player must find items to heal

> **Balance note:** Without defense items, the player takes ~50 damage per 3-enemy room. With 100 HP, they can survive ~2 rooms before needing healing or defense items. This creates strong pressure to visit shops and take calculated risks in sacrifice rooms.

## Player Stats

| Stat | Function |
|------|----------|
| **HP (Vida)** | Total life points. 0 = death |
| **Dice Bag** | All dice rolled each turn, split between move/attack |
| **Power Budget** | Maximum total dice power cost |
| **Affinity Combo** | Character's bonus combo type (+% damage) |

---

# XP / Level Up System
[TBD — Evaluate if intra-run XP/leveling is needed or if items + gold alone provide sufficient progression. To be decided after playtesting the core loop.]

---

# Menu System

## Main Menu
[TBD — Define screens and navigation]

## Pause Menu
[TBD]

## Victory / Defeat Screens
[TBD]

## Pre-Run Setup Screen
[TBD — Character selection + Dice Bag loadout (if unlocked options exist)]

---

# UI & Visualization

## HUD Elements
* **Gold counter** — always visible
* **HP bar** — player health
* **Dice pool** — shows all rolled dice with current values
* **Movement preview** — tiles highlighted when dice are picked for movement
* **Generala panel** — lateral panel showing locked/unlocked dice, roll count, combo preview
* **Enemy HP bars** — visible when in range
* **Enemy intent** — shows what each enemy will do next turn (Slay the Spire style) [TBD]
* **Minimap** — room layout with discovery state

## Combat UI — Pick & Roll Layout
The combat UI stays on the grid (no scene transition):

```
┌──────────────────────────────────────────┐
│  ┌─────────────────┐  ┌───────────────┐  │
│  │                 │  │  GENERALA      │  │
│  │    GRID         │  │  PANEL        │  │
│  │    (always      │  │               │  │
│  │     visible)    │  │  [3][5][6]    │  │
│  │                 │  │  Lock: ☑ ☐ ☑  │  │
│  │  P──→G    O     │  │  Roll 2/3     │  │
│  │         A       │  │  Combo: Pair  │  │
│  │                 │  │  Dmg: 12      │  │
│  └─────────────────┘  └───────────────┘  │
│  ┌─────────────────────────────────────┐  │
│  │  DICE POOL: [4][2][5][3][6][3]     │  │
│  │  MOVE: [drag dice here] = 0 tiles  │  │
│  └─────────────────────────────────────┘  │
└──────────────────────────────────────────┘
```

* **Dice Pool** (bottom): shows all rolled dice. Drag dice to Movement zone.
* **Movement Zone** (bottom): dice placed here are summed for movement.
* **Grid** (left): always visible, shows movement preview, enemies, obstacles.
* **Generala Panel** (right): shows remaining dice for combo, lock/reroll buttons, combo preview, damage preview.

## Visual Hierarchy
Dice > Player > Enemies > Environment

---

# Save Progress
[TBD — Define save system for meta-progression between runs]

---

# Achievements & Rewards
[TBD — Define milestones that unlock new content (Isaac model)]

---

# Dialogue System
[TBD — Boss introductions, shop keeper interactions if any]

---

# Audio & Atmosphere

## Sound Effects (SFX)
* Dice rolling on table (physics-driven)
* Dice landing / settling
* Combo confirmation (satisfying impact, scales with combo tier)
* Generala celebration (special fanfare)
* Damage dealt / received
* Enemy death
* Gold pickup
* UI interactions (dice drag, lock click)

## Music & Ambience
* Casino-themed background music per floor
* Combat intensity layers
* Boss encounter themes
* Shop ambient music

## Visual Effects (VFX)
* Dice physics with bounce and settle
* Combo tier effects (Pair = small flash, Generala = screen explosion)
* Damage numbers scaling with value
* Screen darkening on heavy damage
* Camera shake on big combos
* Enemy death effects (coins burst)
* Movement trail on grid when moving
* Dice glow when locked

---

# Tutorial & Accessibility

## How the Game is Explained to Beginners
* First room teaches: roll dice, pick movement, move on grid
* Second combat teaches: Generala phase, locking, rerolling, combos
* Combo damage chart is always accessible from pause menu
* Tooltips on dice, items, and combos
* Item descriptions explain what rule they break

## Interactive Learning
[TBD — Tutorial room design, progressive mechanic introduction]

---

# Monetization
One-time purchase. No microtransactions. No loot boxes.

---

# Technical & Development

## Core Formulas

### Pick & Roll Turn
```
1. all_dice = DiceBag.RollAll()
2. movement_dice = PlayerPick(all_dice)  // 0 or more dice
3. movement_tiles = Sum(movement_dice.face_values)
4. Player.Move(movement_tiles)  // BFS pathfinding
5. generala_dice = all_dice - movement_dice
6. if Player.IsAdjacentToEnemy():
7.     combo = Generala(generala_dice, max_rolls=3)  // lock/reroll
8.     damage = ComboFormula(combo) + AffinityBonus
9.     target_enemy.TakeDamage(damage)
10. else:
11.     // Generala skipped — wasted dice
```

### Combo Formulas
```
HighDie:        highest_face × 1
Pair:           sum_of_pair × 1.5
TwoPair:        sum_of_both_pairs × 1.2
ThreeOfAKind:   sum_of_trio × 2
Straight:       30 + highest_die
FullHouse:      35 + sum_of_all
FourOfAKind:    sum_of_four × 3
Generala:       sum_of_five × 5
DoubleGenerala: sum_of_six × 8
```

### Enemy Attack
```
roll = sum(random(1, die_max) for each attack_die)
damage = roll (always hits, no reduction in base)
```

### Movement
```
movement_tiles = sum(selected_dice_face_values)
// No separate movement die. Dice are everything.
```

## Item Formula Templates
These formulas activate only when the corresponding item is equipped:

### Bow (Ranged Attack Item)
```
// Allows attacking enemies up to N tiles away
// Still uses Generala combo for damage
range = item.range  // e.g., 3 tiles
if distance(player, target) <= range:
    can_attack = true
```

### Potion (Healing Item)
```
heal_amount = 15  // fixed per potion tier
player.HP = min(player.HP + heal_amount, player.MaxHP)
```

### Pair Shield (Defense Item)
```
// When player rolls a Pair during Generala:
if combo_type == Pair:
    shield = pair_value  // e.g., pair of 5s = 5 shield
    // Shield absorbs that much damage from next hit
```

---

# Reference Games

| Game | What We Take | What We DON'T Take |
|------|-------------|-------------------|
| **The Binding of Isaac** | Procedural dungeons, meta-progression, item-breaks-rules philosophy, extreme variety | Reflex dependency, bullet hell |
| **Balatro** | Build depth, addictive loop, casino aesthetic, base hands weak + builds make them strong | Lack of exploration, static gameplay |
| **Slay the Spire** | Visible enemy intent, deck/hand as resource, strategic decision depth | Card system (we use dice) |
| **Dicey Dungeons** | Dice as combat resource, dice assignment to slots, accessibility | Consumable dice (ours are permanent) |
| **XCOM 2 / Fire Emblem** | Grid tactics, positioning matters, probability-based decisions | Real-time, complex unit management |
| **For the King** | Focus points as scarce control resource, party dice mechanics | Party system |
| **Crypt of the Necrodancer** | Grid movement, tile-based combat, pacing | Music synchronization |

---

# Unique Selling Points

| # | USP | Why It Matters |
|---|-----|---------------|
| 1 | Dice are EVERYTHING | They're not just RNG — they're your movement, your attack, your build, your identity. Every die has dual purpose. |
| 2 | Pick & Roll tension | Every turn starts with THE decision: which dice move you, which dice fight for you? |
| 3 | Generala on a grid | Classic Yahtzee combo-chasing meets tactical positioning. No other game combines these. |
| 4 | Simple core, items transform | Roll, pick, move, combo. Items add defense, range, cleave, healing — Isaac philosophy. |
| 5 | Pure strategy, no reflexes | Anyone can play. Difficulty is thinking, not aiming. |
| 6 | Deep build-crafting | Special dice, items, enchanting, passives that chain. Every run builds differently. |
| 7 | Real casino tension | Every Generala phase is a gamble. Lock or reroll? Chase the Full House or settle for Three of a Kind? |

---

# Design Philosophy — One Line

> Roll all your dice. Pick which ones move you. Use the rest for Generala. Items break the rules. Every die is a decision.

---

# Game Identity Pillars

1. **Dice are EVERYTHING** — They're not RNG, they're the build, the inventory, the identity. Every die serves movement OR attack.
2. **Simple core, items break rules** — Base = roll, pick, move, combo. Items add defense, range, healing, cleave. Isaac philosophy.
3. **Pick & Roll tension** — Every turn starts with THE split decision. Sacrifice dice for position or keep them for damage?
4. **Generala chasing** — Lock, reroll, chase the combo. Balatro feeling: base combos are okay, builds make them devastating.
5. **Strategy over reflexes** — Pure turn-based. The difficulty is thinking.
6. **Roguelite progression** — Each run is unique: dice, items, passives, rooms.

---

# Open Questions (Pending Team Discussion)

## Critical
1. ~~**Combat system** — threshold vs Generala~~ **RESOLVED: Pick & Roll** (Generala with movement dice split)
2. ~~**Dice Bag role**~~ **RESOLVED: Power Budget system**, all dice rolled every turn
3. **Kiting** — Can the player infinitely outrun enemies? Do enemies need anti-kiting acceleration? [TBD — playtest first]
4. **Item pool** — Need at least 20 items for prototype, especially defense items (no base defense = critical)

## Important
5. **Character stats** — Warrior/Mage/Rogue starting dice, HP, affinity, passive (Warrior defined, others TBD)
6. **Boss mechanics** — At least 1 boss with unique rules for prototype
7. **Grid migration** — When to switch from 4-cardinal to hexagonal?
8. ~~**Enemy energy/enrage**~~ **RESOLVED: keep** (already coded, adds urgency)
9. ~~**Craps Mode**~~ **RESOLVED: core mechanic** — available to all players when energy bar reaches 100. Opt-in bet before rolling. Gambler's Token eliminated.

## Nice to Have
10. **Floor themes** — Casino aesthetic variants per floor (art team)
11. **Sacrifice room** — Exact HP cost and item pool
12. **Meta-progression** — What unlocks between runs?
13. **Save system** — Technical approach for meta-progression persistence

---

# Prototype Scope (v0.1)

## What Must Work
* Single character (Warrior) with: 100 HP, 4×d6 + 2×d8, Power Budget 8
* **Pick & Roll turn system**: roll all → pick movement → move → Generala → damage
* Grid-based movement with dice-as-movement (4-cardinal adjacency)
* Rooms with fixed obstacles (8×8 grid, 4-6 obstacles)
* Full Generala phase: lock dice, reroll up to 3 times, 9 combo types with damage formulas
* Combo detection: HighDie, Pair, TwoPair, ThreeOfAKind, Straight, FullHouse, FourOfAKind, Generala, DoubleGenerala
* Grid always visible during combat (lateral panel for Generala)
* Enemy AI: Aggressive (BFS toward player), Flee (maintains distance)
* Enemy types: Goblin (2×d6, 40HP), Orc (2×d8, 60HP), Archer (1×d6 ranged, 30HP)
* Multi-enemy combat: choose target, all adjacent attack
* Combat lock (adjacent enemies attack regardless)
* Opportunity Attack: both roll 1d6 when leaving adjacency (base mechanic)
* Enemy persistence across room visits
* Enemies only active in player's current room
* Procedural floor generation (8-14 rooms: combat, shop, boss)
* 1-3 enemies per combat room (scaling with floor)
* Shop with items, prices, and gold economy
* Gold drops by tier (Weak: 3-7, Normal: 7-13, Strong: 12-18, Boss: 40-60)
* Minimap with discovery and room type icons (T=Shop, B=Boss)
* Floor transition: boss drop + stats screen + next floor
* 5 prototype items: Pair Specialist, Loaded Die, Reroll Token, Combo Chain, Smoke Bomb
* Boss fight: The Dealer (100 HP, 2 phases, 2 Goblin adds, dice curse mechanic)
* Player energy bar (0-100) + Craps Mode (bet on combo before rolling, core mechanic)

## What Can Wait
* Multiple character classes
* Special dice with unique effects
* Full face enchanting system
* Full passive system (20+ passives)
* Sacrifice rooms
* Hexagonal grid migration
* Meta-progression / unlock system
* Additional boss designs (Floor 2+)
* Audio and VFX polish
* Save system
* Enemy energy/enrage system
* Full 20+ item pool
