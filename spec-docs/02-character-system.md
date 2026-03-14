# 02 — Character System

## Overview
Each run begins with character selection. Characters define base stats, speed die, starting dice bag, and potential affinity bonuses. For the prototype, only one character exists (Warrior).

## Dependencies
- References: `01-dice-system.md`
- Referenced by: `03-combat-system.md`, `04-movement-system.md`, `06-inventory-system.md`

---

## 1. Character Data Definition

```csharp
[CreateAssetMenu(menuName = "Game/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string CharacterName;
    public string ClassName;            // "Warrior", "Rogue", etc.
    public string Description;
    public Sprite Portrait;             // placeholder: colored circle
    public Color CharacterColor;        // used for placeholder visuals
    
    [Header("Base Stats")]
    public int MaxHP;                   // starting max health
    public int StartingPowerBudget;     // max dice bag power
    
    [Header("Speed Die")]
    public int SpeedMin;                // min tiles per movement roll
    public int SpeedMax;                // max tiles per movement roll
    
    [Header("Starting Dice")]
    public DiceLoadout[] StartingDice;  // what dice the character starts with
    
    [Header("Affinity (optional)")]
    public CombinationType AffinityCombo;  // combo type this class is good at
    public float AffinityDamageBonus;      // multiplier when hitting that combo (e.g., 1.25 = +25%)
    
    [Header("Unlock Condition")]
    public bool UnlockedByDefault;
    public string UnlockDescription;     // displayed in character select as "???"
}

[System.Serializable]
public struct DiceLoadout
{
    public DiceData DiceType;   // reference to ScriptableObject (d6, d8, etc.)
    public int Quantity;        // how many of this type
}
```

---

## 2. Prototype Character: Warrior

| Stat               | Value                              |
|--------------------|------------------------------------|
| Name               | Warrior                            |
| MaxHP              | 100                                |
| Power Budget       | 8                                  |
| Speed Die          | min 2, max 5                       |
| Starting Dice      | 4x d6 (cost 4) + 2x d8 (cost 4)  |
| Affinity Combo     | Poker (4 of a kind)                |
| Affinity Bonus     | 1.25x damage                       |
| Unlocked by default| Yes                                |

### Why these values
- **6 dice total** allows for meaningful Generala combinations (pairs, triples, straights).
- **Full power budget** at start — in the full game, cheaper builds would leave room for upgrades found during the run. For prototype, start full.
- **Poker affinity** rewards grouping same-value dice, which is intuitive for new players.

---

## 3. Runtime Character State

The player entity holds the runtime state during a run:

```csharp
public class PlayerState
{
    // Identity
    public CharacterData BaseData;
    
    // Health
    public int CurrentHP;
    public int MaxHP;
    
    // Dice
    public DiceBag Bag;
    public SpeedDie SpeedDie;
    
    // Energy
    public float CurrentEnergy;     // 0 to MaxEnergy
    public float MaxEnergy;         // e.g., 100
    
    // Position
    public Vector2Int GridPosition;
    
    // Combat state
    public int ShieldValue;         // current defense from last defense rolls
    public bool CrapsModeAvailable; // true when energy is full
    
    // Initialize from character data
    public static PlayerState Create(CharacterData data)
    {
        var state = new PlayerState();
        state.BaseData = data;
        state.CurrentHP = data.MaxHP;
        state.MaxHP = data.MaxHP;
        state.MaxEnergy = 100f;
        state.CurrentEnergy = 0f;
        state.ShieldValue = 0;
        state.CrapsModeAvailable = false;
        
        // Build starting dice bag
        state.Bag = new DiceBag { MaxPower = data.StartingPowerBudget };
        foreach (var loadout in data.StartingDice)
        {
            for (int i = 0; i < loadout.Quantity; i++)
            {
                var instance = DiceInstance.Create(loadout.DiceType);
                state.Bag.TryAdd(instance);
            }
        }
        
        // Build speed die
        state.SpeedDie = new SpeedDie
        {
            MinValue = data.SpeedMin,
            MaxValue = data.SpeedMax
        };
        
        return state;
    }
    
    public bool IsAlive => CurrentHP > 0;
    
    public void TakeDamage(int rawDamage)
    {
        int blocked = Mathf.Min(ShieldValue, rawDamage);
        int actualDamage = rawDamage - blocked;
        ShieldValue = Mathf.Max(0, ShieldValue - rawDamage);
        CurrentHP = Mathf.Max(0, CurrentHP - actualDamage);
    }
    
    public void Heal(int amount)
    {
        CurrentHP = Mathf.Min(MaxHP, CurrentHP + amount);
    }
}
```

---

## 4. Character on the Grid (Entity)

```csharp
public class PlayerEntity : MonoBehaviour
{
    public PlayerState State { get; private set; }
    public SpriteRenderer Visual;
    
    public void Initialize(CharacterData data, Vector2Int startPosition)
    {
        State = PlayerState.Create(data);
        State.GridPosition = startPosition;
        
        // Placeholder visual: colored square
        Visual.color = data.CharacterColor;
        transform.position = GridManager.Instance.GridToWorld(startPosition);
    }
    
    public void MoveTo(Vector2Int newPosition)
    {
        State.GridPosition = newPosition;
        // For prototype: instant teleport. Later: animate movement.
        transform.position = GridManager.Instance.GridToWorld(newPosition);
    }
}
```

---

## 5. Damage Affinity

When the player scores a combination that matches their character's affinity:
```csharp
public int CalculateDamageWithAffinity(int baseDamage, CombinationType combo, CharacterData character)
{
    if (combo == character.AffinityCombo)
    {
        return Mathf.RoundToInt(baseDamage * character.AffinityDamageBonus);
    }
    return baseDamage;
}
```

---

## 6. Future Characters (Out of Prototype Scope — Design Notes)

These are reference designs for the full game. Do NOT implement for prototype.

| Class    | HP  | Power | Speed   | Affinity         | Bonus  | Starting Dice         |
|----------|-----|-------|---------|------------------|--------|-----------------------|
| Warrior  | 100 | 8     | 2–5     | Poker            | 1.25x  | 4x d6 + 2x d8        |
| Rogue    | 75  | 10    | 3–6     | Straight         | 1.50x  | 3x d6 + 2x d8 + 1xd12|
| Mage     | 60  | 12    | 1–4     | Generala         | 2.00x  | 2x d6 + 2x d8 + 2xd12|
| Cleric   | 120 | 6     | 2–4     | Full House       | 1.30x  | 6x d6                |

Design intent:
- **Warrior**: balanced, rewarded for grouping
- **Rogue**: fast and versatile, rewarded for variety (straights)
- **Mage**: slow and fragile but huge burst with Generala
- **Cleric**: tanky with lots of small dice, rewarded for split combos
