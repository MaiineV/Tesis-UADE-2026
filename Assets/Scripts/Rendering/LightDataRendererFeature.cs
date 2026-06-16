using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Renderer Feature that uploads per-light cel-shading data every frame.
///
/// For each additional light (Point / Spot) that has an AdditionalLightData
/// component, it reads preQuantizeIntensity, falloff and falloffSteps and
/// packs them into _RollgeonLightData[lightIndex] so shaders can read them
/// inside LIGHT_LOOP_BEGIN/END by their lightIndex.
///
/// Lights without the component use default values (boost=1.5, falloff=0, steps=1).
///
/// Setup:
///   1. Open the URP Renderer asset used by your camera.
///   2. Click Add Renderer Feature and choose "Light Data Renderer Feature".
///   3. Attach AdditionalLightData to any Point/Spot light in the scene.
/// </summary>
public class LightDataRendererFeature : ScriptableRendererFeature
{
    // Must match the array size declared in all Rollgeon HLSL shaders.
    const int k_MaxAdditionalLights = 128;

    static readonly int     k_LightDataID = Shader.PropertyToID("_RollgeonLightData");
    static readonly Vector4[] k_DataArray = new Vector4[k_MaxAdditionalLights];
    static readonly Vector4 k_DefaultData = new Vector4(1.5f, 0f, 1f, 0f);

    public override void Create() { }

    /// <summary>
    /// Called every frame before rendering.
    /// Collects AdditionalLightData from every visible light and uploads
    /// the packed array to _RollgeonLightData so shaders can index it.
    /// </summary>
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var visibleLights  = renderingData.lightData.visibleLights;
        int mainLightIndex = renderingData.lightData.mainLightIndex;

        // Fill with defaults (covers lights without the component).
        for (int i = 0; i < k_MaxAdditionalLights; i++)
            k_DataArray[i] = k_DefaultData;

        int additionalIdx = 0;
        for (int i = 0; i < visibleLights.Length && additionalIdx < k_MaxAdditionalLights; i++)
        {
            if (i == mainLightIndex)
                continue;

            var unityLight = visibleLights[i].light;
            if (unityLight != null)
            {
                var data = unityLight.GetComponent<AdditionalLightData>();
                if (data != null)
                {
                    k_DataArray[additionalIdx] = new Vector4(
                        data.preQuantizeIntensity,
                        data.falloff,
                        data.falloffSteps,
                        0f);
                }
            }

            additionalIdx++;
        }

        Shader.SetGlobalVectorArray(k_LightDataID, k_DataArray);
    }
}
