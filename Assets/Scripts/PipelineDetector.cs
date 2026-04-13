// Guardá este archivo en: Assets/Editor/PipelineDetector.cs
// Se ejecuta automáticamente al compilar. Borralo después.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public class PipelineDetector
{
    static PipelineDetector()
    {
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp == null)
            Debug.Log("PIPELINE: Built-in Render Pipeline");
        else
            Debug.Log($"PIPELINE: {rp.GetType().Name} — {rp.name}");
    }
}
