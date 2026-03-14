using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    [SerializeField] private CharacterData PrototypeCharacter;
    [SerializeField] private EnemyData GoblinData;
    [SerializeField] private EnemyData OrcData;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject enemyPrefab;

    // Runtime references
    private PlayerEntity player;
    private List<EnemyEntity> enemies = new List<EnemyEntity>();
    private EnemyEntity currentCombatEnemy;
    private int enemiesDefeated = 0;
    private bool generalaScoredThisRun = false;

    // Craps instance (CrapsMode is a plain class, not MonoBehaviour)
    private CrapsMode crapsMode = new CrapsMode();

    // Statistics (for end screen)
    private int totalRoundsFought = 0;
    private int totalDamageDealt = 0;
    private int totalDamageTaken = 0;
    private CombinationType bestCombo = CombinationType.HighDie;
    private int bestComboDamage = 0;
    private int crapsAttempts = 0;
    private int crapsWins = 0;

    // Movement state
    private List<Vector2Int> currentReachableTiles;

    // Combat state
    private AttackPhase currentAttack;
    private DefensePhase currentDefense;

    // Events
    public static event Action<GameState> OnStateChanged;

    private void Awake()
    {
        Instance = this;
    }

    public void InitializeData(CharacterData character, EnemyData goblin, EnemyData orc,
        GameObject playerPrefabRef, GameObject enemyPrefabRef)
    {
        if (PrototypeCharacter == null) PrototypeCharacter = character;
        if (GoblinData == null) GoblinData = goblin;
        if (OrcData == null) OrcData = orc;
        if (playerPrefab == null) playerPrefab = playerPrefabRef;
        if (enemyPrefab == null) enemyPrefab = enemyPrefabRef;
    }

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    // ──────────── EVENT WIRING ────────────

    private void SubscribeToEvents()
    {
        // CombatUI events
        if (CombatUI.Instance != null)
        {
            CombatUI.Instance.OnRerollClicked += OnPlayerRoll;
            CombatUI.Instance.OnDieLockToggled += OnDiceToggleLock;
            CombatUI.Instance.OnCommitClicked += OnPlayerCommitAttack;
            CombatUI.Instance.OnRollDefenseClicked += OnPlayerDefenseRoll;
            CombatUI.Instance.OnContinueClicked += OnContinueAfterEnemyAttack;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (CombatUI.Instance != null)
        {
            CombatUI.Instance.OnRerollClicked -= OnPlayerRoll;
            CombatUI.Instance.OnDieLockToggled -= OnDiceToggleLock;
            CombatUI.Instance.OnCommitClicked -= OnPlayerCommitAttack;
            CombatUI.Instance.OnRollDefenseClicked -= OnPlayerDefenseRoll;
            CombatUI.Instance.OnContinueClicked -= OnContinueAfterEnemyAttack;
        }
    }

    // Late-bind events that may not be ready at OnEnable time
    private void Start()
    {
        // Re-subscribe since UI singletons may have initialized after us
        UnsubscribeFromEvents();
        SubscribeToEvents();

        // Bind CrapsUI
        var crapsUI = FindObjectOfType<CrapsUI>();
        if (crapsUI != null)
            crapsUI.OnBetSelected += OnCrapsBetPlaced;

        // Bind RewardUI
        var rewardUI = FindObjectOfType<RewardUI>();
        if (rewardUI != null)
            rewardUI.OnRewardChosen += OnRewardSelected;

        // Bind GameOverUI
        var gameOverUI = FindObjectOfType<GameOverUI>();
        if (gameOverUI != null)
            gameOverUI.OnRestartClicked += RestartRun;

        // Bind VictoryUI
        var victoryUI = FindObjectOfType<VictoryUI>();
        if (victoryUI != null)
            victoryUI.OnRestartClicked += RestartRun;

        // Auto-start the run
        StartRun();
    }

    // ──────────── STATE TRANSITIONS ────────────

    public void StartRun()
    {
        // Clean up previous run
        CleanupPreviousRun();

        TransitionTo(GameState.RoomSetup);
        SetupRoom();
    }

    private void CleanupPreviousRun()
    {
        // Destroy old player
        if (player != null)
        {
            GridManager.Instance.ClearOccupant(player.State.GridPosition);
            Destroy(player.gameObject);
            player = null;
        }

        // Destroy old enemies
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                GridManager.Instance.ClearOccupant(enemy.State.GridPosition);
                Destroy(enemy.gameObject);
            }
        }
        enemies.Clear();

        // Destroy old grid
        var oldGrid = GameObject.Find("Grid");
        if (oldGrid != null) Destroy(oldGrid);

        // Hide all UI
        UIManager.Instance.HideAllPanels();
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
        crapsMode = new CrapsMode();
        ResetStats();

        // 6. Update HUD
        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);
        UIManager.Instance.UpdateEnergy(0f);
        UIManager.Instance.UpdateShield(0);

        // 7. Clear combat log
        var combatLog = FindObjectOfType<CombatLogUI>();
        if (combatLog != null) combatLog.Clear();

        // 8. Begin movement phase
        TransitionTo(GameState.MovementPhase);
        BeginPlayerMovement();
    }

    // ── MOVEMENT ──

    private void BeginPlayerMovement()
    {
        int steps = player.State.SpeedDie.Roll();
        UIManager.Instance.ShowPhaseLabel($"YOUR MOVE ({steps} steps)");
        Log($"Speed roll: {steps} tiles");

        currentReachableTiles = MovementManager.Instance.GetReachableTiles(
            player.State.GridPosition, steps);
        GridManager.Instance.HighlightTiles(currentReachableTiles, Color.green);

        // Wait for player to click a tile (handled by Update)
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
        TransitionTo(GameState.MovementPhase);
        BeginPlayerMovement();
    }

    // ── COMBAT ENTRY ──

    private void EnterCombat(EnemyEntity enemy)
    {
        currentCombatEnemy = enemy;
        TransitionTo(GameState.PreCombat);

        UIManager.Instance.ShowPhaseLabel("COMBAT!");
        UIManager.Instance.ShowCombatPanel();
        Log($"Combat started with {enemy.State.BaseData.EnemyName}!");

        // Update HUD with enemy info
        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);

        // Check craps
        if (player.State.CrapsModeAvailable)
        {
            TransitionTo(GameState.CrapsBet);
            UIManager.Instance.ShowCrapsOverlay();
            // Wait for player to select a bet
        }
        else
        {
            StartAttackPhase();
        }
    }

    // Called by CrapsUI when player places craps bet
    public void OnCrapsBetPlaced(CombinationType bet)
    {
        if (CurrentState != GameState.CrapsBet) return;
        crapsMode.Activate();
        crapsMode.PlaceBet(bet);
        crapsAttempts++;
        UIManager.Instance.HideCrapsOverlay();
        StartAttackPhase();
    }

    // ── ATTACK PHASE ──

    private void StartAttackPhase()
    {
        TransitionTo(GameState.AttackPhase);
        currentAttack = new AttackPhase();
        UIManager.Instance.ShowPhaseLabel("YOUR ATTACK");

        // Auto-perform first roll
        OnPlayerRoll();
    }

    // Called by UI: "Roll" / "Reroll" button
    public void OnPlayerRoll()
    {
        if (CurrentState != GameState.AttackPhase) return;
        if (currentAttack.CurrentRoll > 0 && !currentAttack.CanRollAgain) return;

        var results = currentAttack.PerformRoll(player.State.Bag);

        // Evaluate best combo for display
        int[] values = results.Select(r => r.Value).ToArray();
        var preview = CombinationDetector.Evaluate(values, generalaScoredThisRun);

        CombatUI.Instance.ShowAttackUI(results, currentAttack.LockedDiceIds);
        CombatUI.Instance.UpdateComboPreview(preview);
        CombatUI.Instance.UpdateRollCounter(currentAttack.CurrentRoll, currentAttack.MaxRolls);
        CombatUI.Instance.SetRerollEnabled(currentAttack.CanRollAgain);
        CombatUI.Instance.SetCommitEnabled(true);
    }

    // Called by UI: click on a die to lock/unlock
    public void OnDiceToggleLock(string diceId)
    {
        if (CurrentState != GameState.AttackPhase) return;
        if (currentAttack.CurrentRoll == 0) return;

        currentAttack.ToggleLock(diceId);

        // Recalculate combo preview
        int[] values = currentAttack.CurrentResults.Select(r => r.Value).ToArray();
        var preview = CombinationDetector.Evaluate(values, generalaScoredThisRun);

        CombatUI.Instance.ShowAttackUI(currentAttack.CurrentResults, currentAttack.LockedDiceIds);
        CombatUI.Instance.UpdateComboPreview(preview);
    }

    // Called by UI: "Commit Attack" button
    public void OnPlayerCommitAttack()
    {
        if (CurrentState != GameState.AttackPhase) return;
        if (currentAttack.CurrentRoll == 0) return;

        var combo = currentAttack.Commit(generalaScoredThisRun);
        int damage = DamageResolver.ResolvePlayerAttack(combo, player.State.BaseData);

        // Apply craps modifier if active
        if (crapsMode.IsActive)
        {
            var crapsResult = crapsMode.Resolve(combo.Type, damage);
            damage = crapsResult.FinalDamage;

            if (crapsResult.Success) crapsWins++;
            if (crapsResult.HPChange != 0)
            {
                if (crapsResult.HPChange > 0)
                    player.State.Heal(crapsResult.HPChange);
                else
                    player.State.CurrentHP = Mathf.Max(0, player.State.CurrentHP + crapsResult.HPChange);
            }

            // Show craps result
            var crapsUI = FindObjectOfType<CrapsUI>();
            if (crapsUI != null) crapsUI.ShowResult(crapsResult);

            // Screen flash for craps
            if (ScreenFlashUI.Instance != null)
            {
                if (crapsResult.Success)
                    ScreenFlashUI.Instance.FlashCrapsSuccess();
                else
                    ScreenFlashUI.Instance.FlashCrapsFailure();
            }

            Log(crapsResult.Success ? "Craps bet WON!" : "Craps bet LOST!");

            EnergyManager.Instance.ResetPlayerEnergy();
            UIManager.Instance.UpdateEnergy(0f);
        }

        // Track Generala
        if (combo.Type == CombinationType.Generala)
            generalaScoredThisRun = true;

        // Apply damage to enemy
        currentCombatEnemy.State.TakeDamage(damage);
        totalDamageDealt += damage;

        // Visual feedback: floating damage number
        if (FloatingDamageUI.Instance != null)
            FloatingDamageUI.Instance.ShowDamage(damage, currentCombatEnemy.transform.position);

        Log($"You dealt {damage} damage ({combo.Type})");

        // Track best combo
        if (damage > bestComboDamage)
        {
            bestComboDamage = damage;
            bestCombo = combo.Type;
        }

        // Grant energy
        EnergyManager.Instance.ProcessCombatAction(CombatActionType.DealtDamage, combo.Type);
        UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);
        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);

        // Check enemy death
        if (!currentCombatEnemy.State.IsAlive)
        {
            EnergyManager.Instance.ProcessCombatAction(CombatActionType.KilledEnemy);
            UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);
            HandleEnemyDeath();
            return;
        }

        // Move to defense
        StartDefensePhase(currentAttack.RollsUsed);
    }

    // ── DEFENSE PHASE ──

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
        CombatUI.Instance.ShowDefenseUI(currentDefense.AvailableRolls);
    }

    // Called by UI: "Roll Defense" button
    public void OnPlayerDefenseRoll()
    {
        if (CurrentState != GameState.DefensePhase) return;

        currentDefense.PerformDefenseRoll(player.State.Bag, generalaScoredThisRun);

        // Show defense roll results
        var lastResult = currentDefense.DefenseResults.Last();
        CombatUI.Instance.ShowDefenseRollResult(lastResult);

        int remaining = currentDefense.AvailableRolls - currentDefense.DefenseResults.Count;
        CombatUI.Instance.UpdateDefenseRolls(remaining);

        if (currentDefense.DefenseResults.Count >= currentDefense.AvailableRolls)
        {
            // All defense rolls done
            int shield = currentDefense.CalculateShield();
            player.State.ShieldValue = shield;
            UIManager.Instance.UpdateShield(shield);
            CombatUI.Instance.UpdateDefenseShield(shield);
            EnergyManager.Instance.ProcessCombatAction(CombatActionType.Defended);
            UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);
            Log($"Shield: {shield} points");

            StartCoroutine(DelayedAction(0.5f, StartEnemyAttack));
        }
    }

    // ── ENEMY ATTACK ──

    private void StartEnemyAttack()
    {
        TransitionTo(GameState.EnemyAttack);
        UIManager.Instance.ShowPhaseLabel("ENEMY ATTACKS!");

        int rawDamage = currentCombatEnemy.RollAttack();
        int shieldValue = player.State.ShieldValue;
        int netDamage = DamageResolver.ResolveEnemyAttack(rawDamage, shieldValue);

        // Apply damage directly (bypass PlayerState.TakeDamage shield logic since we already resolved it)
        player.State.CurrentHP = Mathf.Max(0, player.State.CurrentHP - netDamage);
        player.State.ShieldValue = 0;
        totalDamageTaken += netDamage;

        if (netDamage > 0)
        {
            EnergyManager.Instance.ProcessCombatAction(CombatActionType.TookDamage);
            UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);

            // Visual feedback
            if (ScreenFlashUI.Instance != null)
                ScreenFlashUI.Instance.FlashDamage();
            if (FloatingDamageUI.Instance != null)
                FloatingDamageUI.Instance.ShowDamage(netDamage, player.transform.position);
        }

        Log($"{currentCombatEnemy.State.BaseData.EnemyName} dealt {netDamage} damage (shield absorbed {shieldValue})");

        CombatUI.Instance.ShowEnemyAttackResult(rawDamage, shieldValue, netDamage);
        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);
        UIManager.Instance.UpdateShield(0);

        // Check player death
        if (!player.State.IsAlive)
        {
            TransitionTo(GameState.GameOver);
            UIManager.Instance.HideCombatPanel();
            var gameOverUI = FindObjectOfType<GameOverUI>();
            if (gameOverUI != null)
                gameOverUI.Show(GetRunStats(), currentCombatEnemy.State.BaseData.EnemyName);
            UIManager.Instance.ShowGameOverOverlay();
            return;
        }

        // Wait for player to click "Continue" (handled by OnContinueAfterEnemyAttack)
    }

    // Called by CombatUI continue button after enemy attack
    private void OnContinueAfterEnemyAttack()
    {
        if (CurrentState != GameState.EnemyAttack) return;

        // Next round
        totalRoundsFought++;
        TransitionTo(GameState.RoundEnd);

        StartCoroutine(NextRoundDelay());
    }

    private IEnumerator NextRoundDelay()
    {
        yield return new WaitForSeconds(0.5f);

        // Check craps for next round
        if (player.State.CrapsModeAvailable)
        {
            TransitionTo(GameState.CrapsBet);
            UIManager.Instance.ShowCrapsOverlay();
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
        string enemyName = currentCombatEnemy.State.BaseData.EnemyName;
        UIManager.Instance.ShowPhaseLabel("ENEMY DEFEATED!");
        Log($"{enemyName} has been slain!");

        // Remove enemy from grid
        GridManager.Instance.ClearOccupant(currentCombatEnemy.State.GridPosition);

        // Play death animation, then proceed
        currentCombatEnemy.PlayDeathAnimation(() =>
        {
            OnEnemyDeathAnimationComplete();
        });
    }

    private void OnEnemyDeathAnimationComplete()
    {
        if (enemiesDefeated == 1)
        {
            // Show reward
            TransitionTo(GameState.RewardSelection);
            var offers = RewardGenerator.GenerateOffers(player.State.Bag, 2);
            UIManager.Instance.HideCombatPanel();

            var rewardUI = FindObjectOfType<RewardUI>();
            if (rewardUI != null) rewardUI.ShowOffers(offers);
            UIManager.Instance.ShowRewardOverlay();
        }
        else if (enemiesDefeated >= 2)
        {
            // Victory!
            TransitionTo(GameState.Victory);
            UIManager.Instance.HideCombatPanel();

            var victoryUI = FindObjectOfType<VictoryUI>();
            if (victoryUI != null) victoryUI.Show(GetRunStats());
            UIManager.Instance.ShowVictoryOverlay();
        }
    }

    // Called by RewardUI when player picks a reward
    public void OnRewardSelected(FaceUpgradeOffer offer)
    {
        if (CurrentState != GameState.RewardSelection) return;

        // Apply upgrade
        var die = player.State.Bag.Dice.First(d => d.Id == offer.TargetDiceId);
        DiceUpgrader.ApplyUpgrade(die, offer.Upgrade);
        Log($"Upgrade applied: {offer.Upgrade.Description}");

        UIManager.Instance.HideRewardOverlay();
        UIManager.Instance.HideCombatPanel();

        // Back to movement phase
        TransitionTo(GameState.MovementPhase);
        BeginPlayerMovement();
    }

    // Called by UI: restart button
    public void RestartRun()
    {
        UIManager.Instance.HideAllPanels();
        StartRun();
    }

    // ── INPUT HANDLING ──

    private void Update()
    {
        if (CurrentState != GameState.MovementPhase) return;
        if (currentReachableTiles == null || currentReachableTiles.Count == 0) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridPos = GridManager.Instance.WorldToGrid(worldPos);

            if (currentReachableTiles.Contains(gridPos))
            {
                currentReachableTiles = null;
                OnPlayerMoveSelected(gridPos);
            }
        }
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

    private IEnumerator DelayedAction(float delay, Action action)
    {
        yield return new WaitForSeconds(delay);
        action?.Invoke();
    }

    private CombatLogUI cachedCombatLog;

    private void Log(string message)
    {
        if (cachedCombatLog == null)
            cachedCombatLog = FindObjectOfType<CombatLogUI>();
        if (cachedCombatLog != null)
            cachedCombatLog.AddMessage(message);
    }
}
