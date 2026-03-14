# 03 — Combat System (Generala)

## Overview
Combat is turn-based. The player attacks using Generala (Yahtzee) dice combinations. Remaining unused rolls become defense. Enemies attack with a single roll. This is the **core system** of the game and must feel satisfying.

## Dependencies
- References: `01-dice-system.md`, `02-character-system.md`
- Referenced by: `05-energy-craps-system.md`, `07-enemy-system.md`

---

## 1. Combat Flow (One Full Round)

```
ROUND START
    │
    ▼
┌─ PLAYER ATTACK PHASE ─────────────────────────────────┐
│  Roll 1: Roll ALL combat dice                          │
│  Player locks dice they want to keep                   │
│  Roll 2 (optional): Reroll unlocked dice               │
│  Player locks dice they want to keep                   │
│  Roll 3 (optional): Reroll unlocked dice               │
│  Player COMMITS → best combination evaluated           │
│  → Deal damage to enemy                                │
│  → Record: how many rolls were USED (1, 2, or 3)      │
└────────────────────────────────────────────────────────┘
    │
    ▼
┌─ PLAYER DEFENSE PHASE ────────────────────────────────┐
│  Defense rolls available = 3 - attack rolls used       │
│  For each defense roll:                                │
│    Roll ALL combat dice                                │
│    Player locks dice they want to keep                 │
│    (no rerolls within a defense roll — 1 throw each)   │
│  Best combination from defense rolls = shield value    │
└────────────────────────────────────────────────────────┘
    │
    ▼
┌─ ENEMY ATTACK PHASE ─────────────────────────────────┐
│  Enemy rolls once → raw damage                        │
│  Subtract player shield → apply net damage to player  │
│  Reset player shield to 0                             │
└───────────────────────────────────────────────────────┘
    │
    ▼
┌─ ROUND END ───────────────────────────────────────────┐
│  Update energy bars (player + enemy)                  │
│  Check: enemy dead? → reward / next enemy             │
│  Check: player dead? → game over                      │
│  Next round starts                                    │
└───────────────────────────────────────────────────────┘
```

**Key design tension**: Using fewer attack rolls = more defense rolls. If the player nails a great combo on roll 1 and commits immediately, they get 2 defense rolls. If they use all 3 attack rolls fishing for a better combo, they get 0 defense rolls and take full enemy damage.

---

## 2. Combination Types

### 2.1 Enum Definition
```csharp
public enum CombinationType
{
    HighDie,        // no combination — just the highest single die
    Pair,           // 2 of a kind
    TwoPair,        // 2 different pairs
    ThreeOfAKind,   // 3 of a kind
    Straight,       // sequential values (at least 5 consecutive)
    FullHouse,      // 3 of a kind + pair
    FourOfAKind,    // 4 of a kind (Poker)
    Generala,       // 5 of a kind
    DoubleGenerala  // 5 of a kind, second time in same run
}
```

### 2.2 Combination Definitions and Damage

```csharp
[System.Serializable]
public struct CombinationResult
{
    public CombinationType Type;
    public int[] MatchingDice;      // values of dice that form the combo
    public int[] RemainingDice;     // values of dice not in the combo
    public int BaseDamage;
}
```

**Damage Formulas:**

| Combination      | Detection Rule                          | Damage Formula                        | Example (6 dice)          |
|------------------|-----------------------------------------|---------------------------------------|---------------------------|
| High Die         | No other combo found                    | Value of highest die                  | [1,2,3,4,5,8] → 8 dmg    |
| Pair             | 2 dice with same value                  | Sum of the pair × 1.5                 | [3,3,...] → 9 dmg         |
| Two Pair         | 2 different pairs                       | Sum of both pairs × 1.5              | [3,3,5,5,...] → 24 dmg    |
| Three of a Kind  | 3 dice with same value                  | Sum of the three × 2                  | [4,4,4,...] → 24 dmg      |
| Straight         | 5+ consecutive values among all dice    | 30 flat + highest die in straight     | [1,2,3,4,5,...] → 35 dmg  |
| Full House       | Three of a Kind + Pair                  | 35 flat + sum of all 5 matching dice  | [3,3,3,5,5] → 54 dmg     |
| Four of a Kind   | 4 dice with same value                  | Sum of the four × 3                   | [6,6,6,6,...] → 72 dmg    |
| Generala         | 5 dice with same value                  | Sum of the five × 5                   | [6,6,6,6,6] → 150 dmg    |
| Double Generala  | 5 of a kind, 2nd time in run            | Sum × 5 × 2                          | [6,6,6,6,6] → 300 dmg    |

