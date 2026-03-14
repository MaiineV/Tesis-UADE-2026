# 01 — Dice System

## Overview
Dice are the central resource of the game. They serve as inventory, weapons, and movement tools. This spec covers dice types, rolling mechanics, and the dice bag (inventory).

## Dependencies
- Referenced by: `02-character-system.md`, `03-combat-system.md`, `04-movement-system.md`, `06-inventory-system.md`
- References: None (this is a foundational system)

---

## 1. Dice Types

### 1.1 Data Definition
Each die type is defined as a ScriptableObject:

```csharp
[CreateAssetMenu(menuName = "Game/DiceData")]
public class DiceData : ScriptableObject
{
    public string DiceName;         // "d6", "d8", "d12"
    public int NumberOfFaces;       // 6, 8, 12
    public int[] DefaultFaces;      // default face values, e.g. {1,2,3,4,5,6}
    public int PowerCost;           // bag slot cost
    public Sprite Icon;             // UI icon (placeholder: colored shape)
    public Color DiceColor;         // for visual distinction
}
```

### 1.2 Prototype Dice (3 types)

| Die  | Faces | Default Values         | Power Cost | Role                |
|------|-------|------------------------|------------|---------------------|
| d6   | 6     | 1, 2, 3, 4, 5, 6      | 1          | Common, reliable    |
| d8   | 8     | 1, 2, 3, 4, 5, 6, 7, 8| 2          | Mid-range           |
| d12  | 12    | 1–12                   | 3          | High risk/reward    |

### 1.3 Dice Instance (Runtime)
Each die the player owns is a unique instance that can be modified:

```csharp
[System.Serializable]
public class DiceInstance
{
    public string Id;               // System.Guid.NewGuid().ToString()
    public DiceData BaseData;       // the ScriptableObject reference
    public int[] CurrentFaces;      // mutable copy of DefaultFaces
    public int PowerCost;           // starts as BaseData.PowerCost, can change
    
    // Create instance from base data
    public static DiceInstance Create(DiceData data)
    {
        return new DiceInstance
        {
            Id = System.Guid.NewGuid().ToString(),
            BaseData = data,
            CurrentFaces = (int[])data.DefaultFaces.Clone(),
            PowerCost = data.PowerCost
        };
    }
    
    // Roll this die — returns random face
    public RollResult Roll()
    {
        int faceIndex = UnityEngine.Random.Range(0, CurrentFaces.Length);
        return new RollResult
        {
            DiceId = this.Id,
            FaceIndex = faceIndex,
            Value = CurrentFaces[faceIndex]
        };
    }
}
```

---

## 2. Rolling Mechanics

### 2.1 Roll Result
```csharp
[System.Serializable]
public struct RollResult
{
    public string DiceId;       // which die instance
    public int FaceIndex;       // which face (0-based index)
    public int Value;           // the number rolled
}
```

### 2.2 Rolling a Set of Dice
The DiceManager handles rolling multiple dice at once:

```csharp
public class DiceManager : MonoBehaviour
{
    // Roll all provided dice instances
    public RollResult[] RollDice(List<DiceInstance> dice)
    {
        RollResult[] results = new RollResult[dice.Count];
        for (int i = 0; i < dice.Count; i++)
        {
            results[i] = dice[i].Roll();
        }
        return results;
    }
    
    // Roll only specific dice (by ID) — used for rerolls
    public RollResult[] RerollDice(List<DiceInstance> dice, HashSet<string> diceIdsToReroll)
    {
        RollResult[] results = new RollResult[dice.Count];
        for (int i = 0; i < dice.Count; i++)
        {
            if (diceIdsToReroll.Contains(dice[i].Id))
            {
                results[i] = dice[i].Roll();
            }
            // dice not in the set keep their previous result (caller manages this)
        }
        return results;
    }
}
```

### 2.3 Roll Flow (Combat Context)
```
Player has N dice in bag.
Roll 1: All N dice are rolled → results shown
Player SELECTS dice to KEEP (click to toggle lock)
Roll 2: Only unlocked dice are rerolled → results updated
Player SELECTS dice to KEEP again
Roll 3: Only unlocked dice are rerolled → final results
Player COMMITS → combination is evaluated
```

- Player can STOP at any roll (skip roll 2, skip roll 3).
- Stopping early means more remaining rolls for defense (see `03-combat-system.md`).

---

## 3. Dice Bag (Inventory)

### 3.1 Bag Rules
- Each character has a **max power budget** (e.g., Warrior = 8 points)
- Each die has a power cost
- Total power cost of all dice in bag must be ≤ max power budget
- Player builds their loadout within this budget before combat

