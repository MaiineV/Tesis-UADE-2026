using Rollgeon.UI.HUD;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// BUG-082: en el HUD de ActionRoll (Forzar Puerta / Heal), <c>ThresholdLabel</c>
    /// ("Necesitas &gt;= 25") y <c>FormulaLabel</c> eran hermanos stretch full-rect en la
    /// misma caja de 700×74, desplazados apenas 33px — con fuente 30 y alineación Middle
    /// los glifos se montaban. Además <c>DamageFormulaView._thresholdLabel</c> estaba sin
    /// cablear (<c>{fileID: 0}</c>) y sobrevivía solo por el auto-resolve por nombre del
    /// Awake.
    ///
    /// <c>Tools → Rollgeon → HUD → Fix ActionRoll Formula Layout (BUG-082)</c> reparte la
    /// caja en dos filas disjuntas (threshold arriba, fórmula abajo) y cablea el campo.
    /// Idempotente: valores absolutos, correrlo dos veces deja el prefab igual.
    /// </summary>
    public static class ActionRollFormulaLayoutInstaller
    {
        private const string LogPrefix = "[ActionRollFormulaLayoutInstaller] ";
        public const string PrefabPath = "Assets/Prefabs/UI/Canvas/Canvas_ActionRoll.prefab";

        // Fila superior (threshold) ocupa 48%..100% del alto; la inferior (fórmula)
        // 0%..48%. El gap de 4% evita que los glifos se toquen con auto-size al máximo.
        private const float RowSplitLow = 0.48f;
        private const float RowSplitHigh = 0.52f;

        [MenuItem("Tools/Rollgeon/HUD/Fix ActionRoll Formula Layout (BUG-082)")]
        public static void Install()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (!Apply(root))
                {
                    Debug.LogError(LogPrefix + "No se pudo aplicar el layout — prefab sin cambios.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log(LogPrefix + "Layout de ThresholdLabel/FormulaLabel reparado y " +
                          "_thresholdLabel cableado en " + PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Aplica el fix sobre una instancia cargada del prefab. Separado del MenuItem
        /// para que el wiring test lo ejercite sin tocar el asset real.
        /// </summary>
        public static bool Apply(GameObject root)
        {
            if (root == null) return false;

            var view = root.GetComponentInChildren<DamageFormulaView>(includeInactive: true);
            if (view == null)
            {
                Debug.LogError(LogPrefix + "DamageFormulaView no encontrado en el prefab.");
                return false;
            }

            var threshold = FindChildTmp(view.transform, "ThresholdLabel");
            var formula = FindChildTmp(view.transform, "FormulaLabel");
            if (threshold == null || formula == null)
            {
                Debug.LogError(LogPrefix + "Hijos ThresholdLabel/FormulaLabel no encontrados " +
                               "bajo DamageFormulaView.");
                return false;
            }

            // 1) Cablear el campo serializado (hoy {fileID: 0} — funcionaba solo por el
            //    auto-resolve por nombre en Awake, que un rename rompería en silencio).
            var so = new SerializedObject(view);
            var prop = so.FindProperty("_thresholdLabel");
            if (prop == null)
            {
                Debug.LogError(LogPrefix + "DamageFormulaView no expone '_thresholdLabel' — ¿cambió el campo?");
                return false;
            }
            prop.objectReferenceValue = threshold;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 2) Dos filas disjuntas dentro de la caja del view (700×74).
            SetRowAnchors((RectTransform)threshold.transform, yMin: RowSplitHigh, yMax: 1f);
            SetRowAnchors((RectTransform)formula.transform, yMin: 0f, yMax: RowSplitLow);

            // 3) La fila del threshold quedó de ~35px: auto-size para que "Necesitas >= 25"
            //    entre sin desbordar, con el tamaño de autoría como techo.
            threshold.enableAutoSizing = true;
            threshold.fontSizeMax = 30f;
            threshold.fontSizeMin = 18f;

            return true;
        }

        private static void SetRowAnchors(RectTransform rect, float yMin, float yMax)
        {
            rect.anchorMin = new Vector2(0f, yMin);
            rect.anchorMax = new Vector2(1f, yMax);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static TextMeshProUGUI FindChildTmp(Transform parent, string childName)
        {
            var t = parent.Find(childName);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }
    }
}
