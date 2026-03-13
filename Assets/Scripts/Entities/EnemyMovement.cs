using UnityEngine;

public static class EnemyMovement
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