### 3.2 Bag Data
```csharp
[System.Serializable]
public class DiceBag
{
    public List<DiceInstance> Dice = new List<DiceInstance>();
    public int MaxPower;    // set by character class
    
    public int CurrentPower
    {
        get
        {
            int total = 0;
            foreach (var d in Dice) total += d.PowerCost;
            return total;
        }
    }
    
    public int RemainingPower => MaxPower - CurrentPower;
    
    public bool CanAdd(DiceInstance die)
    {
        return CurrentPower + die.PowerCost <= MaxPower;
    }
    
    public bool TryAdd(DiceInstance die)
    {
        if (!CanAdd(die)) return false;
        Dice.Add(die);
        return true;
    }
    
    public void Remove(string diceId)
    {
        Dice.RemoveAll(d => d.Id == diceId);
    }
    
    // Roll all dice in the bag
    public RollResult[] RollAll()
    {
        RollResult[] results = new RollResult[Dice.Count];
        for (int i = 0; i < Dice.Count; i++)
        {
            results[i] = Dice[i].Roll();
        }
        return results;
    }
}
```

### 3.3 Prototype Starting Bag
For the single prototype character (Warrior):
- **Max Power**: 8
- **Starting Dice**: 4x d6 (cost 4) + 2x d8 (cost 4) = 8/8 power used
- This gives 6 total dice for Generala combinations

---

## 4. Dice Face Modification (Prototype: Mid-Fight Upgrade)

After defeating enemy 1, the player picks 1 of 2 random upgrades. Each upgrade modifies a specific face on a specific die.

### 4.1 Upgrade Types (Prototype)
```csharp
public enum FaceUpgradeType
{
    ValueIncrease,      // +N to face value
    ValueSet,           // set face to specific value
    FaceRemoval         // remove face (fewer faces = higher chance of remaining)
}

[System.Serializable]
public class FaceUpgrade
{
    public FaceUpgradeType Type;
    public int TargetFaceIndex;     // which face to modify
    public int Value;               // depends on type: increase amount, new value, or unused
    public string Description;      // UI display text
}
```

### 4.2 Applying an Upgrade
```csharp
public void ApplyUpgrade(DiceInstance die, FaceUpgrade upgrade)
{
    switch (upgrade.Type)
    {
        case FaceUpgradeType.ValueIncrease:
            die.CurrentFaces[upgrade.TargetFaceIndex] += upgrade.Value;
            break;
            
        case FaceUpgradeType.ValueSet:
            die.CurrentFaces[upgrade.TargetFaceIndex] = upgrade.Value;
            break;
            
        case FaceUpgradeType.FaceRemoval:
            // Create new array without the target face
            var faces = new List<int>(die.CurrentFaces);
            faces.RemoveAt(upgrade.TargetFaceIndex);
            die.CurrentFaces = faces.ToArray();
            break;
    }
}
```

### 4.3 Generating Random Upgrades
```csharp
public FaceUpgrade GenerateRandomUpgrade(DiceBag bag)
{
    // Pick a random die from the bag
    var die = bag.Dice[Random.Range(0, bag.Dice.Count)];
    // Pick a random face
    int faceIdx = Random.Range(0, die.CurrentFaces.Length);
    int currentValue = die.CurrentFaces[faceIdx];
    
    // Pick random upgrade type (weighted)
    float roll = Random.value;
    if (roll < 0.5f) // 50% chance: value increase
    {
        int increase = Random.Range(1, 4); // +1 to +3
        return new FaceUpgrade
        {
            Type = FaceUpgradeType.ValueIncrease,
            TargetFaceIndex = faceIdx,
            Value = increase,
            Description = $"{die.BaseData.DiceName}: face {currentValue} → {currentValue + increase}"
        };
    }
    else if (roll < 0.85f) // 35% chance: set to high value
    {
        int newValue = die.CurrentFaces.Max() + Random.Range(1, 3);
        return new FaceUpgrade
        {
            Type = FaceUpgradeType.ValueSet,
            TargetFaceIndex = faceIdx,
            Value = newValue,
            Description = $"{die.BaseData.DiceName}: face {currentValue} → {newValue}"
        };
    }
    else // 15% chance: remove face
    {
        if (die.CurrentFaces.Length <= 2) // safety: don't go below 2 faces
            return GenerateRandomUpgrade(bag); // reroll
        return new FaceUpgrade
        {
            Type = FaceUpgradeType.FaceRemoval,
            TargetFaceIndex = faceIdx,
            Value = 0,
            Description = $"{die.BaseData.DiceName}: remove face {currentValue} ({die.CurrentFaces.Length}→{die.CurrentFaces.Length - 1} faces)"
        };
    }
}
```

---

## 5. Speed Die (Movement)
A separate die used exclusively for movement on the grid. NOT part of the combat dice bag.

```csharp
[System.Serializable]
public class SpeedDie
{
    public int MinValue;    // minimum movement (e.g., 1)
    public int MaxValue;    // maximum movement (e.g., 4)
    
    public int Roll()
    {
        return Random.Range(MinValue, MaxValue + 1);
    }
}
```

For the prototype Warrior character:
- **Speed Die**: min 2, max 5 (always moves 2–5 tiles)

---

## 6. Integration Points

| System | How It Uses Dice |
|--------|------------------|
| Combat (03) | Rolls all bag dice, evaluates combinations, calculates damage |
| Defense (03) | Uses remaining rolls to generate shield value |
| Movement (04) | Rolls speed die for grid movement |
| Inventory (06) | Manages dice bag contents and power budget |
| Energy/Craps (05) | Craps mode bets on next roll's combination outcome |
| Rewards | Mid-fight upgrade modifies dice faces |
