using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Items;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Item-specific slice of the list panel (item-editor-spec.md §6.1): icon+rarity rows and the
    /// filter bar (rarity / type / family / implemented effect). Row-size plumbing and the size
    /// slider itself live in <c>BlockEditorWindow.List.cs</c> — this file only decides what a row
    /// looks like and what counts as a match, through the host-hook contract that file exposes.
    /// </summary>
    public sealed partial class ItemEditorWindow
    {
        /// <summary>
        /// Row height above which <see cref="DrawRow"/> switches from a compact icon+name line to a
        /// big centered icon tile with the name wrapped underneath. Sits roughly in the middle of
        /// <c>[MIN_ROW_SIZE, MAX_ROW_SIZE]</c> (18–96) so both ends of the slider get real screen
        /// time.
        /// </summary>
        const float GRID_ROW_THRESHOLD = 48f;
        const float ROW_PADDING = 3f;
        const float GRID_NAME_HEIGHT = 28f;

        // Same blue as the shell's default row tint (BlockEditorWindow.List.cs SELECTED_ROW_TINT) —
        // kept as its own constant rather than exposing that private field, since this row no longer
        // goes through GUI.Button's built-in background tinting (custom icon+label layout needs the
        // click surface invisible), so the selection cue has to be painted by hand instead.
        static readonly Color SelectionBorderColor = new Color(0.45f, 0.75f, 1f);
        static readonly Color MissingIconColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        static readonly string[] RARITY_OPTIONS =
        {
            "All",
            RarityPalette.DisplayName(ItemRarity.Common),
            RarityPalette.DisplayName(ItemRarity.Uncommon),
            RarityPalette.DisplayName(ItemRarity.Rare),
            RarityPalette.DisplayName(ItemRarity.Legendary),
            RarityPalette.DisplayName(ItemRarity.God),
        };

        static readonly string[] TYPE_OPTIONS = { "All", nameof(ItemType.Passive), nameof(ItemType.Active) };

        // ---- filter state — UI only, never touches assets, so no Context.Mutate here -----------
        ItemRarity? _filterRarity;
        ItemType? _filterType;
        string _filterFamilyId;
        Type _filterEffectType;

        // ============================ Filter bar ============================

        protected override void DrawFilterBar()
        {
            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 46f;

            int rarityIndex = _filterRarity.HasValue ? (int)_filterRarity.Value + 1 : 0;
            rarityIndex = EditorGUILayout.Popup("Rareza", rarityIndex, RARITY_OPTIONS);
            _filterRarity = rarityIndex == 0 ? (ItemRarity?)null : (ItemRarity)(rarityIndex - 1);

            int typeIndex = _filterType.HasValue ? (int)_filterType.Value + 1 : 0;
            typeIndex = EditorGUILayout.Popup("Tipo", typeIndex, TYPE_OPTIONS);
            _filterType = typeIndex == 0 ? (ItemType?)null : (ItemType)(typeIndex - 1);

            var familyOptions = BuildFamilyOptions();
            int familyIndex = string.IsNullOrEmpty(_filterFamilyId)
                ? 0
                : Mathf.Max(0, Array.IndexOf(familyOptions, _filterFamilyId));
            familyIndex = EditorGUILayout.Popup("Familia", familyIndex, familyOptions);
            _filterFamilyId = familyIndex == 0 ? null : familyOptions[familyIndex];

            var effectTypes = CollectImplementedEffectTypes();
            var effectOptions = BuildEffectOptions(effectTypes);
            int effectIndex = _filterEffectType == null
                ? 0
                : Mathf.Max(0, effectTypes.IndexOf(_filterEffectType) + 1);
            effectIndex = EditorGUILayout.Popup("Efecto", effectIndex, effectOptions);
            _filterEffectType = effectIndex == 0 ? null : effectTypes[effectIndex - 1];

            EditorGUIUtility.labelWidth = prevLabelWidth;
        }

        protected override bool PassesFilters(ItemSO asset)
        {
            if (asset == null) return false;
            if (_filterRarity.HasValue && asset.Rarity != _filterRarity.Value) return false;
            if (_filterType.HasValue && asset.Type != _filterType.Value) return false;
            if (!string.IsNullOrEmpty(_filterFamilyId) &&
                !string.Equals(asset.FamilyId, _filterFamilyId, StringComparison.Ordinal)) return false;
            if (_filterEffectType != null && !ItemQuery.GetEffectTypes(asset).Contains(_filterEffectType)) return false;
            return true;
        }

        /// <summary>
        /// "All" + every family id present in the shell's <see cref="BlockEditorWindow{T}.Assets"/>,
        /// alphabetical.
        /// </summary>
        /// <remarks>
        /// Calls <c>ItemQuery.GetFamilies(IEnumerable&lt;ItemSO&gt;)</c> — the pure overload — against
        /// the already-cached <c>Assets</c> list rather than <c>ItemQuery.GetFamilies()</c>, which
        /// re-scans the project via <c>AssetDatabase</c>. This runs once per filter-bar repaint, and
        /// re-scanning disk on every IMGUI pass would be wasteful for no benefit: <c>Assets</c> is
        /// already kept current by the shell's own <c>OnProjectChange</c>.
        /// </remarks>
        string[] BuildFamilyOptions()
        {
            var families = ItemQuery.GetFamilies(Assets);
            var options = new string[families.Count + 1];
            options[0] = "All";
            for (int i = 0; i < families.Count; i++) options[i + 1] = families[i].FamilyId;
            return options;
        }

        /// <summary>
        /// Every concrete <see cref="Rollgeon.Effects.IEffect"/> type implemented by at least one
        /// item in <see cref="BlockEditorWindow{T}.Assets"/> — the "everything that touches gold"
        /// filter (spec §6.1). Sorted by type name so the dropdown is stable across repaints.
        /// </summary>
        List<Type> CollectImplementedEffectTypes()
        {
            var set = new HashSet<Type>();
            foreach (var asset in Assets)
                foreach (var t in ItemQuery.GetEffectTypes(asset))
                    set.Add(t);
            return set.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
        }

        static string[] BuildEffectOptions(List<Type> effectTypes)
        {
            var options = new string[effectTypes.Count + 1];
            options[0] = "All";
            for (int i = 0; i < effectTypes.Count; i++) options[i + 1] = DisplayEffectName(effectTypes[i]);
            return options;
        }

        /// <summary>Strips the shared "Eff" prefix for the dropdown label (EffModifyGold → ModifyGold) — cosmetic only, the filter still keys off the exact <see cref="Type"/>.</summary>
        static string DisplayEffectName(Type effectType) =>
            effectType.Name.StartsWith("Eff", StringComparison.Ordinal) ? effectType.Name.Substring(3) : effectType.Name;

        /// <summary>
        /// Switches the shell from a single-column list into a wrapping grid once rows are big
        /// enough to look like tiles rather than text lines. <see cref="DrawGridRow"/> already centers
        /// the icon using the rect it's handed, so this is the only change needed here — the shell
        /// (<c>BlockEditorWindow.List.cs</c>) does the actual column math and cell slicing.
        /// </summary>
        protected override bool UseGridLayout => RowSize > GRID_ROW_THRESHOLD;

        // ============================ Rows ============================

        protected override bool DrawRow(Rect rect, ItemSO asset, bool isSelected, float rowSize)
        {
            if (asset == null) return false;

            Color bg = RarityPalette.BodyColor(asset.Rarity);
            bg.a = isSelected ? 0.55f : 0.28f;
            EditorGUI.DrawRect(rect, bg);

            // Invisible click surface first, custom icon+label painted over it — GUI.Button's own
            // background tinting can't do the icon-tile layout below, but the control still has to
            // cover the whole row for the shell's "return true on click" contract to hold.
            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);

            if (rowSize > GRID_ROW_THRESHOLD)
                DrawGridRow(rect, asset, rowSize);
            else
                DrawCompactRow(rect, asset, rowSize);

            if (isSelected) DrawSelectionBorder(rect);

            return clicked;
        }

        /// <summary>Small rows: <c>[ icon | Name ]</c>, icon square and left-aligned.</summary>
        void DrawCompactRow(Rect rect, ItemSO asset, float rowSize)
        {
            float iconSize = Mathf.Max(0f, rowSize - ROW_PADDING * 2f);
            var iconRect = new Rect(rect.x + ROW_PADDING, rect.y + (rect.height - iconSize) * 0.5f, iconSize, iconSize);
            DrawIcon(iconRect, asset.Icon);

            var labelRect = new Rect(
                iconRect.xMax + 4f, rect.y,
                rect.width - iconRect.width - ROW_PADDING * 3f - 4f, rect.height);
            GUI.Label(labelRect, LabelOf(asset), CompactLabelStyle(rowSize));
        }

        /// <summary>Big rows: centered icon tile with the name wrapped underneath.</summary>
        void DrawGridRow(Rect rect, ItemSO asset, float rowSize)
        {
            var style = GridLabelStyle(rowSize);
            float nameHeight = GridNameHeight(rect.height, style);

            float iconSize = Mathf.Max(0f, Mathf.Min(rect.width - ROW_PADDING * 2f, rect.height - nameHeight));
            var iconRect = new Rect(rect.x + (rect.width - iconSize) * 0.5f, rect.y + ROW_PADDING, iconSize, iconSize);
            DrawIcon(iconRect, asset.Icon);

            var labelRect = new Rect(rect.x + 2f, iconRect.yMax + 2f, rect.width - 4f, nameHeight - 2f);
            GUI.Label(labelRect, Ellipsize(LabelOf(asset), style, labelRect.width, nameHeight - 2f), style);
        }

        /// <summary>
        /// Alto reservado para el nombre: un número <b>entero</b> de líneas que entren en la celda.
        /// </summary>
        /// <remarks>
        /// Antes era una constante de 28 px. Al escalar la fuente con el slider, dos líneas dejaron de
        /// entrar y la segunda quedaba cortada al medio — que es justo lo que se ve feo. Redondear a
        /// líneas enteras hace que el recorte, cuando ocurre, caiga entre renglones y no dentro de uno.
        /// </remarks>
        static float GridNameHeight(float cellHeight, GUIStyle style)
        {
            float lineHeight = style.lineHeight;
            // 0.45 y no menos: con un tercio de la celda solo entraba UNA línea incluso en la celda
            // más grande, y entonces casi todos los nombres se recortaban. Con esto, una celda grande
            // da dos líneas y el icono se queda con el resto.
            float budget = Mathf.Clamp(cellHeight * 0.45f, lineHeight, lineHeight * MAX_GRID_NAME_LINES);
            int lines = Mathf.Max(1, Mathf.FloorToInt(budget / lineHeight));
            return lines * lineHeight + 4f;
        }

        const int MAX_GRID_NAME_LINES = 2;

        /// <summary>
        /// Recorta con «…» lo que no entre en <paramref name="height"/>.
        /// </summary>
        /// <remarks>
        /// Sin esto, un nombre largo simplemente desaparece a mitad de palabra y el ítem queda
        /// irreconocible en la grilla. El puntito final avisa que hay más, que es información: el
        /// nombre completo sigue estando en el panel de la derecha.
        /// <para>
        /// El bucle recorta de a palabras, no de a caracteres, para no partir una en dos. Corre en el
        /// camino de dibujo pero solo mide texto — nada de disco, y a escala de catálogo (decenas de
        /// celdas visibles) es imperceptible.
        /// </para>
        /// </remarks>
        static string Ellipsize(string text, GUIStyle style, float width, float height)
        {
            if (string.IsNullOrEmpty(text) || width <= 0f) return text;

            var content = new GUIContent(text);
            if (style.CalcHeight(content, width) <= height) return text;

            int cut = text.Length;
            while (cut > 1)
            {
                int space = text.LastIndexOf(' ', Mathf.Min(cut - 1, text.Length - 1));
                cut = space > 0 ? space : cut - 1;

                content.text = text.Substring(0, cut) + "…";
                if (style.CalcHeight(content, width) <= height) return content.text;
            }
            return "…";
        }

        static void DrawSelectionBorder(Rect rect)
        {
            const float thickness = 2f;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), SelectionBorderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), SelectionBorderColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), SelectionBorderColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), SelectionBorderColor);
        }

        /// <summary>
        /// Draws one <see cref="Sprite"/> icon without going through <c>AssetPreview.GetAssetPreview</c>.
        /// Every item icon is sliced out of the same shared "UI-sheet_3" atlas texture (Sprite Mode:
        /// Multiple) — <c>AssetPreview</c>'s generated thumbnail can return the same cached image for
        /// every sprite off that sheet before/without correctly cropping per-sprite, so at list scale
        /// (dozens of rows repainting together) the list fills with identical icons. Sidestepped
        /// entirely by drawing the sprite's own pixel rect out of its source texture, normalized into
        /// UV space, via <see cref="GUI.DrawTextureWithTexCoords"/> — no preview cache involved.
        /// </summary>
        static void DrawIcon(Rect rect, Sprite icon)
        {
            if (icon == null || icon.texture == null)
            {
                EditorGUI.DrawRect(rect, MissingIconColor);
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

        // Built per-call rather than cached: caching a GUIStyle derived from EditorStyles across an
        // editor theme switch (Pro/Personal skin) can leave it stale until the next domain reload.
        // Cheap enough at catalog scale (tens of rows) that it isn't worth the risk.
        /// <summary>
        /// Tamaño de fuente para una celda de <paramref name="rowSize"/> píxeles.
        /// </summary>
        /// <remarks>
        /// Escala con el slider en vez de ser fijo: si el texto se queda en el tamaño mínimo mientras
        /// la celda crece, agrandar la lista deja de servir para leerla — que es justamente para lo
        /// que se agranda. El piso de 12 es el tamaño de <c>EditorStyles.label</c>; el techo evita
        /// que en celdas grandes el nombre le coma el lugar al icono.
        /// </remarks>
        static int LabelFontSize(float rowSize) =>
            Mathf.RoundToInt(Mathf.Lerp(12f, 15f, Mathf.InverseLerp(MIN_ROW_SIZE, MAX_ROW_SIZE, rowSize)));

        // Construidos por llamada y no cacheados: un GUIStyle derivado de EditorStyles que sobreviva
        // un cambio de skin (Pro/Personal) queda stale hasta el próximo domain reload. A escala de
        // catálogo (decenas de filas) no vale la pena el riesgo.
        static GUIStyle CompactLabelStyle(float rowSize) => new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
            fontSize = LabelFontSize(rowSize),
        };

        // Sobre EditorStyles.label y no miniLabel: mini es la fuente más chica del editor y en una
        // celda con icono el nombre quedaba casi ilegible.
        static GUIStyle GridLabelStyle(float rowSize) => new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.UpperCenter,
            wordWrap = true,
            fontSize = LabelFontSize(rowSize),
        };
    }
}
