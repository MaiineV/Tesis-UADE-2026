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

        GridManager.Instance.HighlightTiles(reachableTiles, Color.green);
        awaitingMovementInput = true;
    }

    void Update()
    {
        if (!awaitingMovementInput) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector2Int gridPos = GetGridPosFromMouse();

            if (reachableTiles.Contains(gridPos))
            {
                awaitingMovementInput = false;
                GridManager.Instance.ClearHighlights();

                var path = MovementManager.Instance.FindPath(
                    currentPlayer.State.GridPosition, gridPos);
                MovementManager.Instance.MovePlayerAlongPathAnimated(currentPlayer, path, (_) => { });
            }
        }
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
}
