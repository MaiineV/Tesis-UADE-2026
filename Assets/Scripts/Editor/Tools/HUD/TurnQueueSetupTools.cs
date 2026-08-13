using System.Linq;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.EditorTools.HUD
{
    /// <summary>
    /// Installer de los frames del orden de turnos: agrega la Image "Frame"
    /// detrás del portrait en <c>TurnSlot.prefab</c> y cablea los 5 sprites de
    /// borde/fondo del sheet (reemplazan a los tintes de color). Idempotente.
    /// </summary>
    public static class TurnQueueSetupTools
    {
        private const string SlotPrefabPath = "Assets/Prefabs/UI/TurnSlot.prefab";
        private const string UiSheetPath = "Assets/Art/UI/UI-Sheet-sheet.png";

        [MenuItem("Rollgeon/Turn Queue/Setup Frames")]
        public static void SetupFrames()
        {
            var idle = LoadSpriteOrError("UI-Sheet-sheet_10");
            var playerActive = LoadSpriteOrError("UI-Sheet-sheet_11");
            var bossActive = LoadSpriteOrError("UI-Sheet-sheet_12");
            var enemyActive = LoadSpriteOrError("UI-Sheet-sheet_15");
            var bossIdle = LoadSpriteOrError("UI-Sheet-sheet_16");
            if (idle == null || playerActive == null || bossActive == null
                || enemyActive == null || bossIdle == null)
            {
                Debug.LogError("[TurnQueueSetup] Faltan slices en el sheet — abortando.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
            try
            {
                var slot = root.GetComponentInChildren<TurnSlotView>(true);
                if (slot == null)
                {
                    Debug.LogError("[TurnQueueSetup] TurnSlotView no encontrado en el prefab.");
                    return;
                }

                var slotRect = (RectTransform)slot.transform;
                var frameRect = slotRect.Find("Frame") as RectTransform;
                if (frameRect == null)
                {
                    var go = new GameObject("Frame", typeof(RectTransform), typeof(Image));
                    frameRect = (RectTransform)go.transform;
                    frameRect.SetParent(slotRect, worldPositionStays: false);
                }

                // Primer hermano: renderea detrás del portrait/label/overlays.
                // Layout tuneado en playtest: centrado en el slot (50,50), 90x90
                // con preserve aspect y escala 1.3.
                frameRect.SetAsFirstSibling();
                frameRect.anchorMin = frameRect.anchorMax = Vector2.zero;
                frameRect.pivot = new Vector2(0.5f, 0.5f);
                frameRect.anchoredPosition = new Vector2(50f, 50f);
                frameRect.sizeDelta = new Vector2(90f, 90f);
                frameRect.localScale = new Vector3(1.3f, 1.3f, 1.3f);

                var frameImage = frameRect.GetComponent<Image>();
                frameImage.sprite = idle;
                frameImage.preserveAspect = true;
                frameImage.raycastTarget = false;

                // El portrait arrancaba negro en el prefab y sin el tinte runtime
                // (removido con los frames) quedaba invisible — blanco siempre.
                var portraitSo = new SerializedObject(slot);
                var portrait = portraitSo.FindProperty("_portrait").objectReferenceValue as Image;
                if (portrait != null) portrait.color = Color.white;

                var so = new SerializedObject(slot);
                so.FindProperty("_frame").objectReferenceValue = frameImage;
                so.FindProperty("_frameIdle").objectReferenceValue = idle;
                so.FindProperty("_framePlayerActive").objectReferenceValue = playerActive;
                so.FindProperty("_frameEnemyActive").objectReferenceValue = enemyActive;
                so.FindProperty("_frameBossIdle").objectReferenceValue = bossIdle;
                so.FindProperty("_frameBossActive").objectReferenceValue = bossActive;
                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
                Debug.Log("[TurnQueueSetup] TurnSlot.prefab cableado: Frame + 5 sprites de borde.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Sprite LoadSpriteOrError(string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(UiSheetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
            {
                Debug.LogError($"[TurnQueueSetup] Slice '{spriteName}' no encontrado en {UiSheetPath}.");
            }
            return sprite;
        }
    }
}
