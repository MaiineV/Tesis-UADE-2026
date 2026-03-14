# 04 — Movement & Grid System

## Overview
The game takes place on a grid-based room. During combat, both player and enemies move by rolling dice. When player and enemy collide on the same tile, combat begins. In cleared rooms, movement is free (no dice).

## Dependencies
- References: `01-dice-system.md`, `02-character-system.md`, `07-enemy-system.md`
- Referenced by: `03-combat-system.md`

---

## 1. Grid Setup

### 1.1 Grid Data
```csharp
public class GridManager : MonoBehaviour
{
    public static GridManager Instance;
    
    [Header("Grid Config")]
    public int Width = 8;
    public int Height = 8;
    public float TileSize = 1f;         // world units per tile
    public Vector2 GridOrigin;           // bottom-left corner in world space
    
    private TileData[,] tiles;
    
    public void GenerateGrid()
    {
        tiles = new TileData[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                tiles[x, y] = new TileData
                {
                    Position = new Vector2Int(x, y),
                    IsWalkable = true,
                    Occupant = null
                };
            }
        }
        
        // Place some walls/obstacles (optional for prototype)
        // For now: all tiles walkable, add 4-6 random obstacles
        PlaceRandomObstacles(Random.Range(4, 7));
    }
    
    private void PlaceRandomObstacles(int count)
    {
        int placed = 0;
        while (placed < count)
        {
            int x = Random.Range(1, Width - 1);  // don't block edges
            int y = Random.Range(1, Height - 1);
            if (tiles[x, y].IsWalkable && tiles[x, y].Occupant == null)
            {
                tiles[x, y].IsWalkable = false;
                placed++;
            }
        }
    }
    
    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return new Vector3(
            GridOrigin.x + gridPos.x * TileSize + TileSize / 2,
            GridOrigin.y + gridPos.y * TileSize + TileSize / 2,
            0
        );
    }
    
    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt((worldPos.x - GridOrigin.x) / TileSize),
            Mathf.FloorToInt((worldPos.y - GridOrigin.y) / TileSize)
        );
    }
    
    public bool IsValidPosition(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < Width && 
               pos.y >= 0 && pos.y < Height && 
               tiles[pos.x, pos.y].IsWalkable;
    }
    
    public bool IsOccupied(Vector2Int pos)
    {
        return tiles[pos.x, pos.y].Occupant != null;
    }
    
    public TileData GetTile(Vector2Int pos)
    {
        return tiles[pos.x, pos.y];
    }
    
    public void SetOccupant(Vector2Int pos, GameObject occupant)
    {
        tiles[pos.x, pos.y].Occupant = occupant;
    }
    
    public void ClearOccupant(Vector2Int pos)
    {
        tiles[pos.x, pos.y].Occupant = null;
    }
}

[System.Serializable]
public class TileData
{
    public Vector2Int Position;
    public bool IsWalkable;
    public GameObject Occupant;     // null, PlayerEntity, or EnemyEntity
}
```

### 1.2 Grid Visuals (Prototype)
- Each tile: a colored square sprite
  - Walkable: light gray
  - Obstacle: dark gray / black
  - Player on tile: blue highlight
  - Enemy on tile: red highlight
  - Reachable tiles (when moving): green highlight
  - Hover tile: yellow highlight

---

## 2. Player Movement (Combat Mode)

### 2.1 Movement Flow
```
1. Player's movement turn begins
2. Roll speed die → get movement points (e.g., 4)
3. Highlight all reachable tiles within 4 steps (Manhattan distance, pathfinding around obstacles)
4. Player clicks a highlighted tile
5. Player moves to that tile (step by step animation or instant)
6. Check: did player land on or pass through an enemy tile?
   → YES: trigger combat with that enemy
   → NO: movement complete, enemy turn
```

