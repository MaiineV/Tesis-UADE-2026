# 07 — Enemy System

## Overview
Enemies are grid-based entities that move toward the player using their own speed dice and attack with a single roll. For the prototype, there are 2 enemies in a single room: a standard enemy and a tougher variant.

## Dependencies
- References: `01-dice-system.md`, `04-movement-grid-system.md`
- Referenced by: `03-combat-system.md`, `05-energy-craps-system.md`

---

## 1. Enemy Data Definition

```csharp
[CreateAssetMenu(menuName = "Game/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string EnemyName;
    public Sprite Sprite;           // placeholder: colored triangle
    public Color EnemyColor;
    
    [Header("Stats")]
    public int MaxHP;
    public int AttackDiceCount;     // how many dice the enemy rolls to attack
    public int AttackDiceFaces;     // what kind of dice (e.g., 6 = d6)
    
    [Header("Movement")]
    public int SpeedMin;
    public int SpeedMax;
    
    [Header("Energy")]
    public float MaxEnergy;
    public float EnergyPerRound;
    
    [Header("Behavior")]
    public EnemyBehavior Behavior;
}

public enum EnemyBehavior
{
    Aggressive,     // always moves toward player
    Cautious,       // moves toward player but stops 2 tiles away sometimes
    Stationary      // doesn't move (for future use: turrets, traps)
}
```

---

## 2. Enemy Runtime State

```csharp
[System.Serializable]
public class EnemyState
{
    public EnemyData BaseData;
    public int CurrentHP;
    public int MaxHP;
    public Vector2Int GridPosition;
    public float CurrentEnergy;
    public SpeedDie SpeedDie;
    
    public bool IsAlive => CurrentHP > 0;
    public bool IsEnraged => CurrentEnergy >= BaseData.MaxEnergy;
    
    public static EnemyState Create(EnemyData data, Vector2Int position)
    {
        return new EnemyState
        {
            BaseData = data,
            CurrentHP = data.MaxHP,
            MaxHP = data.MaxHP,
            GridPosition = position,
            CurrentEnergy = 0,
            SpeedDie = new SpeedDie
            {
                MinValue = data.SpeedMin,
                MaxValue = data.SpeedMax
            }
        };
    }
    
    public void TakeDamage(int amount)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
    }
    
    public void GainEnergy()
    {
        CurrentEnergy = Mathf.Min(BaseData.MaxEnergy, CurrentEnergy + BaseData.EnergyPerRound);
    }
}
```

---

## 3. Enemy Attack

Enemies roll ONCE per combat round. Their roll = damage dealt to the player.

```csharp
public class EnemyEntity : MonoBehaviour
{
    public EnemyState State { get; private set; }
    public SpriteRenderer Visual;
    
    public void Initialize(EnemyData data, Vector2Int position)
    {
        State = EnemyState.Create(data, position);
        Visual.color = data.EnemyColor;
        transform.position = GridManager.Instance.GridToWorld(position);
    }
    
    /// Roll attack. Returns final damage value.
    public int RollAttack()
    {
        int totalDamage = 0;
        
        // Roll N dice of the enemy's attack type
        for (int i = 0; i < State.BaseData.AttackDiceCount; i++)
        {
            totalDamage += Random.Range(1, State.BaseData.AttackDiceFaces + 1);
        }
        
        // Apply enrage bonus
        if (State.IsEnraged)
        {
            bool crit = Random.value < 0.6f;
            State.CurrentEnergy = 0; // reset
            if (crit)
            {
                totalDamage *= 2;
            }
        }
        
        // Gain energy for next round
        State.GainEnergy();
        
        return totalDamage;
    }
    
    public void MoveTo(Vector2Int newPosition)
    {
        State.GridPosition = newPosition;
        transform.position = GridManager.Instance.GridToWorld(newPosition);
    }
}
```

---

## 4. Prototype Enemies

### Enemy 1: Goblin
| Stat             | Value                           |
|------------------|---------------------------------|
| Name             | Goblin                          |
| HP               | 40                              |
| Attack           | 2x d6 (2–12 damage per round)  |
| Speed            | min 1, max 3                    |
| Energy Max       | 50                              |
| Energy Per Round | 15                              |
| Behavior         | Aggressive                      |
| Color            | Green                           |

