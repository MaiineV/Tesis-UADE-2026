using System.Collections.Generic;
using UnityEngine;

public class PlayerMovementInput : MonoBehaviour
{
    private List<Vector2Int> reachableTiles;
    private bool awaitingMovementInput = false;
    private PlayerEntity currentPlayer;

    /// Called when it's the player's movement turn
    public void BeginMovementSelection(PlayerEntity player, int movementPoints)
    {
        currentPlayer = player;
        reachableTiles = MovementManager.Instance.GetReachableTiles(
            player.State.GridPosition, movementPoints);

        // Highlight reachable tiles
        GridManager.Instance.HighlightTiles(reachableTiles, Color.green);

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
                GridManager.Instance.ClearHighlights();

                // Find path and move
                var path = MovementManager.Instance.FindPath(
                    currentPlayer.State.GridPosition, gridPos);
                MovementManager.Instance.MovePlayerAlongPath(currentPlayer, path);
            }
        }
    }
}
