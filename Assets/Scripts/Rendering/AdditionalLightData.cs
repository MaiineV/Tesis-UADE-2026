using UnityEngine;

/// <summary>
/// Per-light cel-shading controls for Rollgeon shaders.
/// Attach to any Point or Spot light to override how it quantizes
/// on cel-shaded materials. Lights WITHOUT this component use the
/// default values set in LightDataRendererFeature.
/// </summary>
[RequireComponent(typeof(Light))]
[DisallowMultipleComponent]
public class AdditionalLightData : MonoBehaviour
{
    [Tooltip("Multiplies the raw light contribution BEFORE quantization.\n" +
             "Values > 1 push the light into brighter cel bands (mid/light).\n" +
             "Default 1.5 matches typical cel-shading needs.")]
    [Range(0f, 20f)]
    public float preQuantizeIntensity = 1.5f;

    [Tooltip("Steepness of the falloff curve applied after pre-quantize.\n" +
             "0 = linear (default), 1 = sharp cubic drop-off at edges.")]
    [Range(0f, 1f)]
    public float falloff = 0f;

    [Tooltip("Number of discrete brightness steps.\n" +
             "1 = smooth gradient, 2-16 = pixel art bands/rings.")]
    [Range(1f, 16f)]
    public float falloffSteps = 1f;
}
