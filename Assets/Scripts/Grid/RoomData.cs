using System.Collections.Generic;
using UnityEngine;

public enum RoomType { Combat, Shop, Potion, Boss }

[System.Serializable]
public class RoomData
{
    public Vector2Int FloorPosition;
    public RoomType Type;
    public bool Discovered;
    public bool Cleared;
    public List<Vector2Int> Obstacles = new List<Vector2Int>();
    public List<EnemySaveData> Enemies = new List<EnemySaveData>();
    public List<ShopItemData> ShopItems = new List<ShopItemData>();
    public Dictionary<string, Vector2Int> DoorConnections = new Dictionary<string, Vector2Int>();

    // Cached room layout for re-entering
    public Vector2Int PlayerSpawn;
    public List<Vector2Int> EnemySpawns = new List<Vector2Int>();
    public bool LayoutGenerated;
    public bool PotionCollected;
}

[System.Serializable]
public class EnemySaveData
{
    public string EnemyType; // "Goblin", "Orc", "Archer"
    public int CurrentHP;
    public int MaxHP;
    public bool IsAlive;
    public Vector2Int GridPosition;
    public float CurrentEnergy;
}

[System.Serializable]
public class ShopItemData
{
    public string ItemName;
    public string Description;
    public int GoldCost;
    public Vector2Int TilePosition;
    public bool Purchased;
    public string DiceType; // "d6", "d8", "d10", "d12" for dice items
    public ShopItemType ItemType;
}
