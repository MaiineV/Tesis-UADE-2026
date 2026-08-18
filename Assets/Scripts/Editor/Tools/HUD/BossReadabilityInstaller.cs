using Rollgeon.UI.HUD;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Editor.Tools.HUD
{
    /// <summary>
    /// Cablea en los prefabs del HUD las vistas que hacen legibles a los jefes
    /// (<c>Tools → Rollgeon → HUD → Install Boss Readability</c>). Idempotente: re-correrlo actualiza
    /// lo que ya está en vez de duplicarlo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Va en un <see cref="MenuItem"/> y no en un paso manual de escena por el mismo motivo que los
    /// builders de jefes: el wiring tiene que poder reconstruirse desde cero después de un merge, y un
    /// instructivo en un <c>.md</c> no sobrevive a que alguien se olvide de leerlo.
    /// </para>
    /// <para>
    /// <b>Qué instala hoy:</b> el label del candado de los dados — el número que el Croupier cantó,
    /// escrito sobre el dado que confiscó. Sin él, el jugador ve un candado y no tiene con qué atarlo
    /// al sector encendido del paño.
    /// </para>
    /// </remarks>
    public static class BossReadabilityInstaller
    {
        private const string LogPrefix = "[BossReadabilityInstaller] ";

        public const string DiceSlotPrefabPath = "Assets/Prefabs/UI/DiceSlotView.prefab";

        public const string BossBarPrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_BossBar.prefab";

        /// <summary>Hijo de <c>Canvas_BossBar</c> que se prende al entrar a un jefe.</summary>
        public const string BossBarRootName = "Root";

        /// <summary>Altura de la línea del escalón, en píxeles de UI.</summary>
        public const float TierLineHeight = 24f;

        /// <summary>Cuánto cuelga la línea por debajo de la barra.</summary>
        public const float TierLineDrop = 6f;

        /// <summary>La pixel font del HUD — la misma que usa el número de la ruleta.</summary>
        public const string FontPath = "Assets/Fonts/m6x11plus SDF.asset";

        /// <summary>
        /// Latón del Croupier. Mismo matiz que el número del hub de la ruleta y que el bloque del
        /// paño: los tres son el mismo dato y tienen que verse como el mismo dato.
        /// </summary>
        public static readonly Color LabelColor = new Color(0.980f, 0.855f, 0.529f);

        /// <summary>Lado del label, en píxeles de UI. Entra dentro del candado sin taparlo entero.</summary>
        public const float LabelSize = 22f;

        [MenuItem("Tools/Rollgeon/HUD/Install Boss Readability")]
        public static void Install()
        {
            int wired = 0;
            if (InstallDiceLockLabel()) wired++;
            if (InstallCashierTierReadout()) wired++;

            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + $"Listo — {wired}/2 vistas cableadas: el candado del dado puede " +
                      "decir qué número se lo llevó, y la barra del Cajero en qué escalón está.");
        }

        /// <summary>
        /// Agrega el label del candado al <c>DiceSlotView</c> y lo cablea.
        /// </summary>
        /// <remarks>
        /// <b>El label es hermano del candado, no hijo.</b> El ícono viene de un prefab anidado, y
        /// meterle un hijo lo convertiría en un override de ese prefab: cualquier re-import del
        /// paquete de íconos se lo llevaría puesto. Como hermano, el label es del slot y sobrevive.
        /// </remarks>
        private static bool InstallDiceLockLabel()
        {
            var contents = PrefabUtility.LoadPrefabContents(DiceSlotPrefabPath);
            if (contents == null)
            {
                Debug.LogError(LogPrefix + $"No se pudo abrir '{DiceSlotPrefabPath}'.");
                return false;
            }

            try
            {
                var slot = contents.GetComponent<DiceSlotView>();
                if (slot == null)
                {
                    Debug.LogError(LogPrefix + $"'{DiceSlotPrefabPath}' no tiene DiceSlotView en el root.");
                    return false;
                }

                var so = new SerializedObject(slot);
                var lockIconProp = so.FindProperty("_lockIcon");
                var lockLabelProp = so.FindProperty("_lockLabel");
                if (lockLabelProp == null)
                {
                    Debug.LogError(LogPrefix + "DiceSlotView no expone '_lockLabel' — ¿se renombró?");
                    return false;
                }

                var label = FindOrCreateLabel(contents);
                StyleLabel(label);
                PlaceOverLock(label, lockIconProp?.objectReferenceValue as GameObject);

                lockLabelProp.objectReferenceValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, DiceSlotPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Cuelga la línea del escalón bajo la barra del jefe y le cablea su vista.
        /// </summary>
        /// <remarks>
        /// <b>Va bajo <c>Root</c> y no bajo el canvas.</b> <c>Root</c> es lo que <c>BossBarView</c>
        /// prende al entrar a un jefe y apaga al terminar: colgando la línea de ahí, hereda ese
        /// ciclo de vida gratis y no puede quedar flotando en una sala sin jefe. Su propio
        /// encendido/apagado (sólo con el Cajero) lo maneja <see cref="CashierTierReadoutView"/>.
        /// </remarks>
        private static bool InstallCashierTierReadout()
        {
            var contents = PrefabUtility.LoadPrefabContents(BossBarPrefabPath);
            if (contents == null)
            {
                Debug.LogError(LogPrefix + $"No se pudo abrir '{BossBarPrefabPath}'.");
                return false;
            }

            try
            {
                var barRoot = contents.transform.Find(BossBarRootName);
                if (barRoot == null)
                {
                    Debug.LogError(LogPrefix + $"'{BossBarPrefabPath}' no tiene un hijo " +
                                   $"'{BossBarRootName}' — ¿se renombró? La línea del escalón " +
                                   "quedaría fuera del ciclo de vida de la barra.");
                    return false;
                }

                var view = barRoot.GetComponent<CashierTierReadoutView>();
                if (view == null) view = barRoot.gameObject.AddComponent<CashierTierReadoutView>();

                var label = FindOrCreateChildLabel(barRoot, CashierTierReadoutView.LabelChildName);
                StyleTierLabel(label, barRoot as RectTransform);

                var so = new SerializedObject(view);
                var labelProp = so.FindProperty("_label");
                if (labelProp != null)
                {
                    labelProp.objectReferenceValue = label;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    Debug.LogWarning(LogPrefix + "CashierTierReadoutView no expone '_label' — " +
                                     "¿se renombró el campo? Queda el fallback por nombre de hijo.");
                }

                PrefabUtility.SaveAsPrefabAsset(contents, BossBarPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>Estira la línea a lo ancho de la barra y la deja colgando por debajo.</summary>
        private static void StyleTierLabel(TextMeshProUGUI label, RectTransform barRoot)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;

            label.color = LabelColor;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = string.Empty;
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 20f;

            var rect = label.rectTransform;
            // Anclada al borde INFERIOR de la barra y estirada en X: así sigue el ancho de la barra
            // si arte la re-encuadra, en vez de quedar con un ancho cableado que no coincide.
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -(TierLineHeight + TierLineDrop));
            rect.offsetMax = new Vector2(0f, -TierLineDrop);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            label.gameObject.SetActive(false);
        }

        private static TextMeshProUGUI FindOrCreateChildLabel(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                var found = existing.GetComponent<TextMeshProUGUI>();
                if (found != null) return found;
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(childName, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return go.AddComponent<TextMeshProUGUI>();
        }

        private static TextMeshProUGUI FindOrCreateLabel(GameObject root)
        {
            var existing = root.transform.Find(DiceSlotView.LockLabelChildName);
            if (existing != null)
            {
                var found = existing.GetComponent<TextMeshProUGUI>();
                if (found != null) return found;

                // Un hijo con ese nombre pero sin TMP es basura de un build viejo: se reemplaza en
                // vez de dejar dos objetos peleando por el mismo nombre.
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(DiceSlotView.LockLabelChildName, typeof(RectTransform));
            go.transform.SetParent(root.transform, worldPositionStays: false);
            return go.AddComponent<TextMeshProUGUI>();
        }

        private static void StyleLabel(TextMeshProUGUI label)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font != null) label.font = font;
            else Debug.LogWarning(LogPrefix + $"No está la fuente '{FontPath}' — el label del candado " +
                                  "sale con la default de TMP.");

            label.color = LabelColor;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.text = string.Empty;

            label.enableAutoSizing = true;
            label.fontSizeMin = 6f;
            label.fontSizeMax = 40f;

            // Arranca apagado: sólo lo enciende SetBlocked cuando hay etiqueta que mostrar.
            label.gameObject.SetActive(false);
        }

        /// <summary>
        /// Deja el label centrado sobre el candado. Sin ícono cableado cae al centro del slot, que
        /// sigue siendo legible — el candado y el dado comparten centro.
        /// </summary>
        private static void PlaceOverLock(TextMeshProUGUI label, GameObject lockIcon)
        {
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(LabelSize, LabelSize);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            var lockRect = lockIcon != null ? lockIcon.transform as RectTransform : null;
            rect.anchoredPosition = lockRect != null ? lockRect.anchoredPosition : Vector2.zero;

            // Último hijo: el candado se dibuja antes y el número queda encima.
            rect.SetAsLastSibling();
        }
    }
}
