using System;
using Patterns;
using Rollgeon.Heroes;
using Rollgeon.UI;
using Rollgeon.UI.Screens;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools.Menu
{
    /// <summary>
    /// Iteración visual de la pantalla de armado de bolsa: en play mode pushea
    /// <c>BuildSelectionScreen</c> CON payload real (CH_Warrior — sin payload la
    /// pantalla queda vacía) y captura el Game View a
    /// <c>build_selection_capture.png</c> (root, gitignoreado).
    /// </summary>
    public static class BuildSelectionCaptureTool
    {
        private const string OutputPath = "build_selection_capture.png";
        private const string WarriorPath = "Assets/Rollgeon/Classes/CH_Warrior.asset";

        [MenuItem("Rollgeon/Build Selection/Debug - Push & Capture")]
        public static void PushAndCapture()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[BuildSelectionCapture] Solo funciona en play mode (menú principal cargado).");
                return;
            }

            if (!ServiceLocator.TryGetService<IScreenManager>(out var screens) || screens == null)
            {
                Debug.LogWarning("[BuildSelectionCapture] IScreenManager no registrado.");
                return;
            }

            var warrior = AssetDatabase.LoadAssetAtPath<ClassHeroSO>(WarriorPath);
            if (warrior == null)
            {
                Debug.LogWarning("[BuildSelectionCapture] CH_Warrior no encontrado.");
                return;
            }

            screens.PushByStringId("BuildSelectionScreen", new BuildSelectionPayload
            {
                SelectedHero = warrior,
                RunId = Guid.NewGuid(),
                RulesetId = "default",
            });

            EditorApplication.delayCall += () =>
                EditorApplication.delayCall += () =>
                {
                    ScreenCapture.CaptureScreenshot(OutputPath, 1);
                    Debug.Log("[BuildSelectionCapture] Captura pedida → " + OutputPath);
                };
        }
    }
}
