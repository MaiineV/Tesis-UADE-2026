using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Grid Config")]
    public int Width = 8;
    public int Height = 8;
    public float TileSize = 1f;
    public Vector2 GridOrigin;

    private TileData[,] tiles;
    private TileVisual[,] tileVisuals;

    private static readonly Color GridTileColor = new Color(0.086f, 0.129f, 0.243f); // #16213e
    private static readonly Color ObstacleColor = new Color(0.059f, 0.059f, 0.137f); // #0f0f23

    void Awake()
    {
        Instance = this;
    }

    public void GenerateGrid()
    {
        tiles = new TileData[Width, Height];
        tileVisuals = new TileVisual[Width, Height];

        Transform gridParent = new GameObject("Grid").transform;

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

                GameObject tileGO = new GameObject($"Tile_{x}_{y}");
                tileGO.transform.parent = gridParent;
                tileGO.transform.position = GridToWorld(new Vector2Int(x, y));

                TileVisual visual = tileGO.AddComponent<TileVisual>();
                visual.Initialize(GridTileColor);
                tileVisuals[x, y] = visual;
            }
        }

        PlaceRandomObstacles(Random.Range(4, 7));
    }

    private void PlaceRandomObstacles(int count)
    {
        int placed = 0;
        while (placed < count)
        {
            int x = Random.Range(1, Width - 1);
            int y = Random.Range(1, Height - 1);
            if (tiles[x, y].IsWalkable && tiles[x, y].Occupant == null)
            {
                tiles[x, y].IsWalkable = false;
                tileVisuals[x, y].SetColor(ObstacleColor);
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

    public void HighlightTiles(List<Vector2Int> positions, Color color)
    {
        foreach (var pos in positions)
        {
            if (pos.x >= 0 && pos.x < Width && pos.y >= 0 && pos.y < Height)
            {
                tileVisuals[pos.x, pos.y].SetColor(color);
            }
        }
    }

    public void ClearHighlights()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (tiles[x, y].IsWalkable)
                {
                    tileVisuals[x, y].ResetColor();
                }
            }
        }
    }
}