> Note: "Sum of the X" means sum of the dice that form the combination, not all dice.

### 2.3 Combination Detection Logic

```csharp
public static class CombinationDetector
{
    /// Analyze a set of roll results and return the BEST combination found.
    /// "Best" = highest damage output.
    public static CombinationResult Evaluate(int[] diceValues, bool hasGeneralaThisRun)
    {
        // Build frequency map
        Dictionary<int, int> freq = new Dictionary<int, int>();
        foreach (int v in diceValues)
        {
            if (!freq.ContainsKey(v)) freq[v] = 0;
            freq[v]++;
        }
        
        // Check each combination from highest to lowest priority
        // We evaluate ALL and pick the one with highest damage
        
        List<CombinationResult> candidates = new List<CombinationResult>();
        
        // Generala: 5+ of a kind
        foreach (var kvp in freq)
        {
            if (kvp.Value >= 5)
            {
                int sum = kvp.Key * 5;
                var type = hasGeneralaThisRun 
                    ? CombinationType.DoubleGenerala 
                    : CombinationType.Generala;
                int multiplier = type == CombinationType.DoubleGenerala ? 10 : 5;
                candidates.Add(MakeResult(type, kvp.Key, 5, diceValues, sum * multiplier));
            }
        }
        
        // Four of a Kind
        foreach (var kvp in freq)
        {
            if (kvp.Value >= 4)
            {
                int sum = kvp.Key * 4;
                candidates.Add(MakeResult(CombinationType.FourOfAKind, kvp.Key, 4, diceValues, sum * 3));
            }
        }
        
        // Full House: need a 3-of-a-kind AND a separate pair
        var threes = freq.Where(f => f.Value >= 3).ToList();
        var twos = freq.Where(f => f.Value >= 2).ToList();
        foreach (var three in threes)
        {
            foreach (var two in twos)
            {
                if (three.Key != two.Key)
                {
                    int sum = three.Key * 3 + two.Key * 2;
                    candidates.Add(new CombinationResult
                    {
                        Type = CombinationType.FullHouse,
                        MatchingDice = Enumerable.Repeat(three.Key, 3)
                            .Concat(Enumerable.Repeat(two.Key, 2)).ToArray(),
                        RemainingDice = GetRemaining(diceValues, 
                            Enumerable.Repeat(three.Key, 3)
                            .Concat(Enumerable.Repeat(two.Key, 2)).ToArray()),
                        BaseDamage = 35 + sum
                    });
                }
            }
        }
        
        // Straight: find longest consecutive sequence in the VALUES present
        candidates.AddRange(CheckStraights(diceValues));
        
        // Three of a Kind
        foreach (var kvp in freq)
        {
            if (kvp.Value >= 3)
            {
                int sum = kvp.Key * 3;
                candidates.Add(MakeResult(CombinationType.ThreeOfAKind, kvp.Key, 3, diceValues, sum * 2));
            }
        }
        
        // Two Pair
        var pairs = freq.Where(f => f.Value >= 2).OrderByDescending(f => f.Key).ToList();
        if (pairs.Count >= 2)
        {
            int sum = pairs[0].Key * 2 + pairs[1].Key * 2;
            candidates.Add(new CombinationResult
            {
                Type = CombinationType.TwoPair,
                MatchingDice = Enumerable.Repeat(pairs[0].Key, 2)
                    .Concat(Enumerable.Repeat(pairs[1].Key, 2)).ToArray(),
                RemainingDice = GetRemaining(diceValues, 
                    Enumerable.Repeat(pairs[0].Key, 2)
                    .Concat(Enumerable.Repeat(pairs[1].Key, 2)).ToArray()),
                BaseDamage = Mathf.RoundToInt(sum * 1.5f)
            });
        }
        
        // Pair
        foreach (var kvp in freq)
        {
            if (kvp.Value >= 2)
            {
                int sum = kvp.Key * 2;
                candidates.Add(MakeResult(CombinationType.Pair, kvp.Key, 2, diceValues, 
                    Mathf.RoundToInt(sum * 1.5f)));
            }
        }
        
        // High Die (always available as fallback)
        int highest = diceValues.Max();
        candidates.Add(new CombinationResult
        {
            Type = CombinationType.HighDie,
            MatchingDice = new int[] { highest },
            RemainingDice = GetRemaining(diceValues, new int[] { highest }),
            BaseDamage = highest
        });
        
        // Return the candidate with the highest damage
        return candidates.OrderByDescending(c => c.BaseDamage).First();
    }
    
    private static List<CombinationResult> CheckStraights(int[] diceValues)
    {
        var results = new List<CombinationResult>();
        var uniqueSorted = diceValues.Distinct().OrderBy(v => v).ToList();
        
        // Find longest consecutive run
        int bestStart = uniqueSorted[0];
        int bestLength = 1;
        int currentStart = uniqueSorted[0];
        int currentLength = 1;
        
        for (int i = 1; i < uniqueSorted.Count; i++)
        {
            if (uniqueSorted[i] == uniqueSorted[i - 1] + 1)
            {
                currentLength++;
                if (currentLength > bestLength)
                {
                    bestLength = currentLength;
                    bestStart = currentStart;
                }
            }
            else
            {
                currentStart = uniqueSorted[i];
                currentLength = 1;
            }
        }
        
        // Need at least 5 consecutive for a straight
        if (bestLength >= 5)
        {
            int[] straightDice = Enumerable.Range(bestStart, bestLength).ToArray();
            int highestInStraight = straightDice.Max();
            results.Add(new CombinationResult
            {
                Type = CombinationType.Straight,
                MatchingDice = straightDice,
                RemainingDice = GetRemaining(diceValues, straightDice),
                BaseDamage = 30 + highestInStraight
            });
        }
        
        return results;
    }
    
    // Helper: given all dice and matching dice, return the leftover
    private static int[] GetRemaining(int[] allDice, int[] matching)
    {
        var remaining = new List<int>(allDice);
        foreach (int m in matching)
        {
            remaining.Remove(m); // removes first occurrence
        }
        return remaining.ToArray();
    }
    
    private static CombinationResult MakeResult(CombinationType type, int matchValue, 
        int matchCount, int[] allDice, int damage)
    {
        int[] matching = Enumerable.Repeat(matchValue, matchCount).ToArray();
        return new CombinationResult
        {
            Type = type,
            MatchingDice = matching,
            RemainingDice = GetRemaining(allDice, matching),
            BaseDamage = damage
        };
    }
}
```

