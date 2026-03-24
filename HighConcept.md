# High Concept Document

*Document v0.1 — UADE 2026*  
*Team: Gabriel Guerrero, Franco Delocca, Franco N., Santiago Bocco \+ Maine, Sebiche*

## Working Title

**\[Untitled\]** — Dice-Based Roguelite Dungeon Crawler

## One-Line Concept

A turn-based roguelite where dice are your inventory, your weapon, your movement, and your defense: build your dice loadout, explore procedural dungeons, and master the Yahtzee-style combo system to survive.

## Elevator Pitch

Imagine the strategic depth of **Balatro** — where every card matters and every build counts — but inside the procedural dungeons of **The Binding of Isaac**. Instead of cards, your tools are **physical dice** that you collect, enchant face by face, and roll to attack, defend, and move. Combat uses a Yahtzee-style system: three rolls, hold dice, build combos. The better your combo, the more damage you deal. But careful: rolls spent attacking are rolls you don't have for defense. **Every decision matters. Every die counts.**

## Fact Sheet

| Field | Detail |
| :---- | :---- |
| **Genre** | Roguelite dungeon crawler, turn-based |
| **Perspective** | Fixed isometric |
| **Engine** | Unity 6.3 (C\#) |
| **Platform** | PC (itch.io / Steam) |
| **Controls** | Mouse (grid click \+ dice drag). |
| **Target Audience** | Strategy gamers, roguelite fans, tabletop gaming community |
| **Run Duration** | \~1 hour 40 minutes |
| **Team** | 6 people — Game Development B.S. (UADE) |
| **Stage** | Concept / Pre-Prototype |

## Why This Game

### Market Opportunity

* **Underserved niche**: no roguelite combines physical dice as a permanent build system with dungeon exploration. Dicey Dungeons uses dice as consumable resources. Dice A Million nails the dice game feel but has no exploration or spatial gameplay.  
* **Genre momentum**: Balatro (2024) proved that "tabletop game \+ roguelite" is a formula with massive demand — it sold millions in its first month.  
* **The gap between Isaac and Balatro**: Isaac has exploration but requires reflexes. Balatro has strategy but no exploration. Our game bridges both halves.  
* **Yahtzee accessibility**: the Yahtzee-style dice combo system is universally known and easy to learn. In Argentina (where the team is based), the local variant "Generala" is played from childhood, making the entry barrier extremely low.

## Game Feel & Visual Feedback

The dice are the soul of the game. Every interaction with them must produce **immediate, satisfying visual and audio feedback**. The sensation of chance and betting must be constant, while strategy remains the real challenge underneath.

**Core feel principles:**

* **Every roll has impact** — dice hit the table with physics, bounce, settle. The screen reacts. Numbers pop. There is never a "dead" moment where you roll and nothing happens visually.  
* **Combos escalate visually** — a Pair gets a subtle glow. A Three of a Kind gets a flash. A Four of a Kind shakes the screen. A Yahtzee is a full visual explosion. The better the combo, the bigger the spectacle.  
* **Victory and defeat are felt, not read** — landing a devastating combo fills the screen with effects and the enemy reacts. Taking heavy damage darkens the screen, shakes the camera. The player should feel the weight of every outcome through visuals, not text boxes.  
* **The gambling tension never stops** — every decision to hold or reroll should feel like pushing chips across the table. The UI, animations, and pacing must reinforce that you're always betting on something.  
* **Numbers must feel satisfying** — damage numbers should scale visually with their value. A hit for 3 is small. A hit for 50 is big and loud. Chain effects and multipliers from special dice and passives should cascade visually so the player sees their build paying off.

## Core Mechanic — Combat System

| ACTION   \-\> The player grabs their dice, throws them into the cup, sees the result FEEDBACK \-\> Dice roll with physics, combos light up on screen REWARD   \-\> Lands a Four of a Kind, deals massive damage, enemy explodes into coins REPEAT   \-\> New turn, new decisions |
| :---- |

### Combat Flow (per turn)

| PLAYER TURN | \\+-- FLEE OPTION (available any turn during combat) |   \\+-- Press Flee button in HUD |   \\+-- Roll flee die \+ Speed → convert to flee success % |   \\+-- Random 1-100 check |   \\+-- SUCCESS → pay 10% max HP → auto-roll movement die → move away from enemies |   \\+-- FAIL    → turn consumed, no damage penalty \+-- ATTACK PHASE (3 rolls, Yahtzee-style) |   \+-- 1st Roll \-\> Roll all your dice |   \+-- Hold \-\> Keep the ones you want |   \+-- 2nd Roll \-\> Reroll the rest |   \+-- Hold \-\> Adjust selection |   \+-- 3rd Roll \-\> Final result | \+-- BUILD COMBO \-\> Select which dice go to attack | \+-- DEFENSE PHASE \-\> Rolls NOT used for attack \= defense rolls |   \+-- Reroll and build defensive combos that block damage | \+-- CORE STRATEGIC DECISION:     Spend all 3 rolls attacking (max damage, zero defense)?     Attack on roll 1 and save 2 for defense?     Go for a risky combo or secure a smaller one? ENEMY TURN | \+-- Rolls ONCE (fast pacing, no long waits) \+-- Deals damage \-\> Player absorbs with their defense |
| :---- |

Player Turn Actions — Exploration Phase  
Outside of combat, the player's turn offers **three mutually exclusive actions**. Choosing any   
one of them ends the turn and allows enemies to move.

|  Action How It Works Move Roll the speed die → move that many tiles on the grid Use Active Item (Bow) Select a target tile within a 5x5 area → roll d20 \+ Dexterity → convert to hit chance % → random 1-100 check → HIT or MISS Use Potion Roll a die \+ Dexterity → result as % of max HP healed (no fail — always heals something) Note: The Bow and Potion are not available during turn-based combat — only during dungeon exploration. |
| ----- |

Active Items

| Bow The player selects a tile within a 5×5 area centered on their position. Hit resolution: 1\. Roll d20 → result between 1 and 20 2\. Add Dexterity stat → total result 3\. Convert to hit chance: \`(result / (20 \+ Dexterity\_max)) × 100\` 4\. Roll random 1–100 → if ≤ hit chance: HIT → deal bow damage. If \>: MISS Potion Roll a die (proposed: d10) → add Dexterity. No fail state — always heals: \`heal % \= (result / (die\_max \+ Dexterity\_max)) × 100\` applied to max HP. Single use per run. Refilled in Potion rooms. |
| :---- |

###  Combo Table

| Combo | Description | Damage |
| :---- | :---- | :---- |
| **High Die** | No combo — highest die value | Minimum |
| **Pair** | 2 matching dice | Low |
| **Three of a Kind** | 3 matching dice | Medium |
| **Full House** | Three of a Kind \+ Pair | Medium-high (fixed) |
| **Straight** | Numerical sequence | Medium-high (fixed) |
| **Four of a Kind** | 4 matching dice | High |
| **Yahtzee** | 5 matching dice | Maximum |
| **Double Yahtzee** | 2nd Yahtzee in the same run | Devastating bonus |

Exact damage values and probabilities to be defined in the GDD balance section.

## Dice Bag — The Inventory

The inventory is not a backpack: it's a **dice bag** with a fixed budget of **dice power** (inspired by Hollow Knight's charm notch system).

Each die occupies space according to its size:

* **D2, D3, D4, D6, D8** — 1 slot each  
* **D10, D12** — 2 slots each  
* **Maximum capacity**: 5 dice

The player builds their loadout within this limit, creating trade-offs between many small dice (more combo chances) vs few large dice (higher damage potential).

### Special Dice

Beyond standard numbered dice, some dice found during a run can have **built-in special effects** that trigger on specific results. These are D\&D-style dice with unique properties — a die might deal fire damage on its highest face, heal the player when it rolls a 1, or boost adjacent dice values. Special dice follow the same slot rules but add a layer of synergy and build-crafting on top of the Yahtzee combo system. Specific special dice types \[TBD\].

### Passives

Throughout the run, the player can acquire **passive abilities** from shops, combat rewards, or special rooms. Passives are always active and modify how dice, combos, or combat mechanics behave — for example, increasing damage for a specific combo type, granting extra gold per fight, or adding effects when certain conditions are met. Passives stack and interact with each other and with special dice, creating the potential for powerful synergy chains. Specific passives \[TBD\].

### Face Enchanting

In shops or special rooms, the player can **enchant a specific face** of any die:

| Enchantment Type | Effect |
| :---- | :---- |
| Damage multiplier | That face deals increased damage |
| Gold multiplier | That face gives bonus coins |
| Number swap | Replaces the face value with a different number |
| Face removal | Removes that face from the die (fewer faces \= higher chance of remaining values) |
| Special effect | Unique effect when landing on that face (poison, lifesteal, shield, etc.) |

This creates a **unique customization layer**: two D6s are never the same if enchanted differently.

## Craps Mode — The Super Bet

An energy bar that fills during combat. When activated:

| The game asks: "What combo will you roll?" | \+-- You pick a combo (pair, three of a kind, four of a kind, etc.) \+-- You roll the dice | \+-- YOU NAILED IT \-\> Massive damage bonus \+ special effect \+-- YOU MISSED   \-\> Penalty (debuff, reduced damage, lost turn) |
| :---- |

This is the most "casino" mechanic in the game: **high risk, high reward**. Maximum tension moment. A player who masters it can break impossible fights.

## Core Loop — 3 Layers

| \+==============================================================+ |  MICRO LOOP — Combat                            | |  Roll dice \-\> Hold \-\> Reroll \-\> Build combo \-\> Attack         | \+==============================================================+ |  MACRO LOOP  — The floor                      | |  Explore rooms \-\> Fight \-\> Loot \-\> Shop \-\> Boss               | \+==============================================================+ |  META LOOP (between runs) — Progression                      | |  Unlock characters \-\> New dice \-\> New passives                | \+==============================================================+ |
| :---- |

### Micro Loop — Combat

The player rolls, holds, builds combo, decides attack vs defense. **It should feel like playing Yahtzee with friends but with consequences.**

### Macro Loop — Floor

Explore a procedural dungeon on an isometric grid. Movement via speed die. Choose which rooms to visit: combat (XP/loot), shop (upgrade build), craps (gamble), sacrifice (risk/reward), potion (refill active item). The floor ends with a **boss with unique mechanics**.

### Meta Loop — Progression (between runs)

Isaac model. Each run unlocks new content by completing specific milestones: defeating certain bosses, completing runs with specific constraints, reaching certain achievements. The unlock system ensures every run has value — win or lose.

Gold Economy  
Enemies drop Gold on defeat — no dice upgrades as direct drops. Gold accumulates during the run and is spent in Shop rooms. Gold total is always visible in the HUD.

## Characters — Classes and Builds

D\&D-inspired classes, each pushing toward a different playstyle **without forcing a single build**:

| Class | Playstyle |
| :---- | :---- |
| **Warrior** |  |
| **Mage** |  |
| **Rogue** |  |

Each class defines:

* **Base stats**: HP, speed, Dexterity  
* **Speed die**: determines movement range on the grid (min/max tiles)  
* **Class passive bonus**: a unique rule that differentiates gameplay beyond raw stats (e.g. Warrior gets \+1 base defense, Mage can reroll one extra die per combat, Rogue has increased flee success rate)

Specific stats per class \[TBD\]. Characters unlock via **meta-progression** by completing milestones across runs.

## Dungeon Structure

Procedural dungeons organized in a **room matrix** (Isaac-style). Each floor has a different casino aesthetic (specific themes TBD by art team).

| Icon | Type | Description | Frequency |
| :---- | :---- | :---- | :---- |
| SWD | **Combat** | Enemies on grid, turn-based combat | Common |
| BOS | **Boss** | Unique moveset, passives, debuffs | 1 per floor |
| SHP | **Shop** | Buy dice, enchant faces, acquire passives | 1-2 per floor |
| CRP | **Craps** | Bet on combo, bonus or penalty | Rare |
| SKL | **Sacrifice** | Lose max HP in exchange for power | Rare |
| POT | **Potion** | Refills the player's active potion item | Rare |

Minimap  
A minimap is visible at all times in the HUD. It reveals rooms as the player discovers them, and shows adjacent undiscovered rooms as outlines. Only three room types display an icon label:

| \* T — Shop \* B — Boss   \* P — Potion |
| :---- |

All other rooms appear as blank tiles. Door connections between rooms are shown as openings in the minimap tiles.

##  Enemies and Bosses

### Enemy Design

Every enemy blends **classic fantasy \+ casino theming**. The team has identified three enemy archetypes so far:

* **Croupier Goblin** — goblin \+ blackjack dealer aesthetic  
* **Living Die** — a sentient die creature  
* **Chip Golem** — stone golem made of casino chips  
* **Ranged Archer** — maintains minimum distance from the player and attacks with a percentage-based hit roll. If the player gets too close (1×1 tile), the Archer flees to restore optimal range on its next turn.

Each enemy has its own movement die and attack pattern. Specific stats, movement dice per enemy type, and additional enemy designs \[TBD\].

### Boss Design

Bosses are NOT enemies with more health. They have **unique mechanics that change the rules of combat**: disabling combos, stealing dice, applying debuffs, or altering how the Yahtzee system works for that fight. Each boss forces the player to adapt their strategy.

Double Combat

When the player enters combat with one enemy and a second enemy is present in the room, the second enemy advances **1 tile per full turn cycle** (player attack \+ defense \+ enemy attack) toward the player regardless of its type. When it reaches the player, **double combat activates**:

* The player chooses which enemy to target each attack turn  
* Both enemies attack the player on their respective turns  
* Combat only ends when **both enemies are defeated**

Flee Mechanic

A **Flee button** appears in the combat HUD at all times. Resolution: 

	1\. Roll flee die (proposed: d10) \+ Speed stat → convert to flee success %  
2\. Random 1–100 check → SUCCESS or FAIL  
3\. **SUCCESS**: player pays 10% of max HP → movement die auto-rolls → player moves away from enemies → exits combat state  
4\. **FAIL**: turn consumed, no HP penalty

Door Forcing

If the player stands on a door tile, on their next turn a **Force Door** option appears in the HUD:  
	  
	1\. Roll a die (proposed: d10) \+ Dexterity → convert to success %  
2\. Random 1–100 check → SUCCESS or FAIL  
3\. **SUCCESS**: player passes to the next room. Enemies in the current room remain alive but lose a fixed portion of their HP (proposed: 25%)  
4\. **FAIL**: turn consumed, player stays in current room

##  Visual Direction

### Style: Pixel-Poly (3D Low Poly \+ Pixel Art Shader)

| Criterion | Our Choice | Justification |
| :---- | :---- | :---- |
| **Style** | Low-poly 3D \+ pixel art post-process shader | Fast to produce, unique aesthetic, ideal for a 6-person team |
| **Perspective** | Fixed isometric, no camera rotation | Simplifies art and development, focuses on tactical grid |
| **Visual exception** | Dice rotate with real physics when rolled | They are the visual protagonist, deserve special treatment |
| **Palette** | Dark tones \+ casino neons (table green, dice red, chip gold) | High contrast \= readability. Casino atmosphere without being generic |
| **Visual hierarchy** | Dice \> Player \> Enemies \> Environment | The player always knows where to look |

Each floor has a distinct casino-themed aesthetic. Specific floor themes and palettes \[TBD by art team\].

## Target Audience

**Primary**: PC gamers (18-35) who enjoy strategy roguelites and digital tabletop games. They know Balatro, Isaac, Dicey Dungeons.

**Secondary**: Argentine/Latin American players who have known Generala their whole lives and are drawn to seeing a familiar game transformed into something new.

## Unique Selling Points — 5 USPs

| \# | USP | Why It Matters |
| :---- | :---- | :---- |
| 1 | **Dice are EVERYTHING** | They're not just RNG — they're your inventory, your build, your identity. No other roguelite does this. |
| 2 | **Yahtzee as combat** | A universally known mechanic transformed into a deep combat system. Low entry barrier, high skill ceiling. |
| 3 | **Pure strategy, no reflexes** | Anyone can play. The difficulty is thinking, not having good aim or timing. |
| 4 | **Deep build-crafting** | Face enchanting, special dice with unique effects, and passives that chain together. Every run builds differently. |
| 5 | **Real casino tension** | Craps Mode, casino aesthetic, visceral visual feedback on every roll. The screen tells you if you're winning or dying. |

## Reference Games

| Game | What We Take | What We DON'T Take |
| :---- | :---- | :---- |
| **Balatro** | Build depth, addictive loop, casino aesthetic, combo system | Lack of exploration, static/linear gameplay |
| **The Binding of Isaac** | Procedural dungeons, meta-progression, extreme variety | Reflex dependency, bullet hell |
| **Crypt of the Necrodancer** | Grid movement, tile-based combat, pacing | Music synchronization |
| **Dicey Dungeons** | dice as combat resource, accessibility (note: their dice are consumable per turn, ours are permanent builds) |  |
| **Luck be a Landlord** | Dice/slot combo synergies, gambling loop | Passive gameplay, no player agency per turn |
| **Dice A Million** | Dice game feel, satisfying visual/audio feedback, dice with unique effects, "numbers go up" dopamine, build-crafting depth | No exploration, no spatial gameplay, static screen |

## Monetization

One-time purchase. No microtransactions. No loot boxes.

