# 05 — Energy & Craps System

## Overview
Energy builds during combat. When full, the player unlocks "Craps Mode" — a high risk/reward betting mechanic where they predict their next combination before rolling. Enemies have their own energy bar that powers up their attacks.

## Dependencies
- References: `01-dice-system.md`, `03-combat-system.md`
- Referenced by: `03-combat-system.md` (integrated into combat flow)

---

## 1. Player Energy Bar

### 1.1 Energy Rules
- Starts at 0 at the beginning of each combat encounter
- Builds up based on combat actions
- Max energy = 100
- When full (100/100) → Craps Mode becomes available for the NEXT round
- After using Craps Mode (success or fail), energy resets to 0

### 1.2 Energy Gain Sources

| Action                              | Energy Gained |
|--------------------------------------|---------------|
| Deal damage (any combo)             | +10           |
| Score a Three of a Kind or better   | +15           |
| Score a Full House                   | +20           |
| Score a Four of a Kind               | +25           |
| Score a Generala                     | +50 (instant fill option)|
| Successfully defend (shield > 0)     | +5            |
| Take damage                          | +5            |
| Kill an enemy                        | +10           |

### 1.3 Energy Logic
```csharp
public class EnergyManager : MonoBehaviour
{
    public static EnergyManager Instance;
    
    // Events
    public static event Action<float> OnPlayerEnergyChanged;    // normalized 0-1
    public static event Action OnPlayerEnergyFull;
    public static event Action<float> OnEnemyEnergyChanged;
    
    private PlayerState playerState;
    
    public void Initialize(PlayerState player)
    {
        playerState = player;
        playerState.CurrentEnergy = 0;
        playerState.MaxEnergy = 100;
    }
    
    public void AddPlayerEnergy(float amount)
    {
        playerState.CurrentEnergy = Mathf.Min(
            playerState.MaxEnergy, 
            playerState.CurrentEnergy + amount
        );
        
        OnPlayerEnergyChanged?.Invoke(playerState.CurrentEnergy / playerState.MaxEnergy);
        
        if (playerState.CurrentEnergy >= playerState.MaxEnergy)
        {
            playerState.CrapsModeAvailable = true;
            OnPlayerEnergyFull?.Invoke();
        }
    }
    
    public void ResetPlayerEnergy()
    {
        playerState.CurrentEnergy = 0;
        playerState.CrapsModeAvailable = false;
        OnPlayerEnergyChanged?.Invoke(0);
    }
    
    /// Call after combat actions to grant appropriate energy
    public void ProcessCombatAction(CombatActionType action, CombinationType combo = CombinationType.HighDie)
    {
        float gain = 0;
        switch (action)
        {
            case CombatActionType.DealtDamage:
                gain = 10;
                if (combo == CombinationType.ThreeOfAKind) gain = 15;
                else if (combo == CombinationType.FullHouse) gain = 20;
                else if (combo == CombinationType.FourOfAKind) gain = 25;
                else if (combo == CombinationType.Generala || 
                         combo == CombinationType.DoubleGenerala) gain = 50;
                break;
            case CombatActionType.Defended:
                gain = 5;
                break;
            case CombatActionType.TookDamage:
                gain = 5;
                break;
            case CombatActionType.KilledEnemy:
                gain = 10;
                break;
        }
        AddPlayerEnergy(gain);
    }
}

public enum CombatActionType
{
    DealtDamage,
    Defended,
    TookDamage,
    KilledEnemy
}
```

---

## 2. Craps Mode (Player Super Roll)

### 2.1 How It Works
When energy is full, before the player's next attack roll, they enter Craps Mode:

```
1. UI shows: "CRAPS MODE ACTIVATED — Place your bet!"
2. Player selects a combination they think they'll roll:
   - Pair
   - Three of a Kind
   - Straight
   - Full House
   - Four of a Kind
   - Generala
3. Player then rolls normally (3 rolls with locking)
4. After committing:
   - If the final combo matches the bet → BONUS
   - If the final combo does NOT match → PENALTY
5. Energy resets to 0 regardless of outcome
```

