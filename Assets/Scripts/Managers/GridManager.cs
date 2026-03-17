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

    public Vector2Int? LadderPosition { get; private set; }

    private static readonly Color GridTileColor = new Color(0.086f, 0.129f, 0.243f); // #16213e
    private static readonly Color ObstacleColor = new Color(0.059f, 0.059f, 0.137f); // #0f0f23
    private static readonly Color LadderColor = new Color(1f, 0.835f, 0.31f);        // #ffd54f

    void Awake()
    {
        Instance = this;
    }

    // Legacy method — generates with random obstacles (no layout)
    public void GenerateGrid()
    {
        int obstacleCount = Random.Range(4, 7);
        var layout = RoomGenerator.GenerateRoom(Width, Height, obstacleCount, 0);
        // Use only obstacles from layout, ignore spawns
        GenerateGridInternal(layout.Obstacles, null);
    }

    // New method — generates from a pre-computed RoomLayout
    public void GenerateGrid(RoomLayout layout)
    {
        GenerateGridInternal(layout.Obstacles, layout.LadderPosition);
    }

    private void GenerateGridInternal(List<Vector2Int> obstacles, Vector2Int? ladderPos)
    {
        // Destroy old grid if exists
        var oldGrid = GameObject.Find("Grid");
        if (oldGrid != null) Object.Destroy(oldGrid);

        tiles = new TileData[Width, Height];
        tileVisuals = new TileVisual[Width, Height];
        LadderPosition = ladderPos;

        Transform gridParent = new GameObject("Grid").transform;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                tiles[x, y] = new TileData
                {
                    Position = new Vector2Int(x, y),
                    IsWalkable = true,
                    Occupant = null,
                    Type = TileType.Normal
                };

                GameObject tileGO = new GameObject($"Tile_{x}_{y}");
                tileGO.transform.parent = gridParent;
                tileGO.transform.position = GridToWorld(new Vector2Int(x, y));

                TileVisual visual = tileGO.AddComponent<TileVisual>();
                visual.Initialize(GridTileColor);
                tileVisuals[x, y] = visual;
            }
        }

        // Place obstacles
        if (obstacles != null)
        {
            foreach (var obs in obstacles)
            {
                tiles[obs.x, obs.y].IsWalkable = false;
                tiles[obs.x, obs.y].Type = TileType.Obstacle;
                tileVisuals[obs.x, obs.y].SetColor(ObstacleColor);
            }
        }

        // Place ladder
        if (ladderPos.HasValue)
        {
            var lp = ladderPos.Value;
            tiles[lp.x, lp.y].Type = TileType.Ladder;
            tiles[lp.x, lp.y].IsWalkable = true;
            tileVisuals[lp.x, lp.y].SetAsLadder(LadderColor);
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
                    if (tiles[x, y].Type == TileType.Ladder)
                        tileVisuals[x, y].SetAsLadder(LadderColor);
                    else
                        tileVisuals[x, y].ResetColor();
                }
            }
        }
    }
}
