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
                DrawGridRow(rect, asset);
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
            GUI.Label(labelRect, LabelOf(asset), CompactLabelStyle);
        }

        /// <summary>Big rows: centered icon tile with the name wrapped underneath.</summary>
        void DrawGridRow(Rect rect, ItemSO asset)
        {
            float iconSize = Mathf.Max(0f, Mathf.Min(rect.width - ROW_PADDING * 2f, rect.height - GRID_NAME_HEIGHT));
            var iconRect = new Rect(rect.x + (rect.width - iconSize) * 0.5f, rect.y + ROW_PADDING, iconSize, iconSize);
            DrawIcon(iconRect, asset.Icon);

            var labelRect = new Rect(rect.x + 2f, iconRect.yMax + 2f, rect.width - 4f, GRID_NAME_HEIGHT - 2f);
            GUI.Label(labelRect, LabelOf(asset), GridLabelStyle);
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
        static GUIStyle CompactLabelStyle => new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft,
            wordWrap = false,
        };

        static GUIStyle GridLabelStyle => new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperCenter,
            wordWrap = true,
        };
    }
}
