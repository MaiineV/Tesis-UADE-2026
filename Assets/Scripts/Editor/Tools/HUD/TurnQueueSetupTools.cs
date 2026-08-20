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

        private const string CombatHudPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_CombatHUD.prefab";

        /// <summary>
        /// Setup del carrusel de turnos (activo a la izquierda + próximos):
        /// deshabilita el HorizontalLayoutGroup del container (el layout pasa a
        /// ser manual por código), agrega el CanvasGroup al TurnSlot.prefab y
        /// remueve el "PastOverlay" de la iteración anterior (los que ya
        /// actuaron ya no se muestran). Idempotente.
        /// </summary>
        [MenuItem("Rollgeon/Turn Queue/Setup Carousel")]
        public static void SetupCarousel()
        {
            SetupCarouselSlotPrefab();
            SetupCarouselContainer();
        }

        private static void SetupCarouselSlotPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(SlotPrefabPath);
            try
            {
                var slot = root.GetComponentInChildren<TurnSlotView>(true);
                if (slot == null)
                {
                    Debug.LogError("[TurnQueueSetup] TurnSlotView no encontrado en el prefab.");
                    return;
                }

                var slotGo = slot.gameObject;
                if (slotGo.GetComponent<CanvasGroup>() == null)
                {
                    slotGo.AddComponent<CanvasGroup>();
                }

                // Limpieza de la iteración anterior del carrusel (activo al centro
                // con pasados a la izquierda): la capa gris ya no se usa.
                var slotRect = (RectTransform)slot.transform;
                var overlayRect = slotRect.Find("PastOverlay");
                if (overlayRect != null)
                {
                    Object.DestroyImmediate(overlayRect.gameObject);
                }

                PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
                Debug.Log("[TurnQueueSetup] TurnSlot.prefab: CanvasGroup ok, PastOverlay removido.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetupCarouselContainer()
        {
            var root = PrefabUtility.LoadPrefabContents(CombatHudPrefabPath);
            try
            {
                var view = root.GetComponentInChildren<TurnQueueView>(true);
                if (view == null)
                {
                    Debug.LogError("[TurnQueueSetup] TurnQueueView no encontrado en " + CombatHudPrefabPath);
                    return;
                }

                // Deshabilitado (no removido) como rollback barato al layout viejo.
                var layout = view.GetComponent<HorizontalLayoutGroup>();
                if (layout != null && layout.enabled)
                {
                    layout.enabled = false;
                }

                // Tamaño del área de turnos + spacing/escala tuneados en playtest
                // (15/08): el 400x100 original quedaba corto para la ventana de 5,
                // los portraits muy pegados y el activo a 1.25 muy grande.
                var rect = (RectTransform)view.transform;
                rect.sizeDelta = new Vector2(450f, 120f);

                // Centrado vertical con el marco del personaje (playtest 19/08): el
                // centro visual de los slots cae en containerY - 60; el centro del
                // CharacterFrame del PlayerStatus está en y = -88 canvas-space
                // (cluster -24 + frame 128/2) → y = -28 alinea ambos ejes.
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-50f, -28f);

                var so = new SerializedObject(view);
                so.FindProperty("_carousel.Spacing").floatValue = 24f;
                so.FindProperty("_carousel.ActiveScale").floatValue = 0.7f;
                so.FindProperty("_carousel.UpcomingScale").floatValue = 0.6f;
                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(root, CombatHudPrefabPath);
                Debug.Log("[TurnQueueSetup] Canvas_CombatHUD: layout manual, área 450x120, spacing 24, " +
                          "escalas 0.7/0.6, y=-28 (centrado con el marco del personaje).");
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
