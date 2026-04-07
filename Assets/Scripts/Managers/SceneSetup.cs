using UnityEngine;
using UnityEngine.UI;

public class SceneSetup : MonoBehaviour
{
    [Header("Manager Prefabs (optional — created if not present)")]
    [SerializeField] private GameObject gameManagerPrefab;

    private void Awake()
    {
        SetupCamera();
        SetupLighting();
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
        cam.orthographicSize = 4.6f;

        // Isometric camera: 30° pitch, 45° yaw
        cam.transform.rotation = Quaternion.Euler(35f, 45f, 0f);

        // Position to look at center of 8x8 grid
        Vector3 gridCenter = new Vector3(4f, 0f, 4f);
        cam.transform.position = gridCenter + cam.transform.rotation * new Vector3(0, 0, -15f);

        // Background color: #1a1a2e
        ColorUtility.TryParseHtmlString("#1a1a2e", out Color bg);
        cam.backgroundColor = bg;
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    private void SetupLighting()
    {
        // Ambient light
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.5f);

        // Main directional light
        var existingLight = FindObjectOfType<Light>();
        if (existingLight == null)
        {
            var lightGO = new GameObject("DirectionalLight");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = new Color(1f, 0.95f, 0.85f);
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
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
        EnsureManager<AudioManager>("AudioManager");
        EnsureManager<SoundLibrary>("SoundLibrary");
        EnsureManager<UIManager>("UIManager");
        EnsureManager<FloatingDamageUI>("FloatingDamageUI");
        EnsureManager<ScreenFlashUI>("ScreenFlashUI");
        EnsureManager<DungeonManager>("DungeonManager");

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

        var warrior = LoadOrCreateCharacterData();

        // Ensure AvailablePoolDice is always set (persisted asset may lack it)
        if (warrior.AvailablePoolDice == null || warrior.AvailablePoolDice.Length == 0)
        {
            var d6 = CreateDiceData("d6", 6, new[] { 1, 2, 3, 4, 5, 6 }, 1f, "#42a5f5");
            var d10 = CreateDiceData("d10", 10, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 2f, "#ff7043", 4);
            var d12 = CreateDiceData("d12", 12, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, 2.5f, "#ab47bc", 8);
            warrior.AvailablePoolDice = new[] { d6, d10, d12 };
        }

        var goblin = LoadOrCreateEnemyData("Goblin", 40, 2, 6, 1, 3, 50f, 15f, "#66bb6a", 5, 10);
        goblin.OADamage = 5;
        var orc = LoadOrCreateEnemyData("Orc", 60, 2, 8, 1, 2, 40f, 12f, "#ef5350", 10, 20);
        orc.HasComboResistance = true;
        orc.OADamage = 10;
        var archer = LoadOrCreateArcherData();

        var playerPrefab = CreateEntityPrefab("PlayerPrefab", "Mat_Player", typeof(PlayerEntity), PrimitiveType.Cube);
        var enemyPrefab = CreateEntityPrefab("EnemyPrefab", "Mat_Goblin", typeof(EnemyEntity), PrimitiveType.Cube);

        gm.InitializeData(warrior, goblin, orc, playerPrefab, enemyPrefab);
        gm.SetArcherData(archer);
    }

    private CharacterData LoadOrCreateCharacterData()
    {
        var loaded = Resources.Load<CharacterData>("Warrior");
        if (loaded != null) return loaded;

        var data = ScriptableObject.CreateInstance<CharacterData>();
        data.CharacterName = "Warrior";
        data.ClassName = "Warrior";
        data.Description = "A balanced fighter with strong dice combinations.";
        ColorUtility.TryParseHtmlString("#4fc3f7", out Color c);
        data.CharacterColor = c;
        data.MaxHP = 100;
        data.StartingPowerBudget = 5f;
        data.Dexterity = 20;
        data.Speed = 3;
        data.SpeedMin = 2;
        data.SpeedMax = 5;

        // Dice: d6=1pt, d10=2pt, d12=2.5pt — budget 5
        var d6 = CreateDiceData("d6", 6, new[] { 1, 2, 3, 4, 5, 6 }, 1f, "#42a5f5");
        var d10 = CreateDiceData("d10", 10, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 2f, "#ff7043", 4);
        var d12 = CreateDiceData("d12", 12, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, 2.5f, "#ab47bc", 8);

        data.StartingDice = new[]
        {
            new DiceLoadout { DiceType = d6, Quantity = 4 },
            new DiceLoadout { DiceType = d10, Quantity = 1 },
        };
        data.CombatDiceSlots = 5;
        data.MinCombatDiceSlots = 3;
        data.AvailablePoolDice = new[] { d6, d10, d12 };

        data.AffinityCombo = CombinationType.FourOfAKind;
        data.AffinityDamageBonus = 1.25f;
        data.UnlockedByDefault = true;

        return data;
    }

    private DiceData CreateDiceData(string diceName, int faces, int[] defaultFaces, float powerCost, string hex, int bonusDamage = 0)
    {
        var data = ScriptableObject.CreateInstance<DiceData>();
        data.DiceName = diceName;
        data.NumberOfFaces = faces;
        data.DefaultFaces = defaultFaces;
        data.PowerCost = powerCost;
        data.BonusDamage = bonusDamage;
        ColorUtility.TryParseHtmlString(hex, out Color c);
        data.DiceColor = c;
        return data;
    }

    private EnemyData LoadOrCreateEnemyData(string enemyName, int hp, int atkCount, int atkFaces,
        int spdMin, int spdMax, float maxEnergy, float energyPerRound, string colorHex,
        int goldMin = 5, int goldMax = 15)
    {
        var loaded = Resources.Load<EnemyData>(enemyName);
        if (loaded != null)
        {
            // Patch gold values if they were missing from old asset files
            if (loaded.GoldDropMin == 0 && loaded.GoldDropMax == 0)
            {
                loaded.GoldDropMin = goldMin;
                loaded.GoldDropMax = goldMax;
            }
            return loaded;
        }

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
        data.IsRanged = false;
        data.GoldDropMin = goldMin;
        data.GoldDropMax = goldMax;
        return data;
    }

    private EnemyData LoadOrCreateArcherData()
    {
#if UNITY_EDITOR
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Archer.asset");
        if (asset != null) return asset;
#endif
        var data = ScriptableObject.CreateInstance<EnemyData>();
        data.EnemyName = "Archer";
        ColorUtility.TryParseHtmlString("#ffa726", out Color c);
        data.EnemyColor = c;
        data.MaxHP = 18;
        data.AttackDiceCount = 1;
        data.AttackDiceFaces = 6;
        data.SpeedMin = 1;
        data.SpeedMax = 3;
        data.MaxEnergy = 40f;
        data.EnergyPerRound = 10f;
        data.Behavior = EnemyBehavior.Ranged;
        data.IsRanged = true;
        data.PreferredRange = 3;
        data.Accuracy = 60;
        data.Precision = 3;
        data.FiresFirst = true;
        data.GoldDropMin = 10;
        data.GoldDropMax = 10;
        return data;
    }

    private GameObject CreateEntityPrefab(string name, string materialName, System.Type componentType, PrimitiveType shape)
    {
        // Create root active so children are properly initialized
        var prefab = new GameObject(name);

        // Create 3D mesh child
        var meshGO = GameObject.CreatePrimitive(shape);
        meshGO.name = "Visual";
        meshGO.transform.SetParent(prefab.transform, false);
        meshGO.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

        // Remove collider from visual
        var col = meshGO.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);

        // Assign pre-created material from Resources
        var mr = meshGO.GetComponent<MeshRenderer>();
        mr.sharedMaterial = MaterialCache.Get(materialName);

        // Add entity component and wire Visual field
        var comp = prefab.AddComponent(componentType);
        if (comp is PlayerEntity pe) pe.Visual = mr;
        else if (comp is EnemyEntity ee) ee.Visual = mr;

        // Now deactivate so Instantiate creates inactive copies
        prefab.SetActive(false);
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