### 2.2 Movement Logic
```csharp
public class MovementManager : MonoBehaviour
{
    public static MovementManager Instance;
    
    // Events
    public static event Action<Vector2Int, int> OnMovementStarted;  // position, steps
    public static event Action<Vector2Int> OnMovementCompleted;
    public static event Action<EnemyEntity> OnCollisionWithEnemy;
    
    /// Get all tiles reachable within N steps using BFS
    public List<Vector2Int> GetReachableTiles(Vector2Int start, int maxSteps)
    {
        var reachable = new List<Vector2Int>();
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<(Vector2Int pos, int steps)>();
        
        queue.Enqueue((start, 0));
        visited.Add(start);
        
        while (queue.Count > 0)
        {
            var (pos, steps) = queue.Dequeue();
            
            if (steps > 0) // don't include starting tile
                reachable.Add(pos);
            
            if (steps >= maxSteps) continue;
            
            // Check 4 cardinal directions
            Vector2Int[] directions = {
                Vector2Int.up, Vector2Int.down, 
                Vector2Int.left, Vector2Int.right
            };
            
            foreach (var dir in directions)
            {
                Vector2Int next = pos + dir;
                if (!visited.Contains(next) && GridManager.Instance.IsValidPosition(next))
                {
                    visited.Add(next);
                    queue.Enqueue((next, steps + 1));
                }
            }
        }
        
        return reachable;
    }
    
    /// Find shortest path from start to target (BFS)
    public List<Vector2Int> FindPath(Vector2Int start, Vector2Int target)
    {
        var visited = new Dictionary<Vector2Int, Vector2Int>(); // child → parent
        var queue = new Queue<Vector2Int>();
        
        queue.Enqueue(start);
        visited[start] = start;
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            
            if (current == target)
            {
                // Reconstruct path
                var path = new List<Vector2Int>();
                var node = target;
                while (node != start)
                {
                    path.Add(node);
                    node = visited[node];
                }
                path.Reverse();
                return path;
            }
            
            Vector2Int[] directions = {
                Vector2Int.up, Vector2Int.down,
                Vector2Int.left, Vector2Int.right
            };
            
            foreach (var dir in directions)
            {
                Vector2Int next = current + dir;
                if (!visited.ContainsKey(next) && GridManager.Instance.IsValidPosition(next))
                {
                    visited[next] = current;
                    queue.Enqueue(next);
                }
            }
        }
        
        return new List<Vector2Int>(); // no path found
    }
    
    /// Move player step by step. Returns the enemy if collision occurs, null otherwise.
    public EnemyEntity MovePlayerAlongPath(PlayerEntity player, List<Vector2Int> path)
    {
        GridManager.Instance.ClearOccupant(player.State.GridPosition);
        
        foreach (var step in path)
        {
            // Check for enemy at this tile
            var tile = GridManager.Instance.GetTile(step);
            if (tile.Occupant != null && tile.Occupant.TryGetComponent<EnemyEntity>(out var enemy))
            {
                // Collision! Stop at tile before enemy
                player.MoveTo(step);
                GridManager.Instance.SetOccupant(step, player.gameObject);
                OnCollisionWithEnemy?.Invoke(enemy);
                return enemy;
            }
            
            player.MoveTo(step);
        }
        
        // No collision — player reached destination
        GridManager.Instance.SetOccupant(player.State.GridPosition, player.gameObject);
        OnMovementCompleted?.Invoke(player.State.GridPosition);
        return null;
    }
}
```

### 2.3 Player Input for Movement
```csharp
public class PlayerMovementInput : MonoBehaviour
{
    private List<Vector2Int> reachableTiles;
    private bool awaitingMovementInput = false;
    
    /// Called when it's the player's movement turn
    public void BeginMovementSelection(PlayerEntity player, int movementPoints)
    {
        reachableTiles = MovementManager.Instance.GetReachableTiles(
            player.State.GridPosition, movementPoints);
        
        // Highlight reachable tiles
        foreach (var tile in reachableTiles)
        {
            GridManager.Instance.GetTile(tile).Highlight(Color.green);
        }
        
        awaitingMovementInput = true;
    }
    
    void Update()
    {
        if (!awaitingMovementInput) return;
        
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2Int gridPos = GridManager.Instance.WorldToGrid(worldPos);
            
            if (reachableTiles.Contains(gridPos))
            {
                awaitingMovementInput = false;
                ClearHighlights();
                
                // Find path and move
                var path = MovementManager.Instance.FindPath(
                    /* player position */, gridPos);
                // Execute move (handled by MovementManager)
            }
        }
    }
    
    private void ClearHighlights() { /* reset tile colors */ }
}
```

---

## 3. Enemy Movement

### 3.1 Enemy Movement Logic
Enemies always move toward the player using the shortest path. Each enemy has its own speed die.

