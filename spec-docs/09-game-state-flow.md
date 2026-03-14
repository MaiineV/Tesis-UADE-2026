# 09 — Game State & Flow Management

## Overview
This spec defines the complete state machine for the prototype, including all transitions, conditions, and the order of operations. This is the "glue" that connects all other systems.

## Dependencies
- References: All other specs
- Referenced by: None (this is the top-level orchestrator)

---

## 1. Master State Machine

```
                    ┌─────────────┐
                    │  MAIN MENU  │
                    └──────┬──────┘
                           │ [Start]
                           ▼
                    ┌─────────────┐
                    │ ROOM SETUP  │
                    └──────┬──────┘
                           │
                           ▼
              ┌────────────────────────┐
              │    MOVEMENT PHASE      │◄──────────────┐
              │  (Player + Enemies)    │               │
              └────────┬───────────────┘               │
                       │ collision                     │
                       ▼                               │
              ┌────────────────────────┐               │
         ┌───►│    PRE-COMBAT CHECK    │               │
         │    │  (Craps mode ready?)   │               │
         │    └────────┬───────┬───────┘               │
         │             │ no    │ yes                    │
         │             ▼       ▼                        │
         │    ┌──────────┐ ┌───────────┐               │
         │    │          │ │ CRAPS BET │               │
         │    │          │ └─────┬─────┘               │
         │    │          │       │                      │
         │    │          ◄───────┘                      │
         │    │  ATTACK  │                              │
         │    │  PHASE   │                              │
         │    └────┬─────┘                              │
         │         │                                    │
         │         ▼                                    │
         │    ┌──────────┐                              │
         │    │ DEFENSE  │                              │
         │    │  PHASE   │                              │
         │    └────┬─────┘                              │
         │         │                                    │
         │         ▼                                    │
         │    ┌──────────┐                              │
         │    │  ENEMY   │                              │
         │    │  ATTACK  │                              │
         │    └────┬─────┘                              │
         │         │                                    │
         │         ▼                                    │
         │    ┌──────────────┐                          │
         │    │  ROUND END   │                          │
         │    │  Check:      │                          │
         │    │  - Player    │                          │
         │    │    dead?     │──── yes ──► GAME OVER    │
         │    │  - Enemy     │                          │
         │    │    dead?     │──── yes ──► ENEMY DIED   │
         │    │  - Neither?  │                          │
         │    └──────┬───────┘                          │
         │           │ neither                          │
         └───────────┘ (next combat round)              │
                                                        │
              ┌────────────────────────┐                │
              │     ENEMY DIED         │                │
              │  Which enemy?          │                │
              └────┬──────────┬────────┘                │
                   │ enemy 1   │ enemy 2                │
                   ▼           ▼                        │
           ┌─────────────┐ ┌──────────┐                │
           │   REWARD    │ │ VICTORY  │                │
           │   SELECTION │ │          │                │
           └──────┬──────┘ └──────────┘                │
                  │ upgrade picked                      │
                  └────────────────────────────────────┘
                    (back to movement phase)
```

---

## 2. GameManager Implementation

