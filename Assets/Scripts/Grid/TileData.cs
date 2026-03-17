using UnityEngine;

public enum TileType { Normal, Obstacle, Ladder }

[System.Serializable]
public class TileData
{
    public Vector2Int Position;
    public bool IsWalkable;
    public GameObject Occupant;
    public TileType Type;
}
