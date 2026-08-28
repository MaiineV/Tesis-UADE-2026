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
        enum MetricsGroupBy { Rarity, Family, None }

        /// <summary>Columna por la que ordena la tabla. <c>None</c> = respetar el agrupado.</summary>
        enum MetricsSortBy { None, Name, Rarity, Price, Deviation }

        /// <summary>Un filtro puesto al clickear una barra de distribucion.</summary>
        readonly struct MetricsFilter
        {
            public string Label { get; }
            public System.Func<ItemQuery.ItemMetrics, bool> Predicate { get; }
            public bool Active => Predicate != null;

            public MetricsFilter(string label, System.Func<ItemQuery.ItemMetrics, bool> predicate)
            {
                Label = label;
                Predicate = predicate;
            }
        }

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
        IReadOnlyDictionary<ItemRarity, IReadOnlyList<ItemQuery.MagnitudeSummary>> _metricsMagnitudesCache;
        IReadOnlyList<ItemSO> _metricsLooseCache;

        MetricsGroupBy _metricsGroupBy = MetricsGroupBy.Rarity;
        MetricsSortBy _metricsSortBy = MetricsSortBy.None;
        bool _metricsSortDesc;
        MetricsFilter _metricsFilter;
        string _metricsSearch = string.Empty;
        float _metricsDeviationThresholdPct = MetricsDefaultDeviationThreshold;

        bool _metricsShowDistribution = true;
        bool _metricsShowHealth = true;
        bool _metricsShowMagnitudes = true;
        bool _metricsShowTable = true;

        Vector2 _metricsScroll;
        GUIStyle _metricsFallbackPriceStyle;
        GUIStyle _metricsSectionHeaderStyle;
        GUIStyle _metricsStatusStyle;
        GUIStyle _metricsRightStyle;

        [BlockEditorTab("Metrics", 20)]
        void DrawMetricsTab()
        {
            EnsureMetricsStyles();
            if (_metricsCache == null) RefreshMetrics();

            DrawMetricsToolbar();
            DrawStatusStrip();

            _metricsScroll = EditorGUILayout.BeginScrollView(_metricsScroll);

            // Salud primero: es lo unico accionable. Antes venia tercera, debajo de dos tablas que
            // no piden ninguna decision.
            int problemas = _metricsFindingsCache.Count + _metricsPriceOutlierCache.Count;
            if (MetricsSection("Problemas", problemas, ref _metricsShowHealth)) DrawHealth();

            if (MetricsSection("Catálogo", _metricsCache.Count, ref _metricsShowTable)) DrawTable();
            if (MetricsSection("Distribución", 0, ref _metricsShowDistribution)) DrawDistribution();
            if (MetricsSection("Magnitudes", 0, ref _metricsShowMagnitudes)) DrawMagnitudes();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// El titular: cuantos items hay y si algo esta mal, sin tener que leer nada mas.
        /// </summary>
        /// <remarks>
        /// La tab abria con tres tablas de numeros y ninguna respondia la primera pregunta de quien la
        /// abre, que es "¿esta todo bien?". Esta franja la contesta en una linea y se tine de rojo
        /// cuando no.
        /// </remarks>
        void DrawStatusStrip()
        {
            int problemas = _metricsFindingsCache.Count + _metricsPriceOutlierCache.Count;
            int variantes = 0;
            foreach (var f in _metricsFamiliesCache) variantes += f.Variants.Count;

            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(26f));
            EditorGUI.DrawRect(rect, problemas == 0
                ? new Color(0.20f, 0.28f, 0.22f)
                : new Color(0.30f, 0.20f, 0.18f));

            var text = $"{_metricsCache.Count} ítems   ·   {_metricsFamiliesCache.Count} familias ({variantes} variantes)"
                     + $"   ·   {_metricsLooseCache.Count} sueltos   ·   "
                     + (problemas == 0 ? "sin problemas" : $"{problemas} problemas");

            var label = new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height);
            GUI.Label(label, text, _metricsStatusStyle);
        }

        /// <summary>Cabecera de seccion con su cuenta al costado. <paramref name="count"/> 0 = sin contador.</summary>
        bool MetricsSection(string title, int count, ref bool expanded)
        {
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                var header = count > 0 ? $"{title}   ({count})" : title;
                bool next = EditorGUILayout.Foldout(expanded, header, true, _metricsSectionHeaderStyle);
                if (next != expanded) expanded = next;
            }

            var line = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1f));
            EditorGUI.DrawRect(line, new Color(0.35f, 0.35f, 0.35f));
            EditorGUILayout.Space(3);
            return expanded;
        }

        void EnsureMetricsStyles()
        {
            if (_metricsFallbackPriceStyle == null)
                _metricsFallbackPriceStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Italic };

            if (_metricsSectionHeaderStyle == null)
                _metricsSectionHeaderStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };

            if (_metricsStatusStyle == null)
                _metricsStatusStyle = new GUIStyle(EditorStyles.boldLabel)
                { alignment = TextAnchor.MiddleLeft, fontSize = 12 };

            if (_metricsRightStyle == null)
                _metricsRightStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
        }

        void RefreshMetrics()
        {
            _metricsCache = ItemQuery.GetMetrics();
            _metricsFindingsCache = ItemQuery.CheckCatalogHealth();
            _metricsPriceOutlierCache = ComputePriceOutliers(_metricsCache, _metricsDeviationThresholdPct);

            // Los overloads sin argumento reescanean el proyecto entero (FindAssets + cargar cada
            // ItemSO). Llamarlos desde DrawDistribution significaba un escaneo completo POR REPAINT.
            _metricsMagnitudesCache = ItemQuery.GetMagnitudesByRarity();
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

        // ============================ Magnitudes ============================

        /// <summary>
        /// Daño, oro, curación y escudo agregados por rareza (spec §6.6).
        /// </summary>
        /// <remarks>
        /// Es la pregunta que el precio solo no responde: dos ítems del mismo tier pueden costar lo
        /// mismo y uno dar el doble que el otro. Acá se ve si una rareza pega de verdad más que la
        /// de abajo, que es lo que el jugador espera al pagar la diferencia.
        /// </remarks>
        /// <summary>
        /// Cuanto da cada rareza, por recurso.
        /// </summary>
        /// <remarks>
        /// Se dibuja una fila por rareza y recurso con el rango minimo-maximo como barra: lo que
        /// importa no es el promedio sino si los tiers se solapan, y eso en una tabla de numeros hay
        /// que calcularlo mentalmente. Con las barras alineadas a la misma escala, un tier que pisa al
        /// de arriba se ve solo.
        /// </remarks>
        void DrawMagnitudes()
        {
            if (_metricsMagnitudesCache == null || _metricsMagnitudesCache.Count == 0)
            {
                EditorGUILayout.LabelField("Sin magnitudes legibles todavía.", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField(
                "Solo valores fijos. Los que se calculan en vivo se cuentan aparte.", EditorStyles.miniLabel);

            foreach (ItemQuery.MagnitudeKind kind in Enum.GetValues(typeof(ItemQuery.MagnitudeKind)))
            {
                if (kind == ItemQuery.MagnitudeKind.Other) continue;

                int max = 0;
                foreach (var rarity in MetricsRarityOrder)
                    if (_metricsMagnitudesCache.TryGetValue(rarity, out var ss))
                        foreach (var s2 in ss)
                            if (s2.Kind == kind && s2.Max > max) max = s2.Max;
                if (max == 0) continue;

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField(kind.ToString(), EditorStyles.miniBoldLabel);

                // Los tiers se dibujan de menor a mayor sobre la MISMA escala, asi un tier cuyo
                // minimo cae por debajo del maximo del anterior se ve pisado sin tener que restar.
                int prevMax = -1;
                foreach (var rarity in MetricsRarityOrder)
                {
                    if (!_metricsMagnitudesCache.TryGetValue(rarity, out var ss)) continue;
                    foreach (var sum in ss)
                    {
                        if (sum.Kind != kind) continue;
                        bool overlaps = sum.Count > 0 && prevMax >= 0 && sum.Min < prevMax;
                        DrawMagnitudeRow(rarity, sum, max, overlaps);
                        if (sum.Count > 0) prevMax = Mathf.Max(prevMax, sum.Max);
                    }
                }

                DrawScaleAxis(max);
            }
        }

        /// <summary>Los extremos de la escala, para que las barras signifiquen algo sin adivinar.</summary>
        void DrawScaleAxis(int max)
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(12f));
            float labelW = Mathf.Min(110f, rect.width * 0.28f);
            float trackX = rect.x + labelW + 12f;
            float trackW = Mathf.Max(20f, rect.width - labelW - 130f);

            GUI.Label(new Rect(trackX, rect.y, 40f, rect.height), "0", EditorStyles.miniLabel);
            GUI.Label(new Rect(trackX + trackW - 60f, rect.y, 60f, rect.height),
                      max.ToString(), _metricsRightStyle);
        }

        void DrawMagnitudeRow(ItemRarity rarity, ItemQuery.MagnitudeSummary sum, int max, bool overlaps)
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(17f));
            float labelW = Mathf.Min(110f, rect.width * 0.28f);

            EditorGUI.DrawRect(new Rect(rect.x, rect.y + 4f, 3f, 9f), RarityPalette.BodyColor(rarity));
            GUI.Label(new Rect(rect.x + 8f, rect.y, labelW, rect.height),
                      RarityPalette.DisplayName(rarity), EditorStyles.miniLabel);

            float trackX = rect.x + labelW + 12f;
            float trackW = Mathf.Max(20f, rect.width - labelW - 130f);
            EditorGUI.DrawRect(new Rect(trackX, rect.y + 5f, trackW, 8f), new Color(1f, 1f, 1f, 0.06f));

            if (sum.Count > 0)
            {
                // Barra de min a max: el rango es el dato, no el promedio.
                float x0 = trackX + trackW * (sum.Min / (float)max);
                float x1 = trackX + trackW * (sum.Max / (float)max);
                EditorGUI.DrawRect(new Rect(x0, rect.y + 5f, Mathf.Max(2f, x1 - x0), 8f),
                                   overlaps
                                       ? new Color(0.85f, 0.62f, 0.35f, 0.9f)
                                       : new Color(0.45f, 0.65f, 0.85f, 0.85f));
            }

            var text = sum.Count == 0
                ? (sum.Dynamic > 0 ? $"{sum.Dynamic} dinámicos" : "—")
                : $"{sum.Min}–{sum.Max}  (n={sum.Count}{(sum.Dynamic > 0 ? $", +{sum.Dynamic} din." : "")})"
                  + (overlaps ? "  ⚠" : "");
            GUI.Label(new Rect(rect.xMax - 118f, rect.y, 114f, rect.height), text, _metricsRightStyle);
        }

        // ============================ Distribución ============================

        /// <summary>
        /// Cuantos items caen en cada rareza, evento y combo — con barras.
        /// </summary>
        /// <remarks>
        /// Antes eran tres columnas de "etiqueta   numero". Comparar proporciones leyendo numeros es
        /// justo lo que el ojo hace mal y una barra hace sola: un hueco de cobertura — un evento con
        /// un solo item, un combo sin ninguno — salta sin tener que sumar.
        /// </remarks>
        void DrawDistribution()
        {
            DrawBarBlock("Por rareza", CountByRarity(_metricsCache),
                label => new MetricsFilter("rareza: " + label, m => m.RarityLabel == label));
            EditorGUILayout.Space(4);
            DrawBarBlock("Por evento disparador", CountByEvent(_metricsCache),
                label => new MetricsFilter("evento: " + label,
                    m => m.TriggerEvents.Any(e => e.ToString() == label)));
            EditorGUILayout.Space(4);
            DrawBarBlock("Por combo", CountByCombo(_metricsCache),
                label => new MetricsFilter("combo: " + label,
                    m => m.ComboIds.Any(id =>
                        (id == ItemQuery.AnyComboSentinel ? "(cualquier combo)" : id) == label)));
        }

        void DrawBarBlock(string title, List<(string Label, int Count)> rows, System.Func<string, MetricsFilter> filterFor)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

            if (rows.Count == 0)
            {
                EditorGUILayout.LabelField("—", EditorStyles.miniLabel);
                return;
            }

            int max = 1;
            foreach (var r in rows) if (r.Count > max) max = r.Count;

            foreach (var (label, count) in rows)
            {
                var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(15f));
                float labelW = Mathf.Min(150f, rect.width * 0.35f);

                GUI.Label(new Rect(rect.x + 4f, rect.y, labelW, rect.height), label, EditorStyles.miniLabel);

                float trackX = rect.x + labelW + 8f;
                float trackW = Mathf.Max(20f, rect.width - labelW - 48f);
                var track = new Rect(trackX, rect.y + 4f, trackW, 8f);
                EditorGUI.DrawRect(track, new Color(1f, 1f, 1f, 0.06f));

                var fill = new Rect(trackX, track.y, trackW * (count / (float)max), 8f);
                EditorGUI.DrawRect(fill, new Color(0.45f, 0.65f, 0.85f, 0.85f));

                GUI.Label(new Rect(rect.xMax - 36f, rect.y, 32f, rect.height),
                          count.ToString(), EditorStyles.miniLabel);

                // La barra filtra la tabla: convierte el grafico en navegacion en vez de dato suelto.
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    _metricsFilter = filterFor(label);
                    _metricsShowTable = true;
                }
            }
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

        /// <summary>
        /// Los hallazgos, mas severos arriba.
        /// </summary>
        /// <remarks>
        /// Antes cada hallazgo era un <c>helpBox</c> propio: con seis avisos la seccion ocupaba media
        /// pantalla y no se podia barrer de un vistazo. Ahora son filas de una linea, con una barra de
        /// color a la izquierda que dice la severidad sin leer.
        /// </remarks>
        void DrawHealth()
        {
            var all = _metricsFindingsCache.Concat(_metricsPriceOutlierCache)
                .OrderByDescending(f => f.Severity)
                .ToList();

            if (all.Count == 0)
            {
                EditorGUILayout.LabelField("Sin hallazgos — catálogo limpio.", EditorStyles.miniLabel);
                return;
            }

            for (int i = 0; i < all.Count; i++)
            {
                var finding = all[i];
                var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(20f));
                if (i % 2 == 1) EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.03f));

                EditorGUI.DrawRect(new Rect(rect.x, rect.y + 2f, 3f, rect.height - 4f), SeverityColor(finding.Severity));

                GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 68f, rect.height),
                          finding.Message, EditorStyles.miniLabel);

                using (new EditorGUI.DisabledScope(finding.Asset == null))
                {
                    if (GUI.Button(new Rect(rect.xMax - 52f, rect.y + 1f, 48f, 17f), "Ping", EditorStyles.miniButton))
                        EditorGUIUtility.PingObject(finding.Asset);
                }
            }
        }

        static Color SeverityColor(ItemQuery.FindingSeverity severity)
        {
            switch (severity)
            {
                case ItemQuery.FindingSeverity.Error: return new Color(0.80f, 0.30f, 0.25f);
                case ItemQuery.FindingSeverity.Warning: return new Color(0.85f, 0.68f, 0.30f);
                default: return new Color(0.45f, 0.65f, 0.85f);
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

        /// <summary>
        /// El catalogo, una fila por item.
        /// </summary>
        /// <remarks>
        /// La version anterior tenia nueve columnas de ancho fijo que sumaban ~750 px: en un panel
        /// angosto se desbordaban y se pisaban entre si. Ahora las columnas se reparten el ancho
        /// disponible y quedan solo las cuatro que responden algo — quien es, de que familia, cuanto
        /// cuesta contra lo que dicta el GDD, y cuando dispara. El resto se ve seleccionando el item.
        /// </remarks>
        void DrawTable()
        {
            DrawTableControls();

            var rows = FilteredRows();
            if (rows.Count == 0)
            {
                EditorGUILayout.LabelField("Ningún ítem coincide.", EditorStyles.miniLabel);
                return;
            }

            DrawColumnHeaders();

            if (_metricsSortBy != MetricsSortBy.None || _metricsGroupBy == MetricsGroupBy.None)
            {
                // Ordenado: la agrupacion estorbaria, porque lo que se quiere ver es el ranking.
                int flat = 0;
                foreach (var m in Sorted(rows)) DrawRow(m, flat++);
                return;
            }

            var groups = _metricsGroupBy == MetricsGroupBy.Rarity
                ? rows.GroupBy(m => m.RarityLabel).OrderBy(g => MetricsRarityOrderOf(g.Key))
                : rows.GroupBy(m => string.IsNullOrEmpty(m.FamilyId) ? "(sin familia)" : m.FamilyId)
                      .OrderBy(g => g.Key, StringComparer.Ordinal);

            int row = 0;
            foreach (var group in groups)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(group.Key.ToUpperInvariant(), EditorStyles.miniBoldLabel);
                foreach (var m in group.OrderBy(m => m.Asset != null ? m.Asset.VariantIndex : 0))
                    DrawRow(m, row++);
            }
        }

        /// <summary>Busqueda, agrupado y el chip del filtro que puso un clic en una barra.</summary>
        void DrawTableControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _metricsSearch = EditorGUILayout.TextField(_metricsSearch, EditorStyles.toolbarSearchField, GUILayout.MaxWidth(180f));

                EditorGUILayout.LabelField("Agrupar", EditorStyles.miniLabel, GUILayout.Width(50));
                _metricsGroupBy = (MetricsGroupBy)EditorGUILayout.EnumPopup(
                    _metricsGroupBy, EditorStyles.toolbarPopup, GUILayout.Width(80));

                GUILayout.FlexibleSpace();

                if (_metricsFilter.Active)
                {
                    if (GUILayout.Button($"✕  {_metricsFilter.Label}", EditorStyles.miniButton))
                        _metricsFilter = default;
                }

                if (_metricsSortBy != MetricsSortBy.None && GUILayout.Button("✕ orden", EditorStyles.miniButton))
                    _metricsSortBy = MetricsSortBy.None;
            }
        }

        List<ItemQuery.ItemMetrics> FilteredRows()
        {
            var result = new List<ItemQuery.ItemMetrics>();
            foreach (var m in _metricsCache)
            {
                if (_metricsFilter.Active && !_metricsFilter.Predicate(m)) continue;
                if (!string.IsNullOrEmpty(_metricsSearch)
                    && LabelOf(m.Asset).IndexOf(_metricsSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add(m);
            }
            return result;
        }

        IEnumerable<ItemQuery.ItemMetrics> Sorted(List<ItemQuery.ItemMetrics> rows)
        {
            IEnumerable<ItemQuery.ItemMetrics> q;
            switch (_metricsSortBy)
            {
                case MetricsSortBy.Name: q = rows.OrderBy(m => LabelOf(m.Asset), StringComparer.OrdinalIgnoreCase); break;
                case MetricsSortBy.Rarity: q = rows.OrderBy(m => MetricsRarityOrderOf(m.RarityLabel)); break;
                case MetricsSortBy.Price: q = rows.OrderBy(m => m.PriceIsFallback ? int.MinValue : m.Price); break;
                case MetricsSortBy.Deviation: q = rows.OrderBy(DeviationOf); break;
                default: return rows;
            }
            return _metricsSortDesc ? q.Reverse() : q;
        }

        /// <summary>Cuanto se aparta el precio del que dicta el GDD. Negativo si no tiene precio, para que caiga al fondo.</summary>
        static float DeviationOf(ItemQuery.ItemMetrics m)
        {
            if (m.PriceIsFallback || m.GddBasePrice <= 0) return -1f;
            return Mathf.Abs(m.Price - m.GddBasePrice) / (float)m.GddBasePrice;
        }

        void DrawColumnHeaders()
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(18f));
            EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.05f));

            float x = rect.x + 14f;
            float free = rect.width - 14f - 56f;
            float wName = Mathf.Max(120f, free * 0.32f);
            float wFamily = Mathf.Max(80f, free * 0.20f);
            float wPrice = Mathf.Max(90f, free * 0.18f);
            float wTrigger = Mathf.Max(90f, free - wName - wFamily - wPrice);

            SortHeader(new Rect(x, rect.y, wName, rect.height), "Nombre", MetricsSortBy.Name); x += wName;
            GUI.Label(new Rect(x, rect.y, wFamily, rect.height), "Familia", EditorStyles.miniBoldLabel); x += wFamily;
            SortHeader(new Rect(x, rect.y, wPrice, rect.height), "Precio / GDD", MetricsSortBy.Price); x += wPrice;
            SortHeader(new Rect(x, rect.y, wTrigger, rect.height), "Dispara", MetricsSortBy.Deviation);
        }

        /// <summary>Encabezado clickeable. Segundo clic sobre la misma columna invierte el orden.</summary>
        void SortHeader(Rect rect, string title, MetricsSortBy column)
        {
            bool active = _metricsSortBy == column;
            var label = active ? title + (_metricsSortDesc ? "  ▾" : "  ▴") : title;

            if (!GUI.Button(rect, label, EditorStyles.miniBoldLabel)) return;

            if (active) _metricsSortDesc = !_metricsSortDesc;
            else { _metricsSortBy = column; _metricsSortDesc = false; }
        }

        void DrawRow(ItemQuery.ItemMetrics m, int index)
        {
            // 17 px y no 20: con ~90 instancias previstas por el GDD, tres pixeles por fila son
            // cuatro filas mas en pantalla.
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(17f));

            bool isSelected = m.Asset != null && m.Asset == SelectedAsset;
            if (isSelected) EditorGUI.DrawRect(rect, new Color(0.45f, 0.75f, 1f, 0.18f));
            else if (index % 2 == 1) EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.03f));

            // La fila entera lleva a editar el item. La tab decia que estaba mal y no llevaba a
            // arreglarlo: Ping abre el Project, que no es donde se edita.
            var clickArea = new Rect(rect.x, rect.y, rect.width - 56f, rect.height);
            if (GUI.Button(clickArea, GUIContent.none, GUIStyle.none) && m.Asset != null)
            {
                var target = m.Asset;
                EditorApplication.delayCall += () =>
                {
                    SelectAsset(target);
                    ActivateTab("Graph");
                };
            }

            float x = rect.x + 4f;
            EditorGUI.DrawRect(new Rect(x, rect.y + 4f, 4f, 9f), RarityPalette.BodyColor(m.Rarity));
            x += 10f;

            float free = rect.width - (x - rect.x) - 56f;
            float wName = Mathf.Max(120f, free * 0.32f);
            float wFamily = Mathf.Max(80f, free * 0.20f);
            float wPrice = Mathf.Max(90f, free * 0.18f);
            float wTrigger = Mathf.Max(90f, free - wName - wFamily - wPrice);

            GUI.Label(new Rect(x, rect.y, wName, rect.height), LabelOf(m.Asset));
            x += wName;

            var family = string.IsNullOrEmpty(m.FamilyId)
                ? "—"
                : $"{m.FamilyId} · {(m.Asset != null ? m.Asset.VariantIndex : 0)}";
            GUI.Label(new Rect(x, rect.y, wFamily, rect.height), family, EditorStyles.miniLabel);
            x += wFamily;

            DrawPriceCell(new Rect(x, rect.y, wPrice, rect.height), m);
            x += wPrice;

            GUI.Label(new Rect(x, rect.y, wTrigger, rect.height), TriggerSummary(m), EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(m.Asset == null))
            {
                if (GUI.Button(new Rect(rect.xMax - 52f, rect.y, 48f, 16f), "Ping", EditorStyles.miniButton))
                    EditorGUIUtility.PingObject(m.Asset);
            }
        }

        /// <summary>
        /// Precio real y el del GDD en una celda, y en rojo cuando se apartan.
        /// </summary>
        /// <remarks>
        /// Antes eran dos columnas de numeros sueltos y comparar era trabajo del ojo. La comparacion
        /// ES el dato: un precio solo no dice nada sin el tier al lado.
        /// </remarks>
        void DrawPriceCell(Rect rect, ItemQuery.ItemMetrics m)
        {
            if (m.PriceIsFallback)
            {
                GUI.Label(rect, "sin precio", _metricsFallbackPriceStyle);
                return;
            }

            bool off = m.GddBasePrice > 0
                       && Mathf.Abs(m.Price - m.GddBasePrice) / (float)m.GddBasePrice > _metricsDeviationThresholdPct;

            var prev = GUI.color;
            if (off) GUI.color = new Color(1f, 0.65f, 0.55f);
            GUI.Label(rect, m.Price == m.GddBasePrice ? m.Price.ToString() : $"{m.Price}  /  {m.GddBasePrice}");
            GUI.color = prev;
        }

        /// <summary>Cuando dispara, en una linea. El detalle completo esta en el grafo del item.</summary>
        static string TriggerSummary(ItemQuery.ItemMetrics m)
        {
            var combos = m.ComboIds.Count > 0
                ? string.Join(", ", m.ComboIds.Select(id =>
                    id == ItemQuery.AnyComboSentinel ? "cualquier combo" : id.Replace("combo.", "")))
                : null;

            if (combos != null) return combos;
            if (m.TriggerEvents.Count > 0) return string.Join(", ", m.TriggerEvents);
            return "—";
        }

        static int MetricsRarityOrderOf(string rarityLabel)
        {
            for (var i = 0; i < MetricsRarityOrder.Length; i++)
                if (RarityPalette.DisplayName(MetricsRarityOrder[i]) == rarityLabel) return i;
            return MetricsRarityOrder.Length;
        }
    }
}