---

## 3. Attack Phase (Detailed)

### 3.1 State Machine
```csharp
public enum AttackPhaseState
{
    WaitingForRoll,         // player must click "Roll"
    ShowingResults,         // dice results shown, player can lock/unlock
    WaitingForDecision,     // player chooses: Reroll, or Commit
    Committed,              // combination chosen, damage calculated
    Finished                // attack phase complete
}
```

### 3.2 Attack Phase Logic
```csharp
public class AttackPhase
{
    public int MaxRolls = 3;
    public int CurrentRoll = 0;
    public RollResult[] CurrentResults;
    public HashSet<string> LockedDiceIds = new HashSet<string>();
    public CombinationResult FinalCombination;
    public int RollsUsed => CurrentRoll;
    
    // Called when player clicks "Roll" or "Reroll"
    public RollResult[] PerformRoll(DiceBag bag)
    {
        CurrentRoll++;
        
        if (CurrentRoll == 1)
        {
            // First roll: roll everything
            CurrentResults = bag.RollAll();
        }
        else
        {
            // Subsequent rolls: only reroll unlocked dice
            for (int i = 0; i < CurrentResults.Length; i++)
            {
                if (!LockedDiceIds.Contains(CurrentResults[i].DiceId))
                {
                    var die = bag.Dice.First(d => d.Id == CurrentResults[i].DiceId);
                    CurrentResults[i] = die.Roll();
                }
            }
        }
        
        return CurrentResults;
    }
    
    public void ToggleLock(string diceId)
    {
        if (LockedDiceIds.Contains(diceId))
            LockedDiceIds.Remove(diceId);
        else
            LockedDiceIds.Add(diceId);
    }
    
    public bool CanRollAgain => CurrentRoll < MaxRolls;
    
    // Player commits to current dice — evaluate best combo
    public CombinationResult Commit(bool hasGeneralaThisRun)
    {
        int[] values = CurrentResults.Select(r => r.Value).ToArray();
        FinalCombination = CombinationDetector.Evaluate(values, hasGeneralaThisRun);
        return FinalCombination;
    }
}
```

