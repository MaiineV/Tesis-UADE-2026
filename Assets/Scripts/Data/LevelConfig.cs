using System.Collections.Generic;
using UnityEngine;

public static class LevelConfig
{
    // Enemy count: starts at 2, grows by 1 every 2 levels, max 5
    public static int GetEnemyCount(int level)
    {
        return Mathf.Min(2 + (level - 1) / 2, 5);
    }

    // Obstacle count: base 4-6, +1 every 3 levels
    public static int GetObstacleCount(int level)
    {
        return Random.Range(4, 7) + (level - 1) / 3;
    }

    // Enemy HP multiplier: +25% per level
    public static float GetHPMultiplier(int level)
    {
        return 1f + (level - 1) * 0.25f;
    }

    // Enemy damage multiplier: +10% per level (applied to dice count)
    public static float GetDamageMultiplier(int level)
    {
        return 1f + (level - 1) * 0.1f;
    }

    // Returns a list of enemy type names for spawning
    // Early levels: mostly goblins. Later levels: more orcs.
    public static List<string> GetEnemyTypes(int level, int enemyCount)
    {
        var types = new List<string>();

        for (int i = 0; i < enemyCount; i++)
        {
            // Orc probability increases with level
            float orcChance = Mathf.Clamp01(0.2f + (level - 1) * 0.15f);
            types.Add(Random.value < orcChance ? "Orc" : "Goblin");
        }

        // Guarantee at least one orc from level 2+
        if (level >= 2 && !types.Contains("Orc") && types.Count > 0)
            types[types.Count - 1] = "Orc";

        return types;
    }
}
