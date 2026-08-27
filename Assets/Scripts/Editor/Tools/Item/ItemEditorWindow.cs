using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Items;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Authoring window for <see cref="ItemSO"/> — closes the gap TECHNICAL.md §26.1 promised
    /// ("un diseñador crea … un item … sin escribir una línea de C#") and §26.2-26.11 never filled.
    /// </summary>
    // `partial` para que cada tab del host viva en su propio archivo: las tabs se descubren por
    // [BlockEditorTab] y no hay registro central, así que dos features no se pisan al agregarse.
    public sealed partial class ItemEditorWindow : BlockEditorWindow<ItemSO>
    {
        [MenuItem("Tools/Item Editor")]
        static void Open()
        {
            var w = GetWindow<ItemEditorWindow>("Item Editor");
            w.minSize = new Vector2(1040f, 560f);
        }

        protected override string DefaultFolder => "Assets/Rollgeon/Items";
        protected override string NewAssetName => "Item_New";

        protected override string LabelOf(ItemSO asset)
        {
            if (asset == null) return "(null)";
            return string.IsNullOrEmpty(asset.DisplayName) ? asset.name : asset.DisplayName;
        }

        protected override string SearchTextOf(ItemSO asset) =>
            asset == null ? null : $"{asset.name} {asset.DisplayName} {asset.ItemId}";

        protected override string IdOf(ItemSO asset) => asset != null ? asset.ItemId : null;

        /// <summary>`potion.healing` → `Item_PotionHealing`, matching the Item_* convention on disk.</summary>
        protected override string SuggestedAssetName(ItemSO asset)
        {
            if (asset == null || string.IsNullOrEmpty(asset.ItemId)) return null;
            return "Item_" + AssetNaming.PascalCaseId(asset.ItemId);
        }

        protected override void DrawIssues(ItemSO asset)
        {
            if (asset == null) return;

            if (string.IsNullOrEmpty(asset.ItemId))
                EditorGUILayout.HelpBox("ItemId is empty — the catalog looks items up by id.", MessageType.Error);

            if (string.IsNullOrEmpty(asset.DisplayName))
                EditorGUILayout.HelpBox("DisplayName is empty — the shop and HUD show it to players.", MessageType.Warning);

            if (asset.Type == ItemType.Active && asset.OnActivate == null)
                EditorGUILayout.HelpBox("Active item with no OnActivate pipeline — using it will do nothing.", MessageType.Warning);

            if (asset.Type == ItemType.Passive && (asset.PassiveHooks == null || asset.PassiveHooks.Count == 0))
                EditorGUILayout.HelpBox("Passive item with no hooks — it will never fire.", MessageType.Warning);

            WarnIfNotInCatalog(asset);
        }

        /// <summary>
        /// An item that isn't in the catalog can't be granted: the shop, the reward effects and the
        /// dev console all resolve through <c>ItemCatalogSO.GetById</c>. Cheap to forget, and the
        /// symptom (item silently never appears) points nowhere near the cause.
        /// </summary>
        static void WarnIfNotInCatalog(ItemSO asset)
        {
            if (string.IsNullOrEmpty(asset.ItemId)) return;

            var ids = ItemCatalogSO.GetEditorAllIds();
            if (ids == null) return;

            foreach (var id in ids)
                if (id == asset.ItemId) return;

            EditorGUILayout.HelpBox(
                $"'{asset.ItemId}' is not registered in ItemCatalog — the shop, EffAddItemToInventory " +
                "and `giveitem` all resolve through the catalog, so this item can never be granted.",
                MessageType.Warning);

            if (GUILayout.Button("Ping ItemCatalog"))
            {
                var guids = AssetDatabase.FindAssets("t:ItemCatalogSO");
                if (guids.Length > 0)
                    EditorGUIUtility.PingObject(
                        AssetDatabase.LoadAssetAtPath<ItemCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0])));
            }
        }
    }
}
