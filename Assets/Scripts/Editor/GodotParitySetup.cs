using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class GodotParitySetup
{
    const string CelShaderPath = "Assets/Shaders/GodotParityCelLit.shader";
    const string RampFolder = "Assets/Resources/Stylized";

    [MenuItem("Tools/Setup/Apply Godot Shader Parity")]
    public static void ApplyGodotShaderParity()
    {
        EnsureFolder("Assets/Resources", "Stylized");

        var rampDefault = CreateRampTexture(
            $"{RampFolder}/CelRamp_Default.png",
            new[]
            {
                new RampKey(0f, new Color(0.3347849f, 0.31946623f, 0.40799728f, 1f)),
                new RampKey(0.33333334f, new Color(0.514749f, 0.47306123f, 0.50380296f, 1f)),
                new RampKey(0.6666667f, new Color(0.6947131f, 0.62665623f, 0.59960866f, 1f)),
                new RampKey(1f, new Color(0.8746772f, 0.7802512f, 0.69541436f, 1f))
            });

        _ = CreateRampTexture(
            $"{RampFolder}/CelRamp_Purple.png",
            new[]
            {
                new RampKey(0f, new Color(0.31912753f, 0.13507786f, 0.28156704f, 1f)),
                new RampKey(0.33333334f, new Color(0.48864123f, 0.30996922f, 0.38658574f, 1f)),
                new RampKey(0.6666667f, new Color(0.65815496f, 0.4848606f, 0.49160445f, 1f)),
                new RampKey(1f, new Color(0.8276686f, 0.65975195f, 0.5966231f, 1f))
            });

        _ = CreateRampTexture(
            $"{RampFolder}/CelRamp_Warm.png",
            new[]
            {
                new RampKey(0f, new Color(0.37643543f, 0.08948868f, 0.36030167f, 1f)),
                new RampKey(0.33333334f, new Color(0.56924003f, 0.24348585f, 0.39462465f, 1f)),
                new RampKey(0.6666667f, new Color(0.7782801f, 0.38288277f, 0.40822628f, 1f)),
                new RampKey(1f, new Color(1f, 0.5233741f, 0.3947761f, 1f))
            });

        var specRamp = CreateRampTexture(
            $"{RampFolder}/CelSpecularRamp_Default.png",
            new[]
            {
                new RampKey(0f, Color.black),
                new RampKey(0.4f, Color.black),
                new RampKey(0.8f, Color.white),
                new RampKey(1f, Color.white)
            });

        ApplyCelShaderToGameplayMaterials(rampDefault, specRamp);
        SetupPipelineAsset("Assets/Settings/PC_RPAsset.asset");
        SetupPipelineAsset("Assets/Settings/Mobile_RPAsset.asset");
        SetupRendererFeature("Assets/Settings/PC_Renderer.asset");
        SetupRendererFeature("Assets/Settings/Mobile_Renderer.asset");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Godot Parity Setup] Completed. Cel shader, ramps, materials and renderer features are configured.");
    }

    static void ApplyCelShaderToGameplayMaterials(Texture2D ramp, Texture2D specRamp)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(CelShaderPath);
        if (shader == null)
        {
            Debug.LogError($"[Godot Parity Setup] Shader not found: {CelShaderPath}");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Resources/Materials" });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
                continue;

            Color baseColor = Color.white;
            Texture baseMap = null;
            if (mat.HasProperty("_BaseColor"))
                baseColor = mat.GetColor("_BaseColor");
            else if (mat.HasProperty("_Color"))
                baseColor = mat.GetColor("_Color");

            if (mat.HasProperty("_BaseMap"))
                baseMap = mat.GetTexture("_BaseMap");
            else if (mat.HasProperty("_MainTex"))
                baseMap = mat.GetTexture("_MainTex");

            mat.shader = shader;
            mat.SetColor("_BaseColor", baseColor);
            if (baseMap != null)
                mat.SetTexture("_BaseMap", baseMap);

            mat.SetTexture("_CelRamp", ramp);
            mat.SetTexture("_CelSpecularRamp", specRamp);
            mat.SetFloat("_LightWrap", 0.3f);
            mat.SetFloat("_Steepness", 1.0f);
            mat.SetFloat("_ShadowStrength", 1.0f);
            mat.SetFloat("_PointLightAttenuationCurve", 1.0f);
            mat.SetFloat("_SpecularShininess", 32f);
            mat.SetFloat("_SpecularStrength", path.Contains("Portal") ? 1f : 0f);
            mat.SetFloat("_UseDither", 0f);
            mat.SetFloat("_DitherStrength", 0.1f);
            mat.SetFloat("_DitherDirectional", 0f);

            EditorUtility.SetDirty(mat);
        }
    }

    static void SetupRendererFeature(string rendererAssetPath)
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererAssetPath);
        if (rendererData == null)
        {
            Debug.LogWarning($"[Godot Parity Setup] Renderer not found: {rendererAssetPath}");
            return;
        }

        GodotParityFeature parity = null;
        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature is GodotParityFeature f)
            {
                parity = f;
                break;
            }
        }

        if (parity == null)
        {
            parity = ScriptableObject.CreateInstance<GodotParityFeature>();
            parity.name = "GodotParityFeature";
            AssetDatabase.AddObjectToAsset(parity, rendererData);
            AddFeatureToRenderer(rendererData, parity);
        }

        parity.pixelSize = 4;
        parity.autoTexelOffsetFromCamera = true;
        parity.texelOffset = Vector2.zero;
        parity.lineTint = Color.black;
        parity.creaseTint = new Color(0.8329021f, 0.83290213f, 0.83290213f, 1f);
        parity.flipPalettes = false;
        parity.lineOverlay = true;
        parity.lineAlpha = 0.5f;
        parity.creaseOverlay = true;
        parity.creaseAlpha = 1f;
        parity.kernelRadius = 1f;
        parity.zdeltaCutoff = 0.25f;
        parity.angleZCutoff = 0.5f;
        parity.angleZScale = 2f;
        parity.convexCutoff = 0.1f;
        parity.creaseFeather = 0f;
        parity.concaveCutoff = 0.01f;
        parity.concaveZCutoff = 0.5f;

        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature == null)
                continue;

            var so = new SerializedObject(feature);
            var activeProp = so.FindProperty("m_Active");
            if (activeProp != null)
            {
                var t = feature.GetType().Name;
                if (t == nameof(GodotParityFeature))
                    activeProp.boolValue = true;
                else if (t == "PixelationFeature" || t == "ScreenSpaceAmbientOcclusion")
                    activeProp.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        EditorUtility.SetDirty(parity);
        EditorUtility.SetDirty(rendererData);
    }

    static void SetupPipelineAsset(string pipelineAssetPath)
    {
        var rpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelineAssetPath);
        if (rpAsset == null)
        {
            Debug.LogWarning($"[Godot Parity Setup] RP asset not found: {pipelineAssetPath}");
            return;
        }

        var so = new SerializedObject(rpAsset);
        var requireDepth = so.FindProperty("m_RequireDepthTexture");
        var requireOpaque = so.FindProperty("m_RequireOpaqueTexture");
        if (requireDepth != null) requireDepth.boolValue = true;
        if (requireOpaque != null) requireOpaque.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(rpAsset);
    }

    static void AddFeatureToRenderer(ScriptableRendererData rendererData, ScriptableRendererFeature feature)
    {
        var featuresField = typeof(ScriptableRendererData).GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
        var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.Instance | BindingFlags.NonPublic);

        if (featuresField != null && mapField != null)
        {
            var features = (List<ScriptableRendererFeature>)featuresField.GetValue(rendererData);
            var map = (List<long>)mapField.GetValue(rendererData);
            features.Add(feature);
            map.Add(0);

            var updateMapMethod = typeof(ScriptableRendererData).GetMethod("UpdateMap", BindingFlags.Instance | BindingFlags.NonPublic);
            updateMapMethod?.Invoke(rendererData, null);
        }
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    static Texture2D CreateRampTexture(string assetPath, RampKey[] keys, int width = 256)
    {
        var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);
            texture.SetPixel(x, 0, Evaluate(keys, t));
        }
        texture.Apply(false, false);

        string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
        {
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    static Color Evaluate(RampKey[] keys, float t)
    {
        if (keys == null || keys.Length == 0)
            return Color.white;

        if (t <= keys[0].t)
            return keys[0].color;
        if (t >= keys[keys.Length - 1].t)
            return keys[keys.Length - 1].color;

        for (int i = 0; i < keys.Length - 1; i++)
        {
            var a = keys[i];
            var b = keys[i + 1];
            if (t >= a.t && t <= b.t)
            {
                float u = Mathf.InverseLerp(a.t, b.t, t);
                return Color.Lerp(a.color, b.color, u);
            }
        }
        return keys[keys.Length - 1].color;
    }

    struct RampKey
    {
        public readonly float t;
        public readonly Color color;

        public RampKey(float t, Color color)
        {
            this.t = t;
            this.color = color;
        }
    }
}
