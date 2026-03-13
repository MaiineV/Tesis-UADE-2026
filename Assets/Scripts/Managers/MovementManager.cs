using System;
using System.Collections.Generic;
using UnityEngine;

public class MovementManager : MonoBehaviour
{
    public static MovementManager Instance;

    // Events
    public static event Action<Vector2Int, int> OnMovementStarted;  // position, steps
    public static event Action<Vector2Int> OnMovementCompleted;
    public static event Action<EnemyEntity> OnCollisionWithEnemy;

    void Awake()
    {
        Instance = this;
    }

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
        var visited = new Dictionary<Vector2Int, Vector2Int>(); // child -> parent
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

        // No collision -- player reached destination
        GridManager.Instance.SetOccupant(player.State.GridPosition, player.gameObject);
        OnMovementCompleted?.Invoke(player.State.GridPosition);
        return null;
    }
}
