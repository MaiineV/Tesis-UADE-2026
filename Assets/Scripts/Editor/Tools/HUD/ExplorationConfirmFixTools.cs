using System.Linq;
using Rollgeon.UI.HUD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Fix del confirm de exploración (curarse con poción fuera de combate):
    /// el commit que absorbió el Confirm en el botón contextual de turno también
    /// desactivó por override de escena el GO "ConfirmButton" exploración-only de
    /// Canvas_ActionRoll — pero fuera de combate el botón contextual no aplica,
    /// así que el ActionRoll de Heal quedaba sin forma de confirmarse.
    /// Este tool re-activa el GO en 02_Gameplay y guarda la escena.
    /// </summary>
    public static class ExplorationConfirmFixTools
    {
        private const string ScenePath = "Assets/Scenes/02_Gameplay.unity";

        [MenuItem("Rollgeon/HUD/Fix Exploration Confirm Button")]
        public static void ReactivateExplorationConfirm()
        {
            var scene = EnsureSceneOpen();
            if (!scene.IsValid())
            {
                Debug.LogError("[ExplorationConfirmFix] No se pudo abrir " + ScenePath);
                return;
            }

            int fixedCount = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                // includeInactive: el GO a arreglar está justamente desactivado.
                foreach (var gate in root.GetComponentsInChildren<ActionRollConfirmGate>(true))
                {
                    if (gate.gameObject.activeSelf) continue;
                    Undo.RecordObject(gate.gameObject, "Reactivate Exploration Confirm");
                    gate.gameObject.SetActive(true);
                    EditorUtility.SetDirty(gate.gameObject);
                    fixedCount++;
                    Debug.Log("[ExplorationConfirmFix] Reactivado: "
                              + GetPath(gate.transform), gate.gameObject);
                }
            }

            if (fixedCount == 0)
            {
                Debug.Log("[ExplorationConfirmFix] Nada que arreglar — el ConfirmButton ya está activo.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ExplorationConfirmFix] Listo — {fixedCount} GO(s) reactivados y escena guardada.");
        }

        private const string ActionRollPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_ActionRoll.prefab";
        private const string ConfirmSheetPath = "Assets/Art/UI/RollConfirm/Confirm2.png";

        /// <summary>
        /// Restylea el ConfirmButton de exploración como clon del botón contextual
        /// de combate en modo Confirm: mismo rect en pantalla (esquina inferior
        /// derecha, anchors (1,0), pos (-50,50), 200x100), mismo arte Confirm2 vía
        /// <see cref="HudButtonSpriteSwap"/> y texto/subrayado/fondo del estilo
        /// viejo apagados (el sprite ya trae la palabra). La lógica queda intacta:
        /// ActionRollConfirmGate + ActionRollExplorationVisibility.
        /// </summary>
        [MenuItem("Rollgeon/HUD/Restyle Exploration Confirm (Combat Look)")]
        public static void RestyleExplorationConfirm()
        {
            var normal = LoadSprite(ConfirmSheetPath, "Confirm2_1");
            var hover = LoadSprite(ConfirmSheetPath, "Confirm2_0");
            if (normal == null || hover == null)
            {
                Debug.LogError("[ExplorationConfirmFix] Slices Confirm2_1/Confirm2_0 no encontrados en "
                               + ConfirmSheetPath + " — abortando.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(ActionRollPrefabPath);
            try
            {
                var gate = root.GetComponentInChildren<ActionRollConfirmGate>(true);
                if (gate == null)
                {
                    Debug.LogError("[ExplorationConfirmFix] ActionRollConfirmGate no encontrado en "
                                   + ActionRollPrefabPath + " — abortando.");
                    return;
                }

                var go = gate.gameObject;
                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-50f, 50f);
                rect.sizeDelta = new Vector2(200f, 100f);
                rect.localScale = Vector3.one;

                var image = go.GetComponent<Image>();
                image.sprite = normal;
                image.color = Color.white;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                image.raycastTarget = true;

                // ColorTint (no None): el atenuado de disabled lo pone el tint —
                // el set Confirm2 no trae arte propio de disabled.
                var button = go.GetComponent<Button>();
                button.transition = Selectable.Transition.ColorTint;
                button.targetGraphic = image;

                var swap = go.GetComponent<HudButtonSpriteSwap>();
                if (swap == null) swap = go.AddComponent<HudButtonSpriteSwap>();
                var so = new SerializedObject(swap);
                so.FindProperty("_button").objectReferenceValue = button;
                so.FindProperty("_image").objectReferenceValue = image;
                so.FindProperty("_initialSet._normal").objectReferenceValue = normal;
                so.FindProperty("_initialSet._hover").objectReferenceValue = hover;
                so.FindProperty("_initialSet._disabled").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();

                SetChildActive(rect, "Text", false);
                SetChildActive(rect, "Underline", false);
                SetChildActive(rect, "Background", false);

                PrefabUtility.SaveAsPrefabAsset(root, ActionRollPrefabPath);
                Debug.Log("[ExplorationConfirmFix] ConfirmButton restyleado como el botón de combate "
                          + "(rect -50,50 / 200x100, sprites Confirm2).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static Sprite LoadSprite(string sheetPath, string sliceName)
        {
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == sliceName);
        }

        private static void SetChildActive(Transform parent, string childName, bool active)
        {
            var child = parent.Find(childName);
            if (child != null) child.gameObject.SetActive(active);
        }

        private static UnityEngine.SceneManagement.Scene EnsureSceneOpen()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var open = EditorSceneManager.GetSceneAt(i);
                if (open.path == ScenePath && open.isLoaded) return open;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return default;
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