```csharp
public class EnemyMovement
{
    /// Move enemy toward player. Returns true if collision (combat trigger).
    public static bool MoveEnemyTowardPlayer(EnemyEntity enemy, PlayerEntity player)
    {
        // Roll enemy's speed die
        int steps = enemy.State.SpeedDie.Roll();
        
        // Find path to player
        var path = MovementManager.Instance.FindPath(
            enemy.State.GridPosition, player.State.GridPosition);
        
        if (path.Count == 0) return false; // no path
        
        // Move up to 'steps' tiles along path
        int stepsToTake = Mathf.Min(steps, path.Count);
        
        GridManager.Instance.ClearOccupant(enemy.State.GridPosition);
        
        for (int i = 0; i < stepsToTake; i++)
        {
            Vector2Int nextTile = path[i];
            
            // Check if next tile is the player
            if (nextTile == player.State.GridPosition)
            {
                // Collision! Enemy stops adjacent to player
                if (i > 0)
                {
                    enemy.MoveTo(path[i - 1]);
                    GridManager.Instance.SetOccupant(path[i - 1], enemy.gameObject);
                }
                return true; // combat triggered
            }
            
            enemy.MoveTo(nextTile);
        }
        
        GridManager.Instance.SetOccupant(enemy.State.GridPosition, enemy.gameObject);
        return false; // no collision
    }
}
```

---

## 4. Turn Order (Movement Phase)

```
MOVEMENT PHASE (repeats until combat triggered or all enemies dead):

1. PLAYER TURN
   a. Roll player speed die → N steps
   b. Show reachable tiles
   c. Player clicks destination
   d. Move along path
   e. Collision check → if yes, START COMBAT
   
2. ENEMY TURNS (sequential, one by one)
   For each alive enemy:
   a. Roll enemy speed die → M steps
   b. Calculate path to player
   c. Move along path (up to M steps)
   d. Collision check → if yes, START COMBAT

3. If no collision → go back to step 1
```

### Implementation
```csharp
public class TurnManager : MonoBehaviour
{
    public enum Phase { PlayerMove, EnemyMove, Combat, Reward, GameOver }
    
    public Phase CurrentPhase;
    private PlayerEntity player;
    private List<EnemyEntity> enemies;
    private int currentEnemyIndex;
    
    public void StartMovementPhase()
    {
        CurrentPhase = Phase.PlayerMove;
        int steps = player.State.SpeedDie.Roll();
        // Signal UI to show reachable tiles and wait for player input
        PlayerMovementInput.Instance.BeginMovementSelection(player, steps);
    }
    
    // Called when player finishes moving
    public void OnPlayerMoveComplete(EnemyEntity collidedEnemy)
    {
        if (collidedEnemy != null)
        {
            StartCombat(collidedEnemy);
            return;
        }
        
        // Start enemy movement phase
        currentEnemyIndex = 0;
        CurrentPhase = Phase.EnemyMove;
        ProcessNextEnemy();
    }
    
    private void ProcessNextEnemy()
    {
        if (currentEnemyIndex >= enemies.Count)
        {
            // All enemies moved, back to player
            StartMovementPhase();
            return;
        }
        
        var enemy = enemies[currentEnemyIndex];
        if (!enemy.State.IsAlive)
        {
            currentEnemyIndex++;
            ProcessNextEnemy();
            return;
        }
        
        bool collision = EnemyMovement.MoveEnemyTowardPlayer(enemy, player);
        if (collision)
        {
            StartCombat(enemy);
            return;
        }
        
        currentEnemyIndex++;
        ProcessNextEnemy(); // process next enemy (add delay for visual)
    }
    
    private void StartCombat(EnemyEntity enemy)
    {
        CurrentPhase = Phase.Combat;
        CombatManager.Instance.StartCombat(player, enemy);
    }
}
```

---

## 5. Free Movement (Post-Combat / No Enemies)

When no enemies are alive in the room, the player moves freely:
- Click any walkable tile → player moves there instantly (no dice roll)
- No turn structure — real-time point-and-click on grid
- This is relevant for the full game (moving to room exits), but for the prototype it just means the player won and can explore the empty room

```csharp
public void EnableFreeMovement(PlayerEntity player)
{
    // Simple: on click, pathfind and move instantly
    // No dice rolling, no turn limits
}
```

---

## 6. Prototype Grid Layout

```
8x8 grid, single room:

[P] = Player start (bottom-left area, e.g., [1,1])
[E1] = Enemy 1 (top-right area, e.g., [6,5])
[E2] = Enemy 2 (center-right, e.g., [5,3])
[##] = Obstacle (random, 4-6 tiles)

Example:
  0 1 2 3 4 5 6 7
7 . . . . . . . .
6 . . . ## . . . .
5 . . . . . . E1 .
4 . . ## . . . . .
3 . . . . . E2 . .
2 . . . . ## . . .
1 . P . . . . . .
0 . . . . . . . .
```

### Spawn Rules (Prototype)
- Player always spawns in bottom-left quadrant
- Enemies spawn in right half of the grid
- Minimum 3 tiles distance between player and nearest enemy
- Obstacles never block the only path between player and enemies