---

## 4. Defense Phase

### 4.1 Rules
- Defense rolls = 3 minus rolls used in attack
- If attack used 1 roll → 2 defense rolls
- If attack used 2 rolls → 1 defense roll
- If attack used 3 rolls → 0 defense rolls (no defense!)
- Each defense roll: player rolls ALL dice once, no rerolls within that defense roll
- The BEST combination across all defense rolls becomes the shield value

### 4.2 Shield Value Calculation
The shield uses the same combination table but with a defense multiplier (lower than attack):

| Combination      | Shield Formula                   |
|------------------|----------------------------------|
| High Die         | Value × 0.5                     |
| Pair             | Sum of pair × 0.75              |
| Two Pair         | Sum of pairs × 0.75             |
| Three of a Kind  | Sum of three × 1.0              |
| Straight         | 15 flat                          |
| Full House       | 20 flat                          |
| Four of a Kind   | Sum of four × 1.5               |
| Generala         | Sum of five × 2.5               |

### 4.3 Defense Logic
```csharp
public class DefensePhase
{
    public int AvailableRolls;
    public List<CombinationResult> DefenseResults = new List<CombinationResult>();
    public int FinalShieldValue;
    
    public DefensePhase(int rollsUsedForAttack)
    {
        AvailableRolls = 3 - rollsUsedForAttack;
    }
    
    public void PerformDefenseRoll(DiceBag bag, bool hasGeneralaThisRun)
    {
        if (DefenseResults.Count >= AvailableRolls) return;
        
        var results = bag.RollAll();
        int[] values = results.Select(r => r.Value).ToArray();
        var combo = CombinationDetector.Evaluate(values, hasGeneralaThisRun);
        DefenseResults.Add(combo);
    }
    
    public int CalculateShield()
    {
        if (DefenseResults.Count == 0)
        {
            FinalShieldValue = 0;
            return 0;
        }
        
        // Pick best defense result
        var best = DefenseResults.OrderByDescending(r => GetShieldValue(r)).First();
        FinalShieldValue = GetShieldValue(best);
        return FinalShieldValue;
    }
    
    private int GetShieldValue(CombinationResult combo)
    {
        int sum = combo.MatchingDice.Sum();
        switch (combo.Type)
        {
            case CombinationType.HighDie:
                return Mathf.RoundToInt(combo.MatchingDice[0] * 0.5f);
            case CombinationType.Pair:
                return Mathf.RoundToInt(sum * 0.75f);
            case CombinationType.TwoPair:
                return Mathf.RoundToInt(sum * 0.75f);
            case CombinationType.ThreeOfAKind:
                return sum;
            case CombinationType.Straight:
                return 15;
            case CombinationType.FullHouse:
                return 20;
            case CombinationType.FourOfAKind:
                return Mathf.RoundToInt(sum * 1.5f);
            case CombinationType.Generala:
            case CombinationType.DoubleGenerala:
                return Mathf.RoundToInt(sum * 2.5f);
            default:
                return 0;
        }
    }
}
```

