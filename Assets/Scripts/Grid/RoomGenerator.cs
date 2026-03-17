using System.Collections.Generic;
using UnityEngine;

public struct RoomLayout
{
    public List<Vector2Int> Obstacles;
    public Vector2Int PlayerSpawn;
    public List<Vector2Int> EnemySpawns;
    public Vector2Int LadderPosition;
}

public static class RoomGenerator
{
    public static RoomLayout GenerateRoom(int width, int height, int obstacleCount, int enemyCount)
    {
        var layout = new RoomLayout();

        // 1. Place obstacles with connectivity guarantee
        bool[,] walkable = new bool[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                walkable[x, y] = true;

        layout.Obstacles = PlaceObstacles(walkable, obstacleCount, width, height);

        // 2. Pick player spawn in bottom-left quadrant
        layout.PlayerSpawn = PickPlayerSpawn(walkable, width, height);

        // 3. Pick enemy spawns in right half
        layout.EnemySpawns = PickEnemySpawns(walkable, enemyCount, width, height, layout.PlayerSpawn);

        // 4. Pick ladder position far from player
        layout.LadderPosition = PickLadderPosition(walkable, width, height, layout.PlayerSpawn, layout.EnemySpawns);

        return layout;
    }

    private static List<Vector2Int> PlaceObstacles(bool[,] walkable, int count, int width, int height)
    {
        var obstacles = new List<Vector2Int>();
        int maxRetries = 50;

        for (int i = 0; i < count; i++)
        {
            bool placed = false;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                int x = Random.Range(1, width - 1);
                int y = Random.Range(1, height - 1);

                if (!walkable[x, y]) continue;

                // Tentatively place obstacle
                walkable[x, y] = false;

                if (IsFullyConnected(walkable, width, height))
                {
                    obstacles.Add(new Vector2Int(x, y));
                    placed = true;
                    break;
                }

                // Revert
                walkable[x, y] = true;
            }

            if (!placed) break; // can't place more without breaking connectivity
        }

        return obstacles;
    }

    private static bool IsFullyConnected(bool[,] walkable, int width, int height)
    {
        // Find first walkable tile
        Vector2Int? seed = null;
        int totalWalkable = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (walkable[x, y])
                {
                    totalWalkable++;
                    if (seed == null) seed = new Vector2Int(x, y);
                }
            }
        }

        if (seed == null || totalWalkable <= 1) return true;

        // BFS from seed
        var visited = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(seed.Value);
        visited.Add(seed.Value);

        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            foreach (var dir in dirs)
            {
                var next = pos + dir;
                if (next.x >= 0 && next.x < width && next.y >= 0 && next.y < height
                    && walkable[next.x, next.y] && !visited.Contains(next))
                {
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
        }

        return visited.Count == totalWalkable;
    }

    private static Vector2Int PickPlayerSpawn(bool[,] walkable, int width, int height)
    {
        var candidates = new List<Vector2Int>();
        int halfW = width / 2;
        int halfH = height / 2;

        for (int x = 0; x < halfW; x++)
            for (int y = 0; y < halfH; y++)
                if (walkable[x, y])
                    candidates.Add(new Vector2Int(x, y));

        if (candidates.Count == 0)
        {
            // Fallback: any walkable tile
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (walkable[x, y])
                        candidates.Add(new Vector2Int(x, y));
        }

        return candidates[Random.Range(0, candidates.Count)];
    }

    private static List<Vector2Int> PickEnemySpawns(bool[,] walkable, int count, int width, int height, Vector2Int playerPos)
    {
        var spawns = new List<Vector2Int>();
        var candidates = new List<Vector2Int>();
        int halfW = width / 2;

        for (int x = halfW; x < width; x++)
            for (int y = 0; y < height; y++)
                if (walkable[x, y])
                    candidates.Add(new Vector2Int(x, y));

        // Sort by distance from player (farther first) for better spread
        candidates.Sort((a, b) => ManhattanDistance(b, playerPos).CompareTo(ManhattanDistance(a, playerPos)));

        int minDistFromPlayer = 3;
        int minDistFromEachOther = 2;

        foreach (var c in candidates)
        {
            if (spawns.Count >= count) break;

            if (ManhattanDistance(c, playerPos) < minDistFromPlayer) continue;

            bool tooClose = false;
            foreach (var s in spawns)
            {
                if (ManhattanDistance(c, s) < minDistFromEachOther)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            spawns.Add(c);
        }

        // Relaxed fallback if not enough spawns found
        if (spawns.Count < count)
        {
            foreach (var c in candidates)
            {
                if (spawns.Count >= count) break;
                if (spawns.Contains(c)) continue;
                if (c == playerPos) continue;
                spawns.Add(c);
            }
        }

        return spawns;
    }

    private static Vector2Int PickLadderPosition(bool[,] walkable, int width, int height, Vector2Int playerPos, List<Vector2Int> enemySpawns)
    {
        var candidates = new List<Vector2Int>();

        // Prefer top-right area
        for (int x = width / 2; x < width; x++)
        {
            for (int y = height / 2; y < height; y++)
            {
                if (walkable[x, y] && new Vector2Int(x, y) != playerPos && !enemySpawns.Contains(new Vector2Int(x, y)))
                    candidates.Add(new Vector2Int(x, y));
            }
        }

        // Fallback: any walkable tile far from player
        if (candidates.Count == 0)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    if (walkable[x, y] && new Vector2Int(x, y) != playerPos && !enemySpawns.Contains(new Vector2Int(x, y)))
                        candidates.Add(new Vector2Int(x, y));
        }

        if (candidates.Count == 0)
            return new Vector2Int(width - 1, height - 1); // absolute fallback

        // Pick the one farthest from player
        candidates.Sort((a, b) => ManhattanDistance(b, playerPos).CompareTo(ManhattanDistance(a, playerPos)));
        // Take from top candidates with some randomness
        int topCount = Mathf.Min(5, candidates.Count);
        return candidates[Random.Range(0, topCount)];
    }

    private static int ManhattanDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
