using UnityEngine;
using UnityEngine.UI;

public class SceneSetup : MonoBehaviour
{
    [Header("Manager Prefabs (optional — created if not present)")]
    [SerializeField] private GameObject gameManagerPrefab;

    private void Awake()
    {
        SetupCamera();
        EnsureCanvas();
        EnsureManagers();
    }

    private void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        cam.orthographic = true;
        cam.orthographicSize = 6f;

        // Center camera on grid (8x8, tile size 1, centered)
        cam.transform.position = new Vector3(4f, 4f, -10f);

        // Background color: #1a1a2e
        ColorUtility.TryParseHtmlString("#1a1a2e", out Color bg);
        cam.backgroundColor = bg;
    }

    private void EnsureCanvas()
    {
        if (FindObjectOfType<Canvas>() != null) return;

        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
    }

    private void EnsureManagers()
    {
        EnsureManager<GridManager>("GridManager");
        EnsureManager<MovementManager>("MovementManager");
        EnsureManager<EnergyManager>("EnergyManager");
        EnsureManager<DiceManager>("DiceManager");
        EnsureManager<UIManager>("UIManager");
        EnsureManager<FloatingDamageUI>("FloatingDamageUI");
        EnsureManager<ScreenFlashUI>("ScreenFlashUI");

        // GameManager is created last since it depends on others
        EnsureManager<GameManager>("GameManager");
    }

    private T EnsureManager<T>(string name) where T : MonoBehaviour
    {
        var existing = FindObjectOfType<T>();
        if (existing != null) return existing;

        var go = new GameObject(name);
        return go.AddComponent<T>();
    }
}
