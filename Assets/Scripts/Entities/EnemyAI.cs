using UnityEngine;

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
