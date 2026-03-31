using System.Collections.Generic;
using UnityEngine;

public static class EnemyAI
{
    public static Vector2Int DecideMovement(EnemyEntity enemy, PlayerEntity player, int steps)
    {
        return DecideMovement(enemy, player, steps, null);
    }

    public static Vector2Int DecideMovement(EnemyEntity enemy, PlayerEntity player, int steps, HashSet<Vector2Int> blockedTiles)
    {
        switch (enemy.State.BaseData.Behavior)
        {
            case EnemyBehavior.Aggressive:
                return MoveTowardPlayer(enemy, player, steps, blockedTiles);

            case EnemyBehavior.Cautious:
                return MoveTowardPlayer(enemy, player, steps, blockedTiles);

            case EnemyBehavior.Stationary:
                return enemy.State.GridPosition;

            case EnemyBehavior.Ranged:
                return MoveRanged(enemy, player, steps, blockedTiles);

            default:
                return MoveTowardPlayer(enemy, player, steps, blockedTiles);
        }
    }

    private static Vector2Int MoveTowardPlayer(EnemyEntity enemy, PlayerEntity player, int steps, HashSet<Vector2Int> blockedTiles)
    {
        var path = blockedTiles != null
            ? MovementManager.Instance.FindPath(enemy.State.GridPosition, player.State.GridPosition, blockedTiles)
            : MovementManager.Instance.FindPath(enemy.State.GridPosition, player.State.GridPosition);

        if (path.Count == 0) return enemy.State.GridPosition;

        int maxSteps = Mathf.Min(steps, path.Count);

        for (int i = 0; i < maxSteps; i++)
        {
            if (path[i] == player.State.GridPosition)
            {
                return i > 0 ? path[i - 1] : enemy.State.GridPosition;
            }
        }

        return path[maxSteps - 1];
    }

    private static Vector2Int MoveRanged(EnemyEntity enemy, PlayerEntity player, int steps, HashSet<Vector2Int> blockedTiles)
    {
        int currentDist = ManhattanDistance(enemy.State.GridPosition, player.State.GridPosition);
        int preferredRange = enemy.State.BaseData.PreferredRange;

        // If player is too close (adjacent), flee
        if (currentDist <= 1)
        {
            return MoveAwayFromPlayer(enemy, player, steps, blockedTiles);
        }

        // If at preferred range, stay
        if (currentDist >= preferredRange - 1 && currentDist <= preferredRange + 1)
        {
            return enemy.State.GridPosition;
        }

        // If too far, move closer
        if (currentDist > preferredRange + 1)
        {
            return MoveTowardPlayer(enemy, player, Mathf.Min(steps, currentDist - preferredRange), blockedTiles);
        }

        // Default: stay
        return enemy.State.GridPosition;
    }

    private static Vector2Int MoveAwayFromPlayer(EnemyEntity enemy, PlayerEntity player, int steps, HashSet<Vector2Int> blockedTiles)
    {
        var grid = GridManager.Instance;
        var bestPos = enemy.State.GridPosition;
        int bestDist = 0;

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        // Simple greedy: find the walkable tile farthest from player within step range
        var current = enemy.State.GridPosition;
        for (int s = 0; s < steps; s++)
        {
            Vector2Int next = current;
            int nextDist = ManhattanDistance(current, player.State.GridPosition);

            foreach (var dir in dirs)
            {
                var candidate = current + dir;
                if (!grid.IsValidPosition(candidate)) continue;
                if (grid.IsOccupied(candidate) && candidate != enemy.State.GridPosition) continue;
                if (blockedTiles != null && blockedTiles.Contains(candidate)) continue;

                int dist = ManhattanDistance(candidate, player.State.GridPosition);
                if (dist > nextDist)
                {
                    next = candidate;
                    nextDist = dist;
                }
            }

            if (next == current) break;
            current = next;
        }

        return current;
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
