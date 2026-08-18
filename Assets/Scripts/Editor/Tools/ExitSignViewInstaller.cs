using Rollgeon.Dungeon.Components;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.EditorTools
{
    /// <summary>
    /// Agrega <see cref="DoorExitSignView"/> al root de los prefabs de puerta y les
    /// wirea el sprite del cartel de salida (bake 2D del modelo <c>ExitSign.fbx</c>
    /// vía <see cref="ExitSignSpriteBaker"/> — el indicador es UI pero el visual es
    /// el cartel del juego, no la flecha del tutorial). Cubre <c>DoorBoss.prefab</c>
    /// Y <c>Door.prefab</c>: hay boss rooms nuevas que usan puertas normales a
    /// propósito, y <c>MarkBossExitDoor</c> designa la exit sobre el DoorController
    /// que haya. En las puertas no-exit la view es inerte (Apply solo muestra con
    /// IsExit && Open). El hijo 3D <c>ExitSign</c> del DoorBoss NO se toca: quedó
    /// inactivo cuando el cartel pasó a ser screen-space. Re-ejecutable: re-bakea
    /// y re-wirea siempre (idempotente en resultado).
    /// </summary>
    public static class ExitSignViewInstaller
    {
        private static readonly string[] DoorPrefabPaths =
        {
            "Assets/Prefabs/Tiles/DoorBoss.prefab",
            "Assets/Prefabs/Tiles/Door.prefab",
        };

        /// <summary>Alto del cartel en unidades de canvas; el ancho sale del aspect del bake.</summary>
        private const float SignHeightCanvas = 88f;

        /// <summary>Separación casilla→base del cartel (feedback: que flote más arriba del tile).</summary>
        private const float GapPx = 64f;

        [MenuItem("Rollgeon/Tools/Wire Exit Sign View (Doors)")]
        public static void Install()
        {
            var sprite = ExitSignSpriteBaker.Bake();
            if (sprite == null) return; // el baker ya logueó el error

            float aspect = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 1f;
            var size = new Vector2(Mathf.Round(SignHeightCanvas * aspect), SignHeightCanvas);

            foreach (var path in DoorPrefabPaths)
                WirePrefab(path, sprite, size);

            AssetDatabase.SaveAssets();
        }

        private static void WirePrefab(string path, Sprite sprite, Vector2 size)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError($"[ExitSignViewInstaller] No se pudo abrir '{path}'.");
                return;
            }

            try
            {
                // La view va en el MISMO GO que el DoorController: SetState la busca
                // con GetComponent sobre sí mismo.
                var controller = root.GetComponentInChildren<DoorController>(includeInactive: true);
                if (controller == null)
                {
                    Debug.LogError($"[ExitSignViewInstaller] '{path}' no tiene DoorController.");
                    return;
                }

                var view = controller.GetComponent<DoorExitSignView>();
                if (view == null) view = controller.gameObject.AddComponent<DoorExitSignView>();

                var so = new SerializedObject(view);
                so.FindProperty(DoorExitSignView.EditorArrowSpriteField).objectReferenceValue = sprite;
                so.FindProperty(DoorExitSignView.EditorArrowSizeField).vector2Value = size;
                so.FindProperty(DoorExitSignView.EditorGapPxField).floatValue = GapPx;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[ExitSignViewInstaller] DoorExitSignView wireada en '{path}' " +
                          $"(sprite bakeado, size {size.x}x{size.y}).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
