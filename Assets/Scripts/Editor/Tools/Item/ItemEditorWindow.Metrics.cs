using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Items;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// "Metrics" tab (spec §6.6) — a game-designer-facing view over the whole item catalog, not the
    /// single asset the Graph/Raw Data tabs edit. Answers "is the catalog balanced" at a glance:
    /// a comparable table, outliers against the GDD price table, distribution by rarity/event/combo
    /// and a health checklist with Ping. Replaces the hand-maintained
    /// <c>docs/balance/item-inventory.html</c> (stuck at "5 items" while the project has 24) —
    /// everything here is computed from disk on demand, so it can never go stale like that doc did.
    /// </summary>
    /// <remarks>
    /// Read-only by construction: this file never calls <c>Undo.RecordObject</c> or
    /// <c>EditorUtility.SetDirty</c>, only <see cref="ItemQuery"/> queries and <see cref="EditorGUIUtility.PingObject"/>.
    /// All catalog computation that Fase-3-shared code already exposes (per-item metrics, structural
    /// health findings) is read from <see cref="ItemQuery"/> as-is. The two outlier checks the spec
    /// asks for that <see cref="ItemQuery"/> does NOT expose yet — GDD price deviation and cross-rarity
    /// price inversion — are computed locally in <see cref="ComputePriceOutliers"/>, reusing
    /// <see cref="ItemQuery.CatalogFinding"/> as the shared shape so they render in the same list as
    /// the structural findings without a second UI.
    /// </remarks>
    public sealed partial class ItemEditorWindow
    {
        enum MetricsGroupBy { Rarity, Family }

        static readonly ItemRarity[] MetricsRarityOrder =
            { ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Legendary, ItemRarity.God };

        /// <summary>
        /// Default outlier band for GDD price deviation. The GDD itself sanctions Common items going
        /// up to 20 (base 15) "si el efecto es fuerte dentro del tier" (RarityPricing.cs) — that's a
        /// +33% override by design, not a bug. 35% sits just above that sanctioned case so the default
        /// view doesn't cry wolf on it; the slider lets a designer tighten it to actually see it.
        /// </summary>
        const float MetricsDefaultDeviationThreshold = 0.35f;

        IReadOnlyList<ItemQuery.ItemMetrics> _metricsCache;
        IReadOnlyList<ItemQuery.CatalogFinding> _metricsFindingsCache;
        List<ItemQuery.CatalogFinding> _metricsPriceOutlierCache;
        IReadOnlyList<ItemQuery.ItemFamily> _metricsFamiliesCache;
        IReadOnlyList<ItemSO> _metricsLooseCache;

        MetricsGroupBy _metricsGroupBy = MetricsGroupBy.Rarity;
        float _metricsDeviationThresholdPct = MetricsDefaultDeviationThreshold;

        bool _metricsShowDistribution = true;
        bool _metricsShowHealth = true;
        bool _metricsShowTable = true;

        Vector2 _metricsScroll;
        GUIStyle _metricsFallbackPriceStyle;
        GUIStyle _metricsSectionHeaderStyle;

        [BlockEditorTab("Metrics", 20)]
        void DrawMetricsTab()
        {
            EnsureMetricsStyles();
            if (_metricsCache == null) RefreshMetrics();

            DrawMetricsToolbar();

            _metricsScroll = EditorGUILayout.BeginScrollView(_metricsScroll);

            _metricsShowDistribution = EditorGUILayout.Foldout(_metricsShowDistribution, "Distribución", true, _metricsSectionHeaderStyle);
            if (_metricsShowDistribution) DrawDistribution();

            EditorGUILayout.Space(10);

            var healthCount = _metricsFindingsCache.Count + _metricsPriceOutlierCache.Count;
            _metricsShowHealth = EditorGUILayout.Foldout(_metricsShowHealth, $"Salud del catálogo ({healthCount})", true, _metricsSectionHeaderStyle);
            if (_metricsShowHealth) DrawHealth();

            EditorGUILayout.Space(10);

            _metricsShowTable = EditorGUILayout.Foldout(_metricsShowTable, $"Ítems ({_metricsCache.Count})", true, _metricsSectionHeaderStyle);
            if (_metricsShowTable) DrawTable();

            EditorGUILayout.EndScrollView();
        }

        void EnsureMetricsStyles()
        {
            if (_metricsFallbackPriceStyle == null)
                _metricsFallbackPriceStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Italic };

            if (_metricsSectionHeaderStyle == null)
                _metricsSectionHeaderStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        }

        void RefreshMetrics()
        {
            _metricsCache = ItemQuery.GetMetrics();
            _metricsFindingsCache = ItemQuery.CheckCatalogHealth();
            _metricsPriceOutlierCache = ComputePriceOutliers(_metricsCache, _metricsDeviationThresholdPct);

            // Los overloads sin argumento reescanean el proyecto entero (FindAssets + cargar cada
            // ItemSO). Llamarlos desde DrawDistribution significaba un escaneo completo POR REPAINT.
            _metricsFamiliesCache = ItemQuery.GetFamilies();
            _metricsLooseCache = ItemQuery.GetLooseItems();
        }

        /// <summary>La lista cambió en disco: la próxima vez que se dibuje la tab se recalcula.</summary>
        partial void OnMetricsAssetsRefreshed()
        {
            _metricsCache = null;
        }

        // ============================ Toolbar ============================

        void DrawMetricsToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                RefreshMetrics();

            GUILayout.Space(12);
            EditorGUILayout.LabelField("Agrupar por", GUILayout.Width(70));
            _metricsGroupBy = (MetricsGroupBy)EditorGUILayout.EnumPopup(_metricsGroupBy, EditorStyles.toolbarPopup, GUILayout.Width(80));

            GUILayout.FlexibleSpace();

            EditorGUILayout.LabelField("Umbral outlier de precio", GUILayout.Width(150));
            var newThreshold = EditorGUILayout.Slider(_metricsDeviationThresholdPct, 0.05f, 1f, GUILayout.Width(140));
            EditorGUILayout.LabelField($"{newThreshold:P0}", GUILayout.Width(40));

            if (!Mathf.Approximately(newThreshold, _metricsDeviationThresholdPct))
            {
                _metricsDeviationThresholdPct = newThreshold;
                _metricsPriceOutlierCache = ComputePriceOutliers(_metricsCache, _metricsDeviationThresholdPct);
            }

            EditorGUILayout.EndHorizontal();
        }

        // ============================ Distribución ============================

        void DrawDistribution()
        {
            var families = _metricsFamiliesCache;
            var loose = _metricsLooseCache;
            EditorGUILayout.LabelField(
                $"{_metricsCache.Count} ítems totales — {families.Count} familias ({families.Sum(f => f.Variants.Count)} variantes), {loose.Count} sueltos.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            DrawCountsColumn("Por rareza", CountByRarity(_metricsCache));
            DrawCountsColumn("Por evento", CountByEvent(_metricsCache));
            DrawCountsColumn("Por combo", CountByCombo(_metricsCache));
            EditorGUILayout.EndHorizontal();
        }

        static void DrawCountsColumn(string title, List<(string Label, int Count)> rows)
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(200));
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (rows.Count == 0)
            {
                EditorGUILayout.LabelField("—", EditorStyles.miniLabel);
            }
            else
            {
                foreach (var (label, count) in rows)
                    EditorGUILayout.LabelField(label, count.ToString());
            }
            EditorGUILayout.EndVertical();
        }

        static List<(string Label, int Count)> CountByRarity(IReadOnlyList<ItemQuery.ItemMetrics> metrics) =>
            MetricsRarityOrder
                .Select(r => (RarityPalette.DisplayName(r), metrics.Count(m => m.Rarity == r)))
                .Where(t => t.Item2 > 0)
                .ToList();

        static List<(string Label, int Count)> CountByEvent(IReadOnlyList<ItemQuery.ItemMetrics> metrics)
        {
            var counts = new Dictionary<EventName, int>();
            foreach (var m in metrics)
                foreach (var e in m.TriggerEvents)
                    counts[e] = counts.TryGetValue(e, out var c) ? c + 1 : 1;

            return counts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key.ToString(), StringComparer.Ordinal)
                .Select(kv => (kv.Key.ToString(), kv.Value))
                .ToList();
        }

        static List<(string Label, int Count)> CountByCombo(IReadOnlyList<ItemQuery.ItemMetrics> metrics)
        {
            var counts = new Dictionary<string, int>();
            foreach (var m in metrics)
                foreach (var id in m.ComboIds)
                {
                    var label = id == ItemQuery.AnyComboSentinel ? "(cualquier combo)" : id;
                    counts[label] = counts.TryGetValue(label, out var c) ? c + 1 : 1;
                }

            return counts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }

        // ============================ Salud del catálogo ============================

        void DrawHealth()
        {
            var all = _metricsFindingsCache.Concat(_metricsPriceOutlierCache)
                .OrderByDescending(f => f.Severity)
                .ToList();

            if (all.Count == 0)
            {
                EditorGUILayout.HelpBox("Sin hallazgos — catálogo limpio.", MessageType.Info);
                return;
            }

            foreach (var finding in all)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField(IconFor(finding.Severity), GUILayout.Width(20));
                EditorGUILayout.LabelField(finding.Message, EditorStyles.wordWrappedLabel);

                using (new EditorGUI.DisabledScope(finding.Asset == null))
                {
                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                        EditorGUIUtility.PingObject(finding.Asset);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        static string IconFor(ItemQuery.FindingSeverity severity)
        {
            switch (severity)
            {
                case ItemQuery.FindingSeverity.Error: return "⛔";
                case ItemQuery.FindingSeverity.Warning: return "⚠";
                default: return "ℹ";
            }
        }

        /// <summary>
        /// Outliers the spec asks for (§6.6) that aren't in <see cref="ItemQuery.CheckCatalogHealth"/>
        /// because they're a metrics-tab concern, not a structural catalog check: "sin precio" is
        /// already covered there (an item outside the ShopPool). This covers the other two —
        /// deviation from the GDD's rarity price table, and one rarity's items pricing above/below
        /// another's — both against <see cref="ItemQuery.ItemMetrics"/> the caller already has.
        /// </summary>
        List<ItemQuery.CatalogFinding> ComputePriceOutliers(
            IReadOnlyList<ItemQuery.ItemMetrics> metrics, float thresholdPct)
        {
            var outliers = new List<ItemQuery.CatalogFinding>();
            if (metrics == null || metrics.Count == 0) return outliers;

            // --- Desvío contra la tabla de precios del GDD. Solo tiene sentido para ítems
            // efectivamente en el pool: un precio fallback ES el precio del GDD, nunca se desvía de
            // sí mismo (ver ItemQuery.ItemMetrics.PriceIsFallback).
            foreach (var m in metrics)
            {
                if (m.PriceIsFallback || m.GddBasePrice <= 0) continue;

                var delta = m.Price - m.GddBasePrice;
                if (delta == 0) continue;

                var pct = delta / (float)m.GddBasePrice;
                if (Mathf.Abs(pct) < thresholdPct) continue;

                var sign = delta > 0 ? "+" : "";
                outliers.Add(new ItemQuery.CatalogFinding(
                    ItemQuery.FindingSeverity.Warning,
                    $"'{LabelOf(m.Asset)}' ({m.RarityLabel}) cuesta {m.Price} — el GDD dicta {m.GddBasePrice} " +
                    $"para esa rareza ({sign}{pct:P0}).",
                    m.Asset));
            }

            // --- Inversiones entre rarezas: un ítem de rareza baja más caro que uno de rareza alta
            // (o al revés). Se calcula con min/max por tier y prefix-max / suffix-min sobre las 5
            // rarezas del GDD — O(rarezas) en vez de comparar cada par de ítems.
            var byRarity = metrics.Where(m => m.Price > 0).ToLookup(m => m.Rarity);
            var rarityMin = new int?[MetricsRarityOrder.Length];
            var rarityMax = new int?[MetricsRarityOrder.Length];
            for (var i = 0; i < MetricsRarityOrder.Length; i++)
            {
                var prices = byRarity[MetricsRarityOrder[i]].Select(m => m.Price).ToList();
                if (prices.Count == 0) continue;
                rarityMin[i] = prices.Min();
                rarityMax[i] = prices.Max();
            }

            // suffixMin[i] = precio mínimo entre ítems de rareza estrictamente MAYOR que MetricsRarityOrder[i].
            var suffixMin = new int?[MetricsRarityOrder.Length];
            int? runningMin = null;
            for (var i = MetricsRarityOrder.Length - 1; i >= 0; i--)
            {
                suffixMin[i] = runningMin;
                if (rarityMin[i].HasValue)
                    runningMin = runningMin.HasValue ? Math.Min(runningMin.Value, rarityMin[i].Value) : rarityMin[i];
            }

            // prefixMax[i] = precio máximo entre ítems de rareza estrictamente MENOR que MetricsRarityOrder[i].
            var prefixMax = new int?[MetricsRarityOrder.Length];
            int? runningMax = null;
            for (var i = 0; i < MetricsRarityOrder.Length; i++)
            {
                prefixMax[i] = runningMax;
                if (rarityMax[i].HasValue)
                    runningMax = runningMax.HasValue ? Math.Max(runningMax.Value, rarityMax[i].Value) : rarityMax[i];
            }

            for (var i = 0; i < MetricsRarityOrder.Length; i++)
            {
                foreach (var m in byRarity[MetricsRarityOrder[i]])
                {
                    if (suffixMin[i].HasValue && m.Price > suffixMin[i].Value)
                        outliers.Add(new ItemQuery.CatalogFinding(
                            ItemQuery.FindingSeverity.Warning,
                            $"'{LabelOf(m.Asset)}' ({m.RarityLabel}, {m.Price}) cuesta más que un ítem de rareza " +
                            $"superior (mín ahí: {suffixMin[i].Value}).",
                            m.Asset));

                    if (prefixMax[i].HasValue && m.Price < prefixMax[i].Value)
                        outliers.Add(new ItemQuery.CatalogFinding(
                            ItemQuery.FindingSeverity.Warning,
                            $"'{LabelOf(m.Asset)}' ({m.RarityLabel}, {m.Price}) cuesta menos que un ítem de rareza " +
                            $"inferior (máx ahí: {prefixMax[i].Value}).",
                            m.Asset));
                }
            }

            return outliers;
        }

        // ============================ Tabla comparable ============================

        void DrawTable()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Nombre", EditorStyles.boldLabel, GUILayout.Width(160));
            GUILayout.Space(16); // swatch de rareza
            EditorGUILayout.LabelField("Rareza", EditorStyles.boldLabel, GUILayout.Width(70));
            EditorGUILayout.LabelField("Tipo", EditorStyles.boldLabel, GUILayout.Width(55));
            EditorGUILayout.LabelField("Familia", EditorStyles.boldLabel, GUILayout.Width(110));
            EditorGUILayout.LabelField("Precio", EditorStyles.boldLabel, GUILayout.Width(55));
            EditorGUILayout.LabelField("GDD", EditorStyles.boldLabel, GUILayout.Width(45));
            EditorGUILayout.LabelField("Eventos", EditorStyles.boldLabel, GUILayout.Width(150));
            EditorGUILayout.LabelField("Combos", EditorStyles.boldLabel, GUILayout.MinWidth(120));
            GUILayout.Space(50); // Ping
            EditorGUILayout.EndHorizontal();

            var groups = _metricsGroupBy == MetricsGroupBy.Rarity
                ? _metricsCache.GroupBy(m => m.RarityLabel).OrderBy(g => MetricsRarityOrderOf(g.Key))
                : _metricsCache
                    .GroupBy(m => string.IsNullOrEmpty(m.FamilyId) ? "(sin familia)" : m.FamilyId)
                    .OrderBy(g => g.Key, StringComparer.Ordinal);

            foreach (var group in groups)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(group.Key, EditorStyles.miniBoldLabel);

                var ordered = _metricsGroupBy == MetricsGroupBy.Rarity
                    ? group.OrderBy(m => string.IsNullOrEmpty(m.FamilyId) ? "" : m.FamilyId, StringComparer.Ordinal)
                        .ThenBy(m => m.Asset != null ? m.Asset.VariantIndex : 0)
                    : group.OrderBy(m => m.Asset != null ? m.Asset.VariantIndex : 0);

                foreach (var m in ordered)
                    DrawRow(m);
            }
        }

        void DrawRow(ItemQuery.ItemMetrics m)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(LabelOf(m.Asset), GUILayout.Width(160));

            var swatch = GUILayoutUtility.GetRect(12, 14, GUILayout.Width(12));
            EditorGUI.DrawRect(swatch, RarityPalette.BodyColor(m.Rarity));
            GUILayout.Space(4);
            EditorGUILayout.LabelField(m.RarityLabel, GUILayout.Width(66));

            EditorGUILayout.LabelField(m.Type.ToString(), GUILayout.Width(55));
            EditorGUILayout.LabelField(string.IsNullOrEmpty(m.FamilyId) ? "—" : m.FamilyId, GUILayout.Width(110));

            var priceText = m.PriceIsFallback ? $"({m.Price})" : m.Price.ToString();
            EditorGUILayout.LabelField(priceText, m.PriceIsFallback ? _metricsFallbackPriceStyle : EditorStyles.label, GUILayout.Width(55));
            EditorGUILayout.LabelField(m.GddBasePrice.ToString(), GUILayout.Width(45));

            var events = m.TriggerEvents.Count > 0 ? string.Join(", ", m.TriggerEvents) : "—";
            EditorGUILayout.LabelField(events, GUILayout.Width(150));

            var combos = m.ComboIds.Count > 0
                ? string.Join(", ", m.ComboIds.Select(id => id == ItemQuery.AnyComboSentinel ? "*" : id))
                : "—";
            EditorGUILayout.LabelField(combos, GUILayout.MinWidth(120));

            using (new EditorGUI.DisabledScope(m.Asset == null))
            {
                if (GUILayout.Button("Ping", GUILayout.Width(50)))
                    EditorGUIUtility.PingObject(m.Asset);
            }

            EditorGUILayout.EndHorizontal();
        }

        static int MetricsRarityOrderOf(string rarityLabel)
        {
            for (var i = 0; i < MetricsRarityOrder.Length; i++)
                if (RarityPalette.DisplayName(MetricsRarityOrder[i]) == rarityLabel) return i;
            return MetricsRarityOrder.Length;
        }
    }
}
