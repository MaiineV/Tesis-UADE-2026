using System.Collections.Generic;
using Rollgeon.Attributes;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// Authoring window for <see cref="EnchantmentSO"/> — the dice channel of the in-run upgrade
    /// system, and by far the biggest body of authored content in the project.
    /// </summary>
    public sealed class EnchantmentEditorWindow : BlockEditorWindow<EnchantmentSO>
    {
        [MenuItem("Tools/Enchantment Editor")]
        static void Open()
        {
            var w = GetWindow<EnchantmentEditorWindow>("Enchantment Editor");
            w.minSize = new Vector2(1040f, 560f);
        }

        protected override string DefaultFolder => "Assets/Rollgeon/Upgrades/Dice/Enchantments";
        protected override string NewAssetName => "Ench_New";

        protected override string LabelOf(EnchantmentSO asset)
        {
            if (asset == null) return "(null)";
            return string.IsNullOrEmpty(asset.DisplayName) ? asset.name : asset.DisplayName;
        }

        protected override string SearchTextOf(EnchantmentSO asset) =>
            asset == null ? null : $"{asset.name} {asset.DisplayName} {asset.UpgradeId}";

        protected override string IdOf(EnchantmentSO asset) => asset != null ? asset.UpgradeId : null;

        /// <summary>
        /// `ench.multiplo_de_3` → `Ench_MultiploDe3`. The `ench.` prefix is the channel, not part of
        /// the name — every asset on disk already drops it.
        /// </summary>
        protected override string SuggestedAssetName(EnchantmentSO asset)
        {
            if (asset == null || string.IsNullOrEmpty(asset.UpgradeId)) return null;

            string id = asset.UpgradeId;
            const string prefix = "ench.";
            if (id.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                id = id.Substring(prefix.Length);

            return "Ench_" + AssetNaming.PascalCaseId(id);
        }

        protected override void DrawIssues(EnchantmentSO asset)
        {
            if (asset == null) return;

            bool hasTriggers = asset.Triggers != null && asset.Triggers.Count > 0;
            bool hasCapabilities = asset.Capabilities != null && asset.Capabilities.Count > 0;
            if (asset.FaceFilter == null && !hasTriggers && !hasCapabilities)
                EditorGUILayout.HelpBox(
                    "No face filter, no triggers and no capabilities — this enchantment does nothing.",
                    MessageType.Warning);

            WarnAboutUnwired(asset);
        }

        /// <summary>
        /// Surfaces triggers/capabilities that compile and configure but are no-ops in game.
        /// </summary>
        /// <remarks>
        /// Without this, an author can tune <c>CapWildcard</c>, drop it in a pool and playtest
        /// it, and nothing in the inspector hints that <c>ContractSheet</c> never reads the flag.
        /// Until now the only record was a hand-maintained table in
        /// <c>docs/balance/item-inventory.html</c>; the marker lives next to the stub instead.
        /// </remarks>
        static void WarnAboutUnwired(EnchantmentSO asset)
        {
            List<string> unwired = null;

            void Collect(object entry)
            {
                if (entry == null) return;
                var attr = entry.GetType().GetCustomAttributes(typeof(NotYetWiredAttribute), true);
                if (attr.Length == 0) return;
                (unwired ??= new List<string>()).Add(
                    $"• {entry.GetType().Name} — {((NotYetWiredAttribute)attr[0]).Reason}");
            }

            if (asset.Triggers != null)
                foreach (var trigger in asset.Triggers) Collect(trigger);
            if (asset.Capabilities != null)
                foreach (var capability in asset.Capabilities) Collect(capability);

            if (unwired == null) return;

            EditorGUILayout.HelpBox(
                "Estas piezas todavía no hacen nada in-game:\n" + string.Join("\n", unwired),
                MessageType.Warning);
        }
    }
}
