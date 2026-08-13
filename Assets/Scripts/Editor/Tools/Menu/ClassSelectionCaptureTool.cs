using Patterns;
using Rollgeon.UI;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Menu
{
    /// <summary>
    /// Herramienta de iteración visual: en play mode pushea la pantalla de
    /// selección de clase y captura el Game View a
    /// <c>class_selection_capture.png</c> (root del proyecto, gitignoreado).
    /// </summary>
    public static class ClassSelectionCaptureTool
    {
        private const string OutputPath = "class_selection_capture.png";

        [MenuItem("Rollgeon/Class Selection/Debug - Push & Capture")]
        public static void PushAndCapture()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ClassSelectionCapture] Solo funciona en play mode (menú principal cargado).");
                return;
            }

            if (!ServiceLocator.TryGetService<IScreenManager>(out var screens) || screens == null)
            {
                Debug.LogWarning("[ClassSelectionCapture] IScreenManager no registrado.");
                return;
            }

            screens.PushByStringId("ClassSelectionScreen");

            // Dos delayCalls: dejar que el push active la screen y el layout corra
            // al menos un frame antes de capturar.
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                {
                    ScreenCapture.CaptureScreenshot(OutputPath, 1);
                    Debug.Log("[ClassSelectionCapture] Captura pedida → " + OutputPath);
                };
        }
    }
}