---

## 5. Damage Application

```csharp
public class DamageResolver
{
    /// Apply player attack to enemy
    public static int ResolvePlayerAttack(CombinationResult combo, CharacterData character)
    {
        int damage = combo.BaseDamage;
        
        // Apply affinity bonus
        if (combo.Type == character.AffinityCombo)
        {
            damage = Mathf.RoundToInt(damage * character.AffinityDamageBonus);
        }
        
        return damage;
    }
    
    /// Apply enemy attack to player, considering shield
    public static int ResolveEnemyAttack(int enemyRawDamage, int playerShield)
    {
        int netDamage = Mathf.Max(0, enemyRawDamage - playerShield);
        return netDamage;
    }
}
```

---

## 6. Combat Manager (Orchestrator)

```csharp
public class CombatManager : MonoBehaviour
{
    public enum CombatState
    {
        NotInCombat,
        PlayerAttack,
        PlayerDefense,
        EnemyAttack,
        RoundEnd,
        CombatOver
    }
    
    public CombatState State { get; private set; }
    public AttackPhase CurrentAttack { get; private set; }
    public DefensePhase CurrentDefense { get; private set; }
    
    private PlayerEntity player;
    private EnemyEntity currentEnemy;
    private bool generalaScoredThisRun = false;
    
    // Events
    public static event Action<CombatState> OnStateChanged;
    public static event Action<RollResult[]> OnDiceRolled;
    public static event Action<CombinationResult> OnComboScored;
    public static event Action<int, bool> OnDamageApplied; // amount, isPlayer
    public static event Action<int> OnShieldGenerated;
    public static event Action<bool> OnCombatEnded; // true = player won
    
    public void StartCombat(PlayerEntity player, EnemyEntity enemy)
    {
        this.player = player;
        this.currentEnemy = enemy;
        State = CombatState.PlayerAttack;
        CurrentAttack = new AttackPhase();
        OnStateChanged?.Invoke(State);
    }
    
    // --- Player calls these via UI ---
    
    public void PlayerRoll()
    {
        if (State != CombatState.PlayerAttack) return;
        var results = CurrentAttack.PerformRoll(player.State.Bag);
        OnDiceRolled?.Invoke(results);
    }
    
    public void PlayerToggleLock(string diceId)
    {
        if (State != CombatState.PlayerAttack) return;
        CurrentAttack.ToggleLock(diceId);
    }
    
    public void PlayerCommitAttack()
    {
        if (State != CombatState.PlayerAttack) return;
        
        var combo = CurrentAttack.Commit(generalaScoredThisRun);
        
        // Track Generala for double generala
        if (combo.Type == CombinationType.Generala)
            generalaScoredThisRun = true;
        
        int damage = DamageResolver.ResolvePlayerAttack(combo, player.State.BaseData);
        currentEnemy.State.TakeDamage(damage);
        
        OnComboScored?.Invoke(combo);
        OnDamageApplied?.Invoke(damage, false);
        
        if (!currentEnemy.State.IsAlive)
        {
            State = CombatState.CombatOver;
            OnCombatEnded?.Invoke(true);
            return;
        }
        
        // Move to defense phase
        CurrentDefense = new DefensePhase(CurrentAttack.RollsUsed);
        if (CurrentDefense.AvailableRolls > 0)
        {
            State = CombatState.PlayerDefense;
            OnStateChanged?.Invoke(State);
        }
        else
        {
            // No defense rolls — go directly to enemy attack
            player.State.ShieldValue = 0;
            GoToEnemyAttack();
        }
    }
    
    public void PlayerDefenseRoll()
    {
        if (State != CombatState.PlayerDefense) return;
        CurrentDefense.PerformDefenseRoll(player.State.Bag, generalaScoredThisRun);
        
        if (CurrentDefense.DefenseResults.Count >= CurrentDefense.AvailableRolls)
        {
            // All defense rolls used
            int shield = CurrentDefense.CalculateShield();
            player.State.ShieldValue = shield;
            OnShieldGenerated?.Invoke(shield);
            GoToEnemyAttack();
        }
    }
    
    private void GoToEnemyAttack()
    {
        State = CombatState.EnemyAttack;
        OnStateChanged?.Invoke(State);
        
        // Enemy attacks (see 07-enemy-system.md for details)
        int enemyRoll = currentEnemy.RollAttack();
        int netDamage = DamageResolver.ResolveEnemyAttack(enemyRoll, player.State.ShieldValue);
        player.State.TakeDamage(netDamage);
        player.State.ShieldValue = 0; // reset shield after use
        
        OnDamageApplied?.Invoke(netDamage, true);
        
        if (!player.State.IsAlive)
        {
            State = CombatState.CombatOver;
            OnCombatEnded?.Invoke(false);
            return;
        }
        
        // Round end — start new round
        State = CombatState.RoundEnd;
        OnStateChanged?.Invoke(State);
        
        // Auto-start next round after brief delay
        StartNextRound();
    }
    
    private void StartNextRound()
    {
        CurrentAttack = new AttackPhase();
        State = CombatState.PlayerAttack;
        OnStateChanged?.Invoke(State);
    }
}
```

