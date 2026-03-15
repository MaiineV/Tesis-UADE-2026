using UnityEngine;
using UnityEngine.UI;

public class SceneSetup : MonoBehaviour
{
    [Header("Manager Prefabs (optional — created if not present)")]
    [SerializeField] private GameObject gameManagerPrefab;

    private void Awake()
    {
        SetupCamera();
        EnsureEventSystem();
        var canvas = EnsureCanvas();
        EnsureManagers();
        BuildUI(canvas);
        WireGameManagerData();
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

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    private Canvas EnsureCanvas()
    {
        var existing = FindObjectOfType<Canvas>();
        if (existing != null) return existing;

        var canvasGO = new GameObject("Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        return canvas;
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

    private void BuildUI(Canvas canvas)
    {
        var builderGO = new GameObject("UIBuilder");
        var builder = builderGO.AddComponent<UIBuilder>();
        builder.BuildAllUI(canvas);
    }

    private void WireGameManagerData()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // Load ScriptableObject data from Assets/Data/ using Resources if available,
        // otherwise create runtime instances with matching values
        var warrior = LoadOrCreateCharacterData();
        var goblin = LoadOrCreateEnemyData("Goblin", 40, 2, 6, 1, 3, 50f, 15f, "#66bb6a");
        var orc = LoadOrCreateEnemyData("Orc", 60, 2, 8, 1, 2, 40f, 12f, "#ef5350");

        // Create simple entity prefabs at runtime
        var playerPrefab = CreateEntityPrefab("PlayerPrefab", "#4fc3f7", typeof(PlayerEntity));
        var enemyPrefab = CreateEntityPrefab("EnemyPrefab", "#66bb6a", typeof(EnemyEntity));

        gm.InitializeData(warrior, goblin, orc, playerPrefab, enemyPrefab);
    }

    private CharacterData LoadOrCreateCharacterData()
    {
        // Try Resources.Load first
        var loaded = Resources.Load<CharacterData>("Warrior");
        if (loaded != null) return loaded;

        // Create runtime instance
        var data = ScriptableObject.CreateInstance<CharacterData>();
        data.CharacterName = "Warrior";
        data.ClassName = "Warrior";
        data.Description = "A balanced fighter with strong dice combinations.";
        ColorUtility.TryParseHtmlString("#4fc3f7", out Color c);
        data.CharacterColor = c;
        data.MaxHP = 100;
        data.StartingPowerBudget = 8;
        data.SpeedMin = 2;
        data.SpeedMax = 5;

        // Create dice data at runtime
        var d6 = CreateDiceData("d6", 6, new[] { 1, 2, 3, 4, 5, 6 }, 1, "#42a5f5");
        var d8 = CreateDiceData("d8", 8, new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, 2, "#66bb6a");

        data.StartingDice = new[]
        {
            new DiceLoadout { DiceType = d6, Quantity = 4 },
            new DiceLoadout { DiceType = d8, Quantity = 2 }
        };
        data.CombatDiceSlots = 5;

        data.AffinityCombo = CombinationType.FourOfAKind;
        data.AffinityDamageBonus = 1.25f;
        data.UnlockedByDefault = true;

        return data;
    }

    private DiceData CreateDiceData(string diceName, int faces, int[] defaultFaces, int powerCost, string hex)
    {
        var data = ScriptableObject.CreateInstance<DiceData>();
        data.DiceName = diceName;
        data.NumberOfFaces = faces;
        data.DefaultFaces = defaultFaces;
        data.PowerCost = powerCost;
        ColorUtility.TryParseHtmlString(hex, out Color c);
        data.DiceColor = c;
        return data;
    }

    private EnemyData LoadOrCreateEnemyData(string enemyName, int hp, int atkCount, int atkFaces,
        int spdMin, int spdMax, float maxEnergy, float energyPerRound, string colorHex)
    {
        var loaded = Resources.Load<EnemyData>(enemyName);
        if (loaded != null) return loaded;

        var data = ScriptableObject.CreateInstance<EnemyData>();
        data.EnemyName = enemyName;
        ColorUtility.TryParseHtmlString(colorHex, out Color c);
        data.EnemyColor = c;
        data.MaxHP = hp;
        data.AttackDiceCount = atkCount;
        data.AttackDiceFaces = atkFaces;
        data.SpeedMin = spdMin;
        data.SpeedMax = spdMax;
        data.MaxEnergy = maxEnergy;
        data.EnergyPerRound = energyPerRound;
        data.Behavior = EnemyBehavior.Aggressive;
        return data;
    }

    private GameObject CreateEntityPrefab(string name, string colorHex, System.Type componentType)
    {
        var prefab = new GameObject(name);
        prefab.SetActive(false);

        var sr = prefab.AddComponent<SpriteRenderer>();
        ColorUtility.TryParseHtmlString(colorHex, out Color c);
        sr.color = c;

        // Create a simple white square sprite at runtime
        var tex = new Texture2D(32, 32);
        var pixels = new Color[32 * 32];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);

        prefab.AddComponent(componentType);

        DontDestroyOnLoad(prefab);
        return prefab;
    }

    private T EnsureManager<T>(string name) where T : MonoBehaviour
    {
        var existing = FindObjectOfType<T>();
        if (existing != null) return existing;

        var go = new GameObject(name);
        return go.AddComponent<T>();
    }
}
