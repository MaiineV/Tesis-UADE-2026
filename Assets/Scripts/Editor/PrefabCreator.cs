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

        // 3D cube visual (blue)
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        var col = visual.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        var mr = visual.GetComponent<MeshRenderer>();
        ColorUtility.TryParseHtmlString("#4fc3f7", out Color playerColor);

        go.AddComponent<PlayerEntity>();
        var entity = go.GetComponent<PlayerEntity>();
        entity.Visual = mr;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("Created Player prefab at " + path);
    }

    private static void CreateEnemyPrefab()
    {
        string path = "Assets/Prefabs/Enemy.prefab";
        EnsureDirectory("Assets/Prefabs");

        var go = new GameObject("Enemy");

        // 3D cube visual
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        var col = visual.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        var mr = visual.GetComponent<MeshRenderer>();

        go.AddComponent<EnemyEntity>();
        var entity = go.GetComponent<EnemyEntity>();
        entity.Visual = mr;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("Created Enemy prefab at " + path);
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
