using UnityEngine;

public enum TileType { Normal, Obstacle, Ladder, Door }

[System.Serializable]
public class TileData
{
    public Vector2Int Position;
    public bool IsWalkable;
    public GameObject Occupant;
    public TileType Type;
    public string DoorDirection; // "N", "S", "E", "W" — null if not a door
}
