using System;
using System.Collections;
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

    /// Move player step by step (animated). Calls onComplete with enemy if collision, null otherwise.
    public void MovePlayerAlongPathAnimated(PlayerEntity player, List<Vector2Int> path, Action<EnemyEntity> onComplete)
    {
        StartCoroutine(MovePlayerAlongPathRoutine(player, path, onComplete));
    }

    private IEnumerator MovePlayerAlongPathRoutine(PlayerEntity player, List<Vector2Int> path, Action<EnemyEntity> onComplete)
    {
        GridManager.Instance.ClearOccupant(player.State.GridPosition);
        float moveSpeed = 5f; // tiles per second (0.2s per tile)

        for (int i = 0; i < path.Count; i++)
        {
            var step = path[i];

            // Check for enemy at this tile before moving
            var tile = GridManager.Instance.GetTile(step);
            if (tile.Occupant != null && tile.Occupant.TryGetComponent<EnemyEntity>(out var enemy))
            {
                GridManager.Instance.SetOccupant(player.State.GridPosition, player.gameObject);
                OnCollisionWithEnemy?.Invoke(enemy);
                onComplete?.Invoke(enemy);
                yield break;
            }

            // Animate one step
            Vector3 start = player.transform.position;
            Vector3 target = GridManager.Instance.GridToWorld(step);
            float duration = 1f / moveSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                player.transform.position = Vector3.Lerp(start, target, t);
                yield return null;
            }

            player.transform.position = target;
            player.State.GridPosition = step;

            // Footstep sound per tile
            if (SoundLibrary.Instance != null)
                AudioManager.PlayWithPitch(SoundLibrary.Instance.Footstep, 0.6f);
        }

        GridManager.Instance.SetOccupant(player.State.GridPosition, player.gameObject);
        OnMovementCompleted?.Invoke(player.State.GridPosition);
        onComplete?.Invoke(null);
    }

    /// Move enemy toward player (animated). Caller provides steps (from speed die roll). Calls onComplete(true) on collision.
    public void MoveEnemyAnimated(EnemyEntity enemy, PlayerEntity player, int steps, Action<bool> onComplete)
    {
        StartCoroutine(MoveEnemyAnimatedRoutine(enemy, player, steps, onComplete));
    }

    private IEnumerator MoveEnemyAnimatedRoutine(EnemyEntity enemy, PlayerEntity player, int steps, Action<bool> onComplete)
    {
        var path = FindPath(enemy.State.GridPosition, player.State.GridPosition);
        if (path.Count == 0)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        int stepsToTake = Mathf.Min(steps, path.Count);
        GridManager.Instance.ClearOccupant(enemy.State.GridPosition);

        for (int i = 0; i < stepsToTake; i++)
        {
            Vector2Int nextTile = path[i];

            // Check if next tile is the player
            if (nextTile == player.State.GridPosition)
            {
                if (i > 0)
                {
                    GridManager.Instance.SetOccupant(path[i - 1], enemy.gameObject);
                }
                else
                {
                    GridManager.Instance.SetOccupant(enemy.State.GridPosition, enemy.gameObject);
                }
                onComplete?.Invoke(true);
                yield break;
            }

            // Animate step using EnemyEntity's existing animation
            bool stepDone = false;
            enemy.AnimateMoveTo(nextTile, () => stepDone = true);
            while (!stepDone) yield return null;

            // Footstep sound per tile
            if (SoundLibrary.Instance != null)
                AudioManager.PlayWithPitch(SoundLibrary.Instance.Footstep, 0.5f);
        }

        GridManager.Instance.SetOccupant(enemy.State.GridPosition, enemy.gameObject);
        onComplete?.Invoke(false);
    }
}
