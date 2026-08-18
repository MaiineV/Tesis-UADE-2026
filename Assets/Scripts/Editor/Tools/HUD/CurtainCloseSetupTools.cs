using System.Collections.Generic;
using System.Linq;
using Rollgeon.UI;
using Rollgeon.UI.Screens;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Wiring del telón de cierre (<see cref="CurtainCloseTransition"/>) en las
    /// pantallas de victoria y derrota de la escena de gameplay abierta. Las hojas
    /// del DefeatScreen las autoró el usuario a mano ("Courtain" x2, en posición
    /// cerrada); este installer las clasifica por lado, las deja inactivas (solo
    /// existen para la transición) y clona el mismo setup en el VictoryScreen si
    /// aún no lo tiene. Idempotente — reejecutar converge sin duplicar.
    /// </summary>
    public static class CurtainCloseSetupTools
    {
        private const string LeftName = "CourtainLeft";
        private const string RightName = "CourtainRight";

        [MenuItem("Rollgeon/Victory Defeat/Wire Curtain Close")]
        public static void WireCurtainClose()
        {
            var defeat = FindScreen<DefeatScreen>();
            var victory = FindScreen<VictoryScreen>();
            if (defeat == null || victory == null)
            {
                Debug.LogError("[CurtainCloseSetup] DefeatScreen/VictoryScreen no encontrados — " +
                               "abrir 02_Gameplay antes de correr el wiring.");
                return;
            }

            var (defeatLeft, defeatRight) = WireScreen(defeat, sourceLeft: null, sourceRight: null);
            if (defeatLeft == null || defeatRight == null) return;

            WireScreen(victory, defeatLeft, defeatRight);

            EditorSceneManager.MarkSceneDirty(defeat.gameObject.scene);
            Debug.Log("[CurtainCloseSetup] Telón de cierre cableado en Defeat y Victory.");
        }

        /// <summary>
        /// Deja la screen con sus dos hojas (clonándolas de <paramref name="sourceLeft"/>/
        /// <paramref name="sourceRight"/> si no tiene ninguna), el componente
        /// <see cref="CurtainCloseTransition"/> y la ref <c>_curtainClose</c> cableada.
        /// </summary>
        private static (RectTransform left, RectTransform right) WireScreen(
            BaseScreen screen, RectTransform sourceLeft, RectTransform sourceRight)
        {
            var screenRect = (RectTransform)screen.transform;

            var curtains = new List<RectTransform>();
            foreach (Transform child in screenRect)
            {
                if (child.name.StartsWith("Courtain"))
                    curtains.Add((RectTransform)child);
            }

            RectTransform left;
            RectTransform right;
            if (curtains.Count == 0 && sourceLeft != null && sourceRight != null)
            {
                // Instantiate con parent conserva los valores locales: la hoja clonada
                // queda en la misma posición cerrada que autoró el usuario en Defeat.
                left = (RectTransform)Object.Instantiate(sourceLeft.gameObject, screenRect).transform;
                right = (RectTransform)Object.Instantiate(sourceRight.gameObject, screenRect).transform;
            }
            else if (curtains.Count == 2)
            {
                var ordered = curtains.OrderBy(c => c.anchoredPosition.x).ToList();
                left = ordered[0];
                right = ordered[1];
            }
            else
            {
                Debug.LogError($"[CurtainCloseSetup] {screen.name}: se esperaban 2 hojas 'Courtain*' " +
                               $"(o ninguna para clonar) y hay {curtains.Count} — resolver a mano.");
                return (null, null);
            }

            SetUpCurtain(left, LeftName);
            SetUpCurtain(right, RightName);

            if (!screen.TryGetComponent<CurtainCloseTransition>(out var transition))
                transition = screen.gameObject.AddComponent<CurtainCloseTransition>();
            var so = new SerializedObject(transition);
            so.FindProperty("_curtainLeft").objectReferenceValue = left;
            so.FindProperty("_curtainRight").objectReferenceValue = right;
            so.ApplyModifiedPropertiesWithoutUndo();

            var screenSo = new SerializedObject(screen);
            var curtainCloseProp = screenSo.FindProperty("_curtainClose");
            if (curtainCloseProp == null)
            {
                Debug.LogError($"[CurtainCloseSetup] {screen.name}: no expone _curtainClose.");
                return (null, null);
            }
            curtainCloseProp.objectReferenceValue = transition;
            screenSo.ApplyModifiedPropertiesWithoutUndo();

            return (left, right);
        }

        private static void SetUpCurtain(RectTransform curtain, string name)
        {
            curtain.name = name;
            // Al final de la jerarquía: mientras se cierra tapa título y botón.
            curtain.SetAsLastSibling();
            // Inactiva hasta que la transición la dispare — autorada en posición
            // cerrada, activa taparía la pantalla entera.
            curtain.gameObject.SetActive(false);
            if (curtain.TryGetComponent<Image>(out var image))
                image.raycastTarget = true;
        }

        private static T FindScreen<T>() where T : BaseScreen
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var screen = root.GetComponentInChildren<T>(true);
                    if (screen != null) return screen;
                }
            }
            return null;
        }
    }
}