### 2.2 Bet Outcomes

| Bet Combination  | On Success                          | On Failure                    |
|------------------|-------------------------------------|-------------------------------|
| Pair             | +25% damage                         | -10% damage                   |
| Three of a Kind  | +50% damage                         | -15% damage                   |
| Straight         | +50% damage + heal 10 HP            | -15% damage                   |
| Full House       | +75% damage                         | -20% damage                   |
| Four of a Kind   | +100% damage (2x)                   | -25% damage + lose 5 HP       |
| Generala         | +200% damage (3x) + heal 20 HP      | -50% damage + lose 10 HP      |

Higher-risk bets give more reward but also worse penalties. This creates a meaningful choice.

### 2.3 Craps Mode Logic
```csharp
public class CrapsMode
{
    public bool IsActive { get; private set; }
    public CombinationType BetCombo { get; private set; }
    
    // Events
    public static event Action OnCrapsModeStarted;
    public static event Action<CombinationType> OnBetPlaced;
    public static event Action<bool, CrapsResult> OnCrapsResolved; // success, result
    
    public void Activate()
    {
        IsActive = true;
        OnCrapsModeStarted?.Invoke();
    }
    
    public void PlaceBet(CombinationType bet)
    {
        BetCombo = bet;
        OnBetPlaced?.Invoke(bet);
    }
    
    public CrapsResult Resolve(CombinationType actualCombo, int baseDamage)
    {
        bool success = IsMatchingBet(actualCombo);
        var result = new CrapsResult();
        result.Success = success;
        result.BetCombo = BetCombo;
        result.ActualCombo = actualCombo;
        
        if (success)
        {
            result.DamageMultiplier = GetSuccessMultiplier(BetCombo);
            result.HPChange = GetSuccessHeal(BetCombo);
        }
        else
        {
            result.DamageMultiplier = GetFailureMultiplier(BetCombo);
            result.HPChange = GetFailureDamage(BetCombo);
        }
        
        result.FinalDamage = Mathf.RoundToInt(baseDamage * result.DamageMultiplier);
        
        IsActive = false;
        OnCrapsResolved?.Invoke(success, result);
        return result;
    }
    
    private bool IsMatchingBet(CombinationType actual)
    {
        // Exact match or better counts as success
        // e.g., bet Pair but got Four of a Kind → still success
        if (actual == BetCombo) return true;
        
        // "Or better" rules:
        int betRank = GetComboRank(BetCombo);
        int actualRank = GetComboRank(actual);
        
        // Special case: Straight and Full House don't upgrade to each other
        // Only N-of-a-kind combos upgrade within their line
        if (IsNOfAKind(BetCombo) && IsNOfAKind(actual) && actualRank >= betRank)
            return true;
        
        return false;
    }
    
    private bool IsNOfAKind(CombinationType type)
    {
        return type == CombinationType.Pair || 
               type == CombinationType.ThreeOfAKind ||
               type == CombinationType.FourOfAKind || 
               type == CombinationType.Generala;
    }
    
    private int GetComboRank(CombinationType type)
    {
        switch (type)
        {
            case CombinationType.Pair: return 1;
            case CombinationType.ThreeOfAKind: return 2;
            case CombinationType.Straight: return 3;
            case CombinationType.FullHouse: return 4;
            case CombinationType.FourOfAKind: return 5;
            case CombinationType.Generala: return 6;
            default: return 0;
        }
    }
    
    private float GetSuccessMultiplier(CombinationType bet)
    {
        switch (bet)
        {
            case CombinationType.Pair: return 1.25f;
            case CombinationType.ThreeOfAKind: return 1.5f;
            case CombinationType.Straight: return 1.5f;
            case CombinationType.FullHouse: return 1.75f;
            case CombinationType.FourOfAKind: return 2.0f;
            case CombinationType.Generala: return 3.0f;
            default: return 1.0f;
        }
    }
    
    private float GetFailureMultiplier(CombinationType bet)
    {
        switch (bet)
        {
            case CombinationType.Pair: return 0.9f;
            case CombinationType.ThreeOfAKind: return 0.85f;
            case CombinationType.Straight: return 0.85f;
            case CombinationType.FullHouse: return 0.8f;
            case CombinationType.FourOfAKind: return 0.75f;
            case CombinationType.Generala: return 0.5f;
            default: return 1.0f;
        }
    }
    
    private int GetSuccessHeal(CombinationType bet)
    {
        switch (bet)
        {
            case CombinationType.Straight: return 10;
            case CombinationType.Generala: return 20;
            default: return 0;
        }
    }
    
    private int GetFailureDamage(CombinationType bet)
    {
        switch (bet)
        {
            case CombinationType.FourOfAKind: return -5;
            case CombinationType.Generala: return -10;
            default: return 0;
        }
    }
}

[System.Serializable]
public struct CrapsResult
{
    public bool Success;
    public CombinationType BetCombo;
    public CombinationType ActualCombo;
    public float DamageMultiplier;
    public int HPChange;            // positive = heal, negative = self-damage
    public int FinalDamage;
}
```

