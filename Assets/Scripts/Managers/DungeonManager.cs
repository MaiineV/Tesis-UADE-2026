using System;
using System.Collections.Generic;
using UnityEngine;

public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance;

    public List<RoomData> Rooms { get; private set; } = new List<RoomData>();
    public RoomData CurrentRoom { get; private set; }
    public Vector2Int StartRoomPosition { get; private set; }

    public event Action<RoomData> OnRoomChanged;

    void Awake() { Instance = this; }

    public void GenerateFloor(int targetRoomCount = 10)
    {
        Rooms = FloorGenerator.GenerateFloor(targetRoomCount);
        StartRoomPosition = Vector2Int.zero;
        CurrentRoom = GetRoom(StartRoomPosition);
    }

    public RoomData GetRoom(Vector2Int floorPos)
    {
        foreach (var room in Rooms)
            if (room.FloorPosition == floorPos) return room;
        return null;
    }

    public void SetCurrentRoom(RoomData room)
    {
        CurrentRoom = room;
        room.Discovered = true;

        // Discover adjacent rooms
        foreach (var kvp in room.DoorConnections)
        {
            var adjacent = GetRoom(kvp.Value);
            if (adjacent != null) adjacent.Discovered = true;
        }

        OnRoomChanged?.Invoke(room);
    }

    public void SaveEnemyState(List<EnemyEntity> enemies)
    {
        if (CurrentRoom == null) return;
        CurrentRoom.Enemies.Clear();
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.State == null) continue;
            CurrentRoom.Enemies.Add(new EnemySaveData
            {
                EnemyType = enemy.State.BaseData.EnemyName,
                CurrentHP = enemy.State.CurrentHP,
                MaxHP = enemy.State.MaxHP,
                IsAlive = enemy.State.IsAlive,
                GridPosition = enemy.State.GridPosition,
                CurrentEnergy = enemy.State.CurrentEnergy
            });
        }
    }

    public void MarkCurrentRoomCleared()
    {
        if (CurrentRoom != null) CurrentRoom.Cleared = true;
    }

    public Vector2Int? GetConnectedRoomPosition(string direction)
    {
        if (CurrentRoom == null) return null;
        if (CurrentRoom.DoorConnections.TryGetValue(direction, out var pos))
            return pos;
        return null;
    }

    public bool TransitionToRoom(string direction)
    {
        var targetPos = GetConnectedRoomPosition(direction);
        if (!targetPos.HasValue) return false;

        var targetRoom = GetRoom(targetPos.Value);
        if (targetRoom == null) return false;

        SetCurrentRoom(targetRoom);
        return true;
    }
}
