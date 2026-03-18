using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class PixelationFeatureSetup
{
    [MenuItem("Tools/Setup Pixelation Feature")]
    public static void TryAddPixelationFeature()
    {
        var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>("Assets/Settings/PC_Renderer.asset");
        if (rendererData == null)
        {
            Debug.LogError("[Pixelation Setup] Could not find PC_Renderer.asset");
            return;
        }

        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature != null && feature.GetType() == typeof(PixelationFeature))
            {
                Debug.Log("[Pixelation Setup] Already added to PC_Renderer.");
                return;
            }
        }

        var pixelationFeature = ScriptableObject.CreateInstance<PixelationFeature>();
        pixelationFeature.name = "PixelationFeature";
        pixelationFeature.pixelSize = 6;
        pixelationFeature.normalEdgeStrength = 0.3f;
        pixelationFeature.depthEdgeStrength = 0.4f;

        AssetDatabase.AddObjectToAsset(pixelationFeature, rendererData);

        var featuresField = typeof(ScriptableRendererData).GetField("m_RendererFeatures", BindingFlags.Instance | BindingFlags.NonPublic);
        var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.Instance | BindingFlags.NonPublic);

        if (featuresField != null && mapField != null)
        {
            var features = (System.Collections.Generic.List<ScriptableRendererFeature>)featuresField.GetValue(rendererData);
            var map = (System.Collections.Generic.List<long>)mapField.GetValue(rendererData);
            features.Add(pixelationFeature);
            map.Add(0);

            var updateMapMethod = typeof(ScriptableRendererData).GetMethod("UpdateMap", BindingFlags.Instance | BindingFlags.NonPublic);
            updateMapMethod?.Invoke(rendererData, null);
        }

        EditorUtility.SetDirty(rendererData);
        EditorUtility.SetDirty(pixelationFeature);
        AssetDatabase.SaveAssets();

        Debug.Log("[Pixelation Setup] PixelationFeature successfully added to PC_Renderer!");
    }
}