```csharp
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    
    public enum GameState
    {
        MainMenu,
        RoomSetup,
        MovementPhase,
        PreCombat,
        CrapsBet,
        AttackPhase,
        DefensePhase,
        EnemyAttack,
        RoundEnd,
        RewardSelection,
        GameOver,
        Victory
    }
    
    public GameState CurrentState { get; private set; }
    
    // References (assigned in Inspector or found at runtime)
    public CharacterData PrototypeCharacter;    // Warrior ScriptableObject
    public EnemyData GoblinData;                // Enemy 1
    public EnemyData OrcData;                   // Enemy 2
    
    // Runtime references
    private PlayerEntity player;
    private List<EnemyEntity> enemies = new List<EnemyEntity>();
    private EnemyEntity currentCombatEnemy;
    private int enemiesDefeated = 0;
    private bool generalaScoredThisRun = false;
    
    // Statistics (for end screen)
    private int totalRoundsFought = 0;
    private int totalDamageDealt = 0;
    private int totalDamageTaken = 0;
    private CombinationType bestCombo = CombinationType.HighDie;
    private int bestComboDamage = 0;
    private int crapsAttempts = 0;
    private int crapsWins = 0;
    
    // Events
    public static event Action<GameState> OnStateChanged;
    
    private void Awake()
    {
        Instance = this;
    }
    
    // ──────────── STATE TRANSITIONS ────────────
    
    public void StartRun()
    {
        TransitionTo(GameState.RoomSetup);
        SetupRoom();
    }
    
    private void SetupRoom()
    {
        // 1. Generate grid
        GridManager.Instance.GenerateGrid();
        
        // 2. Spawn player
        Vector2Int playerSpawn = new Vector2Int(1, 1);
        player = SpawnPlayer(PrototypeCharacter, playerSpawn);
        
        // 3. Spawn enemies
        enemies.Clear();
        enemies.Add(SpawnEnemy(GoblinData, new Vector2Int(6, 5)));
        enemies.Add(SpawnEnemy(OrcData, new Vector2Int(5, 3)));
        
        // 4. Initialize energy
        EnergyManager.Instance.Initialize(player.State);
        
        // 5. Reset run state
        enemiesDefeated = 0;
        generalaScoredThisRun = false;
        ResetStats();
        
        // 6. Begin movement phase
        TransitionTo(GameState.MovementPhase);
        BeginPlayerMovement();
    }
    
    // ── MOVEMENT ──
    
    private void BeginPlayerMovement()
    {
        int steps = player.State.SpeedDie.Roll();
        UIManager.Instance.ShowSpeedRoll(steps);
        UIManager.Instance.ShowPhaseLabel("YOUR MOVE");
        
        var reachable = MovementManager.Instance.GetReachableTiles(
            player.State.GridPosition, steps);
        GridManager.Instance.HighlightTiles(reachable, TileHighlight.Reachable);
        
        // Wait for player to click a tile (handled by input system)
    }
    
    // Called by input system when player clicks a reachable tile
    public void OnPlayerMoveSelected(Vector2Int target)
    {
        if (CurrentState != GameState.MovementPhase) return;
        
        GridManager.Instance.ClearHighlights();
        
        var path = MovementManager.Instance.FindPath(player.State.GridPosition, target);
        var enemy = MovementManager.Instance.MovePlayerAlongPath(player, path);
        
        if (enemy != null)
        {
            EnterCombat(enemy);
        }
        else
        {
            // Enemy turn
            ProcessEnemyMovement();
        }
    }
    
    private void ProcessEnemyMovement()
    {
        UIManager.Instance.ShowPhaseLabel("ENEMY MOVE");
        
        foreach (var enemy in enemies)
        {
            if (!enemy.State.IsAlive) continue;
            
            bool collision = EnemyMovement.MoveEnemyTowardPlayer(enemy, player);
            if (collision)
            {
                EnterCombat(enemy);
                return;
            }
        }
        
        // No collision — back to player movement
        BeginPlayerMovement();
    }
    
    // ── COMBAT ENTRY ──
    
    private void EnterCombat(EnemyEntity enemy)
    {
        currentCombatEnemy = enemy;
        TransitionTo(GameState.PreCombat);
        
        UIManager.Instance.ShowPhaseLabel("COMBAT!");
        UIManager.Instance.ShowCombatUI(player, enemy);
        
        // Check craps
        if (player.State.CrapsModeAvailable)
        {
            TransitionTo(GameState.CrapsBet);
            UIManager.Instance.ShowCrapsBetUI();
            // Wait for player to select a bet
        }
        else
        {
            StartAttackPhase();
        }
    }
    
    // Called by UI when player places craps bet
    public void OnCrapsBetPlaced(CombinationType bet)
    {
        if (CurrentState != GameState.CrapsBet) return;
        CrapsMode.Instance.Activate();
        CrapsMode.Instance.PlaceBet(bet);
        crapsAttempts++;
        StartAttackPhase();
    }
    
    // ── ATTACK PHASE ──
    
    private AttackPhase currentAttack;
    
    private void StartAttackPhase()
    {
        TransitionTo(GameState.AttackPhase);
        currentAttack = new AttackPhase();
        UIManager.Instance.ShowPhaseLabel("YOUR ATTACK");
        UIManager.Instance.EnableAttackControls();
    }
    
    // Called by UI: "Roll" button
    public void OnPlayerRoll()
    {
        if (CurrentState != GameState.AttackPhase) return;
        if (currentAttack.CurrentRoll > 0 && !currentAttack.CanRollAgain) return;
        
        var results = currentAttack.PerformRoll(player.State.Bag);
        
        // Evaluate best combo for display
        int[] values = results.Select(r => r.Value).ToArray();
        var preview = CombinationDetector.Evaluate(values, generalaScoredThisRun);
        
        UIManager.Instance.UpdateDiceDisplay(results, currentAttack.LockedDiceIds);
        UIManager.Instance.UpdateComboPreview(preview);
        UIManager.Instance.UpdateRollCounter(currentAttack.CurrentRoll, currentAttack.MaxRolls);
        UIManager.Instance.SetRerollEnabled(currentAttack.CanRollAgain);
    }
    
    // Called by UI: click on a die to lock/unlock
    public void OnDiceToggleLock(string diceId)
    {
        if (CurrentState != GameState.AttackPhase) return;
        if (currentAttack.CurrentRoll == 0) return; // must roll at least once
        
        currentAttack.ToggleLock(diceId);
        
        // Recalculate combo preview
        int[] values = currentAttack.CurrentResults.Select(r => r.Value).ToArray();
        var preview = CombinationDetector.Evaluate(values, generalaScoredThisRun);
        
        UIManager.Instance.UpdateDiceDisplay(currentAttack.CurrentResults, currentAttack.LockedDiceIds);
        UIManager.Instance.UpdateComboPreview(preview);
    }
    
    // Called by UI: "Commit Attack" button
    public void OnPlayerCommitAttack()
    {
        if (CurrentState != GameState.AttackPhase) return;
        if (currentAttack.CurrentRoll == 0) return;
        
        var combo = currentAttack.Commit(generalaScoredThisRun);
        int damage = DamageResolver.ResolvePlayerAttack(combo, player.State.BaseData);
        
        // Apply craps modifier if active
        if (CrapsMode.Instance.IsActive)
        {
            var crapsResult = CrapsMode.Instance.Resolve(combo.Type, damage);
            damage = crapsResult.FinalDamage;
            
            if (crapsResult.Success) crapsWins++;
            if (crapsResult.HPChange != 0)
            {
                if (crapsResult.HPChange > 0) player.State.Heal(crapsResult.HPChange);
                else player.State.TakeDamage(-crapsResult.HPChange);
            }
            
            UIManager.Instance.ShowCrapsResult(crapsResult);
            EnergyManager.Instance.ResetPlayerEnergy();
        }
        
        // Track Generala
        if (combo.Type == CombinationType.Generala)
            generalaScoredThisRun = true;
        
        // Apply damage
        currentCombatEnemy.State.TakeDamage(damage);
        totalDamageDealt += damage;
        
        // Track best combo
        if (damage > bestComboDamage)
        {
            bestComboDamage = damage;
            bestCombo = combo.Type;
        }
        
        // Grant energy
        EnergyManager.Instance.ProcessCombatAction(CombatActionType.DealtDamage, combo.Type);
        
        UIManager.Instance.ShowDamageNumber(damage, currentCombatEnemy.transform.position);
        UIManager.Instance.UpdateEnemyHP(currentCombatEnemy);
        
        // Check enemy death
        if (!currentCombatEnemy.State.IsAlive)
        {
            EnergyManager.Instance.ProcessCombatAction(CombatActionType.KilledEnemy);
            HandleEnemyDeath();
            return;
        }
        
        // Move to defense
        StartDefensePhase(currentAttack.RollsUsed);
    }
    
    // ── DEFENSE PHASE ──
    
    private DefensePhase currentDefense;
    
    private void StartDefensePhase(int rollsUsedForAttack)
    {
        currentDefense = new DefensePhase(rollsUsedForAttack);
        
        if (currentDefense.AvailableRolls <= 0)
        {
            player.State.ShieldValue = 0;
            UIManager.Instance.UpdateShield(0);
            StartEnemyAttack();
            return;
        }
        
        TransitionTo(GameState.DefensePhase);
        UIManager.Instance.ShowPhaseLabel("DEFENSE");
        UIManager.Instance.ShowDefenseUI(currentDefense.AvailableRolls);
    }
    
    // Called by UI: "Roll Defense" button
    public void OnPlayerDefenseRoll()
    {
        if (CurrentState != GameState.DefensePhase) return;
        
        currentDefense.PerformDefenseRoll(player.State.Bag, generalaScoredThisRun);
        
        // Show defense roll results
        var lastResult = currentDefense.DefenseResults.Last();
        UIManager.Instance.ShowDefenseRollResult(lastResult);
        
        if (currentDefense.DefenseResults.Count >= currentDefense.AvailableRolls)
        {
            // All defense rolls done
            int shield = currentDefense.CalculateShield();
            player.State.ShieldValue = shield;
            UIManager.Instance.UpdateShield(shield);
            EnergyManager.Instance.ProcessCombatAction(CombatActionType.Defended);
            StartEnemyAttack();
        }
    }
    
    // ── ENEMY ATTACK ──
    
    private void StartEnemyAttack()
    {
        TransitionTo(GameState.EnemyAttack);
        UIManager.Instance.ShowPhaseLabel("ENEMY ATTACKS!");
        
        int rawDamage = currentCombatEnemy.RollAttack();
        int netDamage = DamageResolver.ResolveEnemyAttack(rawDamage, player.State.ShieldValue);
        
        player.State.TakeDamage(netDamage);
        player.State.ShieldValue = 0;
        totalDamageTaken += netDamage;
        
        if (netDamage > 0)
            EnergyManager.Instance.ProcessCombatAction(CombatActionType.TookDamage);
        
        UIManager.Instance.ShowEnemyAttackResult(rawDamage, player.State.ShieldValue, netDamage);
        UIManager.Instance.UpdatePlayerHP(player);
        UIManager.Instance.UpdateShield(0);
        
        // Check player death
        if (!player.State.IsAlive)
        {
            TransitionTo(GameState.GameOver);
            UIManager.Instance.ShowGameOver(GetRunStats());
            return;
        }
        
        // Next round
        totalRoundsFought++;
        TransitionTo(GameState.RoundEnd);
        
        // Brief pause then start next round
        StartCoroutine(NextRoundDelay());
    }
    
    private IEnumerator NextRoundDelay()
    {
        yield return new WaitForSeconds(1f);
        
        // Check craps for next round
        if (player.State.CrapsModeAvailable)
        {
            TransitionTo(GameState.CrapsBet);
            UIManager.Instance.ShowCrapsBetUI();
        }
        else
        {
            StartAttackPhase();
        }
    }
    
    // ── ENEMY DEATH ──
    
    private void HandleEnemyDeath()
    {
        enemiesDefeated++;
        UIManager.Instance.ShowEnemyDeath(currentCombatEnemy);
        
        // Remove enemy from grid
        GridManager.Instance.ClearOccupant(currentCombatEnemy.State.GridPosition);
        currentCombatEnemy.gameObject.SetActive(false);
        
        if (enemiesDefeated == 1)
        {
            // Show reward
            TransitionTo(GameState.RewardSelection);
            var offers = RewardGenerator.GenerateOffers(player.State.Bag, 2);
            UIManager.Instance.ShowRewardSelection(offers);
        }
        else if (enemiesDefeated >= 2)
        {
            // Victory!
            TransitionTo(GameState.Victory);
            UIManager.Instance.ShowVictory(GetRunStats());
        }
    }
    
    // Called by UI when player picks a reward
    public void OnRewardSelected(FaceUpgradeOffer offer)
    {
        if (CurrentState != GameState.RewardSelection) return;
        
        // Apply upgrade
        var die = player.State.Bag.Dice.First(d => d.Id == offer.TargetDiceId);
        DiceUpgrader.ApplyUpgrade(die, offer.Upgrade);
        
        UIManager.Instance.ShowUpgradeApplied(offer);
        UIManager.Instance.HideRewardSelection();
        UIManager.Instance.HideCombatUI();
        
        // Back to movement phase
        TransitionTo(GameState.MovementPhase);
        BeginPlayerMovement();
    }
    
    // Called by UI: restart button
    public void RestartRun()
    {
        StartRun();
    }
    
    // ── HELPERS ──
    
    private void TransitionTo(GameState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }
    
    private RunStats GetRunStats()
    {
        return new RunStats
        {
            RoundsFought = totalRoundsFought,
            DamageDealt = totalDamageDealt,
            DamageTaken = totalDamageTaken,
            BestCombo = bestCombo,
            BestComboDamage = bestComboDamage,
            CrapsAttempts = crapsAttempts,
            CrapsWins = crapsWins,
            EnemiesDefeated = enemiesDefeated
        };
    }
    
    private void ResetStats()
    {
        totalRoundsFought = 0;
        totalDamageDealt = 0;
        totalDamageTaken = 0;
        bestCombo = CombinationType.HighDie;
        bestComboDamage = 0;
        crapsAttempts = 0;
        crapsWins = 0;
    }
    
    private PlayerEntity SpawnPlayer(CharacterData data, Vector2Int pos)
    {
        var go = Instantiate(playerPrefab, GridManager.Instance.GridToWorld(pos), Quaternion.identity);
        var entity = go.GetComponent<PlayerEntity>();
        entity.Initialize(data, pos);
        GridManager.Instance.SetOccupant(pos, go);
        return entity;
    }
    
    private EnemyEntity SpawnEnemy(EnemyData data, Vector2Int pos)
    {
        var go = Instantiate(enemyPrefab, GridManager.Instance.GridToWorld(pos), Quaternion.identity);
        var entity = go.GetComponent<EnemyEntity>();
        entity.Initialize(data, pos);
        GridManager.Instance.SetOccupant(pos, go);
        return entity;
    }
}

[System.Serializable]
public struct RunStats
{
    public int RoundsFought;
    public int DamageDealt;
    public int DamageTaken;
    public CombinationType BestCombo;
    public int BestComboDamage;
    public int CrapsAttempts;
    public int CrapsWins;
    public int EnemiesDefeated;
}
```

---

## 3. Input Handling Summary

| Game State       | Mouse Click Action                          | Keyboard Shortcut |
|------------------|---------------------------------------------|--------------------|
| MovementPhase    | Click reachable tile → move player          | None               |
| AttackPhase      | Click die → toggle lock                     | Space → Roll       |
| AttackPhase      | Click "Reroll" → reroll unlocked dice       | R → Reroll         |
| AttackPhase      | Click "Commit" → commit attack              | Enter → Commit     |
| DefensePhase     | Click "Roll Defense" → defense roll          | Space → Roll       |
| CrapsBet         | Click a combo option → place bet            | 1-6 → Select bet  |
| RewardSelection  | Click option A or B → pick reward           | 1/2 → Select      |
| GameOver/Victory | Click "Restart" → restart run               | R → Restart        |
