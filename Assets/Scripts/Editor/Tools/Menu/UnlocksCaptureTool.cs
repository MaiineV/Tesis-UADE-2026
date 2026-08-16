using Patterns;
using Rollgeon.UI;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Menu
{
    /// <summary>
    /// Herramienta de iteración visual: en play mode pushea la pantalla de
    /// desbloqueos y captura el Game View a <c>unlocks_capture.png</c>
    /// (root del proyecto, gitignoreado).
    /// </summary>
    public static class UnlocksCaptureTool
    {
        private const string OutputPath = "unlocks_capture.png";

        [MenuItem("Rollgeon/Unlocks/Debug - Push & Capture")]
        public static void PushAndCapture()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[UnlocksCapture] Solo funciona en play mode (menú principal cargado).");
                return;
            }

            if (!ServiceLocator.TryGetService<IScreenManager>(out var screens) || screens == null)
            {
                Debug.LogWarning("[UnlocksCapture] IScreenManager no registrado.");
                return;
            }

            screens.PushByStringId("UnlocksScreen");

            // Dos delayCalls: dejar que el push active la screen y el layout corra
            // al menos un frame antes de capturar.
            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                {
                    ScreenCapture.CaptureScreenshot(OutputPath, 1);
                    Debug.Log("[UnlocksCapture] Captura pedida → " + OutputPath);
                };
        }
    }
}
