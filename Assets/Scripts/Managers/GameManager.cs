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
        InventorySetup,
        MovementRoll,
        MovementPhase,
        PreCombat,
        CrapsBet,
        AttackPhase,
        DefensePhase,
        EnemyAttack,
        RoundEnd,
        RewardSelection,
        LevelTransition,
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

    // Level progression
    private int currentLevel = 0;

    // Craps instance (CrapsMode is a plain class, not MonoBehaviour)
    private CrapsMode crapsMode = new CrapsMode();

    // Statistics (for end screen — accumulate across levels)
    private int totalRoundsFought = 0;
    private int totalDamageDealt = 0;
    private int totalDamageTaken = 0;
    private CombinationType bestCombo = CombinationType.HighDie;
    private int bestComboDamage = 0;
    private int crapsAttempts = 0;
    private int crapsWins = 0;
    private int totalEnemiesDefeated = 0;

    // Movement state
    private List<Vector2Int> currentReachableTiles;
    private bool isAnimating;

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
        if (CombatUI.Instance != null)
        {
            CombatUI.Instance.OnRerollClicked += OnPlayerRoll;
            CombatUI.Instance.OnDieLockToggled += OnDiceToggleLock;
            CombatUI.Instance.OnCommitClicked += OnPlayerCommitAttack;
            CombatUI.Instance.OnRollDefenseClicked += OnDefenseReroll;
            CombatUI.Instance.OnDefenseDieLockToggled += OnDefenseDieLock;
            CombatUI.Instance.OnDefenseCommitClicked += OnDefenseCommit;
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
            CombatUI.Instance.OnRollDefenseClicked -= OnDefenseReroll;
            CombatUI.Instance.OnDefenseDieLockToggled -= OnDefenseDieLock;
            CombatUI.Instance.OnDefenseCommitClicked -= OnDefenseCommit;
            CombatUI.Instance.OnContinueClicked -= OnContinueAfterEnemyAttack;
        }
    }

    // Late-bind events that may not be ready at OnEnable time
    private void Start()
    {
        // Re-subscribe since UI singletons may have initialized after us
        UnsubscribeFromEvents();
        SubscribeToEvents();

        // Bind InventoryBuilderUI
        var inventoryBuilderUI = FindObjectOfType<InventoryBuilderUI>(true);
        if (inventoryBuilderUI != null)
            inventoryBuilderUI.OnInventoryConfirmed += OnInventoryConfirmed;

        // Bind MovementRollUI
        var movementRollUI = FindObjectOfType<MovementRollUI>(true);
        if (movementRollUI != null)
            movementRollUI.OnRollClicked += OnMovementRollClicked;

        // Bind CrapsUI (inactive by default — pass true to include inactive)
        var crapsUI = FindObjectOfType<CrapsUI>(true);
        if (crapsUI != null)
            crapsUI.OnBetSelected += OnCrapsBetPlaced;

        // Bind RewardUI
        var rewardUI = FindObjectOfType<RewardUI>(true);
        if (rewardUI != null)
            rewardUI.OnRewardChosen += OnRewardSelected;

        // Bind GameOverUI
        var gameOverUI = FindObjectOfType<GameOverUI>(true);
        if (gameOverUI != null)
            gameOverUI.OnRestartClicked += RestartRun;

        // Bind VictoryUI
        var victoryUI = FindObjectOfType<VictoryUI>(true);
        if (victoryUI != null)
            victoryUI.OnRestartClicked += RestartRun;

        // Auto-start the run
        StartRun();
    }

    // ──────────── STATE TRANSITIONS ────────────

    public void StartRun()
    {
        // Clean up everything from previous run
        CleanupPreviousRun();

        // Reset run-level state
        currentLevel = 0;
        totalEnemiesDefeated = 0;
        generalaScoredThisRun = false;
        ResetStats();

        // Start first level
        SetupNextLevel();
    }

    private void CleanupPreviousRun()
    {
        // Destroy old player
        if (player != null)
        {
            if (GridManager.Instance != null)
                GridManager.Instance.ClearOccupant(player.State.GridPosition);
            Destroy(player.gameObject);
            player = null;
        }

        CleanupEnemiesAndGrid();

        // Hide all UI
        if (UIManager.Instance != null)
            UIManager.Instance.HideAllPanels();
    }

    private void CleanupForNextLevel()
    {
        // Destroy enemies only, preserve player
        CleanupEnemiesAndGrid();
    }

    private void CleanupEnemiesAndGrid()
    {
        // Destroy old enemies
        foreach (var enemy in enemies)
        {
            if (enemy != null)
            {
                if (GridManager.Instance != null)
                    GridManager.Instance.ClearOccupant(enemy.State.GridPosition);
                Destroy(enemy.gameObject);
            }
        }
        enemies.Clear();

        // Destroy old grid
        var oldGrid = GameObject.Find("Grid");
        if (oldGrid != null) Destroy(oldGrid);
    }

    private void SetupNextLevel()
    {
        currentLevel++;
        bool isFirstLevel = (currentLevel == 1);

        TransitionTo(GameState.RoomSetup);

        // Generate room layout
        int enemyCount = LevelConfig.GetEnemyCount(currentLevel);
        int obstacleCount = LevelConfig.GetObstacleCount(currentLevel);
        var layout = RoomGenerator.GenerateRoom(
            GridManager.Instance.Width,
            GridManager.Instance.Height,
            obstacleCount,
            enemyCount);

        // Generate grid with layout
        GridManager.Instance.GenerateGrid(layout);

        if (isFirstLevel)
        {
            // Spawn new player
            player = SpawnPlayer(PrototypeCharacter, layout.PlayerSpawn);
            EnergyManager.Instance.Initialize(player.State);
        }
        else
        {
            // Reposition existing player
            if (player != null)
            {
                GridManager.Instance.ClearOccupant(player.State.GridPosition);
                player.State.GridPosition = layout.PlayerSpawn;
                player.State.ShieldValue = 0;
                player.transform.position = GridManager.Instance.GridToWorld(layout.PlayerSpawn);
                GridManager.Instance.SetOccupant(layout.PlayerSpawn, player.gameObject);
            }
        }

        // Spawn enemies with scaling
        enemies.Clear();
        enemiesDefeated = 0;
        crapsMode = new CrapsMode();

        var enemyTypes = LevelConfig.GetEnemyTypes(currentLevel, enemyCount);
        float hpMult = LevelConfig.GetHPMultiplier(currentLevel);

        for (int i = 0; i < enemyTypes.Count && i < layout.EnemySpawns.Count; i++)
        {
            EnemyData baseData = enemyTypes[i] == "Orc" ? OrcData : GoblinData;
            var scaledData = CreateScaledEnemyData(baseData, hpMult);
            enemies.Add(SpawnEnemy(scaledData, layout.EnemySpawns[i]));
        }

        // Update HUD
        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);
        UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);
        UIManager.Instance.UpdateShield(player.State.ShieldValue);
        UIManager.Instance.UpdateLevel(currentLevel);

        // Clear combat log
        var combatLog = FindObjectOfType<CombatLogUI>();
        if (combatLog != null) combatLog.Clear();

        Log($"--- LEVEL {currentLevel} ---");

        if (isFirstLevel)
        {
            // Show inventory builder for first level only
            TransitionTo(GameState.InventorySetup);
            UIManager.Instance.ShowInventoryBuilder(player.State.FullInventory, player.State.CombatDiceSlots);
        }
        else
        {
            // Skip inventory — go straight to movement
            BeginPlayerMovement();
        }
    }

    private EnemyData CreateScaledEnemyData(EnemyData baseData, float hpMultiplier)
    {
        var scaled = ScriptableObject.CreateInstance<EnemyData>();
        scaled.EnemyName = baseData.EnemyName;
        scaled.Sprite = baseData.Sprite;
        scaled.EnemyColor = baseData.EnemyColor;
        scaled.MaxHP = Mathf.RoundToInt(baseData.MaxHP * hpMultiplier);
        scaled.AttackDiceCount = baseData.AttackDiceCount;
        scaled.AttackDiceFaces = baseData.AttackDiceFaces;
        scaled.SpeedMin = baseData.SpeedMin;
        scaled.SpeedMax = baseData.SpeedMax;
        scaled.MaxEnergy = baseData.MaxEnergy;
        scaled.EnergyPerRound = baseData.EnergyPerRound;
        scaled.Behavior = baseData.Behavior;
        return scaled;
    }

    // ── INVENTORY ──

    private void OnInventoryConfirmed(System.Collections.Generic.List<DiceInstance> selectedDice)
    {
        if (CurrentState != GameState.InventorySetup) return;

        // Fill bag with selected dice
        player.State.Bag = new DiceBag { MaxPower = player.State.BaseData.StartingPowerBudget };
        foreach (var die in selectedDice)
            player.State.Bag.Dice.Add(die); // bypass power check — selection already validated

        Log($"Inventario listo: {selectedDice.Count} dados seleccionados");
        BeginPlayerMovement();
    }

    // ── MOVEMENT ──

    private void BeginPlayerMovement()
    {
        bool enemiesAlive = enemies.Any(e => e != null && e.State != null && e.State.IsAlive);

        if (!enemiesAlive)
        {
            // Check if player is already on the ladder
            if (GridManager.Instance.LadderPosition.HasValue &&
                player.State.GridPosition == GridManager.Instance.LadderPosition.Value)
            {
                StartLevelTransition();
                return;
            }

            // No enemies — free movement, no dice roll required
            TransitionTo(GameState.MovementPhase);
            UIManager.Instance.ShowPhaseLabel("HEAD TO THE LADDER!");
            currentReachableTiles = MovementManager.Instance.GetReachableTiles(
                player.State.GridPosition, 100);
            GridManager.Instance.HighlightTiles(currentReachableTiles, Color.green);
            return;
        }

        TransitionTo(GameState.MovementRoll);
        UIManager.Instance.ShowMovementRollPanel(
            player.State.BaseData.SpeedMin,
            player.State.BaseData.SpeedMax);
    }

    private void OnMovementRollClicked()
    {
        if (CurrentState != GameState.MovementRoll) return;

        int steps = player.State.SpeedDie.Roll();
        UIManager.Instance.ShowMovementRollResult(steps);
        Log($"Speed roll: {steps} tiles");

        StartCoroutine(DelayedAction(0.8f, () =>
        {
            UIManager.Instance.HideMovementRollPanel();
            TransitionTo(GameState.MovementPhase);
            UIManager.Instance.ShowPhaseLabel($"TU TURNO ({steps} pasos)");
            currentReachableTiles = MovementManager.Instance.GetReachableTiles(
                player.State.GridPosition, steps);
            GridManager.Instance.HighlightTiles(currentReachableTiles, Color.green);
        }));
    }

    // Called by input system when player clicks a reachable tile
    public void OnPlayerMoveSelected(Vector2Int target)
    {
        if (CurrentState != GameState.MovementPhase) return;

        GridManager.Instance.ClearHighlights();
        isAnimating = true;

        var path = MovementManager.Instance.FindPath(player.State.GridPosition, target);
        MovementManager.Instance.MovePlayerAlongPathAnimated(player, path, (enemy) =>
        {
            isAnimating = false;
            if (enemy != null)
            {
                EnterCombat(enemy);
            }
            else
            {
                // Check ladder after movement
                CheckLadderTransition();
            }
        });
    }

    private void CheckLadderTransition()
    {
        bool enemiesAlive = enemies.Any(e => e != null && e.State != null && e.State.IsAlive);

        if (!enemiesAlive && GridManager.Instance.LadderPosition.HasValue &&
            player.State.GridPosition == GridManager.Instance.LadderPosition.Value)
        {
            StartLevelTransition();
            return;
        }

        ProcessEnemyMovement();
    }

    private void StartLevelTransition()
    {
        TransitionTo(GameState.LevelTransition);
        UIManager.Instance.ShowPhaseLabel($"LEVEL {currentLevel} COMPLETE!");
        Log($"Level {currentLevel} complete!");

        StartCoroutine(DelayedAction(1.5f, () =>
        {
            CleanupForNextLevel();
            SetupNextLevel();
        }));
    }

    private void ProcessEnemyMovement()
    {
        StartCoroutine(ProcessEnemyMovementRoutine());
    }

    private IEnumerator ProcessEnemyMovementRoutine()
    {
        isAnimating = true;
        UIManager.Instance.ShowPhaseLabel("ENEMY MOVE");
        yield return new WaitForSeconds(0.4f);

        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.State.IsAlive) continue;

            // Roll speed die and show result
            int steps = enemy.State.SpeedDie.Roll();
            UIManager.Instance.ShowPhaseLabel($"{enemy.State.BaseData.EnemyName} rolls {steps}!");
            Log($"{enemy.State.BaseData.EnemyName} speed roll: {steps}");
            yield return new WaitForSeconds(0.5f);

            // Animate movement
            bool? collision = null;
            MovementManager.Instance.MoveEnemyAnimated(enemy, player, steps, (col) =>
            {
                collision = col;
            });

            // Wait for animation to finish
            while (collision == null) yield return null;

            if (collision.Value)
            {
                isAnimating = false;
                EnterCombat(enemy);
                yield break;
            }

            // Pause between enemies
            yield return new WaitForSeconds(0.3f);
        }

        isAnimating = false;
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

        CombatUI.Instance.ShowAttackUI(results, currentAttack.LockedDiceIds, player.State.Bag);
        CombatUI.Instance.ClearComboPreview();
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

        // Only update the lock visual on the specific die — no full recreate
        bool nowLocked = currentAttack.LockedDiceIds.Contains(diceId);
        CombatUI.Instance.UpdateDieLock(diceId, nowLocked);

        // Show combo based on locked dice only
        if (currentAttack.LockedDiceIds.Count == 0)
        {
            CombatUI.Instance.ClearComboPreview();
        }
        else
        {
            int[] lockedValues = currentAttack.CurrentResults
                .Where(r => currentAttack.LockedDiceIds.Contains(r.DiceId))
                .Select(r => r.Value)
                .ToArray();
            var preview = CombinationDetector.Evaluate(lockedValues, generalaScoredThisRun);
            CombatUI.Instance.UpdateComboPreview(preview);
        }
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
            var crapsUI = FindObjectOfType<CrapsUI>(true);
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
        if (currentCombatEnemy == null) return;
        currentCombatEnemy.State.TakeDamage(damage);
        totalDamageDealt += damage;

        // Attack sound
        if (SoundLibrary.Instance != null)
            AudioManager.PlayWithPitch(SoundLibrary.Instance.AttackToEnemy);

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

        if (currentDefense.MaxRolls <= 0)
        {
            player.State.ShieldValue = 0;
            UIManager.Instance.UpdateShield(0);
            StartEnemyAttack();
            return;
        }

        TransitionTo(GameState.DefensePhase);
        UIManager.Instance.ShowPhaseLabel("DEFENSA");

        // Auto-first roll
        OnDefenseReroll();
    }

    // Called by UI: reroll button (same as attack reroll mechanic)
    public void OnDefenseReroll()
    {
        if (CurrentState != GameState.DefensePhase) return;
        if (currentDefense.CurrentRoll > 0 && !currentDefense.CanRollAgain) return;

        var results = currentDefense.PerformRoll(player.State.Bag);

        CombatUI.Instance.ShowDefenseDiceUI(results, currentDefense.LockedDiceIds, player.State.Bag);
        CombatUI.Instance.ClearDefenseComboPreview();
        CombatUI.Instance.UpdateDefenseRollCounter(currentDefense.CurrentRoll, currentDefense.MaxRolls);
        CombatUI.Instance.SetDefenseRerollEnabled(currentDefense.CanRollAgain);
        CombatUI.Instance.SetDefenseCommitEnabled(true);
    }

    // Called by UI: lock/unlock a defense die
    public void OnDefenseDieLock(string diceId)
    {
        if (CurrentState != GameState.DefensePhase) return;
        if (!currentDefense.HasRolled) return;

        currentDefense.ToggleLock(diceId);
        bool nowLocked = currentDefense.LockedDiceIds.Contains(diceId);
        CombatUI.Instance.UpdateDefenseDieLock(diceId, nowLocked);

        // Show combo from locked dice only
        if (currentDefense.LockedDiceIds.Count == 0)
        {
            CombatUI.Instance.ClearDefenseComboPreview();
        }
        else
        {
            int[] lockedValues = currentDefense.CurrentResults
                .Where(r => currentDefense.LockedDiceIds.Contains(r.DiceId))
                .Select(r => r.Value)
                .ToArray();
            var preview = CombinationDetector.Evaluate(lockedValues, generalaScoredThisRun);
            CombatUI.Instance.UpdateDefenseComboPreview(preview);
        }
    }

    // Called by UI: commit defense button
    public void OnDefenseCommit()
    {
        if (CurrentState != GameState.DefensePhase) return;
        if (!currentDefense.HasRolled) return;

        int shield = currentDefense.Commit(generalaScoredThisRun);
        player.State.ShieldValue = shield;
        UIManager.Instance.UpdateShield(shield);
        CombatUI.Instance.UpdateDefenseShield(shield);

        string comboName = currentDefense.FinalCombination.Type == CombinationType.HighDie
            ? "None" : currentDefense.FinalCombination.Type.ToString();
        Log($"Escudo: {shield} ({comboName})");

        EnergyManager.Instance.ProcessCombatAction(CombatActionType.Defended);
        UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);

        StartCoroutine(DelayedAction(0.5f, StartEnemyAttack));
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

            // Damage sound
            if (SoundLibrary.Instance != null)
                AudioManager.PlayWithPitch(SoundLibrary.Instance.AttackToPlayer);

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
            var gameOverUI = FindObjectOfType<GameOverUI>(true);
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
        totalEnemiesDefeated++;
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
        UIManager.Instance.HideCombatPanel();

        // Always offer a reward after killing an enemy
        TransitionTo(GameState.RewardSelection);
        var offers = RewardGenerator.GenerateOffers(player.State.Bag, 2);
        var rewardUI = FindObjectOfType<RewardUI>(true);
        if (rewardUI != null) rewardUI.ShowOffers(offers);
        UIManager.Instance.ShowRewardOverlay();
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

        // Check if any enemies remain alive on the grid
        bool enemiesRemain = enemies.Any(e => e != null && e.State != null && e.State.IsAlive);
        if (!enemiesRemain)
        {
            UIManager.Instance.ShowPhaseLabel("ROOM CLEARED! Head to the ladder!");
            Log("All enemies defeated! Head to the ladder!");
        }

        // Always return to movement phase — fight remaining enemies or move to ladder
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
        if (isAnimating || CurrentState != GameState.MovementPhase) return;
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
            EnemiesDefeated = totalEnemiesDefeated,
            LevelsCleared = currentLevel - 1
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
        go.SetActive(true);
        var entity = go.GetComponent<PlayerEntity>();
        entity.Initialize(data, pos);
        GridManager.Instance.SetOccupant(pos, go);
        return entity;
    }

    private EnemyEntity SpawnEnemy(EnemyData data, Vector2Int pos)
    {
        var go = Instantiate(enemyPrefab, GridManager.Instance.GridToWorld(pos), Quaternion.identity);
        go.SetActive(true);
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
