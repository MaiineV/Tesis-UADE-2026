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
    // `partial` para que cada superficie del host viva en su propio archivo: las tabs se descubren
    // por [BlockEditorTab] y no hay registro central, así que dos features no se pisan al agregarse.
    public sealed partial class EnchantmentEditorWindow : BlockEditorWindow<EnchantmentSO>
    {
        [MenuItem("Tools/Enchantment Editor")]
        static void Open()
        {
            var w = GetWindow<EnchantmentEditorWindow>("Enchantment Editor");
            w.minSize = new Vector2(1040f, 560f);
        }

        protected override string DefaultFolder => EnchantmentAuthoring.DefaultFolder;
        protected override string NewAssetName => "Ench_New";

        // Cada superficie del host (lista, métricas, disparadores, textos) vive en su propio
        // parcial y necesita recalcular lo que derive de la lista de assets. Como
        // `OnAssetsRefreshed` se puede sobrescribir una sola vez por clase, el override vive acá y
        // reparte a métodos parciales: cada archivo implementa el suyo sin conocer a los demás, y
        // el que no lo necesite simplemente no lo implementa (el compilador borra la llamada).
        partial void OnListAssetsRefreshed();
        partial void OnMetricsAssetsRefreshed();
        partial void OnTriggerAssetsRefreshed();
        partial void OnLocalizationAssetsRefreshed();

        // Mismo criterio: OnEnable/OnDisable se pueden sobrescribir una sola vez por clase, así que
        // el override vive acá y reparte.
        partial void OnLocalizationEnable();
        partial void OnLocalizationDisable();

        protected override void OnEnable()
        {
            base.OnEnable();
            OnLocalizationEnable();
        }

        protected override void OnDisable()
        {
            OnLocalizationDisable();
            base.OnDisable();
        }

        protected override void OnAssetsRefreshed()
        {
            base.OnAssetsRefreshed();
            _healthFindingsCache = null;
            OnListAssetsRefreshed();
            OnMetricsAssetsRefreshed();
            OnTriggerAssetsRefreshed();
            OnLocalizationAssetsRefreshed();
        }

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
            const string prefix = EnchantmentIdSlug.Prefix;
            if (id.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                id = id.Substring(prefix.Length);

            return "Ench_" + AssetNaming.PascalCaseId(id);
        }

        // ---- salud por asset -------------------------------------------------------------------

        // La salud completa (catálogo, pool, localización) recorre disco: calcularla en cada
        // repaint del panel sería un escaneo por frame — misma trampa medida que el Catalog del
        // shell. Se cachea y se suelta en OnAssetsRefreshed; lo editado sin guardar queda stale
        // hasta el próximo cambio de proyecto, aceptable para avisos.
        List<EnchantmentQuery.CatalogFinding> _healthFindingsCache;

        /// <summary>
        /// Hallazgos de salud del catálogo entero (estructura + localización), cacheados por
        /// rebuild de la lista. Los consumen <see cref="DrawIssues"/> (filtrados al asset
        /// seleccionado) y la tab de métricas — una sola pasada de disco para ambos.
        /// </summary>
        IReadOnlyList<EnchantmentQuery.CatalogFinding> HealthFindings
        {
            get
            {
                if (_healthFindingsCache == null)
                {
                    var all = EnchantmentQuery.GetAll();
                    _healthFindingsCache = new List<EnchantmentQuery.CatalogFinding>(
                        EnchantmentQuery.CheckCatalogHealth(all));
                    _healthFindingsCache.AddRange(EnchantmentQuery.CheckLocalizationHealth(all));
                }
                return _healthFindingsCache;
            }
        }

        /// <summary>Fuerza el recálculo de <see cref="HealthFindings"/> en la próxima lectura.</summary>
        void InvalidateHealthFindings() => _healthFindingsCache = null;

        protected override void DrawIssues(EnchantmentSO asset)
        {
            if (asset == null) return;

            foreach (var finding in HealthFindings)
            {
                if (finding.Asset != asset) continue;
                EditorGUILayout.HelpBox(finding.Message, ToMessageType(finding.Severity));
            }

            WarnAboutUnwired(asset);
        }

        static MessageType ToMessageType(EnchantmentQuery.FindingSeverity severity)
        {
            switch (severity)
            {
                case EnchantmentQuery.FindingSeverity.Error: return MessageType.Error;
                case EnchantmentQuery.FindingSeverity.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
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
