using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameState
    {
        MainMenu,
        RoomSetup,
        InventorySetup,
        MovementPhase,      // Free movement (room cleared) or Pick & Roll movement step
        BowTargeting,       // [Item] Bow attack targeting
        CrapsBet,           // Optional bet when energy = 100
        PickAndRoll,        // Roll ALL dice + player picks movement dice
        GeneralaPhase,      // Lock / reroll / commit remaining dice for damage
        EnemyAttack,
        RoundEnd,
        RewardSelection,
        LevelTransition,
        RoomTransition,
        GameOver,
        Victory
    }

    public GameState CurrentState { get; private set; }

    [SerializeField] private CharacterData PrototypeCharacter;
    [SerializeField] private EnemyData GoblinData;
    [SerializeField] private EnemyData OrcData;
    [SerializeField] private EnemyData ArcherData;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject enemyPrefab;

    private PlayerEntity player;
    private List<EnemyEntity> enemies = new List<EnemyEntity>();
    private EnemyEntity currentCombatEnemy;
    private List<EnemyEntity> waitingEnemies = new List<EnemyEntity>();
    private int enemiesDefeated = 0;
    private bool generalaScoredThisRun = false;

    private int currentLevel = 0;
    private CrapsMode crapsMode = new CrapsMode();

    // Statistics
    private int totalRoundsFought = 0;
    private int totalDamageDealt = 0;
    private int totalDamageTaken = 0;
    private CombinationType bestCombo = CombinationType.HighDie;
    private int bestComboDamage = 0;
    private int crapsAttempts = 0;
    private int crapsWins = 0;
    private int totalEnemiesDefeated = 0;

    // Movement / combat state
    private List<Vector2Int> currentReachableTiles;
    private bool isAnimating;
    private AttackPhase currentAttack;
    private bool _inPickAndRollTurn;                                 // true = combat turn (not free movement)
    private HashSet<string> _selectedMovementDiceIds = new HashSet<string>();

    // Bow targeting
    private List<Vector2Int> bowTargetTiles;

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

    public void SetArcherData(EnemyData archer)
    {
        if (ArcherData == null) ArcherData = archer;
    }

    private void OnEnable() { SubscribeToEvents(); }
    private void OnDisable() { UnsubscribeFromEvents(); }

    // ──────────── EVENT WIRING ────────────

    private void SubscribeToEvents()
    {
        if (CombatUI.Instance != null)
        {
            CombatUI.Instance.OnMovementDieToggled   += OnMovementDieToggled;
            CombatUI.Instance.OnConfirmMovementClicked += OnConfirmMovementDice;
            CombatUI.Instance.OnRerollClicked        += OnPlayerRoll;
            CombatUI.Instance.OnDieLockToggled       += OnDiceToggleLock;
            CombatUI.Instance.OnCommitClicked        += OnPlayerCommitAttack;
            CombatUI.Instance.OnContinueClicked      += OnContinueAfterEnemyAttack;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (CombatUI.Instance != null)
        {
            CombatUI.Instance.OnMovementDieToggled   -= OnMovementDieToggled;
            CombatUI.Instance.OnConfirmMovementClicked -= OnConfirmMovementDice;
            CombatUI.Instance.OnRerollClicked        -= OnPlayerRoll;
            CombatUI.Instance.OnDieLockToggled       -= OnDiceToggleLock;
            CombatUI.Instance.OnCommitClicked        -= OnPlayerCommitAttack;
            CombatUI.Instance.OnContinueClicked      -= OnContinueAfterEnemyAttack;
        }
    }

    private void Start()
    {
        UnsubscribeFromEvents();
        SubscribeToEvents();

        var inventoryBuilderUI = FindObjectOfType<InventoryBuilderUI>(true);
        if (inventoryBuilderUI != null)
            inventoryBuilderUI.OnInventoryConfirmed += OnInventoryConfirmed;

        var crapsUI = FindObjectOfType<CrapsUI>(true);
        if (crapsUI != null)
            crapsUI.OnBetSelected += OnCrapsBetPlaced;

        var rewardUI = FindObjectOfType<RewardUI>(true);
        if (rewardUI != null)
            rewardUI.OnRewardChosen += OnRewardSelected;

        var gameOverUI = FindObjectOfType<GameOverUI>(true);
        if (gameOverUI != null)
            gameOverUI.OnRestartClicked += RestartRun;

        var victoryUI = FindObjectOfType<VictoryUI>(true);
        if (victoryUI != null)
            victoryUI.OnRestartClicked += RestartRun;

        if (ExplorationActionsUI.Instance != null)
        {
            ExplorationActionsUI.Instance.OnBowSelected      += OnBowActionSelected;
            ExplorationActionsUI.Instance.OnPotionSelected   += OnPotionActionSelected;
            ExplorationActionsUI.Instance.OnFleeSelected     += OnFleeActionSelected;
            ExplorationActionsUI.Instance.OnForceDoorSelected += OnForceDoorSelected;
        }

        if (ShopUI.Instance != null)
            ShopUI.Instance.OnBuyClicked += OnShopBuy;

        StartRun();
    }

    // ──────────── STATE TRANSITIONS ────────────

    private void TransitionTo(GameState newState)
    {
        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }

    // ──────────── RUN / ROOM SETUP ────────────

    public void StartRun()
    {
        CleanupPreviousRun();
        currentLevel = 0;
        totalEnemiesDefeated = 0;
        generalaScoredThisRun = false;
        ResetStats();

        DungeonManager.Instance.GenerateFloor(Random.Range(8, 15));

        if (MinimapUI.Instance != null)
        {
            MinimapUI.Instance.BuildMinimap(DungeonManager.Instance.Rooms);
            MinimapUI.Instance.UpdateCurrentRoom(DungeonManager.Instance.StartRoomPosition);
        }

        SetupCurrentRoom(true);
    }

    private void CleanupPreviousRun()
    {
        if (player != null)
        {
            if (GridManager.Instance != null)
                GridManager.Instance.ClearOccupant(player.State.GridPosition);
            Destroy(player.gameObject);
            player = null;
        }

        CleanupEnemiesAndGrid();

        if (UIManager.Instance != null)
            UIManager.Instance.HideAllPanels();
    }

    private void CleanupEnemiesAndGrid()
    {
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
        waitingEnemies.Clear();

        var oldGrid = GameObject.Find("Grid");
        if (oldGrid != null) Destroy(oldGrid);
    }

    private void SetupCurrentRoom(bool isFirstRoom)
    {
        var room = DungeonManager.Instance.CurrentRoom;
        if (room == null) return;

        currentLevel++;
        TransitionTo(GameState.RoomSetup);

        int enemyCount = GetEnemyCountForRoom(room);
        int obstacleCount = Random.Range(3, 6);
        var layout = RoomGenerator.GenerateRoom(
            GridManager.Instance.Width,
            GridManager.Instance.Height,
            obstacleCount,
            enemyCount);

        GridManager.Instance.GenerateGrid(layout, room.DoorConnections);

        if (isFirstRoom && player == null)
        {
            player = SpawnPlayer(PrototypeCharacter, layout.PlayerSpawn);
            EnergyManager.Instance.Initialize(player.State);
        }
        else if (player != null)
        {
            GridManager.Instance.ClearOccupant(player.State.GridPosition);
            var entryPos = layout.PlayerSpawn;
            player.State.GridPosition = entryPos;
            player.transform.position = GridManager.Instance.GridToWorld(entryPos) + new Vector3(0, 0.4f, 0);
            GridManager.Instance.SetOccupant(entryPos, player.gameObject);
        }

        enemies.Clear();
        enemiesDefeated = 0;
        crapsMode = new CrapsMode();
        currentAttack = null;
        _inPickAndRollTurn = false;

        if (room.Type == RoomType.Combat || room.Type == RoomType.Boss)
        {
            SpawnEnemiesForRoom(room, layout);
        }
        else if (room.Type == RoomType.Shop)
        {
            GenerateShopItems(room);
            Log("Bienvenido a la tienda!");
        }
        else if (room.Type == RoomType.Potion)
        {
            if (player != null)
            {
                player.State.HasPotion = true;
                player.State.PotionCount = 1;
                Log("Pocion recargada!");
            }
        }

        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);
        UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);
        UIManager.Instance.UpdateGold(player.State.Gold);
        UIManager.Instance.UpdateLevel(currentLevel);

        var combatLog = FindObjectOfType<CombatLogUI>();
        if (combatLog != null) combatLog.Clear();

        string roomLabel = room.Type.ToString().ToUpper();
        Log($"--- {roomLabel} ROOM ---");

        if (isFirstRoom)
        {
            TransitionTo(GameState.InventorySetup);
            UIManager.Instance.ShowInventoryBuilder(
                player.State.FullInventory,
                player.State.BaseData.StartingPowerBudget);
        }
        else
        {
            if (room.Type == RoomType.Shop && ShopUI.Instance != null)
                ShowNextShopItem(room);

            BeginPlayerMovement();
        }
    }

    private void ShowNextShopItem(RoomData room)
    {
        foreach (var item in room.ShopItems)
        {
            if (!item.Purchased)
            {
                ShopUI.Instance.ShowItem(item, player.State.Gold);
                return;
            }
        }
        if (ShopUI.Instance != null) ShopUI.Instance.Hide();
    }

    private int GetEnemyCountForRoom(RoomData room)
    {
        if (room.Type == RoomType.Shop || room.Type == RoomType.Potion) return 0;
        if (room.Type == RoomType.Boss) return 1;
        if (room.Enemies.Count > 0)
            return room.Enemies.Count(e => e.IsAlive);
        return Random.Range(1, 3);
    }

    private void SpawnEnemiesForRoom(RoomData room, RoomLayout layout)
    {
        float hpMult = LevelConfig.GetHPMultiplier(currentLevel);

        if (room.Enemies.Count > 0)
        {
            int spawnIdx = 0;
            foreach (var saved in room.Enemies)
            {
                if (!saved.IsAlive) continue;
                if (spawnIdx >= layout.EnemySpawns.Count) break;

                EnemyData baseData = GetEnemyDataByName(saved.EnemyType);
                var scaledData = CreateScaledEnemyData(baseData, 1f);
                scaledData.MaxHP = saved.MaxHP;
                var entity = SpawnEnemy(scaledData, layout.EnemySpawns[spawnIdx]);
                entity.State.CurrentHP = saved.CurrentHP;
                enemies.Add(entity);
                spawnIdx++;
            }
        }
        else
        {
            int count = GetEnemyCountForRoom(room);
            for (int i = 0; i < count && i < layout.EnemySpawns.Count; i++)
            {
                EnemyData baseData;
                if (room.Type == RoomType.Boss)
                {
                    baseData = OrcData;
                    hpMult *= 2f;
                }
                else
                {
                    float roll = Random.value;
                    if (roll < 0.2f && ArcherData != null)
                        baseData = ArcherData;
                    else if (roll < 0.5f)
                        baseData = OrcData;
                    else
                        baseData = GoblinData;
                }

                var scaledData = CreateScaledEnemyData(baseData, hpMult);
                enemies.Add(SpawnEnemy(scaledData, layout.EnemySpawns[i]));
            }
        }
    }

    private EnemyData GetEnemyDataByName(string name)
    {
        if (name == "Orc") return OrcData;
        if (name == "Archer" && ArcherData != null) return ArcherData;
        return GoblinData;
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
        scaled.IsRanged = baseData.IsRanged;
        scaled.PreferredRange = baseData.PreferredRange;
        scaled.Accuracy = baseData.Accuracy;
        scaled.GoldDropMin = baseData.GoldDropMin;
        scaled.GoldDropMax = baseData.GoldDropMax;
        scaled.ModelPrefab = baseData.ModelPrefab;
        return scaled;
    }

    // ──────────── INVENTORY ────────────

    private void OnInventoryConfirmed(List<DiceInstance> selectedDice)
    {
        if (CurrentState != GameState.InventorySetup) return;

        player.State.Bag = new DiceBag { MaxPower = player.State.BaseData.StartingPowerBudget };
        foreach (var die in selectedDice)
            player.State.Bag.Dice.Add(die);

        Log($"Inventario listo: {selectedDice.Count} dados seleccionados");
        BeginPlayerMovement();
    }

    // ──────────── PLAYER TURN: PICK & ROLL ────────────

    // Entry point for each player turn.
    // If enemies alive: start Pick & Roll. If room cleared: free movement.
    private void BeginPlayerMovement()
    {
        bool enemiesAlive = enemies.Any(e => e != null && e.State != null && e.State.IsAlive);

        if (!enemiesAlive)
        {
            if (DungeonManager.Instance.CurrentRoom != null)
                DungeonManager.Instance.MarkCurrentRoomCleared();

            _inPickAndRollTurn = false;
            currentAttack = null;
            TransitionTo(GameState.MovementPhase);
            UIManager.Instance.ShowPhaseLabel("SALA LIMPIA! Ve a una puerta.");
            currentReachableTiles = MovementManager.Instance.GetReachableTiles(
                player.State.GridPosition, 100);
            GridManager.Instance.HighlightTiles(currentReachableTiles, Color.green);
            ShowExplorationUI();
            return;
        }

        // Enemies alive: start Pick & Roll (with optional Craps Bet)
        _inPickAndRollTurn = true;
        if (player.State.CrapsModeAvailable)
        {
            TransitionTo(GameState.CrapsBet);
            UIManager.Instance.ShowCombatPanel();
            UIManager.Instance.ShowCrapsOverlay();
        }
        else
        {
            StartPickAndRoll();
        }
    }

    // 1. Roll ALL dice — player then selects which to use for movement
    private void StartPickAndRoll()
    {
        TransitionTo(GameState.PickAndRoll);
        currentAttack = new AttackPhase();
        _selectedMovementDiceIds.Clear();

        UIManager.Instance.ShowCombatPanel();
        UIManager.Instance.ShowPhaseLabel("PICK & ROLL");

        if (crapsMode.IsActive)
            CombatUI.Instance.ShowCrapsBetIndicator(crapsMode.BetCombo);
        else
            CombatUI.Instance.HideCrapsBetIndicator();

        // Initial roll (counts as roll 1 of 3)
        currentAttack.PerformRoll(player.State.Bag);
        CombatUI.Instance.ShowPickMovementUI(currentAttack.AllInitialResults, player.State.Bag);
        Log("Dados tirados! Elegí cuáles usar para moverte.");
    }

    // 2. Player toggles a die for movement selection
    public void OnMovementDieToggled(string diceId)
    {
        if (CurrentState != GameState.PickAndRoll) return;

        bool isNowSelected;
        if (_selectedMovementDiceIds.Contains(diceId))
        {
            _selectedMovementDiceIds.Remove(diceId);
            isNowSelected = false;
        }
        else
        {
            _selectedMovementDiceIds.Add(diceId);
            isNowSelected = true;
        }

        int steps = 0;
        foreach (var r in currentAttack.AllInitialResults)
            if (_selectedMovementDiceIds.Contains(r.DiceId))
                steps += r.Value;

        CombatUI.Instance.ToggleMovementDieSelection(diceId, isNowSelected, steps);
    }

    // 3. Player confirms movement dice selection
    public void OnConfirmMovementDice()
    {
        if (CurrentState != GameState.PickAndRoll) return;

        currentAttack.SetMovementDice(_selectedMovementDiceIds);
        int steps = currentAttack.GetMovementSteps();

        Log($"Movimiento: {steps} tiles ({_selectedMovementDiceIds.Count} dados)");

        if (steps <= 0)
        {
            // No movement chosen — skip to Generala
            StartGeneralaPhase();
            return;
        }

        TransitionTo(GameState.MovementPhase);
        UIManager.Instance.ShowPhaseLabel($"MOVÉ ({steps} tiles)");
        currentReachableTiles = MovementManager.Instance.GetReachableTiles(
            player.State.GridPosition, steps);
        GridManager.Instance.HighlightTiles(currentReachableTiles, Color.green);
    }

    // 4. Player selects a tile to move to
    public void OnPlayerMoveSelected(Vector2Int target)
    {
        if (CurrentState != GameState.MovementPhase) return;

        GridManager.Instance.ClearHighlights();
        UIManager.Instance.HideExplorationActions();
        isAnimating = true;

        var path = MovementManager.Instance.FindPath(player.State.GridPosition, target);
        MovementManager.Instance.MovePlayerAlongPathAnimated(player, path, (enemy) =>
        {
            isAnimating = false;

            if (enemy != null)
            {
                currentCombatEnemy = enemy;
                waitingEnemies.Clear();
                foreach (var e in enemies)
                    if (e != null && e.State.IsAlive && e != enemy)
                        waitingEnemies.Add(e);
                UIManager.Instance.ShowEnemyInfo(enemy);
            }

            // Door check (only valid when room is cleared)
            var tile = GridManager.Instance.GetTile(player.State.GridPosition);
            if (tile != null && tile.Type == TileType.Door && tile.DoorDirection != null)
            {
                bool enemiesAlive = enemies.Any(e => e != null && e.State != null && e.State.IsAlive);
                if (!enemiesAlive)
                {
                    StartRoomTransition(tile.DoorDirection);
                    return;
                }
            }

            // Free movement (room cleared, no Pick & Roll this turn)
            if (!_inPickAndRollTurn)
            {
                ProcessEnemyMovement();
                return;
            }

            // Pick & Roll turn: proceed to Generala Phase with remaining dice
            StartGeneralaPhase();
        });
    }

    // 5. Generala Phase — lock / reroll / commit remaining dice
    private void StartGeneralaPhase()
    {
        if (currentCombatEnemy == null)
            currentCombatEnemy = FindAdjacentEnemy();

        TransitionTo(GameState.GeneralaPhase);
        UIManager.Instance.ShowPhaseLabel(crapsMode.IsActive ? "CRAPS ROUND" : "GENERALA");

        if (currentCombatEnemy != null)
        {
            UIManager.Instance.ShowEnemyInfo(currentCombatEnemy);
            if (ExplorationActionsUI.Instance != null)
            {
                ExplorationActionsUI.Instance.SetCombatMode(IsPlayerOnDoor());
                ExplorationActionsUI.Instance.Show();
            }
        }

        CombatUI.Instance.ShowGeneralaUI(
            currentAttack.CurrentResults,
            currentAttack.LockedDiceIds,
            player.State.Bag);
        CombatUI.Instance.ClearComboPreview();
        CombatUI.Instance.UpdateRollCounter(currentAttack.CurrentRoll, currentAttack.MaxRolls);
        CombatUI.Instance.SetRerollEnabled(currentAttack.CanRollAgain);
        CombatUI.Instance.SetCommitEnabled(true);
    }

    public void OnPlayerRoll()
    {
        if (CurrentState != GameState.GeneralaPhase) return;
        if (!currentAttack.CanRollAgain) return;

        var results = currentAttack.PerformRoll(player.State.Bag);

        CombatUI.Instance.ShowGeneralaUI(results, currentAttack.LockedDiceIds, player.State.Bag);
        CombatUI.Instance.ClearComboPreview();
        CombatUI.Instance.UpdateRollCounter(currentAttack.CurrentRoll, currentAttack.MaxRolls);
        CombatUI.Instance.SetRerollEnabled(currentAttack.CanRollAgain);
        CombatUI.Instance.SetCommitEnabled(true);
    }

    public void OnDiceToggleLock(string diceId)
    {
        if (CurrentState != GameState.GeneralaPhase) return;

        currentAttack.ToggleLock(diceId);
        bool nowLocked = currentAttack.LockedDiceIds.Contains(diceId);
        CombatUI.Instance.UpdateDieLock(diceId, nowLocked);

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

    public void OnPlayerCommitAttack()
    {
        if (CurrentState != GameState.GeneralaPhase) return;

        var combo = currentAttack.Commit(generalaScoredThisRun);
        int damage = DamageResolver.ResolvePlayerAttack(combo, player.State.BaseData);

        bool crapsWon = false;
        if (crapsMode.IsActive)
        {
            var crapsResult = crapsMode.Resolve(combo.Type, damage);
            damage = crapsResult.FinalDamage;
            crapsWon = crapsResult.Success;

            if (crapsResult.Success) crapsWins++;
            if (crapsResult.HPChange != 0)
            {
                if (crapsResult.HPChange > 0)
                    player.State.Heal(crapsResult.HPChange);
                else
                    player.State.CurrentHP = Mathf.Max(0, player.State.CurrentHP + crapsResult.HPChange);
            }

            if (CrapsToastUI.Instance != null)
                CrapsToastUI.Instance.ShowResult(crapsResult);
            CombatUI.Instance.HideCrapsBetIndicator();

            if (ScreenFlashUI.Instance != null)
            {
                if (crapsResult.Success) ScreenFlashUI.Instance.FlashCrapsSuccess();
                else ScreenFlashUI.Instance.FlashCrapsFailure();
            }

            Log(crapsResult.Success ? "Craps WON!" : "Craps LOST!");
            EnergyManager.Instance.ResetPlayerEnergy();
            UIManager.Instance.UpdateEnergy(0f);
        }

        if (combo.Type == CombinationType.Generala)
            generalaScoredThisRun = true;

        // Attack nearest adjacent enemy
        if (currentCombatEnemy == null || !currentCombatEnemy.State.IsAlive)
            currentCombatEnemy = FindAdjacentEnemy();

        if (currentCombatEnemy != null && currentCombatEnemy.State.IsAlive)
        {
            currentCombatEnemy.State.TakeDamage(damage);
            totalDamageDealt += damage;
            UIManager.Instance.UpdateEnemyHP(currentCombatEnemy.State.CurrentHP, currentCombatEnemy.State.MaxHP);

            if (SoundLibrary.Instance != null)
            {
                if (crapsWon) AudioManager.PlayWithLowPitch(SoundLibrary.Instance.AttackToEnemy);
                else AudioManager.PlayWithPitch(SoundLibrary.Instance.AttackToEnemy);
            }

            if (FloatingDamageUI.Instance != null)
            {
                if (crapsWon) FloatingDamageUI.Instance.ShowCrapsDamage(damage, currentCombatEnemy.transform.position);
                else FloatingDamageUI.Instance.ShowDamage(damage, currentCombatEnemy.transform.position);
            }

            Log($"Causaste {damage} dmg ({combo.Type})");

            if (damage > bestComboDamage)
            {
                bestComboDamage = damage;
                bestCombo = combo.Type;
            }

            EnergyManager.Instance.ProcessCombatAction(CombatActionType.DealtDamage, combo.Type);

            if (!currentCombatEnemy.State.IsAlive)
            {
                EnergyManager.Instance.ProcessCombatAction(CombatActionType.KilledEnemy);
                UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);
                HandleEnemyDeath();
                return;
            }
        }
        else
        {
            // No adjacent enemy — combo still generates energy
            EnergyManager.Instance.ProcessCombatAction(CombatActionType.DealtDamage, combo.Type);
            Log($"Combo: {combo.Type} (sin enemigo adyacente)");
        }

        UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);
        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);

        StartEnemyAttack();
    }

    // ──────────── CRAPS BET ────────────

    public void OnCrapsBetPlaced(CombinationType bet)
    {
        if (CurrentState != GameState.CrapsBet) return;
        crapsMode.Activate();
        crapsMode.PlaceBet(bet);
        crapsAttempts++;
        UIManager.Instance.HideCrapsOverlay();
        StartPickAndRoll();
    }

    // ──────────── ENEMY ATTACK ────────────

    private void StartEnemyAttack()
    {
        TransitionTo(GameState.EnemyAttack);

        if (currentCombatEnemy == null || !currentCombatEnemy.State.IsAlive)
        {
            AdvanceWaitingEnemies();
            OnContinueAfterEnemyAttack();
            return;
        }

        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        // 1. Enemy movement
        int steps = Random.Range(
            currentCombatEnemy.State.BaseData.SpeedMin,
            currentCombatEnemy.State.BaseData.SpeedMax + 1);

        UIManager.Instance.ShowPhaseLabel($"{currentCombatEnemy.State.BaseData.EnemyName} mueve {steps}!");
        Log($"{currentCombatEnemy.State.BaseData.EnemyName}: {steps} tiles");
        yield return new WaitForSeconds(0.4f);

        bool? moved = null;
        MovementManager.Instance.MoveEnemyAnimated(currentCombatEnemy, player, steps, col => moved = col);
        while (moved == null) yield return null;

        AdvanceWaitingEnemies();
        yield return new WaitForSeconds(0.2f);

        // 2. Check adjacency — only attack if adjacent
        int dist = Mathf.Abs(currentCombatEnemy.State.GridPosition.x - player.State.GridPosition.x)
                 + Mathf.Abs(currentCombatEnemy.State.GridPosition.y - player.State.GridPosition.y);

        if (dist > 1)
        {
            Log($"{currentCombatEnemy.State.BaseData.EnemyName} no está adyacente, no ataca.");
            UIManager.Instance.ShowPhaseLabel("ENEMIGO FUERA DE RANGO");
            yield return new WaitForSeconds(0.5f);
            OnContinueAfterEnemyAttack();
            yield break;
        }

        // 3. Attack
        DoEnemyAttack();
    }

    private void DoEnemyAttack()
    {
        UIManager.Instance.ShowPhaseLabel("ENEMIGO ATACA!");

        int rawDamage = currentCombatEnemy.RollAttack();
        int netDamage = DamageResolver.ResolveEnemyAttack(rawDamage);

        player.State.TakeDamage(netDamage);
        totalDamageTaken += netDamage;

        if (netDamage > 0)
        {
            EnergyManager.Instance.ProcessCombatAction(CombatActionType.TookDamage);
            UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);

            if (SoundLibrary.Instance != null)
                AudioManager.PlayWithPitch(SoundLibrary.Instance.AttackToPlayer);
            if (ScreenFlashUI.Instance != null)
                ScreenFlashUI.Instance.FlashDamage();
            if (FloatingDamageUI.Instance != null)
                FloatingDamageUI.Instance.ShowDamage(netDamage, player.transform.position);
        }

        Log($"{currentCombatEnemy.State.BaseData.EnemyName} causó {netDamage} dmg");

        CombatUI.Instance.ShowEnemyAttackResult(rawDamage, 0, netDamage);
        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);

        if (!player.State.IsAlive)
        {
            TransitionTo(GameState.GameOver);
            UIManager.Instance.HideCombatPanel();
            UIManager.Instance.HideEnemyInfo();
            UIManager.Instance.HideExplorationActions();
            var gameOverUI = FindObjectOfType<GameOverUI>(true);
            if (gameOverUI != null)
                gameOverUI.Show(GetRunStats(), currentCombatEnemy.State.BaseData.EnemyName);
            UIManager.Instance.ShowGameOverOverlay();
        }
    }

    private void OnContinueAfterEnemyAttack()
    {
        if (CurrentState != GameState.EnemyAttack) return;
        totalRoundsFought++;
        TransitionTo(GameState.RoundEnd);
        StartCoroutine(NextRoundDelay());
    }

    private IEnumerator NextRoundDelay()
    {
        yield return new WaitForSeconds(0.5f);

        // Re-detect adjacent enemy for next round
        currentCombatEnemy = FindAdjacentEnemy();

        // Enemy gains energy per round
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.State.IsAlive)
                enemy.State.GainEnergy();
        }

        BeginPlayerMovement();
    }

    private void AdvanceWaitingEnemies()
    {
        foreach (var waiting in waitingEnemies.ToList())
        {
            if (waiting == null || !waiting.State.IsAlive) continue;

            var path = MovementManager.Instance.FindPath(
                waiting.State.GridPosition, player.State.GridPosition);
            if (path.Count > 0)
            {
                var nextTile = path[0];
                if (nextTile != player.State.GridPosition)
                {
                    GridManager.Instance.ClearOccupant(waiting.State.GridPosition);
                    waiting.MoveTo(nextTile);
                    GridManager.Instance.SetOccupant(nextTile, waiting.gameObject);
                }
                else
                {
                    Log($"{waiting.State.BaseData.EnemyName} se unió al combate!");
                }
            }
        }
    }

    // ──────────── ENEMY MOVEMENT ────────────

    private void EnterCombat(EnemyEntity enemy)
    {
        currentCombatEnemy = enemy;
        crapsMode = new CrapsMode();

        waitingEnemies.Clear();
        foreach (var e in enemies)
            if (e != null && e.State.IsAlive && e != enemy)
                waitingEnemies.Add(e);

        UIManager.Instance.ShowPhaseLabel("COMBATE!");
        UIManager.Instance.ShowCombatPanel();
        UIManager.Instance.ShowEnemyInfo(enemy);
        UIManager.Instance.HideExplorationActions();
        Log($"Combate con {enemy.State.BaseData.EnemyName}!");
        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);

        if (ExplorationActionsUI.Instance != null)
        {
            ExplorationActionsUI.Instance.SetCombatMode(IsPlayerOnDoor());
            ExplorationActionsUI.Instance.Show();
        }

        // Enemy moved into player → player gets their full turn (Pick & Roll)
        BeginPlayerMovement();
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

            // Movement from EnemyData speed range (no SpeedDie)
            int steps = Random.Range(enemy.State.BaseData.SpeedMin, enemy.State.BaseData.SpeedMax + 1);
            UIManager.Instance.ShowPhaseLabel($"{enemy.State.BaseData.EnemyName} mueve {steps}!");
            Log($"{enemy.State.BaseData.EnemyName}: {steps} tiles");
            yield return new WaitForSeconds(0.5f);

            bool? collision = null;
            MovementManager.Instance.MoveEnemyAnimated(enemy, player, steps, (col) =>
            {
                collision = col;
            });

            while (collision == null) yield return null;

            if (collision.Value)
            {
                isAnimating = false;
                EnterCombat(enemy);
                yield break;
            }

            yield return new WaitForSeconds(0.3f);
        }

        isAnimating = false;
        BeginPlayerMovement();
    }

    // ──────────── ENEMY DEATH ────────────

    private void HandleEnemyDeath()
    {
        enemiesDefeated++;
        totalEnemiesDefeated++;
        string enemyName = currentCombatEnemy.State.BaseData.EnemyName;
        UIManager.Instance.ShowPhaseLabel("ENEMIGO DERROTADO!");
        Log($"{enemyName} eliminado!");

        int goldMin = currentCombatEnemy.State.BaseData.GoldDropMin;
        int goldMax = currentCombatEnemy.State.BaseData.GoldDropMax;
        if (goldMin <= 0 && goldMax <= 0) { goldMin = 5; goldMax = 15; }
        int goldDrop = Random.Range(goldMin, goldMax + 1);
        player.State.Gold += goldDrop;
        UIManager.Instance.UpdateGold(player.State.Gold);
        Log($"+{goldDrop} Gold");

        if (FloatingDamageUI.Instance != null)
            FloatingDamageUI.Instance.ShowGold(goldDrop, currentCombatEnemy.transform.position);

        GridManager.Instance.ClearOccupant(currentCombatEnemy.State.GridPosition);
        currentCombatEnemy.PlayDeathAnimation(OnEnemyDeathAnimationComplete);
    }

    private void OnEnemyDeathAnimationComplete()
    {
        UIManager.Instance.HideCombatPanel();
        UIManager.Instance.HideEnemyInfo();
        UIManager.Instance.HideExplorationActions();

        bool moreEnemies = enemies.Any(e => e != null && e.State != null && e.State.IsAlive);

        if (!moreEnemies)
        {
            var currentRoom = DungeonManager.Instance.CurrentRoom;
            if (currentRoom != null && currentRoom.Type == RoomType.Boss)
            {
                TransitionTo(GameState.Victory);
                UIManager.Instance.ShowPhaseLabel("BOSS DERROTADO! VICTORIA!");
                var victoryUI = FindObjectOfType<VictoryUI>(true);
                if (victoryUI != null) victoryUI.Show(GetRunStats());
                UIManager.Instance.ShowVictoryOverlay();
                return;
            }
        }

        TransitionTo(GameState.RewardSelection);
        var offers = RewardGenerator.GenerateOffers(player.State.Bag, 2);
        var rewardUI = FindObjectOfType<RewardUI>(true);
        if (rewardUI != null) rewardUI.ShowOffers(offers);
        UIManager.Instance.ShowRewardOverlay();
    }

    public void OnRewardSelected(FaceUpgradeOffer offer)
    {
        if (CurrentState != GameState.RewardSelection) return;

        var die = player.State.Bag.Dice.First(d => d.Id == offer.TargetDiceId);
        DiceUpgrader.ApplyUpgrade(die, offer.Upgrade);
        Log($"Upgrade aplicado: {offer.Upgrade.Description}");

        UIManager.Instance.HideRewardOverlay();
        BeginPlayerMovement();
    }

    public void RestartRun()
    {
        UIManager.Instance.HideAllPanels();
        StartRun();
    }

    // ──────────── EXPLORATION ACTIONS ────────────

    private void ShowExplorationUI()
    {
        if (ExplorationActionsUI.Instance != null)
        {
            bool onDoor = IsPlayerOnDoor();
            ExplorationActionsUI.Instance.SetExplorationMode(
                player.State.HasPotion, player.State.PotionCount, onDoor);
            ExplorationActionsUI.Instance.Show();
        }
    }

    private bool IsPlayerOnDoor()
    {
        var tile = GridManager.Instance.GetTile(player.State.GridPosition);
        return tile != null && tile.Type == TileType.Door;
    }

    private void OnBowActionSelected()
    {
        if (CurrentState != GameState.MovementPhase) return;

        TransitionTo(GameState.BowTargeting);
        bowTargetTiles = GridManager.Instance.GetTilesInRange(player.State.GridPosition, 5);
        GridManager.Instance.ClearHighlights();
        GridManager.Instance.HighlightTiles(bowTargetTiles, new Color(1f, 0.4f, 0.4f, 0.5f));
        UIManager.Instance.ShowPhaseLabel("ELEGÍ OBJETIVO DEL ARCO");
    }

    private void OnPotionActionSelected()
    {
        if (CurrentState != GameState.MovementPhase) return;
        if (!player.State.HasPotion || player.State.PotionCount <= 0) return;

        UIManager.Instance.HideExplorationActions();

        var result = ExplorationActions.AttemptPotion(player.State.MaxHP);
        player.State.Heal(result.healAmount);
        player.State.PotionCount--;
        if (player.State.PotionCount <= 0) player.State.HasPotion = false;

        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);
        Log($"Pocion usada! +{result.healAmount} HP");

        if (FloatingDamageUI.Instance != null)
            FloatingDamageUI.Instance.ShowHeal(result.healAmount, player.transform.position);

        ProcessEnemyMovement();
    }

    private void OnFleeActionSelected()
    {
        if (CurrentState != GameState.GeneralaPhase && CurrentState != GameState.CrapsBet) return;

        var result = ExplorationActions.AttemptFlee();
        Log($"Huida: roll {result.roll}, exito={result.success}");

        if (result.success)
        {
            // Opportunity Attack: el enemigo tira 1d6 al jugador que huye
            if (currentCombatEnemy != null && currentCombatEnemy.State.IsAlive)
            {
                int oppDamage = Random.Range(1, 7); // 1d6
                player.State.TakeDamage(oppDamage);
                UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);
                Log($"Ataque de Oportunidad! {currentCombatEnemy.State.BaseData.EnemyName} tiró {oppDamage} dmg.");
                if (FloatingDamageUI.Instance != null)
                    FloatingDamageUI.Instance.ShowDamage(oppDamage, player.transform.position);
            }

            Log("Huida exitosa!");
            UIManager.Instance.ShowPhaseLabel("HUIDA EXITOSA!");
            UIManager.Instance.HideCombatPanel();
            UIManager.Instance.HideEnemyInfo();
            UIManager.Instance.HideCrapsOverlay();
            currentCombatEnemy = null;
            StartCoroutine(DelayedAction(0.5f, BeginPlayerMovement));
        }
        else
        {
            Log("Huida fallida! Turno perdido.");
            UIManager.Instance.ShowPhaseLabel("FLEE FAILED!");
            StartCoroutine(DelayedAction(0.5f, StartEnemyAttack));
        }
    }

    private void OnForceDoorSelected()
    {
        if (CurrentState != GameState.GeneralaPhase) return;
        if (!IsPlayerOnDoor()) return;

        var tile = GridManager.Instance.GetTile(player.State.GridPosition);
        var result = ExplorationActions.AttemptForceDoor();
        Log($"Forzar puerta: roll {result.roll}, exito={result.success}");

        if (result.success)
        {
            foreach (var enemy in enemies)
            {
                if (enemy != null && enemy.State.IsAlive)
                {
                    int damage = Mathf.RoundToInt(enemy.State.MaxHP * 0.25f);
                    enemy.State.TakeDamage(damage);
                    Log($"{enemy.State.BaseData.EnemyName} pierde {damage} HP");
                }
            }
            DungeonManager.Instance.SaveEnemyState(enemies);
            UIManager.Instance.HideCombatPanel();
            UIManager.Instance.HideEnemyInfo();
            StartRoomTransition(tile.DoorDirection);
        }
        else
        {
            Log("Forzar puerta fallido! Turno perdido.");
            UIManager.Instance.ShowPhaseLabel("FORCE FAILED!");
            StartCoroutine(DelayedAction(0.5f, StartEnemyAttack));
        }
    }

    // ──────────── ROOM TRANSITION ────────────

    private void StartRoomTransition(string direction)
    {
        DungeonManager.Instance.SaveEnemyState(enemies);
        TransitionTo(GameState.RoomTransition);
        UIManager.Instance.ShowPhaseLabel("ENTRANDO A NUEVA SALA...");

        StartCoroutine(DelayedAction(1f, () =>
        {
            CleanupEnemiesAndGrid();
            bool success = DungeonManager.Instance.TransitionToRoom(direction);
            if (success)
            {
                if (MinimapUI.Instance != null)
                    MinimapUI.Instance.UpdateCurrentRoom(DungeonManager.Instance.CurrentRoom.FloorPosition);
                SetupCurrentRoom(false);
            }
            else
            {
                BeginPlayerMovement();
            }
        }));
    }

    // ──────────── SHOP ────────────

    private void GenerateShopItems(RoomData room)
    {
        if (room.ShopItems.Count > 0) return;

        room.ShopItems.Add(new ShopItemData
        {
            ItemName = "Pocion", Description = "Recarga tu pocion (1 uso)",
            GoldCost = 15, DiceType = null, Purchased = false
        });
        room.ShopItems.Add(new ShopItemData
        {
            ItemName = "d10", Description = "Un dado de 10 caras (costo 3)",
            GoldCost = 25, DiceType = "d10", Purchased = false
        });
        room.ShopItems.Add(new ShopItemData
        {
            ItemName = "d12", Description = "Un dado de 12 caras (costo 4)",
            GoldCost = 40, DiceType = "d12", Purchased = false
        });

        foreach (var item in room.ShopItems)
            if (!item.Purchased)
                Log($"  [{item.ItemName}] - {item.GoldCost}G: {item.Description}");
    }

    private void OnShopBuy(ShopItemData item)
    {
        if (player.State.Gold < item.GoldCost) return;

        player.State.Gold -= item.GoldCost;
        item.Purchased = true;
        UIManager.Instance.UpdateGold(player.State.Gold);

        if (item.ItemName == "Pocion")
        {
            player.State.HasPotion = true;
            player.State.PotionCount = 1;
            Log($"Comprado: Pocion por {item.GoldCost}G");
        }
        else if (item.DiceType != null)
        {
            DiceData diceData = null;
            foreach (var die in player.State.FullInventory)
            {
                if (die.BaseData.DiceName == item.DiceType)
                {
                    diceData = die.BaseData;
                    break;
                }
            }
            if (diceData != null)
            {
                var newDie = DiceInstance.Create(diceData);
                player.State.FullInventory.Add(newDie);
                Log($"Comprado: {item.ItemName} por {item.GoldCost}G");
            }
            else
            {
                Log($"Comprado: {item.ItemName} por {item.GoldCost}G");
            }
        }
        else
        {
            Log($"Comprado: {item.ItemName} por {item.GoldCost}G");
        }
    }

    // ──────────── INPUT ────────────

    private void Update()
    {
        if (isAnimating) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (CurrentState == GameState.MovementPhase)
                HandleMovementClick();
            else if (CurrentState == GameState.BowTargeting)
                HandleBowClick();
        }
    }

    private void HandleMovementClick()
    {
        if (currentReachableTiles == null || currentReachableTiles.Count == 0) return;

        Vector2Int gridPos = GetGridPosFromMouse();
        if (currentReachableTiles.Contains(gridPos))
        {
            currentReachableTiles = null;
            OnPlayerMoveSelected(gridPos);
        }
    }

    private void HandleBowClick()
    {
        if (bowTargetTiles == null) return;

        Vector2Int gridPos = GetGridPosFromMouse();
        if (!bowTargetTiles.Contains(gridPos))
        {
            GridManager.Instance.ClearHighlights();
            bowTargetTiles = null;
            BeginPlayerMovement();
            return;
        }

        GridManager.Instance.ClearHighlights();
        bowTargetTiles = null;
        UIManager.Instance.HideExplorationActions();

        EnemyEntity targetEnemy = null;
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.State.IsAlive && enemy.State.GridPosition == gridPos)
            {
                targetEnemy = enemy;
                break;
            }
        }

        if (targetEnemy == null)
        {
            Log("Sin enemigo en esa casilla!");
            UIManager.Instance.ShowPhaseLabel("MISS - Sin objetivo!");
            ProcessEnemyMovement();
            return;
        }

        var result = ExplorationActions.AttemptBow();
        Log($"Arco: roll {result.roll}, hit={result.hit}");

        if (result.hit)
        {
            int damage = ExplorationActions.CalculateBowDamage(result.roll);
            targetEnemy.State.TakeDamage(damage);
            Log($"Impacto! {damage} dmg a {targetEnemy.State.BaseData.EnemyName}");
            UIManager.Instance.ShowPhaseLabel("BOW HIT!");

            if (FloatingDamageUI.Instance != null)
                FloatingDamageUI.Instance.ShowDamage(damage, targetEnemy.transform.position);

            if (!targetEnemy.State.IsAlive)
            {
                HandleBowKill(targetEnemy);
                return;
            }
        }
        else
        {
            Log("Arco falló!");
            UIManager.Instance.ShowPhaseLabel("BOW MISS!");
        }

        ProcessEnemyMovement();
    }

    private void HandleBowKill(EnemyEntity target)
    {
        totalEnemiesDefeated++;
        int gMin = target.State.BaseData.GoldDropMin;
        int gMax = target.State.BaseData.GoldDropMax;
        if (gMin <= 0 && gMax <= 0) { gMin = 5; gMax = 15; }
        int goldDrop = Random.Range(gMin, gMax + 1);
        player.State.Gold += goldDrop;
        UIManager.Instance.UpdateGold(player.State.Gold);
        GridManager.Instance.ClearOccupant(target.State.GridPosition);
        target.PlayDeathAnimation(() => ProcessEnemyMovement());
    }

    private Vector2Int GetGridPosFromMouse()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            return GridManager.Instance.WorldToGrid(hitPoint);
        }

        return new Vector2Int(-1, -1);
    }

    // ──────────── HELPERS ────────────

    // Returns the first enemy adjacent to the player (Manhattan distance = 1)
    private EnemyEntity FindAdjacentEnemy()
    {
        var playerPos = player.State.GridPosition;
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.State.IsAlive) continue;
            var diff = enemy.State.GridPosition - playerPos;
            if (Mathf.Abs(diff.x) <= 1 && Mathf.Abs(diff.y) <= 1 && (diff.x != 0 || diff.y != 0))
                return enemy;
        }
        return null;
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
        var go = Instantiate(playerPrefab,
            GridManager.Instance.GridToWorld(pos) + new Vector3(0, 0.4f, 0),
            Quaternion.identity);
        go.SetActive(true);
        var entity = go.GetComponent<PlayerEntity>();
        entity.Initialize(data, pos);
        GridManager.Instance.SetOccupant(pos, go);
        return entity;
    }

    private EnemyEntity SpawnEnemy(EnemyData data, Vector2Int pos)
    {
        var go = Instantiate(enemyPrefab,
            GridManager.Instance.GridToWorld(pos) + new Vector3(0, 0.4f, 0),
            Quaternion.identity);
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