### Enemy 2: Orc
| Stat             | Value                           |
|------------------|---------------------------------|
| Name             | Orc                             |
| HP               | 60                              |
| Attack           | 2x d8 (2–16 damage per round)  |
| Speed            | min 1, max 2                    |
| Energy Max       | 40                              |
| Energy Per Round | 12                              |
| Behavior         | Aggressive                      |
| Color            | Red                             |

**Design intent**: Goblin is fast but weak. Orc is slow but hits harder and has more HP. The player fights the Goblin first, gets an upgrade, then faces the Orc — testing whether the upgrade makes the second fight feel meaningfully different.

---

## 5. Enemy AI (Movement)

For the prototype, enemy AI is simple: always move toward the player using the shortest path.

```csharp
public static class EnemyAI
{
    public static Vector2Int DecideMovement(EnemyEntity enemy, PlayerEntity player, int steps)
    {
        switch (enemy.State.BaseData.Behavior)
        {
            case EnemyBehavior.Aggressive:
                return MoveTowardPlayer(enemy, player, steps);
                
            case EnemyBehavior.Cautious:
                // Keep 2 tiles distance sometimes (future feature)
                return MoveTowardPlayer(enemy, player, steps);
                
            case EnemyBehavior.Stationary:
                return enemy.State.GridPosition; // don't move
                
            default:
                return MoveTowardPlayer(enemy, player, steps);
        }
    }
    
    private static Vector2Int MoveTowardPlayer(EnemyEntity enemy, PlayerEntity player, int steps)
    {
        var path = MovementManager.Instance.FindPath(
            enemy.State.GridPosition, player.State.GridPosition);
        
        if (path.Count == 0) return enemy.State.GridPosition;
        
        // Move up to 'steps' tiles, but stop 1 before player tile
        int maxSteps = Mathf.Min(steps, path.Count);
        
        // If the path reaches the player, stop at the last tile before player
        // (combat triggers on adjacency or collision)
        for (int i = 0; i < maxSteps; i++)
        {
            if (path[i] == player.State.GridPosition)
            {
                // Return the tile just before the player
                return i > 0 ? path[i - 1] : enemy.State.GridPosition;
            }
        }
        
        return path[maxSteps - 1];
    }
}
```

---

## 6. Enemy Death & Prototype Flow

```csharp
public class PrototypeFlowManager : MonoBehaviour
{
    private int enemiesDefeated = 0;
    private PlayerEntity player;
    private List<EnemyEntity> enemies;
    
    private void OnEnable()
    {
        CombatManager.OnCombatEnded += HandleCombatEnd;
    }
    
    private void HandleCombatEnd(bool playerWon)
    {
        if (!playerWon)
        {
            // Game Over
            ShowGameOver();
            return;
        }
        
        enemiesDefeated++;
        
        if (enemiesDefeated == 1)
        {
            // First enemy dead → show reward selection
            var offers = RewardGenerator.GenerateOffers(player.State.Bag, 2);
            ShowRewardUI(offers, () =>
            {
                // After reward picked, resume movement phase
                // Player must now reach and fight enemy 2
                TurnManager.Instance.StartMovementPhase();
            });
        }
        else if (enemiesDefeated == 2)
        {
            // Both enemies dead → victory!
            ShowVictory();
        }
    }
    
    private void ShowRewardUI(List<FaceUpgradeOffer> offers, System.Action onComplete)
    {
        // Display 2 options, wait for player to pick one
        // Apply the chosen upgrade to the player's dice bag
        // Then call onComplete()
    }
    
    private void ShowGameOver()
    {
        // Show "GAME OVER" screen
        // Option to restart
    }
    
    private void ShowVictory()
    {
        // Show "VICTORY" screen
        // Show stats: rounds fought, damage dealt, combos scored
        // Option to restart
    }
}
```

---

## 7. Enemy Visual Feedback

### Health Bar (above enemy sprite)
```
GOBLIN
██████████░░░░ 25/40
```
- Green when > 50% HP
- Yellow when 25-50% HP
- Red when < 25% HP
- Flash white when taking damage

### Enrage Indicator
- When energy is full: enemy sprite pulses red
- Text above: "⚡ ENRAGED"
- Next attack will show a warning: "Enemy is powering up!"

### Damage Numbers
- When enemy takes damage: floating number rises from enemy position
- Color: white for normal, yellow for affinity bonus, red for craps bonus
- When player takes damage: floating red number rises from player
