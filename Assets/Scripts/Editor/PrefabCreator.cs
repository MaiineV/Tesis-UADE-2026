using UnityEngine;
using UnityEditor;

public class PrefabCreator
{
    [MenuItem("Tools/Create Prefabs")]
    public static void CreatePrefabs()
    {
        CreatePlayerPrefab();
        CreateEnemyPrefab();
        AssetDatabase.Refresh();
        Debug.Log("Prefabs created in Assets/Prefabs/");
    }

    private static void CreatePlayerPrefab()
    {
        string path = "Assets/Prefabs/Player.prefab";
        EnsureDirectory("Assets/Prefabs");

        var go = new GameObject("Player");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        ColorUtility.TryParseHtmlString("#4fc3f7", out Color playerColor);
        sr.color = playerColor;
        sr.sortingOrder = 10;

        go.AddComponent<PlayerEntity>();

        // Assign the SpriteRenderer to the Visual field
        var entity = go.GetComponent<PlayerEntity>();
        entity.Visual = sr;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("Created Player prefab at " + path);
    }

    private static void CreateEnemyPrefab()
    {
        string path = "Assets/Prefabs/Enemy.prefab";
        EnsureDirectory("Assets/Prefabs");

        var go = new GameObject("Enemy");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = Color.red;
        sr.sortingOrder = 10;

        go.AddComponent<EnemyEntity>();

        var entity = go.GetComponent<EnemyEntity>();
        entity.Visual = sr;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("Created Enemy prefab at " + path);
    }

    private static Sprite CreateSquareSprite()
    {
        // Create a simple 4x4 white texture as placeholder sprite
        var tex = new Texture2D(4, 4);
        var colors = new Color[16];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    private static void EnsureDirectory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
