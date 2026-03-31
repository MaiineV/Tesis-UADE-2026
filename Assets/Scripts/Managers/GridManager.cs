using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance;

    [Header("Grid Config")]
    public int Width = 8;
    public int Height = 8;
    public float TileSize = 1f;
    public Vector3 GridOrigin = Vector3.zero;

    private TileData[,] tiles;
    private TileVisual[,] tileVisuals;

    public Vector2Int? LadderPosition { get; private set; }

    private static readonly Color GridTileColor = new Color(0.086f, 0.129f, 0.243f);
    private static readonly Color ObstacleColor = new Color(0.059f, 0.059f, 0.137f);
    private static readonly Color LadderColor = new Color(1f, 0.835f, 0.31f);
    private static readonly Color DoorColor = new Color(0.75f, 0.55f, 0.2f);

    // Door color coding by state
    private static readonly Color DoorColorGreen;  // cleared / passable
    private static readonly Color DoorColorYellow; // forceable (enemies alive, not boss)
    private static readonly Color DoorColorRed;    // locked / boss room

    static GridManager()
    {
        ColorUtility.TryParseHtmlString("#66bb6a", out Color green);
        DoorColorGreen = green;
        ColorUtility.TryParseHtmlString("#ffb300", out Color yellow);
        DoorColorYellow = yellow;
        ColorUtility.TryParseHtmlString("#e53935", out Color red);
        DoorColorRed = red;
    }

    void Awake()
    {
        Instance = this;
    }

    public void GenerateGrid()
    {
        int obstacleCount = Random.Range(4, 7);
        var layout = RoomGenerator.GenerateRoom(Width, Height, obstacleCount, 0);
        GenerateGridInternal(layout.Obstacles, null, null);
    }

    public void GenerateGrid(RoomLayout layout)
    {
        GenerateGridInternal(layout.Obstacles, layout.LadderPosition, null);
    }

    public void GenerateGrid(RoomLayout layout, Dictionary<string, Vector2Int> doorConnections)
    {
        GenerateGridInternal(layout.Obstacles, null, doorConnections);
    }

    private void GenerateGridInternal(List<Vector2Int> obstacles, Vector2Int? ladderPos, Dictionary<string, Vector2Int> doors)
    {
        // Destroy old grid
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
                    Type = TileType.Normal,
                    DoorDirection = null
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
                if (obs.x < 0 || obs.x >= Width || obs.y < 0 || obs.y >= Height) continue;
                tiles[obs.x, obs.y].IsWalkable = false;
                tiles[obs.x, obs.y].Type = TileType.Obstacle;
                tileVisuals[obs.x, obs.y].SetColor(ObstacleColor);

                // Make obstacle tiles taller cubes
                var cube = tileVisuals[obs.x, obs.y].GetComponentInChildren<MeshRenderer>();
                if (cube != null)
                    cube.transform.localScale = new Vector3(0.92f, 0.5f, 0.92f);
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

        // Place doors on edges
        if (doors != null)
        {
            foreach (var kvp in doors)
            {
                var doorTile = GetDoorTilePosition(kvp.Key);
                if (doorTile.x < 0 || doorTile.x >= Width || doorTile.y < 0 || doorTile.y >= Height) continue;

                tiles[doorTile.x, doorTile.y].Type = TileType.Door;
                tiles[doorTile.x, doorTile.y].IsWalkable = true;
                tiles[doorTile.x, doorTile.y].DoorDirection = kvp.Key;

                // Clear obstacle if one was placed on door tile
                tileVisuals[doorTile.x, doorTile.y].SetAsDoor(DoorColor);
            }
        }
    }

    private Vector2Int GetDoorTilePosition(string direction)
    {
        int midX = Width / 2;
        int midY = Height / 2;
        switch (direction)
        {
            case "N": return new Vector2Int(midX, Height - 1);
            case "S": return new Vector2Int(midX, 0);
            case "E": return new Vector2Int(Width - 1, midY);
            case "W": return new Vector2Int(0, midY);
            default: return Vector2Int.zero;
        }
    }

    // 3D isometric coordinate conversion
    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        float x = gridPos.x * TileSize;
        float z = gridPos.y * TileSize;
        return new Vector3(GridOrigin.x + x, 0f, GridOrigin.z + z);
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        float x = (worldPos.x - GridOrigin.x) / TileSize;
        float z = (worldPos.z - GridOrigin.z) / TileSize;
        return new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(z));
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
        if (pos.x < 0 || pos.x >= Width || pos.y < 0 || pos.y >= Height) return null;
        return tiles[pos.x, pos.y];
    }

    public void SetOccupant(Vector2Int pos, GameObject occupant)
    {
        tiles[pos.x, pos.y].Occupant = occupant;
    }

    public void ClearOccupant(Vector2Int pos)
    {
        if (pos.x >= 0 && pos.x < Width && pos.y >= 0 && pos.y < Height)
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
                    else if (tiles[x, y].Type == TileType.Door)
                    {
                        // Preserve door color coding — UpdateDoorColors will set proper colors
                        // Use default door color as fallback
                        tileVisuals[x, y].SetAsDoor(DoorColor);
                    }
                    else
                        tileVisuals[x, y].ResetColor();
                }
            }
        }

        // Re-apply door color coding after clearing highlights
        UpdateDoorColors();
    }

    // Get all door tiles
    public List<TileData> GetDoorTiles()
    {
        var doorTiles = new List<TileData>();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (tiles[x, y].Type == TileType.Door)
                    doorTiles.Add(tiles[x, y]);
        return doorTiles;
    }

    // Update door tile colors based on connected room state
    public void UpdateDoorColors()
    {
        if (tiles == null || DungeonManager.Instance == null) return;

        var currentRoom = DungeonManager.Instance.CurrentRoom;
        if (currentRoom == null) return;

        bool roomCleared = currentRoom.Cleared;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (tiles[x, y].Type != TileType.Door) continue;
                string dir = tiles[x, y].DoorDirection;
                if (dir == null) continue;

                Color doorColor;

                if (roomCleared)
                {
                    // All doors green when room is cleared
                    doorColor = DoorColorGreen;
                }
                else
                {
                    // Check connected room type
                    var targetPos = DungeonManager.Instance.GetConnectedRoomPosition(dir);
                    if (targetPos.HasValue)
                    {
                        var targetRoom = DungeonManager.Instance.GetRoom(targetPos.Value);
                        if (targetRoom != null && targetRoom.Type == RoomType.Boss)
                            doorColor = DoorColorRed;
                        else
                            doorColor = DoorColorYellow; // forceable
                    }
                    else
                    {
                        doorColor = DoorColorRed; // no connection = locked
                    }
                }

                tileVisuals[x, y].SetAsDoor(doorColor);
            }
        }
    }

    // Get tiles within range for bow targeting
    public List<Vector2Int> GetTilesInRange(Vector2Int center, int range)
    {
        var result = new List<Vector2Int>();
        int halfRange = range / 2;
        for (int x = center.x - halfRange; x <= center.x + halfRange; x++)
        {
            for (int y = center.y - halfRange; y <= center.y + halfRange; y++)
            {
                if (x >= 0 && x < Width && y >= 0 && y < Height)
                    result.Add(new Vector2Int(x, y));
            }
        }
        return result;
    }
}
