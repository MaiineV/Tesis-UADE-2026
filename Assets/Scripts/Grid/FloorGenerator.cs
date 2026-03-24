using System.Collections.Generic;
using UnityEngine;

public static class FloorGenerator
{
    private static readonly Vector2Int[] Directions = {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public static List<RoomData> GenerateFloor(int targetRoomCount)
    {
        targetRoomCount = Mathf.Clamp(targetRoomCount, 8, 14);
        var rooms = new List<RoomData>();
        var occupied = new HashSet<Vector2Int>();

        // Start room at center
        var startPos = Vector2Int.zero;
        var startRoom = new RoomData
        {
            FloorPosition = startPos,
            Type = RoomType.Combat,
            Discovered = true
        };
        rooms.Add(startRoom);
        occupied.Add(startPos);

        // Random walk to generate connected rooms
        var frontier = new List<Vector2Int> { startPos };

        while (rooms.Count < targetRoomCount && frontier.Count > 0)
        {
            // Pick random room from frontier
            var fromPos = frontier[Random.Range(0, frontier.Count)];

            // Try random direction
            var shuffledDirs = ShuffleDirections();
            bool placed = false;

            foreach (var dir in shuffledDirs)
            {
                var newPos = fromPos + dir;
                if (occupied.Contains(newPos)) continue;

                // Limit neighbor count to avoid too-dense clusters
                int neighborCount = CountNeighbors(newPos, occupied);
                if (neighborCount > 2) continue;

                var newRoom = new RoomData
                {
                    FloorPosition = newPos,
                    Type = RoomType.Combat
                };
                rooms.Add(newRoom);
                occupied.Add(newPos);
                frontier.Add(newPos);
                placed = true;
                break;
            }

            if (!placed)
                frontier.Remove(fromPos);
        }

        // Assign special room types
        AssignRoomTypes(rooms, startPos);

        // Build door connections between adjacent rooms
        BuildDoorConnections(rooms, occupied);

        return rooms;
    }

    private static void AssignRoomTypes(List<RoomData> rooms, Vector2Int startPos)
    {
        // Boss = farthest room from start
        RoomData bossRoom = null;
        int maxDist = 0;
        foreach (var room in rooms)
        {
            int dist = ManhattanDistance(room.FloorPosition, startPos);
            if (dist > maxDist)
            {
                maxDist = dist;
                bossRoom = room;
            }
        }
        if (bossRoom != null) bossRoom.Type = RoomType.Boss;

        // Shop = ~mid distance from start, not boss
        RoomData shopRoom = null;
        int targetShopDist = maxDist / 2;
        int bestShopDelta = int.MaxValue;
        foreach (var room in rooms)
        {
            if (room == bossRoom || room.FloorPosition == startPos) continue;
            int dist = ManhattanDistance(room.FloorPosition, startPos);
            int delta = Mathf.Abs(dist - targetShopDist);
            if (delta < bestShopDelta)
            {
                bestShopDelta = delta;
                shopRoom = room;
            }
        }
        if (shopRoom != null) shopRoom.Type = RoomType.Shop;

        // Potion = pick a random combat room that's not start, shop, or boss
        var candidates = new List<RoomData>();
        foreach (var room in rooms)
        {
            if (room.Type == RoomType.Combat && room.FloorPosition != startPos)
                candidates.Add(room);
        }
        if (candidates.Count > 0)
            candidates[Random.Range(0, candidates.Count)].Type = RoomType.Potion;
    }

    private static void BuildDoorConnections(List<RoomData> rooms, HashSet<Vector2Int> occupied)
    {
        var posToRoom = new Dictionary<Vector2Int, RoomData>();
        foreach (var room in rooms) posToRoom[room.FloorPosition] = room;

        foreach (var room in rooms)
        {
            if (occupied.Contains(room.FloorPosition + Vector2Int.up) && posToRoom.ContainsKey(room.FloorPosition + Vector2Int.up))
                room.DoorConnections["N"] = room.FloorPosition + Vector2Int.up;
            if (occupied.Contains(room.FloorPosition + Vector2Int.down) && posToRoom.ContainsKey(room.FloorPosition + Vector2Int.down))
                room.DoorConnections["S"] = room.FloorPosition + Vector2Int.down;
            if (occupied.Contains(room.FloorPosition + Vector2Int.right) && posToRoom.ContainsKey(room.FloorPosition + Vector2Int.right))
                room.DoorConnections["E"] = room.FloorPosition + Vector2Int.right;
            if (occupied.Contains(room.FloorPosition + Vector2Int.left) && posToRoom.ContainsKey(room.FloorPosition + Vector2Int.left))
                room.DoorConnections["W"] = room.FloorPosition + Vector2Int.left;
        }
    }

    private static Vector2Int[] ShuffleDirections()
    {
        var dirs = (Vector2Int[])Directions.Clone();
        for (int i = dirs.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = dirs[i];
            dirs[i] = dirs[j];
            dirs[j] = tmp;
        }
        return dirs;
    }

    private static int CountNeighbors(Vector2Int pos, HashSet<Vector2Int> occupied)
    {
        int count = 0;
        foreach (var dir in Directions)
            if (occupied.Contains(pos + dir)) count++;
        return count;
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