---

## 3. Enemy Energy Bar

### 3.1 Rules
- Each enemy has its own energy bar
- Fills over time (each round of combat)
- When full → enemy's next attack has a chance to deal double damage

### 3.2 Enemy Energy Logic
```csharp
// Inside EnemyState
public float CurrentEnergy;
public float MaxEnergy = 50f;   // fills faster than player's
public float EnergyPerRound = 15f;
public bool IsEnraged => CurrentEnergy >= MaxEnergy;

public void GainEnergy()
{
    CurrentEnergy = Mathf.Min(MaxEnergy, CurrentEnergy + EnergyPerRound);
}

public int RollAttackWithEnergy(int baseAttackRoll)
{
    if (IsEnraged)
    {
        // 60% chance to deal double damage when enraged
        bool criticalHit = Random.value < 0.6f;
        CurrentEnergy = 0; // reset after use
        return criticalHit ? baseAttackRoll * 2 : baseAttackRoll;
    }
    return baseAttackRoll;
}
```

---

## 4. Integration with Combat Flow

The Craps system inserts into the combat flow BEFORE the attack phase:

```
ROUND START
    │
    ├── Is Craps Mode available?
    │   ├── YES → Show bet selection UI
    │   │         Player picks a combo to bet on
    │   │         ↓
    │   │   Normal attack phase (3 rolls)
    │   │         ↓
    │   │   Evaluate: did actual combo match bet?
    │   │   Apply bonus/penalty to damage
    │   │   Reset energy to 0
    │   │
    │   └── NO → Normal attack phase
    │
    ▼
(rest of combat round as normal)
```

---

## 5. UI Requirements

### Energy Bar (Player)
```
ENERGY: ██████████░░░░░░░░░░ 50/100
```
- Fills from left to right
- Color ramp: blue (low) → yellow (mid) → red (high) → glowing gold (full)
- When full: pulsing animation + text "CRAPS MODE READY!"

### Energy Bar (Enemy)
```
ENEMY ENERGY: ████████░░ 40/50
```
- Same visual concept, red color scheme
- When full: enemy sprite flashes red, text "ENRAGED!"

### Craps Bet UI
When Craps Mode activates, show an overlay:
```
┌──────────────────────────────────┐
│      🎰 CRAPS MODE — PLACE BET   │
│                                  │
│  What combo will you roll?       │
│                                  │
│  [Pair]           +25% / -10%    │
│  [Three of a Kind] +50% / -15%   │
│  [Straight]        +50% / -15%   │
│  [Full House]      +75% / -20%   │
│  [Four of a Kind]  +100% / -25%  │
│  [GENERALA]        +200% / -50%  │
│                                  │
│  Risk higher = reward higher!    │
└──────────────────────────────────┘
```

### Craps Result
After the attack resolves, flash the result:
- **Success**: Green screen flash, "BET WON! 🎰 +75% damage!"
- **Failure**: Red screen flash, "BET LOST! 💀 -20% damage"
