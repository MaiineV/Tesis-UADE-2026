using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class StylizedPresetManager : MonoBehaviour
{
    public static StylizedPresetManager Instance { get; private set; }

    public enum StylizedPreset
    {
        Default = 0,
        Purple = 1,
        Warm = 2
    }

    const string PresetKey = "StylizedPresetIndex";
    const string MaterialFolder = "Materials";
    const string SpecRampResource = "Stylized/CelSpecularRamp_Default";

    static readonly string[] PresetLabels = { "DEFAULT", "PURPLE", "WARM" };
    static readonly string[] RampResources =
    {
        "Stylized/CelRamp_Default",
        "Stylized/CelRamp_Purple",
        "Stylized/CelRamp_Warm"
    };

    Texture2D[] _ramps;
    Texture2D _specRamp;
    int _currentPresetIndex;

    public event Action<int, string> PresetChanged;

    public int CurrentPresetIndex => _currentPresetIndex;
    public string CurrentPresetLabel => PresetLabels[_currentPresetIndex];
    public StylizedPreset CurrentPreset => (StylizedPreset)_currentPresetIndex;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadTextures();

        int saved = PlayerPrefs.GetInt(PresetKey, 0);
        _currentPresetIndex = Mathf.Clamp(saved, 0, PresetLabels.Length - 1);
        ApplyPresetInternal(_currentPresetIndex, false);
    }

    void Start()
    {
        // Re-apply once on Start to ensure camera/renderer feature references are ready.
        ApplyPresetInternal(_currentPresetIndex, false);
    }

    public void CyclePreset()
    {
        int next = (_currentPresetIndex + 1) % PresetLabels.Length;
        SetPreset(next);
    }

    public void SetPreset(int presetIndex)
    {
        int clamped = Mathf.Clamp(presetIndex, 0, PresetLabels.Length - 1);
        ApplyPresetInternal(clamped, true);
    }

    public void SetPreset(StylizedPreset preset)
    {
        SetPreset((int)preset);
    }

    void ApplyPresetInternal(int presetIndex, bool persist)
    {
        _currentPresetIndex = presetIndex;

        var ramp = (_ramps != null && presetIndex < _ramps.Length) ? _ramps[presetIndex] : null;
        ApplyToMaterials(ramp, _specRamp);
        ApplyToPostFeature(presetIndex);

        if (persist)
        {
            PlayerPrefs.SetInt(PresetKey, presetIndex);
            PlayerPrefs.Save();
        }

        PresetChanged?.Invoke(_currentPresetIndex, CurrentPresetLabel);
    }

    void LoadTextures()
    {
        _ramps = new Texture2D[RampResources.Length];
        for (int i = 0; i < RampResources.Length; i++)
            _ramps[i] = Resources.Load<Texture2D>(RampResources[i]);

        _specRamp = Resources.Load<Texture2D>(SpecRampResource);
    }

    void ApplyToMaterials(Texture2D ramp, Texture2D specRamp)
    {
        var materials = Resources.LoadAll<Material>(MaterialFolder);
        if (materials == null || materials.Length == 0)
            return;

        for (int i = 0; i < materials.Length; i++)
        {
            var mat = materials[i];
            if (mat == null || !mat.name.StartsWith("Mat_"))
                continue;

            if (ramp != null && mat.HasProperty("_CelRamp"))
                mat.SetTexture("_CelRamp", ramp);

            if (specRamp != null && mat.HasProperty("_CelSpecularRamp"))
                mat.SetTexture("_CelSpecularRamp", specRamp);
        }
    }

    void ApplyToPostFeature(int presetIndex)
    {
        if (!TryGetParityFeature(out var feature))
            return;

        feature.pixelSize = 4;
        feature.autoTexelOffsetFromCamera = true;
        feature.texelOffset = Vector2.zero;
        feature.lineOverlay = true;
        feature.creaseOverlay = true;
        feature.flipPalettes = false;
        feature.kernelRadius = 1f;
        feature.zdeltaCutoff = 0.25f;
        feature.angleZCutoff = 0.5f;
        feature.angleZScale = 2f;
        feature.convexCutoff = 0.1f;
        feature.creaseFeather = 0f;
        feature.concaveCutoff = 0.01f;
        feature.concaveZCutoff = 0.5f;

        switch ((StylizedPreset)presetIndex)
        {
            case StylizedPreset.Purple:
                feature.lineTint = Color.black;
                feature.creaseTint = new Color(0.74f, 0.66f, 0.87f, 1f);
                feature.lineAlpha = 0.52f;
                feature.creaseAlpha = 1f;
                break;
            case StylizedPreset.Warm:
                feature.lineTint = Color.black;
                feature.creaseTint = new Color(0.98f, 0.76f, 0.63f, 1f);
                feature.lineAlpha = 0.54f;
                feature.creaseAlpha = 1f;
                break;
            default:
                feature.lineTint = Color.black;
                feature.creaseTint = new Color(0.833f, 0.833f, 0.833f, 1f);
                feature.lineAlpha = 0.5f;
                feature.creaseAlpha = 1f;
                break;
        }
    }

    bool TryGetParityFeature(out GodotParityFeature parityFeature)
    {
        parityFeature = null;

        var cam = Camera.main;
        if (cam == null)
            cam = FindAnyObjectByType<Camera>();
        if (cam == null)
            return false;

        var cameraData = cam.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null || cameraData.scriptableRenderer == null)
            return false;

        var featuresField = typeof(ScriptableRenderer).GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
        if (featuresField == null)
            return false;

        var features = featuresField.GetValue(cameraData.scriptableRenderer) as List<ScriptableRendererFeature>;
        if (features == null || features.Count == 0)
            return false;

        for (int i = 0; i < features.Count; i++)
        {
            if (features[i] is GodotParityFeature feature)
            {
                parityFeature = feature;
                return true;
            }
        }

        return false;
    }
}
