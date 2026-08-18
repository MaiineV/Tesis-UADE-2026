using System.Linq;
using Rollgeon.Entities.Visuals;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Le agrega la barra de vida world-space que les falta a los prefabs de jefe viejos
    /// (<c>Tools → Rollgeon → Bosses → Add Missing Boss Healthbars</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué.</b> <c>GeneralDirector.prefab</c> tiene su canvas con
    /// <see cref="WorldSpaceHealthBar"/>; <c>SunkedGrand</c> y <c>SecurityGuardBoss</c> no. Como los
    /// jefes nuevos reusan esos dos prefabs como placeholder visual, la mitad de los encuentros
    /// quedaba sin barra sobre la cabeza. La barra de HUD (<c>Canvas_BossBar</c>) no lo tapa: es
    /// screen-space y no dice qué pawn es el jefe cuando hay adds en pantalla.
    /// </para>
    /// <para>
    /// <b>Idempotente.</b> Si el prefab ya tiene un <see cref="WorldSpaceHealthBar"/> (propio o
    /// heredado de un nested prefab) se saltea sin tocar el asset: nunca pisa una barra que un
    /// artista haya reposicionado a mano.
    /// </para>
    /// <para>
    /// La estructura (nombres, rects, slices y offsets) se clona de <c>GeneralDirector.prefab</c>
    /// para que las tres barras se vean iguales. El alto efectivo en runtime lo manda
    /// <c>WorldSpaceHealthBar._offset</c> (LateUpdate reescribe la posición local), no el
    /// <c>anchoredPosition</c> del canvas — ese solo importa para verlo en el editor.
    /// </para>
    /// </remarks>
    public static class BossHealthbarInstaller
    {
        private const string LogPrefix = "[BossHealthbarInstaller] ";

        private const string HealthbarSheetPath = "Assets/Art/UI/EnemiesHealthBar/EnemiesHealthbarv2.png";
        private const string FrameSlice = "EnemiesHealthbarv2_0";
        private const string BackgroundSlice = "EnemiesHealthbarv2_4";
        private const string FillSlice = "EnemiesHealthbarv2_5";
        private const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";

        private static readonly string[] TargetPrefabs =
        {
            "Assets/Prefabs/Enemies/SunkedGrand.prefab",
            "Assets/Prefabs/Enemies/SecurityGuardBoss.prefab",
        };

        // Layout copiado 1:1 del canvas del GeneralDirector.
        private static readonly Vector2 CanvasPos = new Vector2(0f, 2.5f);
        private static readonly Vector2 CanvasSize = new Vector2(3f, 1f);
        private static readonly Vector2 BarPos = new Vector2(0.0067f, 0.0735f);
        private static readonly Vector2 BarSize = new Vector2(2.7982f, 0.2227f);
        private static readonly Vector2 FrameSize = new Vector2(3f, 0.5f);
        private static readonly Vector2 TextSize = new Vector2(3f, 0.5f);
        private static readonly Vector3 BarOffset = new Vector3(0f, 3f, 0f);
        private const float TextFontSize = 0.5f;
        private const string TextFormat = "{0}/{1}";

        [MenuItem("Tools/Rollgeon/Bosses/Add Missing Boss Healthbars")]
        public static void AddMissingHealthbars()
        {
            var frame = LoadSliceOrError(FrameSlice);
            var background = LoadSliceOrError(BackgroundSlice);
            var fill = LoadSliceOrError(FillSlice);
            if (frame == null || background == null || fill == null) return;

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                Debug.LogWarning(LogPrefix + $"No se encontró {FontPath} — el texto de HP queda con el font default de TMP.");

            int installed = 0;
            int skipped = 0;

            foreach (var prefabPath in TargetPrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                {
                    Debug.LogWarning(LogPrefix + $"No existe '{prefabPath}' — salteado.");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    if (root.GetComponentInChildren<WorldSpaceHealthBar>(includeInactive: true) != null)
                    {
                        skipped++;
                        continue;
                    }

                    BuildHealthBar(root, frame, background, fill, font);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    installed++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (installed > 0) AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + $"{installed} barra(s) instalada(s), {skipped} prefab(s) ya tenían una.");
        }

        private static void BuildHealthBar(GameObject root, Sprite frame, Sprite background, Sprite fill,
            TMP_FontAsset font)
        {
            var canvasRect = EnsureChildRect(root.transform, "Canvas", CanvasPos, CanvasSize);
            canvasRect.anchorMin = canvasRect.anchorMax = Vector2.zero;
            canvasRect.pivot = new Vector2(0.5f, 0.5f);

            var canvas = Ensure<Canvas>(canvasRect.gameObject);
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = Ensure<CanvasScaler>(canvasRect.gameObject);
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 1f;
            Ensure<GraphicRaycaster>(canvasRect.gameObject);

            // Orden de hijos = orden de dibujado: fondo, fill, marco, texto.
            BuildBarImage(canvasRect, "LifeBackground", BarPos, BarSize, background);
            var fillImage = BuildBarImage(canvasRect, "LifeFill", BarPos, BarSize, fill);
            BuildBarImage(canvasRect, "Image", Vector2.zero, FrameSize, frame);

            var textRect = EnsureChildRect(canvasRect, "HealthText", Vector2.zero, TextSize);
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            var hpText = Ensure<TextMeshProUGUI>(textRect.gameObject);
            if (font != null) hpText.font = font;
            hpText.fontSize = TextFontSize;
            hpText.fontStyle = FontStyles.Bold;
            hpText.alignment = TextAlignmentOptions.Center;
            hpText.text = "0/0";

            var bar = Ensure<WorldSpaceHealthBar>(canvasRect.gameObject);
            var barSo = new SerializedObject(bar);
            barSo.FindProperty("_fillImage").objectReferenceValue = fillImage;
            barSo.FindProperty("_hpText").objectReferenceValue = hpText;
            barSo.FindProperty("_textFormat").stringValue = TextFormat;
            // El root que se apaga al morir es el canvas entero, igual que en el GeneralDirector.
            barSo.FindProperty("_barRoot").objectReferenceValue = canvasRect.gameObject;
            barSo.FindProperty("_offset").vector3Value = BarOffset;
            barSo.ApplyModifiedPropertiesWithoutUndo();

            // Sin este wiring el pawn nunca llama Initialize() y la barra queda en 0/0.
            var pawn = root.GetComponent<EntityPawn>();
            if (pawn == null)
            {
                Debug.LogWarning(LogPrefix + $"'{root.name}' no tiene EntityPawn — la barra queda sin bindear.");
                return;
            }
            var pawnSo = new SerializedObject(pawn);
            pawnSo.FindProperty("_healthBar").objectReferenceValue = bar;
            pawnSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Image de la barra. Type Filled (como el GeneralDirector) incluso en fondo y marco:
        /// con <c>fillAmount = 1</c> se ve igual que Simple y el fill real usa el mismo setup.
        /// </summary>
        private static Image BuildBarImage(RectTransform parent, string name, Vector2 pos, Vector2 size,
            Sprite sprite)
        {
            var rect = EnsureChildRect(parent, name, pos, size);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);

            var image = Ensure<Image>(rect.gameObject);
            image.sprite = sprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 1f;
            return image;
        }

        private static Sprite LoadSliceOrError(string spriteName)
        {
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(HealthbarSheetPath)
                .OfType<Sprite>()
                .FirstOrDefault(s => s.name == spriteName);
            if (sprite == null)
                Debug.LogError(LogPrefix + $"Slice '{spriteName}' no encontrado en {HealthbarSheetPath}.");
            return sprite;
        }

        private static RectTransform EnsureChildRect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                rect = (RectTransform)go.transform;
                rect.SetParent(parent, worldPositionStays: false);
            }
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static T Ensure<T>(GameObject go) where T : Component
            => go.TryGetComponent<T>(out var existing) ? existing : go.AddComponent<T>();
    }
}
