using System.Collections.Generic;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// La lista, agrupada por categoría del GDD con cabeceras plegables — el análogo de la vista
    /// por familia de items: acá la agrupación natural es la <see cref="EnchantmentCategory"/>,
    /// no escalones de variantes. El search y el slider de tamaño siguen siendo del shell
    /// (<c>BlockEditorWindow.List.cs</c>); este archivo solo decide el orden, las cabeceras y qué
    /// pinta cada fila.
    /// </summary>
    public sealed partial class EnchantmentEditorWindow
    {
        const float LIST_ROW_PADDING = 3f;
        const string CategoryCollapsedPrefPrefix = "Rollgeon.EnchantmentEditor.CategoryCollapsed.";

        // Mismo azul que el tinte de selección del shell (SELECTED_ROW_TINT es privado): la fila
        // dejó de pasar por el tinte de GUI.Button, así que el cue de selección se pinta a mano.
        static readonly Color ListSelectionBorderColor = new Color(0.45f, 0.75f, 1f);
        static readonly Color ListMissingIconColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        // Estado derivado de la lista, recalculado por rebuild y no por repaint (ver el remark de
        // OnAssetsRefreshed en el shell: derivar en el dibujo es un recorrido por frame).
        readonly Dictionary<EnchantmentCategory, int> _categoryCounts =
            new Dictionary<EnchantmentCategory, int>();
        readonly Dictionary<EnchantmentCategory, EnchantmentSO> _categoryHeaderBearer =
            new Dictionary<EnchantmentCategory, EnchantmentSO>();
        readonly HashSet<EnchantmentCategory> _collapsedCategories = new HashSet<EnchantmentCategory>();
        bool _collapsedPrefsLoaded;

        // Con qué categoría terminó la fila anterior, para saber cuándo abrir cabecera nueva.
        // Se resetea en DrawFilterBar, que el shell dibuja antes de las filas en cada pasada IMGUI.
        EnchantmentCategory? _lastRowCategory;

        partial void OnListAssetsRefreshed()
        {
            _categoryCounts.Clear();
            _categoryHeaderBearer.Clear();

            // La agrupación viene de EnchantmentQuery.GetByCategory (grupos en orden del enum,
            // None primero si hay sin clasificar — que se vea). De ahí salen las cuentas de las
            // cabeceras, el asset que carga la cabecera de un grupo plegado, y el orden de dibujo.
            var order = new Dictionary<EnchantmentSO, int>();
            int index = 0;
            foreach (var group in EnchantmentQuery.GetByCategory(Assets))
            {
                _categoryCounts[group.Category] = group.Enchantments.Count;
                if (group.Enchantments.Count > 0)
                    _categoryHeaderBearer[group.Category] = group.Enchantments[0];
                foreach (var ench in group.Enchantments) order[ench] = index++;
            }

            // El shell ordena por LabelOf y no expone hook de orden, pero las cabeceras exigen
            // grupos contiguos. Se reordena la misma lista en su lugar, justo después del sort del
            // shell (RefreshList → OnAssetsRefreshed): no se agrega ni saca nada, y ningún estado
            // del shell depende del orden alfabético.
            if (Assets is List<EnchantmentSO> assets)
                assets.Sort((a, b) =>
                    (order.TryGetValue(a, out int ia) ? ia : int.MaxValue)
                        .CompareTo(order.TryGetValue(b, out int ib) ? ib : int.MaxValue));
        }

        /// <summary>Nombre para UI de la categoría — el enum no lleva acentos.</summary>
        internal static string CategoryLabelOf(EnchantmentCategory category)
        {
            switch (category)
            {
                case EnchantmentCategory.None: return "Sin categoría";
                case EnchantmentCategory.Economia: return "Economía";
                case EnchantmentCategory.Maldicion: return "Maldición";
                default: return category.ToString();
            }
        }

        // ============================ Colapso por categoría ============================

        bool IsCategoryCollapsed(EnchantmentCategory category)
        {
            if (!_collapsedPrefsLoaded)
            {
                _collapsedPrefsLoaded = true;
                foreach (EnchantmentCategory value in System.Enum.GetValues(typeof(EnchantmentCategory)))
                    if (EditorPrefs.GetBool(CategoryCollapsedPrefPrefix + value, false))
                        _collapsedCategories.Add(value);
            }
            return _collapsedCategories.Contains(category);
        }

        void SetCategoryCollapsed(EnchantmentCategory category, bool collapsed)
        {
            if (collapsed) _collapsedCategories.Add(category);
            else _collapsedCategories.Remove(category);
            EditorPrefs.SetBool(CategoryCollapsedPrefPrefix + category, collapsed);
            RepaintList();
        }

        // ============================ Hooks del shell ============================

        protected override void DrawFilterBar()
        {
            // No hay filtros propios: la barra solo resetea el rastreo de categoría de la pasada.
            // Va acá porque el shell la dibuja antes de las filas en cada evento IMGUI — el único
            // punto per-pasada que el contrato de la lista expone.
            _lastRowCategory = null;
        }

        /// <summary>
        /// Con el grupo plegado solo pasa el asset que carga la cabecera: sin él, un grupo plegado
        /// desaparecería entero y no habría dónde clickear para volver a abrirlo.
        /// </summary>
        protected override bool PassesFilters(EnchantmentSO asset)
        {
            if (asset == null) return false;
            if (!IsCategoryCollapsed(asset.Category)) return true;
            return _categoryHeaderBearer.TryGetValue(asset.Category, out var bearer) && bearer == asset;
        }

        protected override bool DrawRow(Rect rect, EnchantmentSO asset, bool isSelected, float rowSize)
        {
            if (asset == null) return false;

            var category = asset.Category;
            bool firstOfCategory = _lastRowCategory != category;
            _lastRowCategory = category;

            if (firstOfCategory)
            {
                DrawCategoryHeader(rect, category);
                if (IsCategoryCollapsed(category)) return false;

                // La cabecera se comió el rect que reservó el shell; la fila reserva el suyo con
                // el mismo estilo para conservar el espaciado. Reservar acá es consistente entre
                // Layout y Repaint porque este código corre igual en ambos eventos.
                rect = GUILayoutUtility.GetRect(
                    GUIContent.none, GUI.skin.button,
                    GUILayout.Height(rowSize), GUILayout.ExpandWidth(true));
            }

            Color bg = EnchantmentPalette.CategoryColor(category);
            bg.a = isSelected ? 0.55f : 0.28f;
            EditorGUI.DrawRect(rect, bg);

            // Superficie de clic invisible primero, icono+nombre pintados encima: el layout de
            // icono no sale del tinte propio de GUI.Button, pero el control tiene que cubrir la
            // fila entera para sostener el contrato "true al clic" del shell.
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);

            float iconSize = Mathf.Max(0f, rowSize - LIST_ROW_PADDING * 2f);
            var iconRect = new Rect(
                rect.x + LIST_ROW_PADDING, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
            DrawSpriteIcon(iconRect, asset.Icon);

            var labelRect = new Rect(
                iconRect.xMax + 4f, rect.y,
                rect.width - iconRect.width - LIST_ROW_PADDING * 3f - 4f, rect.height);
            GUI.Label(labelRect, LabelOf(asset), ListLabelStyle(rowSize));

            if (isSelected) DrawListSelectionBorder(rect);

            return clicked;
        }

        // ============================ Dibujo ============================

        void DrawCategoryHeader(Rect rect, EnchantmentCategory category)
        {
            bool collapsed = IsCategoryCollapsed(category);

            Color band = EnchantmentPalette.CategoryColor(category);
            band.a = 0.85f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), band);

            var bg = new Color(band.r, band.g, band.b, 0.12f);
            EditorGUI.DrawRect(new Rect(rect.x + 4f, rect.y, rect.width - 4f, rect.height), bg);

            _categoryCounts.TryGetValue(category, out int count);
            string arrow = collapsed ? "▸" : "▾";
            var label = $"{arrow}  {CategoryLabelOf(category)}  ({count})";

            var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleLeft };
            style.normal.textColor = Color.Lerp(band, Color.white, 0.35f);
            GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 12f, rect.height), label, style);

            if (!GUI.Button(rect, GUIContent.none, GUIStyle.none)) return;

            // Diferido: plegar durante el MouseDown cambia cuántos rects reservan las filas que
            // siguen en esta misma pasada, y IMGUI revienta si Layout y Repaint no coinciden.
            var target = category;
            bool next = !collapsed;
            EditorApplication.delayCall += () => SetCategoryCollapsed(target, next);
        }

        static void DrawListSelectionBorder(Rect rect)
        {
            const float thickness = 2f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), ListSelectionBorderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), ListSelectionBorderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), ListSelectionBorderColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), ListSelectionBorderColor);
        }

        /// <summary>
        /// Dibuja un <see cref="Sprite"/> sin pasar por <c>AssetPreview.GetAssetPreview</c>: los
        /// iconos salen de atlas compartidos (Sprite Mode: Multiple) y el thumbnail cacheado puede
        /// devolver la misma imagen para todos los sprites de la hoja. Se recorta el pixel rect
        /// del sprite en UV space y se dibuja directo de la textura.
        /// </summary>
        static void DrawSpriteIcon(Rect rect, Sprite icon)
        {
            if (icon == null || icon.texture == null)
            {
                EditorGUI.DrawRect(rect, ListMissingIconColor);
                return;
            }

            var tex = icon.texture;
            var uv = new Rect(
                icon.rect.x / tex.width,
                icon.rect.y / tex.height,
                icon.rect.width / tex.width,
                icon.rect.height / tex.height);

            GUI.DrawTextureWithTexCoords(rect, tex, uv);
        }

        // Construido por llamada y no cacheado: un GUIStyle derivado de EditorStyles que sobreviva
        // un cambio de skin (Pro/Personal) queda stale hasta el próximo domain reload. A escala de
        // catálogo (decenas de filas) no vale la pena el riesgo.
        static GUIStyle ListLabelStyle(float rowSize) => new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            fontSize = Mathf.RoundToInt(
                Mathf.Lerp(12f, 15f, Mathf.InverseLerp(MIN_ROW_SIZE, MAX_ROW_SIZE, rowSize))),
        };
    }
}
