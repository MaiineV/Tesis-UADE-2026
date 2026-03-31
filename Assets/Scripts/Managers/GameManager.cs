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
        MovementRoll,
        MovementPhase,
        BowTargeting,
        PreCombat,
        CrapsBet,
        AttackPhase,
        DefensePhase,
        EnemyAttack,
        RoundEnd,
        RewardSelection,
        LevelTransition,
        RoomTransition,
        GameOver,
        Victory
    }

    public GameState CurrentState { get; private set; }

    // References (assigned in Inspector or found at runtime)
    [SerializeField] private CharacterData PrototypeCharacter;
    [SerializeField] private EnemyData GoblinData;
    [SerializeField] private EnemyData OrcData;
    [SerializeField] private EnemyData ArcherData;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject enemyPrefab;

    // Runtime references
    private PlayerEntity player;
    private List<EnemyEntity> enemies = new List<EnemyEntity>();
    private EnemyEntity currentCombatEnemy;
    private List<EnemyEntity> waitingEnemies = new List<EnemyEntity>();
    private int enemiesDefeated = 0;
    private bool generalaScoredThisRun = false;

    // Level/dungeon
    private int currentLevel = 0;
    private int _currentFloor = 1;

    // Craps
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

    // Movement state
    private List<Vector2Int> currentReachableTiles;
    private bool isAnimating;

    // Combat state
    private AttackPhase currentAttack;
    private DefensePhase currentDefense;

    // Bow targeting
    private List<Vector2Int> bowTargetTiles;

    // Potion room pickup
    private GameObject _potionPickupObj;
    private Vector2Int _potionPickupTile;

    // Boss portal
    private GameObject _portalObj;
    private Vector2Int _portalTile;

    // Multi-enemy combat
    private List<EnemyEntity> _activeCombatEnemies = new List<EnemyEntity>();
    private int _targetEnemyIndex = 0;
    private bool _awaitingTargetSelection;

    // Shop items on grid
    private List<ShopItemEntity> _shopItemEntities = new List<ShopItemEntity>();

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

    private void Start()
    {
        UnsubscribeFromEvents();
        SubscribeToEvents();

        var inventoryBuilderUI = FindObjectOfType<InventoryBuilderUI>(true);
        if (inventoryBuilderUI != null)
            inventoryBuilderUI.OnInventoryConfirmed += OnInventoryConfirmed;

        var movementRollUI = FindObjectOfType<MovementRollUI>(true);
        if (movementRollUI != null)
            movementRollUI.OnRollClicked += OnMovementRollClicked;

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

        // Bind exploration actions
        if (ExplorationActionsUI.Instance != null)
        {
            ExplorationActionsUI.Instance.OnBowSelected += OnBowActionSelected;
            ExplorationActionsUI.Instance.OnPotionSelected += OnPotionActionSelected;
            ExplorationActionsUI.Instance.OnFleeSelected += OnFleeActionSelected;
            ExplorationActionsUI.Instance.OnForceDoorSelected += OnForceDoorSelected;
        }

        // Bind shop
        if (ShopUI.Instance != null)
            ShopUI.Instance.OnBuyClicked += OnShopBuy;

        // Wire target selection buttons
        if (UIManager.Instance != null)
        {
            var enemyInfo1 = UIManager.Instance.GetEnemyInfoUI();
            var enemyInfo2 = UIManager.Instance.GetSecondEnemyInfoUI();
            if (enemyInfo1 != null) enemyInfo1.OnTargetClicked += () => OnTargetSelected(0);
            if (enemyInfo2 != null) enemyInfo2.OnTargetClicked += () => OnTargetSelected(1);
        }

        StartRun();
    }

    // ──────────── STATE TRANSITIONS ────────────

    public void StartRun()
    {
        CleanupPreviousRun();

        currentLevel = 0;
        _currentFloor = 1;
        totalEnemiesDefeated = 0;
        generalaScoredThisRun = false;
        ResetStats();

        // Generate dungeon floor
        DungeonManager.Instance.GenerateFloor(Random.Range(8, 15));

        // Update minimap
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

        if (_potionPickupObj != null) { Destroy(_potionPickupObj); _potionPickupObj = null; }
        if (_portalObj != null) { Destroy(_portalObj); _portalObj = null; }

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
        _activeCombatEnemies.Clear();

        // Cleanup shop item entities
        foreach (var shopEntity in _shopItemEntities)
        {
            if (shopEntity != null)
                Destroy(shopEntity.gameObject);
        }
        _shopItemEntities.Clear();

        var oldGrid = GameObject.Find("Grid");
        if (oldGrid != null) Destroy(oldGrid);
    }

    private void SetupCurrentRoom(bool isFirstRoom)
    {
        var room = DungeonManager.Instance.CurrentRoom;
        if (room == null) return;

        currentLevel++;
        TransitionTo(GameState.RoomSetup);

        // Generate room layout
        int enemyCount = GetEnemyCountForRoom(room);
        int obstacleCount = Random.Range(3, 6);
        var layout = RoomGenerator.GenerateRoom(
            GridManager.Instance.Width,
            GridManager.Instance.Height,
            obstacleCount,
            enemyCount);

        // Generate grid with doors
        GridManager.Instance.GenerateGrid(layout, room.DoorConnections);

        if (isFirstRoom && player == null)
        {
            player = SpawnPlayer(PrototypeCharacter, layout.PlayerSpawn);
            EnergyManager.Instance.Initialize(player.State);
        }
        else if (player != null)
        {
            // Reposition existing player at the entry point
            GridManager.Instance.ClearOccupant(player.State.GridPosition);
            var entryPos = layout.PlayerSpawn;
            player.State.GridPosition = entryPos;
            player.State.ShieldValue = 0;
            player.transform.position = GridManager.Instance.GridToWorld(entryPos) + new Vector3(0, 0.4f, 0);
            GridManager.Instance.SetOccupant(entryPos, player.gameObject);
        }

        // Spawn enemies based on room type
        enemies.Clear();
        enemiesDefeated = 0;
        crapsMode = new CrapsMode();

        if (room.Type == RoomType.Combat || room.Type == RoomType.Boss)
        {
            SpawnEnemiesForRoom(room, layout);
        }
        else if (room.Type == RoomType.Shop)
        {
            GenerateShopItems(room);
            spawnShopItemEntities(room);
            Log("Bienvenido a la tienda!");
        }
        else if (room.Type == RoomType.Potion)
        {
            if (!room.PotionCollected)
            {
                var center = new Vector2Int(GridManager.Instance.Width / 2, GridManager.Instance.Height / 2);
                _potionPickupTile = center;
                _potionPickupObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _potionPickupObj.name = "PotionPickup";
                _potionPickupObj.transform.position = GridManager.Instance.GridToWorld(center) + new Vector3(0, 0.3f, 0);
                _potionPickupObj.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                var potionRenderer = _potionPickupObj.GetComponent<MeshRenderer>();
                ColorUtility.TryParseHtmlString("#66bb6a", out Color potionColor);
                potionRenderer.material.color = potionColor;
                var potionCol = _potionPickupObj.GetComponent<Collider>();
                if (potionCol != null) Destroy(potionCol);
                Log("A potion awaits! Walk to it to collect.");
            }
            else
            {
                Log("Room is empty.");
            }
        }

        // Update HUD
        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);
        UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);
        UIManager.Instance.UpdateShield(player.State.ShieldValue);
        UIManager.Instance.UpdateGold(player.State.Gold);
        UIManager.Instance.UpdateDexterity(player.State.Dexterity);
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
                player.State.CombatDiceSlots,
                player.State.BaseData.StartingPowerBudget);
        }
        else
        {
            BeginPlayerMovement();
        }
    }

    private void ShowNextShopItem(RoomData room)
    {
        if (ShopUI.Instance != null)
            ShopUI.Instance.ShowAllItems(room.ShopItems, player.State.Gold);
    }

    private int GetEnemyCountForRoom(RoomData room)
    {
        if (room.Type == RoomType.Shop || room.Type == RoomType.Potion) return 0;
        if (room.Type == RoomType.Boss) return 1;

        // Check if room has saved enemies
        if (room.Enemies.Count > 0)
            return room.Enemies.Count(e => e.IsAlive);

        return Random.Range(1, 3);
    }

    private void SpawnEnemiesForRoom(RoomData room, RoomLayout layout)
    {
        float hpMult = LevelConfig.GetHPMultiplier(_currentFloor);

        if (room.Enemies.Count > 0)
        {
            // Re-spawn saved enemies with their HP, position, and energy
            int spawnIdx = 0;
            foreach (var saved in room.Enemies)
            {
                if (!saved.IsAlive) continue;
                if (spawnIdx >= layout.EnemySpawns.Count) break;

                EnemyData baseData = GetEnemyDataByName(saved.EnemyType);
                var scaledData = CreateScaledEnemyData(baseData, 1f);
                scaledData.MaxHP = saved.MaxHP;

                // Use saved position if walkable, otherwise fallback to layout spawn
                Vector2Int spawnPos = saved.GridPosition;
                var tile = GridManager.Instance.GetTile(spawnPos);
                if (tile == null || !tile.IsWalkable || tile.Occupant != null)
                    spawnPos = layout.EnemySpawns[spawnIdx];

                var entity = SpawnEnemy(scaledData, spawnPos);
                entity.State.CurrentHP = saved.CurrentHP;
                entity.State.CurrentEnergy = saved.CurrentEnergy;
                enemies.Add(entity);
                spawnIdx++;
            }
        }
        else
        {
            // Fresh enemies
            int count = GetEnemyCountForRoom(room);
            for (int i = 0; i < count && i < layout.EnemySpawns.Count; i++)
            {
                EnemyData baseData;
                if (room.Type == RoomType.Boss)
                {
                    baseData = OrcData;
                    hpMult *= 2f; // Boss has 2x HP
                }
                else
                {
                    // Mix enemy types including archer
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
        scaled.Precision = baseData.Precision;
        scaled.FiresFirst = baseData.FiresFirst;
        scaled.GoldDropMin = baseData.GoldDropMin;
        scaled.GoldDropMax = baseData.GoldDropMax;
        scaled.ModelPrefab = baseData.ModelPrefab;
        return scaled;
    }

    // ── INVENTORY ──

    private void OnInventoryConfirmed(List<DiceInstance> selectedDice)
    {
        if (CurrentState != GameState.InventorySetup) return;

        player.State.Bag = new DiceBag { MaxPower = player.State.BaseData.StartingPowerBudget };
        foreach (var die in selectedDice)
            player.State.Bag.Dice.Add(die);

        Log($"Inventario listo: {selectedDice.Count} dados seleccionados");
        BeginPlayerMovement();
    }

    // ── MOVEMENT ──

    private void BeginPlayerMovement()
    {
        bool enemiesAlive = enemies.Any(e => e != null && e.State != null && e.State.IsAlive);

        if (!enemiesAlive)
        {
            // Room cleared — check door proximity or show move freely
            if (DungeonManager.Instance.CurrentRoom != null)
                DungeonManager.Instance.MarkCurrentRoomCleared();

            TransitionTo(GameState.MovementPhase);
            UIManager.Instance.ShowPhaseLabel("ROOM CLEARED! Go to a door.");
            currentReachableTiles = MovementManager.Instance.GetReachableTiles(
                player.State.GridPosition, 100);
            GridManager.Instance.HighlightTiles(currentReachableTiles, Color.green);
            ShowExplorationUI();
            return;
        }

        TransitionTo(GameState.MovementRoll);
        UIManager.Instance.ShowMovementRollPanel(
            player.State.BaseData.SpeedMin,
            player.State.BaseData.SpeedMax);
        ShowExplorationUI();
    }

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

            // Check potion pickup
            if (_potionPickupObj != null && player.State.GridPosition == _potionPickupTile)
            {
                player.State.HasPotion = true;
                player.State.PotionCount = 1;
                var room = DungeonManager.Instance.CurrentRoom;
                if (room != null) room.PotionCollected = true;
                Destroy(_potionPickupObj);
                _potionPickupObj = null;
                UIManager.Instance.ShowPhaseLabel("POTION COLLECTED!");
                Log("Potion collected!");
                if (FloatingDamageUI.Instance != null)
                    FloatingDamageUI.Instance.ShowText("POTION!", player.transform.position, Color.green);
            }

            // Check portal pickup
            if (_portalObj != null && player.State.GridPosition == _portalTile)
            {
                AdvanceToNextFloor();
                return;
            }

            // Check shop item proximity in shop rooms
            checkShopItemProximity();

            if (enemy != null)
            {
                EnterCombat(enemy);
            }
            else
            {
                // Check if player landed on a door
                CheckDoorTransition();
            }
        });
    }

    private void CheckDoorTransition()
    {
        var tile = GridManager.Instance.GetTile(player.State.GridPosition);
        if (tile != null && tile.Type == TileType.Door && tile.DoorDirection != null)
        {
            bool enemiesAlive = enemies.Any(e => e != null && e.State != null && e.State.IsAlive);
            if (!enemiesAlive)
            {
                // Auto-transition to next room
                StartRoomTransition(tile.DoorDirection);
                return;
            }
        }

        ProcessEnemyMovement();
    }

    private void StartRoomTransition(string direction)
    {
        // Save current room state
        DungeonManager.Instance.SaveEnemyState(enemies);

        TransitionTo(GameState.RoomTransition);
        UIManager.Instance.ShowPhaseLabel("ENTERING NEW ROOM...");

        StartCoroutine(DelayedAction(1f, () =>
        {
            CleanupEnemiesAndGrid();
            bool success = DungeonManager.Instance.TransitionToRoom(direction);
            if (success)
            {
                // Update minimap
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

    // ── EXPLORATION ACTIONS ──

    private void OnBowActionSelected()
    {
        if (CurrentState != GameState.MovementPhase && CurrentState != GameState.MovementRoll) return;

        UIManager.Instance.HideMovementRollPanel();
        TransitionTo(GameState.BowTargeting);

        // Highlight 5x5 area
        bowTargetTiles = GridManager.Instance.GetTilesInRange(player.State.GridPosition, 5);
        GridManager.Instance.ClearHighlights();
        GridManager.Instance.HighlightTiles(bowTargetTiles, new Color(1f, 0.4f, 0.4f, 0.5f));
        UIManager.Instance.ShowPhaseLabel("SELECT BOW TARGET");
    }

    private void OnPotionActionSelected()
    {
        if (CurrentState != GameState.MovementPhase && CurrentState != GameState.MovementRoll) return;
        if (!player.State.HasPotion || player.State.PotionCount <= 0) return;

        UIManager.Instance.HideMovementRollPanel();
        UIManager.Instance.HideExplorationActions();

        var result = ExplorationActions.AttemptPotion(
            player.State.Dexterity, 100, player.State.MaxHP);

        player.State.Heal(result.healAmount);
        player.State.PotionCount--;
        if (player.State.PotionCount <= 0) player.State.HasPotion = false;

        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);
        Log($"Pocion usada! Curado {result.healAmount} HP (roll: {result.roll}, {result.healPercent:0}%)");

        if (FloatingDamageUI.Instance != null)
        {
            FloatingDamageUI.Instance.ShowHeal(result.healAmount, player.transform.position);
            FloatingDamageUI.Instance.ShowText($"d10:{result.roll} ({result.healPercent:0}%)",
                player.transform.position + Vector3.up * 0.5f, Color.cyan);
        }

        // Potion consumes the turn
        ProcessEnemyMovement();
    }

    private void OnFleeActionSelected()
    {
        if (CurrentState != GameState.AttackPhase && CurrentState != GameState.PreCombat
            && CurrentState != GameState.CrapsBet) return;

        var result = ExplorationActions.AttemptFlee(player.State.Speed);
        Log($"Intento de huida: roll {result.roll}, chance {result.successChance}%");

        if (result.success)
        {
            // Pay 10% max HP
            int hpCost = Mathf.RoundToInt(player.State.MaxHP * 0.1f);
            player.State.CurrentHP = Mathf.Max(1, player.State.CurrentHP - hpCost);
            UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);

            Log($"Huida exitosa! Perdiste {hpCost} HP");
            UIManager.Instance.HideCombatPanel();
            UIManager.Instance.HideEnemyInfo();
            UIManager.Instance.HideCrapsOverlay();

            DungeonManager.Instance.SaveEnemyState(enemies);
            currentCombatEnemy = null;

            // Roll speed die and auto-move away from enemies
            int fleeSteps = player.State.SpeedDie.Roll();
            Log($"Flee speed roll: {fleeSteps} tiles");
            UIManager.Instance.ShowPhaseLabel($"FLED! Rolling {fleeSteps} steps");

            StartCoroutine(FleeMovementRoutine(fleeSteps));
        }
        else
        {
            Log("Huida fallida! Turno perdido.");
            UIManager.Instance.ShowPhaseLabel("FLEE FAILED!");

            // Enemy gets a free attack
            StartCoroutine(DelayedAction(0.5f, StartEnemyAttack));
        }
    }

    private IEnumerator FleeMovementRoutine(int steps)
    {
        yield return new WaitForSeconds(0.6f);

        Vector2Int destination = findFleeDestination(steps);
        if (destination == player.State.GridPosition)
        {
            // No reachable flee tile — just resume exploration
            BeginPlayerMovement();
            yield break;
        }

        var path = MovementManager.Instance.FindPath(player.State.GridPosition, destination);
        if (path.Count == 0)
        {
            BeginPlayerMovement();
            yield break;
        }

        TransitionTo(GameState.MovementPhase);
        isAnimating = true;
        GridManager.Instance.ClearHighlights();

        MovementManager.Instance.MovePlayerAlongPathAnimated(player, path, (enemy) =>
        {
            isAnimating = false;

            if (enemy != null)
            {
                // Bumped into another enemy while fleeing — enter combat
                EnterCombat(enemy);
            }
            else
            {
                BeginPlayerMovement();
            }
        });
    }

    private Vector2Int findFleeDestination(int steps)
    {
        var reachable = MovementManager.Instance.GetReachableTiles(player.State.GridPosition, steps);
        if (reachable.Count == 0) return player.State.GridPosition;

        Vector2Int bestTile = player.State.GridPosition;
        int bestDistance = -1;

        for (int i = 0; i < reachable.Count; i++)
        {
            var tile = reachable[i];
            int totalDist = 0;
            for (int j = 0; j < enemies.Count; j++)
            {
                var e = enemies[j];
                if (e == null || !e.State.IsAlive) continue;
                totalDist += Mathf.Abs(tile.x - e.State.GridPosition.x)
                           + Mathf.Abs(tile.y - e.State.GridPosition.y);
            }

            if (totalDist > bestDistance)
            {
                bestDistance = totalDist;
                bestTile = tile;
            }
        }

        return bestTile;
    }

    private void OnForceDoorSelected()
    {
        if (CurrentState != GameState.AttackPhase && CurrentState != GameState.PreCombat) return;
        if (!IsPlayerOnDoor()) return;

        var room = DungeonManager.Instance.CurrentRoom;
        if (room != null && room.Type == RoomType.Boss)
        {
            Log("No se puede forzar la puerta del Boss!");
            UIManager.Instance.ShowPhaseLabel("CANNOT FORCE BOSS DOOR!");
            return;
        }

        int hpCost = ExplorationActions.ForceDoorHPCost();
        if (player.State.CurrentHP < hpCost)
        {
            Log($"Necesitas al menos {hpCost} HP para forzar la puerta!");
            return;
        }

        player.State.CurrentHP -= hpCost;
        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);
        Log($"Puerta forzada! -{hpCost} HP");
        UIManager.Instance.ShowPhaseLabel("DOOR FORCED!");

        var tile = GridManager.Instance.GetTile(player.State.GridPosition);

        // Mark door as forced
        if (room != null && tile.DoorDirection != null && !room.ForcedDoors.Contains(tile.DoorDirection))
            room.ForcedDoors.Add(tile.DoorDirection);

        DungeonManager.Instance.SaveEnemyState(enemies);
        UIManager.Instance.HideCombatPanel();
        UIManager.Instance.HideEnemyInfo();
        StartRoomTransition(tile.DoorDirection);
    }

    private void GenerateShopItems(RoomData room)
    {
        if (room.ShopItems.Count > 0) return; // Already generated

        var pool = new List<ShopItemData>
        {
            new ShopItemData { ItemName = "Pocion", Description = "Recarga tu pocion", GoldCost = 15, ItemType = ShopItemType.PotionRefill },
            new ShopItemData { ItemName = "d6 Bonus", Description = "Un dado d6 extra", GoldCost = 10, ItemType = ShopItemType.DiceAdd, DiceType = "d6" },
            new ShopItemData { ItemName = "d10", Description = "Dado de 10 caras (costo 2)", GoldCost = 25, ItemType = ShopItemType.DiceAdd, DiceType = "d10" },
            new ShopItemData { ItemName = "d12", Description = "Dado de 12 caras (costo 2.5)", GoldCost = 40, ItemType = ShopItemType.DiceAdd, DiceType = "d12" },
            new ShopItemData { ItemName = "Destreza+5", Description = "Aumenta Destreza en 5", GoldCost = 20, ItemType = ShopItemType.StatBoostDex },
            new ShopItemData { ItemName = "Velocidad+1", Description = "Aumenta Velocidad en 1", GoldCost = 30, ItemType = ShopItemType.StatBoostSpeed },
        };

        // Shuffle and pick 3
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var t = pool[i]; pool[i] = pool[j]; pool[j] = t;
        }
        for (int i = 0; i < 3 && i < pool.Count; i++)
            room.ShopItems.Add(pool[i]);

        // Assign tile positions across room center row
        Vector2Int[] shopPositions = {
            new Vector2Int(2, 4),
            new Vector2Int(4, 4),
            new Vector2Int(6, 4)
        };

        for (int i = 0; i < room.ShopItems.Count; i++)
        {
            Vector2Int pos = shopPositions[i];
            var tile = GridManager.Instance.GetTile(pos);
            if (tile == null || !tile.IsWalkable || tile.Occupant != null)
            {
                // Find nearby walkable tile
                pos = findNearbyWalkableTile(pos);
            }
            room.ShopItems[i].TilePosition = pos;
        }

        // Show shop items in log
        foreach (var item in room.ShopItems)
        {
            if (!item.Purchased)
                Log($"  [{item.ItemName}] - {item.GoldCost}G: {item.Description}");
        }
    }

    private Vector2Int findNearbyWalkableTile(Vector2Int center)
    {
        // Search outward in a small radius
        for (int r = 1; r <= 3; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    var candidate = new Vector2Int(center.x + dx, center.y + dy);
                    var tile = GridManager.Instance.GetTile(candidate);
                    if (tile != null && tile.IsWalkable && tile.Occupant == null)
                        return candidate;
                }
            }
        }
        return center;
    }

    private void spawnShopItemEntities(RoomData room)
    {
        _shopItemEntities.Clear();

        foreach (var item in room.ShopItems)
        {
            if (item.Purchased) continue;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"ShopItem_{item.ItemName}";
            var entity = go.AddComponent<ShopItemEntity>();
            entity.Initialize(item, item.TilePosition);
            _shopItemEntities.Add(entity);
        }
    }

    private void checkShopItemProximity()
    {
        var room = DungeonManager.Instance.CurrentRoom;
        if (room == null || room.Type != RoomType.Shop) return;

        ShopItemData closestItem = null;
        int closestDist = int.MaxValue;

        for (int i = 0; i < _shopItemEntities.Count; i++)
        {
            var entity = _shopItemEntities[i];
            if (entity == null || !entity.gameObject.activeSelf) continue;

            int dist = Mathf.Abs(player.State.GridPosition.x - entity.GridPosition.x)
                     + Mathf.Abs(player.State.GridPosition.y - entity.GridPosition.y);

            if (dist <= 2 && dist < closestDist)
            {
                closestDist = dist;
                closestItem = entity.ItemData;
            }
        }

        if (closestItem != null)
        {
            if (closestDist <= 1)
            {
                // Adjacent — show buy panel
                if (ShopUI.Instance != null)
                    ShopUI.Instance.ShowProximityItem(closestItem, player.State.Gold);
            }
            else
            {
                // Within 2 tiles — show info in log
                Log($"Nearby: [{closestItem.ItemName}] - {closestItem.GoldCost}G");
                if (ShopUI.Instance != null)
                    ShopUI.Instance.HideProximity();
            }
        }
        else
        {
            if (ShopUI.Instance != null)
                ShopUI.Instance.HideProximity();
        }
    }

    private void OnShopBuy(ShopItemData item)
    {
        if (player.State.Gold < item.GoldCost) return;

        player.State.Gold -= item.GoldCost;
        item.Purchased = true;
        UIManager.Instance.UpdateGold(player.State.Gold);

        switch (item.ItemType)
        {
            case ShopItemType.PotionRefill:
                player.State.HasPotion = true;
                player.State.PotionCount = 1;
                Log($"Comprado: Pocion por {item.GoldCost}G - Pocion recargada!");
                break;

            case ShopItemType.DiceAdd:
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
                    Log($"Comprado: {item.ItemName} por {item.GoldCost}G - Dado agregado!");
                }
                else
                {
                    Log($"Comprado: {item.ItemName} por {item.GoldCost}G");
                }
                break;

            case ShopItemType.StatBoostDex:
                player.State.Dexterity += 5;
                UIManager.Instance.UpdateDexterity(player.State.Dexterity);
                Log($"Comprado: {item.ItemName} por {item.GoldCost}G - Destreza +5!");
                break;

            case ShopItemType.StatBoostSpeed:
                player.State.Speed += 1;
                Log($"Comprado: {item.ItemName} por {item.GoldCost}G - Velocidad +1!");
                break;

            default:
                Log($"Comprado: {item.ItemName} por {item.GoldCost}G");
                break;
        }

        // Remove matching ShopItemEntity from the grid
        for (int i = _shopItemEntities.Count - 1; i >= 0; i--)
        {
            var entity = _shopItemEntities[i];
            if (entity != null && entity.ItemData == item)
            {
                entity.Remove();
                _shopItemEntities.RemoveAt(i);
                break;
            }
        }

        // Hide proximity UI after purchase
        if (ShopUI.Instance != null)
            ShopUI.Instance.HideProximity();
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

            int steps = enemy.State.SpeedDie.Roll();
            UIManager.Instance.ShowPhaseLabel($"{enemy.State.BaseData.EnemyName} rolls {steps}!");
            Log($"{enemy.State.BaseData.EnemyName} speed roll: {steps}");
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

    // ── COMBAT ENTRY ──

    private void EnterCombat(EnemyEntity enemy)
    {
        currentCombatEnemy = enemy;
        crapsMode = new CrapsMode();

        // Check all enemies for adjacency to player
        _activeCombatEnemies.Clear();
        waitingEnemies.Clear();
        _activeCombatEnemies.Add(enemy);

        foreach (var e in enemies)
        {
            if (e != null && e.State.IsAlive && e != enemy)
            {
                int dist = Mathf.Abs(e.State.GridPosition.x - player.State.GridPosition.x)
                         + Mathf.Abs(e.State.GridPosition.y - player.State.GridPosition.y);
                if (dist <= 1)
                    _activeCombatEnemies.Add(e);
                else
                    waitingEnemies.Add(e);
            }
        }

        _targetEnemyIndex = 0;

        TransitionTo(GameState.PreCombat);

        UIManager.Instance.ShowPhaseLabel(_activeCombatEnemies.Count > 1 ? "DOUBLE COMBAT!" : "COMBAT!");
        UIManager.Instance.ShowCombatPanel();
        UIManager.Instance.ShowEnemyInfo(enemy);

        // Show second enemy info if double combat
        if (_activeCombatEnemies.Count > 1)
            UIManager.Instance.ShowSecondEnemyInfo(_activeCombatEnemies[1]);

        UIManager.Instance.HideExplorationActions();
        Log($"Combat started with {enemy.State.BaseData.EnemyName}!");
        if (_activeCombatEnemies.Count > 1)
            Log($"{_activeCombatEnemies[1].State.BaseData.EnemyName} also engages!");

        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);

        // Show flee/force door buttons during combat
        if (ExplorationActionsUI.Instance != null)
        {
            var currentRoom = DungeonManager.Instance.CurrentRoom;
            bool isBossRoom = currentRoom != null && currentRoom.Type == RoomType.Boss;
            ExplorationActionsUI.Instance.SetCombatMode(IsPlayerOnDoor(), player.State.CurrentHP, isBossRoom);
            ExplorationActionsUI.Instance.Show();
        }

        if (player.State.CrapsModeAvailable)
        {
            TransitionTo(GameState.CrapsBet);
            UIManager.Instance.ShowCrapsOverlay();
        }
        else
        {
            ProcessArcherPreAttack();
        }
    }

    public void OnCrapsBetPlaced(CombinationType bet)
    {
        if (CurrentState != GameState.CrapsBet) return;
        crapsMode.Activate();
        crapsMode.PlaceBet(bet);
        crapsAttempts++;
        UIManager.Instance.HideCrapsOverlay();
        ProcessArcherPreAttack();
    }

    // ── TARGET SELECTION ──

    private void OnTargetSelected(int index)
    {
        if (index >= 0 && index < _activeCombatEnemies.Count)
        {
            _targetEnemyIndex = index;
            currentCombatEnemy = _activeCombatEnemies[index];
            UIManager.Instance.ShowEnemyInfo(currentCombatEnemy);
            Log($"Target: {currentCombatEnemy.State.BaseData.EnemyName}");

            // Highlight selected
            var info1 = UIManager.Instance.GetEnemyInfoUI();
            var info2 = UIManager.Instance.GetSecondEnemyInfoUI();
            if (info1 != null) info1.HighlightTarget(index == 0);
            if (info2 != null) info2.HighlightTarget(index == 1);

            // If we were awaiting target selection, proceed with attack
            if (_awaitingTargetSelection)
            {
                _awaitingTargetSelection = false;
                UIManager.Instance.ShowPhaseLabel(crapsMode.IsActive ? "CRAPS ROUND" : "YOUR ATTACK");
                OnPlayerRoll();
            }
        }
    }

    private void showTargetSelectionIfNeeded()
    {
        if (_activeCombatEnemies.Count > 1)
        {
            UIManager.Instance.ShowTargetSelection(true);
        }
        else
        {
            UIManager.Instance.ShowTargetSelection(false);
        }
    }

    // ── ARCHER PRE-ATTACK ──

    private void ProcessArcherPreAttack()
    {
        if (currentCombatEnemy == null || !currentCombatEnemy.State.IsAlive) { StartAttackPhase(); return; }
        if (!currentCombatEnemy.State.BaseData.FiresFirst) { StartAttackPhase(); return; }

        int damage = Random.Range(1, 7) + currentCombatEnemy.State.BaseData.Precision;
        var dodge = ExplorationActions.AttemptDodge(player.State.Dexterity, currentCombatEnemy.State.BaseData.Precision);

        Log($"Archer fires! d6+{currentCombatEnemy.State.BaseData.Precision} = {damage}");
        UIManager.Instance.ShowPhaseLabel("ARCHER FIRES!");

        StartCoroutine(ArcherPreAttackRoutine(damage, dodge));
    }

    private IEnumerator ArcherPreAttackRoutine(int damage, (bool dodged, int playerRoll, int enemyRoll) dodge)
    {
        // Arrow visual: lerp from enemy to player
        var arrowObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arrowObj.name = "Arrow";
        arrowObj.transform.localScale = new Vector3(0.1f, 0.1f, 0.4f);
        var arrowCol = arrowObj.GetComponent<Collider>();
        if (arrowCol != null) Destroy(arrowCol);
        ColorUtility.TryParseHtmlString("#ffa726", out Color arrowColor);
        arrowObj.GetComponent<MeshRenderer>().material.color = arrowColor;

        Vector3 start = currentCombatEnemy.transform.position + Vector3.up * 0.5f;
        Vector3 end = player.transform.position + Vector3.up * 0.5f;
        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * 4f;
            arrowObj.transform.position = Vector3.Lerp(start, end, t);
            arrowObj.transform.LookAt(end);
            yield return null;
        }
        Destroy(arrowObj);

        if (dodge.dodged)
        {
            Log($"Player dodged! (d6+Dex={dodge.playerRoll} vs d6+Prec={dodge.enemyRoll})");
            UIManager.Instance.ShowPhaseLabel("DODGED!");
            if (FloatingDamageUI.Instance != null)
                FloatingDamageUI.Instance.ShowText("DODGE!", player.transform.position, Color.yellow);
        }
        else
        {
            player.State.CurrentHP = Mathf.Max(0, player.State.CurrentHP - damage);
            totalDamageTaken += damage;
            UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);
            Log($"Arrow hit! {damage} damage (d6+Dex={dodge.playerRoll} vs d6+Prec={dodge.enemyRoll})");
            if (FloatingDamageUI.Instance != null)
                FloatingDamageUI.Instance.ShowDamage(damage, player.transform.position);
            if (ScreenFlashUI.Instance != null) ScreenFlashUI.Instance.FlashDamage();
        }

        yield return new WaitForSeconds(0.5f);

        if (!player.State.IsAlive)
        {
            TransitionTo(GameState.GameOver);
            UIManager.Instance.HideCombatPanel();
            UIManager.Instance.HideEnemyInfo();
            UIManager.Instance.HideExplorationActions();
            var gameOverUI = FindObjectOfType<GameOverUI>(true);
            if (gameOverUI != null) gameOverUI.Show(GetRunStats(), currentCombatEnemy.State.BaseData.EnemyName);
            UIManager.Instance.ShowGameOverOverlay();
            yield break;
        }

        StartAttackPhase();
    }

    // ── ATTACK PHASE ──

    private void StartAttackPhase()
    {
        TransitionTo(GameState.AttackPhase);
        currentAttack = new AttackPhase();

        showTargetSelectionIfNeeded();

        if (crapsMode.IsActive)
            CombatUI.Instance.ShowCrapsBetIndicator(crapsMode.BetCombo);
        else
            CombatUI.Instance.HideCrapsBetIndicator();

        // Force target selection before first roll when multiple enemies
        if (_activeCombatEnemies.Count > 1)
        {
            _awaitingTargetSelection = true;
            UIManager.Instance.ShowPhaseLabel("SELECT TARGET");
            return;
        }

        UIManager.Instance.ShowPhaseLabel(crapsMode.IsActive ? "CRAPS ROUND" : "YOUR ATTACK");
        OnPlayerRoll();
    }

    public void OnPlayerRoll()
    {
        if (CurrentState != GameState.AttackPhase) return;
        if (_awaitingTargetSelection) return;
        if (currentAttack.CurrentRoll > 0 && !currentAttack.CanRollAgain) return;

        var results = currentAttack.PerformRoll(player.State.Bag);

        CombatUI.Instance.ShowAttackUI(results, currentAttack.LockedDiceIds, player.State.Bag);
        CombatUI.Instance.ClearComboPreview();
        CombatUI.Instance.UpdateRollCounter(currentAttack.CurrentRoll, currentAttack.MaxRolls);
        CombatUI.Instance.SetRerollEnabled(currentAttack.CanRollAgain);
        CombatUI.Instance.SetCommitEnabled(true);
    }

    public void OnDiceToggleLock(string diceId)
    {
        if (CurrentState != GameState.AttackPhase) return;
        if (currentAttack.CurrentRoll == 0) return;

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
        if (CurrentState != GameState.AttackPhase) return;
        if (currentAttack.CurrentRoll == 0) return;

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
                if (crapsResult.Success)
                    ScreenFlashUI.Instance.FlashCrapsSuccess();
                else
                    ScreenFlashUI.Instance.FlashCrapsFailure();
            }

            Log(crapsResult.Success ? "Craps bet WON!" : "Craps bet LOST!");
            EnergyManager.Instance.ResetPlayerEnergy();
            UIManager.Instance.UpdateEnergy(0f);
        }

        if (combo.Type == CombinationType.Generala)
            generalaScoredThisRun = true;

        if (currentCombatEnemy == null) return;
        currentCombatEnemy.State.TakeDamage(damage);
        totalDamageDealt += damage;

        // Update the correct enemy HP panel
        if (_targetEnemyIndex == 0 || _activeCombatEnemies.Count <= 1)
            UIManager.Instance.UpdateEnemyHP(currentCombatEnemy.State.CurrentHP, currentCombatEnemy.State.MaxHP);
        else
            UIManager.Instance.UpdateSecondEnemyHP(currentCombatEnemy.State.CurrentHP, currentCombatEnemy.State.MaxHP);

        if (SoundLibrary.Instance != null)
        {
            if (crapsWon)
                AudioManager.PlayWithLowPitch(SoundLibrary.Instance.AttackToEnemy);
            else
                AudioManager.PlayWithPitch(SoundLibrary.Instance.AttackToEnemy);
        }

        if (FloatingDamageUI.Instance != null)
        {
            if (crapsWon)
                FloatingDamageUI.Instance.ShowCrapsDamage(damage, currentCombatEnemy.transform.position);
            else
                FloatingDamageUI.Instance.ShowDamage(damage, currentCombatEnemy.transform.position);
        }

        Log($"You dealt {damage} damage ({combo.Type})");

        if (damage > bestComboDamage)
        {
            bestComboDamage = damage;
            bestCombo = combo.Type;
        }

        EnergyManager.Instance.ProcessCombatAction(CombatActionType.DealtDamage, combo.Type);
        UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);
        UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);

        if (!currentCombatEnemy.State.IsAlive)
        {
            EnergyManager.Instance.ProcessCombatAction(CombatActionType.KilledEnemy);
            UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);
            HandleEnemyDeath();
            return;
        }

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
        OnDefenseReroll();
    }

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

    public void OnDefenseDieLock(string diceId)
    {
        if (CurrentState != GameState.DefensePhase) return;
        if (!currentDefense.HasRolled) return;

        currentDefense.ToggleLock(diceId);
        bool nowLocked = currentDefense.LockedDiceIds.Contains(diceId);
        CombatUI.Instance.UpdateDefenseDieLock(diceId, nowLocked);

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
        StartCoroutine(MultiEnemyAttackRoutine());
    }

    private IEnumerator MultiEnemyAttackRoutine()
    {
        // Build attack order from active combat enemies
        var attackOrder = new List<EnemyEntity>(_activeCombatEnemies);
        attackOrder.RemoveAll(e => e == null || !e.State.IsAlive);

        if (attackOrder.Count == 0)
        {
            OnContinueAfterEnemyAttack();
            yield break;
        }

        // Roll speed once per enemy, then sort by speed descending
        var speedRolls = new Dictionary<EnemyEntity, int>();
        for (int i = 0; i < attackOrder.Count; i++)
            speedRolls[attackOrder[i]] = Random.Range(attackOrder[i].State.BaseData.SpeedMin, attackOrder[i].State.BaseData.SpeedMax + 1);
        attackOrder.Sort((a, b) => speedRolls[b].CompareTo(speedRolls[a]));

        foreach (var enemy in attackOrder)
        {
            if (enemy == null || !enemy.State.IsAlive) continue;
            UIManager.Instance.ShowPhaseLabel($"{enemy.State.BaseData.EnemyName} ATTACKS!");

            int rawDamage = enemy.RollAttack();
            int shieldValue = player.State.ShieldValue;
            int netDamage = DamageResolver.ResolveEnemyAttack(rawDamage, shieldValue);

            player.State.CurrentHP = Mathf.Max(0, player.State.CurrentHP - netDamage);
            player.State.ShieldValue = 0;
            totalDamageTaken += netDamage;

            if (netDamage > 0)
            {
                EnergyManager.Instance.ProcessCombatAction(CombatActionType.TookDamage);
                UIManager.Instance.UpdateEnergy(player.State.CurrentEnergy / player.State.MaxEnergy);
                if (SoundLibrary.Instance != null) AudioManager.PlayWithPitch(SoundLibrary.Instance.AttackToPlayer);
                if (ScreenFlashUI.Instance != null) ScreenFlashUI.Instance.FlashDamage();
                if (FloatingDamageUI.Instance != null) FloatingDamageUI.Instance.ShowDamage(netDamage, player.transform.position);
            }

            Log($"{enemy.State.BaseData.EnemyName} dealt {netDamage} damage");
            CombatUI.Instance.ShowEnemyAttackResult(rawDamage, shieldValue, netDamage);
            UIManager.Instance.UpdateHP(player.State.CurrentHP, player.State.MaxHP);
            UIManager.Instance.UpdateShield(0);

            if (!player.State.IsAlive)
            {
                TransitionTo(GameState.GameOver);
                UIManager.Instance.HideCombatPanel();
                UIManager.Instance.HideEnemyInfo();
                UIManager.Instance.HideExplorationActions();
                var gameOverUI = FindObjectOfType<GameOverUI>(true);
                if (gameOverUI != null) gameOverUI.Show(GetRunStats(), enemy.State.BaseData.EnemyName);
                UIManager.Instance.ShowGameOverOverlay();
                yield break;
            }

            yield return new WaitForSeconds(0.8f);
        }

        AdvanceWaitingEnemies();
    }

    private void AdvanceWaitingEnemies()
    {
        var toPromote = new List<EnemyEntity>();
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

                    // Check if now adjacent
                    int dist = Mathf.Abs(nextTile.x - player.State.GridPosition.x)
                             + Mathf.Abs(nextTile.y - player.State.GridPosition.y);
                    if (dist <= 1)
                        toPromote.Add(waiting);
                }
                else
                {
                    toPromote.Add(waiting);
                }
            }
        }

        foreach (var promoted in toPromote)
        {
            waitingEnemies.Remove(promoted);
            if (!_activeCombatEnemies.Contains(promoted))
            {
                _activeCombatEnemies.Add(promoted);
                Log($"{promoted.State.BaseData.EnemyName} joined the combat!");
                UIManager.Instance.ShowSecondEnemyInfo(promoted);
            }
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

        if (player.State.CrapsModeAvailable)
        {
            TransitionTo(GameState.CrapsBet);
            UIManager.Instance.ShowCrapsOverlay();
        }
        else
        {
            ProcessArcherPreAttack();
        }
    }

    // ── ENEMY DEATH ──

    private void HandleEnemyDeath()
    {
        enemiesDefeated++;
        totalEnemiesDefeated++;
        _activeCombatEnemies.Remove(currentCombatEnemy);

        string enemyName = currentCombatEnemy.State.BaseData.EnemyName;
        UIManager.Instance.ShowPhaseLabel("ENEMY DEFEATED!");
        Log($"{enemyName} has been slain!");

        // Gold drop — fallback to 5-15 if data has no gold configured
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

        // Promote waiting enemies before checking if combat continues
        AdvanceWaitingEnemies();

        // A4: Update enemy info UI after death
        if (_activeCombatEnemies.Count == 1)
        {
            _targetEnemyIndex = 0;
            currentCombatEnemy.PlayDeathAnimation(() =>
            {
                // Switch to remaining enemy
                var remaining = _activeCombatEnemies[0];
                currentCombatEnemy = remaining;
                UIManager.Instance.ShowEnemyInfo(remaining);
                UIManager.Instance.HideSecondEnemyInfo();
                UIManager.Instance.ShowTargetSelection(false);
                onEnemyDeathCombatContinue();
            });
        }
        else if (_activeCombatEnemies.Count > 1)
        {
            _targetEnemyIndex = 0;
            currentCombatEnemy.PlayDeathAnimation(() =>
            {
                // Update both panels with remaining enemies
                currentCombatEnemy = _activeCombatEnemies[0];
                UIManager.Instance.ShowEnemyInfo(_activeCombatEnemies[0]);
                UIManager.Instance.ShowSecondEnemyInfo(_activeCombatEnemies[1]);
                showTargetSelectionIfNeeded();
                onEnemyDeathCombatContinue();
            });
        }
        else
        {
            // All active enemies dead — play death anim then check room clear
            UIManager.Instance.HideSecondEnemyInfo();
            UIManager.Instance.ShowTargetSelection(false);
            currentCombatEnemy.PlayDeathAnimation(OnEnemyDeathAnimationComplete);
        }
    }

    private void onEnemyDeathCombatContinue()
    {
        // Continue combat — player gets to attack remaining enemies before enemy turn
        UIManager.Instance.ShowCombatPanel();
        StartAttackPhase();
    }

    private void OnEnemyDeathAnimationComplete()
    {
        UIManager.Instance.HideCombatPanel();
        UIManager.Instance.HideEnemyInfo();
        UIManager.Instance.HideExplorationActions();

        // All active combat enemies are dead — show reward
        var currentRoom = DungeonManager.Instance.CurrentRoom;
        if (currentRoom != null && currentRoom.Type == RoomType.Boss)
        {
            Log("BOSS DEFEATED! A portal appears...");
            UIManager.Instance.ShowPhaseLabel("BOSS DEFEATED!");

            // Spawn portal at center
            var center = new Vector2Int(GridManager.Instance.Width / 2, GridManager.Instance.Height / 2);
            _portalTile = center;
            _portalObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _portalObj.name = "Portal";
            _portalObj.transform.position = GridManager.Instance.GridToWorld(center) + new Vector3(0, 0.3f, 0);
            _portalObj.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
            var portalRenderer = _portalObj.GetComponent<MeshRenderer>();
            ColorUtility.TryParseHtmlString("#ffd54f", out Color portalColor);
            portalRenderer.material.color = portalColor;
            var col = _portalObj.GetComponent<Collider>();
            if (col != null) Destroy(col);

            DungeonManager.Instance.MarkCurrentRoomCleared();

            // Show reward first, then player can move to portal
            TransitionTo(GameState.RewardSelection);
            var bossOffers = RewardGenerator.GenerateOffers(player.State.Bag, 2);
            var bossRewardUI = FindObjectOfType<RewardUI>(true);
            if (bossRewardUI != null) bossRewardUI.ShowOffers(bossOffers);
            UIManager.Instance.ShowRewardOverlay();
            return;
        }

        // Room cleared
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
        Log($"Upgrade applied: {offer.Upgrade.Description}");

        UIManager.Instance.HideRewardOverlay();

        bool enemiesRemain = enemies.Any(e => e != null && e.State != null && e.State.IsAlive);
        if (!enemiesRemain)
        {
            UIManager.Instance.ShowPhaseLabel("ROOM CLEARED! Go to a door!");
            DungeonManager.Instance.MarkCurrentRoomCleared();
        }

        TransitionTo(GameState.MovementPhase);
        BeginPlayerMovement();
    }

    public void RestartRun()
    {
        UIManager.Instance.HideAllPanels();
        StartRun();
    }

    private void AdvanceToNextFloor()
    {
        _currentFloor++;
        Log("Advancing to next floor...");
        UIManager.Instance.ShowPhaseLabel("NEXT FLOOR!");
        if (_portalObj != null) { Destroy(_portalObj); _portalObj = null; }

        TransitionTo(GameState.LevelTransition);
        StartCoroutine(DelayedAction(1f, () =>
        {
            CleanupEnemiesAndGrid();
            DungeonManager.Instance.GenerateFloor(10);
            if (MinimapUI.Instance != null)
            {
                MinimapUI.Instance.BuildMinimap(DungeonManager.Instance.Rooms);
                MinimapUI.Instance.UpdateCurrentRoom(DungeonManager.Instance.CurrentRoom.FloorPosition);
            }
            SetupCurrentRoom(false);
        }));
    }

    // ── INPUT HANDLING ──

    private void Update()
    {
        if (isAnimating) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (CurrentState == GameState.MovementPhase)
            {
                HandleMovementClick();
            }
            else if (CurrentState == GameState.BowTargeting)
            {
                HandleBowClick();
            }
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
            // Cancel bow
            GridManager.Instance.ClearHighlights();
            bowTargetTiles = null;
            BeginPlayerMovement();
            return;
        }

        GridManager.Instance.ClearHighlights();
        bowTargetTiles = null;
        UIManager.Instance.HideExplorationActions();

        // Check if enemy is on target tile
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
            Log("No hay enemigo en esa casilla!");
            UIManager.Instance.ShowPhaseLabel("MISS - No target!");
            ProcessEnemyMovement();
            return;
        }

        var result = ExplorationActions.AttemptBow(player.State.Dexterity, 100);
        Log($"Arco: roll {result.roll}, chance {result.hitChance}%");

        if (result.hit)
        {
            int damage = ExplorationActions.CalculateBowDamage(result.roll);
            targetEnemy.State.TakeDamage(damage);
            Log($"Impacto! {damage} de daño a {targetEnemy.State.BaseData.EnemyName}");
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
        int bGoldMin = target.State.BaseData.GoldDropMin;
        int bGoldMax = target.State.BaseData.GoldDropMax;
        if (bGoldMin <= 0 && bGoldMax <= 0) { bGoldMin = 5; bGoldMax = 15; }
        int goldDrop = Random.Range(bGoldMin, bGoldMax + 1);
        player.State.Gold += goldDrop;
        UIManager.Instance.UpdateGold(player.State.Gold);
        GridManager.Instance.ClearOccupant(target.State.GridPosition);
        target.PlayDeathAnimation(() => ProcessEnemyMovement());
    }

    private Vector2Int GetGridPosFromMouse()
    {
        // Raycast from camera to XZ ground plane for 3D isometric
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 hitPoint = ray.GetPoint(distance);
            return GridManager.Instance.WorldToGrid(hitPoint);
        }

        return new Vector2Int(-1, -1);
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
        var go = Instantiate(playerPrefab, GridManager.Instance.GridToWorld(pos) + new Vector3(0, 0.4f, 0), Quaternion.identity);
        go.SetActive(true);
        var entity = go.GetComponent<PlayerEntity>();
        entity.Initialize(data, pos);
        GridManager.Instance.SetOccupant(pos, go);
        return entity;
    }

    private EnemyEntity SpawnEnemy(EnemyData data, Vector2Int pos)
    {
        var go = Instantiate(enemyPrefab, GridManager.Instance.GridToWorld(pos) + new Vector3(0, 0.4f, 0), Quaternion.identity);
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
