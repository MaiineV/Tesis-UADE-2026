# 06 — Inventory System (Dice Bag)

## Overview
The dice bag is the player's inventory. It holds combat dice limited by a power budget (similar to Hollow Knight's charm notch system). This spec covers bag management, dice swapping, and the mid-fight upgrade reward for the prototype.

## Dependencies
- References: `01-dice-system.md`, `02-character-system.md`
- Referenced by: `03-combat-system.md`

---

## 1. Power Budget System

### 1.1 Core Rules
- Each character class has a **max power budget** (e.g., Warrior = 8)
- Each die has a **power cost** based on its type
- Total power cost of all equipped dice ≤ max power budget
- The player CANNOT exceed the budget (hard limit)
- The budget cannot be negative (removing dice gives room, not debt)

### 1.2 Power Costs

| Die Type | Faces | Power Cost | Notes                    |
|----------|-------|------------|--------------------------|
| d6       | 6     | 1          | Cheap, reliable          |
| d8       | 8     | 2          | Mid-range                |
| d12      | 12    | 3          | Expensive, high variance |

### 1.3 Example Builds (Warrior, budget = 8)

| Build Name     | Dice Composition              | Total Cost | Dice Count |
|----------------|-------------------------------|------------|------------|
| Standard       | 4x d6 + 2x d8                | 4 + 4 = 8 | 6 dice     |
| Many Smalls    | 8x d6                         | 8          | 8 dice     |
| Balanced       | 2x d6 + 1x d8 + 2x d12       | 2+2+6 = 10| INVALID    |
| High Roller    | 2x d6 + 2x d12               | 2 + 6 = 8 | 4 dice     |
| All-In         | 1x d8 + 2x d12               | 2 + 6 = 8 | 3 dice     |

**Design insight**: More dice = more combo opportunities but lower individual values. Fewer dice = higher values but harder to form combos. This is the core build tension.

---

## 2. Dice Bag Implementation

The `DiceBag` class is defined in `01-dice-system.md`. This section adds management operations:

```csharp
public class DiceBagManager
{
    private DiceBag bag;
    
    public DiceBagManager(DiceBag bag)
    {
        this.bag = bag;
    }
    
    /// Swap a die in the bag for a new one (shop, reward, etc.)
    public bool SwapDie(string removeDiceId, DiceInstance newDie)
    {
        var existing = bag.Dice.FirstOrDefault(d => d.Id == removeDiceId);
        if (existing == null) return false;
        
        int powerAfterRemove = bag.CurrentPower - existing.PowerCost;
        if (powerAfterRemove + newDie.PowerCost > bag.MaxPower) return false;
        
        bag.Remove(removeDiceId);
        bag.TryAdd(newDie);
        return true;
    }
    
    /// Check if a die can be added without removing anything
    public bool CanAddDie(DiceInstance die)
    {
        return bag.CanAdd(die);
    }
    
    /// Get a summary for UI display
    public BagSummary GetSummary()
    {
        return new BagSummary
        {
            TotalDice = bag.Dice.Count,
            UsedPower = bag.CurrentPower,
            MaxPower = bag.MaxPower,
            RemainingPower = bag.RemainingPower,
            DiceList = bag.Dice.Select(d => new DiceSummary
            {
                Id = d.Id,
                TypeName = d.BaseData.DiceName,
                Faces = d.CurrentFaces,
                PowerCost = d.PowerCost,
                Color = d.BaseData.DiceColor
            }).ToList()
        };
    }
}

[System.Serializable]
public struct BagSummary
{
    public int TotalDice;
    public int UsedPower;
    public int MaxPower;
    public int RemainingPower;
    public List<DiceSummary> DiceList;
}

[System.Serializable]
public struct DiceSummary
{
    public string Id;
    public string TypeName;
    public int[] Faces;
    public int PowerCost;
    public Color Color;
}
```

---

## 3. Mid-Fight Reward System (Prototype)

After defeating enemy 1 (and before fighting enemy 2), the player chooses 1 of 2 random dice face upgrades. This is the only "progression within a run" mechanic in the prototype.

### 3.1 Reward Flow
```
Enemy 1 dies
    ↓
Generate 2 random FaceUpgrade offers
    ↓
Show reward UI (pick 1 of 2)
    ↓
Player clicks an upgrade
    ↓
Apply upgrade to the target die
    ↓
Show updated dice bag
    ↓
Resume: fight enemy 2
```