---

## 7. UI Requirements (Combat Screen)

```
┌──────────────────────────────────────────────────┐
│  PLAYER HP: ████████░░ 80/100    SHIELD: 12      │
│  ENERGY:    ██████░░░░ 60/100                    │
│                                                  │
│  ┌─────────────────────────────────────────────┐ │
│  │           GRID VIEW (8x8)                   │ │
│  │      [P] player    [E1] [E2] enemies       │ │
│  └─────────────────────────────────────────────┘ │
│                                                  │
│  YOUR DICE:                                      │
│  [🎲4] [🎲6*] [🎲2] [🎲6*] [🎲3] [🎲8]        │
│   d6    d6     d8    d6     d6    d8            │
│  (* = locked)                                    │
│                                                  │
│  BEST COMBO: Pair of 6s → 18 damage             │
│  Roll 2/3                                        │
│                                                  │
│  [REROLL]  [COMMIT ATTACK]                       │
│                                                  │
│  ENEMY HP: ██████████░░ 45/60                    │
│                                                  │
│  COMBAT LOG:                                     │
│  > You rolled: 4, 6, 2, 6, 3, 8                 │
│  > Best combo: Pair of 6s (18 dmg)              │
└──────────────────────────────────────────────────┘
```

### UI Actions
- Click a die → toggle lock/unlock (visual: locked dice glow or have border)
- Click "Reroll" → rerolls unlocked dice (disabled if no rolls left)
- Click "Commit Attack" → evaluates combo, deals damage, moves to defense
- Defense phase: "Roll Defense" button (auto-evaluates, no locking needed)
- Combat log shows each action with text description

---

## 8. Balance Notes (Prototype Tuning)

These values should be easily tweakable via ScriptableObjects or a config:

| Parameter               | Starting Value | Notes                          |
|--------------------------|---------------|--------------------------------|
| Player HP                | 100           | Warrior default                |
| Enemy 1 HP               | 40            | Should die in 2-4 rounds       |
| Enemy 2 HP               | 60            | Harder, tests upgraded dice    |
| Affinity bonus            | 1.25x         | Noticeable but not dominant    |
| Defense multipliers       | See §4.2      | Shield should block ~30-50%    |
| Enemy damage range        | 8-20          | Single roll, see enemy spec   |

Target combat duration: **3-5 rounds per enemy**. If fights take longer, reduce enemy HP. If fights are too short, increase it.
