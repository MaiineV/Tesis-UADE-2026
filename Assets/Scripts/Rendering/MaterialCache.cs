using System.Collections.Generic;
using UnityEngine;

public static class MaterialCache
{
    static readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>();

    public static Material Get(string name)
    {
        if (_cache.TryGetValue(name, out var cached))
            return cached;

        var mat = Resources.Load<Material>("Materials/" + name);
        if (mat == null)
            Debug.LogWarning($"[MaterialCache] Material not found: Materials/{name}");

        _cache[name] = mat;
        return mat;
    }

    public static Material Player => Get("Mat_Player");
    public static Material Goblin => Get("Mat_Goblin");
    public static Material Orc => Get("Mat_Orc");
    public static Material Archer => Get("Mat_Archer");
    public static Material Potion => Get("Mat_Potion");
    public static Material Arrow => Get("Mat_Arrow");
    public static Material Portal => Get("Mat_Portal");
    public static Material ShopGold => Get("Mat_ShopGold");
    public static Material ShopGreen => Get("Mat_ShopGreen");
    public static Material ShopCyan => Get("Mat_ShopCyan");
    public static Material ShopPurple => Get("Mat_ShopPurple");
    public static Material GridTile => Get("Mat_GridTile");
    public static Material Obstacle => Get("Mat_Obstacle");
}