### 3.2 Reward Generation
```csharp
public class RewardGenerator
{
    /// Generate N unique upgrade offers for the player's current bag
    public static List<FaceUpgradeOffer> GenerateOffers(DiceBag bag, int count)
    {
        var offers = new List<FaceUpgradeOffer>();
        var usedDieFacePairs = new HashSet<string>(); // prevent duplicate targets
        
        int attempts = 0;
        while (offers.Count < count && attempts < 50)
        {
            attempts++;
            
            // Pick random die
            var die = bag.Dice[Random.Range(0, bag.Dice.Count)];
            int faceIdx = Random.Range(0, die.CurrentFaces.Length);
            string key = $"{die.Id}_{faceIdx}";
            
            if (usedDieFacePairs.Contains(key)) continue;
            usedDieFacePairs.Add(key);
            
            // Generate upgrade
            var upgrade = GenerateUpgrade(die, faceIdx);
            offers.Add(new FaceUpgradeOffer
            {
                TargetDiceId = die.Id,
                TargetDiceName = die.BaseData.DiceName,
                Upgrade = upgrade
            });
        }
        
        return offers;
    }
    
    private static FaceUpgrade GenerateUpgrade(DiceInstance die, int faceIdx)
    {
        int currentValue = die.CurrentFaces[faceIdx];
        float roll = Random.value;
        
        if (roll < 0.5f)
        {
            int increase = Random.Range(2, 5);
            return new FaceUpgrade
            {
                Type = FaceUpgradeType.ValueIncrease,
                TargetFaceIndex = faceIdx,
                Value = increase,
                Description = $"{die.BaseData.DiceName}: face [{currentValue}] gains +{increase} → [{currentValue + increase}]"
            };
        }
        else if (roll < 0.8f)
        {
            int maxFace = die.CurrentFaces.Max();
            int newVal = maxFace + Random.Range(1, 4);
            return new FaceUpgrade
            {
                Type = FaceUpgradeType.ValueSet,
                TargetFaceIndex = faceIdx,
                Value = newVal,
                Description = $"{die.BaseData.DiceName}: face [{currentValue}] becomes [{newVal}]"
            };
        }
        else
        {
            if (die.CurrentFaces.Length <= 3)
            {
                // Safety: if die has few faces, do value increase instead
                return new FaceUpgrade
                {
                    Type = FaceUpgradeType.ValueIncrease,
                    TargetFaceIndex = faceIdx,
                    Value = 3,
                    Description = $"{die.BaseData.DiceName}: face [{currentValue}] gains +3 → [{currentValue + 3}]"
                };
            }
            return new FaceUpgrade
            {
                Type = FaceUpgradeType.FaceRemoval,
                TargetFaceIndex = faceIdx,
                Value = 0,
                Description = $"{die.BaseData.DiceName}: REMOVE face [{currentValue}] ({die.CurrentFaces.Length} → {die.CurrentFaces.Length - 1} faces)"
            };
        }
    }
}

[System.Serializable]
public struct FaceUpgradeOffer
{
    public string TargetDiceId;
    public string TargetDiceName;
    public FaceUpgrade Upgrade;
}
```

---

## 4. UI Requirements

### 4.1 Dice Bag Display (Always Visible During Combat)
```
┌─ DICE BAG ─────────────────────────────┐
│  Power: ██████░░ 6/8                    │
│                                         │
│  [d6]  [1][2][3][4][5][6]    cost: 1   │
│  [d6]  [1][2][3][4][5][6]    cost: 1   │
│  [d6]  [1][2][3][4][5][6]    cost: 1   │
│  [d6]  [1][2][3][4][5][6]    cost: 1   │
│  [d8]  [1][2][3][4][5][6][7][8] cost:2 │
│  [d8]  [1][2][3][4][5][6][7][8] cost:2 │
│                                         │
│  Total: 6 dice | 8/8 power             │
└─────────────────────────────────────────┘
```

### 4.2 Reward Selection UI (After Enemy 1)
```
┌──────────────────────────────────────────┐
│         🎁 CHOOSE YOUR REWARD            │
│                                          │
│  ┌─ Option A ──────────────────────────┐ │
│  │  d6: face [2] gains +3 → [5]       │ │
│  │  (more consistent high rolls)       │ │
│  └─────────────────────────────────────┘ │
│                                          │
│  ┌─ Option B ──────────────────────────┐ │
│  │  d8: REMOVE face [1]               │ │
│  │  (8 → 7 faces, better odds)        │ │
│  └─────────────────────────────────────┘ │
│                                          │
│  Click to choose!                        │
└──────────────────────────────────────────┘
```

---

## 5. Prototype Constraints

For the prototype, the inventory is **static** except for the mid-fight upgrade:
- Player starts with the Warrior's default loadout (4x d6, 2x d8)
- No shops, no buying/selling dice
- No enchantment system (beyond the reward upgrade)
- No bag expansion items
- The bag UI is read-only during combat (shows current dice and their faces)
