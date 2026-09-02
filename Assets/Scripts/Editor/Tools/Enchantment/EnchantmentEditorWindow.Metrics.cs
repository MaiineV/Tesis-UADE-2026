using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Upgrades.Dice;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enchantment
{
    /// <summary>
    /// Tab "Metrics" — la vista de diseño sobre el catálogo entero de encantamientos, no sobre el
    /// asset que editan Graph/Raw Data. Espejo de la tab de items con el eje económico cambiado:
    /// acá no hay precio por asset (el costo del altar es global en <c>EnchantmentConfigSO</c>) —
    /// las columnas de balance son el <b>peso del pool</b> y el <b>piso mínimo</b>.
    /// </summary>
    /// <remarks>
    /// Read-only por construcción: este archivo nunca llama <c>Undo.RecordObject</c> ni
    /// <c>EditorUtility.SetDirty</c> — solo queries de <see cref="EnchantmentQuery"/>, selección y
    /// <see cref="EditorGUIUtility.PingObject"/>. Los hallazgos salen de <c>HealthFindings</c>
    /// (cacheados en el archivo principal), así el panel del asset y esta tab comparten una sola
    /// pasada de disco.
    /// </remarks>
    public sealed partial class EnchantmentEditorWindow
    {
        /// <summary>Un filtro puesto al clickear una barra de distribución.</summary>
        readonly struct MetricsFilter
        {
            public string Label { get; }
            public Func<EnchantmentQuery.EnchantmentMetrics, bool> Predicate { get; }
            public bool Active => Predicate != null;

            public MetricsFilter(string label, Func<EnchantmentQuery.EnchantmentMetrics, bool> predicate)
            {
                Label = label;
                Predicate = predicate;
            }
        }

        IReadOnlyList<EnchantmentQuery.EnchantmentMetrics> _metricsCache;
        MetricsFilter _metricsFilter;
        string _metricsSearch = string.Empty;

        bool _metricsShowHealth = true;
        bool _metricsShowTable = true;
        bool _metricsShowDistribution = true;

        Vector2 _metricsScroll;
        GUIStyle _metricsSectionHeaderStyle;
        GUIStyle _metricsStatusStyle;
        GUIStyle _metricsMutedStyle;

        [BlockEditorTab("Metrics", 20)]
        void DrawMetricsTab()
        {
            EnsureMetricsStyles();
            if (_metricsCache == null) RefreshMetrics();

            DrawMetricsToolbar();
            DrawMetricsStatusStrip();

            _metricsScroll = EditorGUILayout.BeginScrollView(_metricsScroll);

            // Salud primero: es lo único accionable — las tablas no piden ninguna decisión.
            var findings = HealthFindings;
            if (MetricsSection("Problemas", findings.Count, ref _metricsShowHealth)) DrawMetricsHealth(findings);
            if (MetricsSection("Catálogo", _metricsCache.Count, ref _metricsShowTable)) DrawMetricsTable();
            if (MetricsSection("Distribución", 0, ref _metricsShowDistribution)) DrawMetricsDistribution();

            EditorGUILayout.EndScrollView();
        }

        void RefreshMetrics()
        {
            // El overload sin argumento reescanea el proyecto entero (FindAssets + carga de cada
            // EnchantmentSO). Llamarlo desde el dibujo sería un escaneo completo POR REPAINT.
            _metricsCache = EnchantmentQuery.GetMetrics();
        }

        /// <summary>La lista cambió en disco: la próxima vez que se dibuje la tab se recalcula.</summary>
        partial void OnMetricsAssetsRefreshed()
        {
            _metricsCache = null;
        }

        void EnsureMetricsStyles()
        {
            _metricsSectionHeaderStyle ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            _metricsStatusStyle ??= new GUIStyle(EditorStyles.boldLabel)
            { alignment = TextAnchor.MiddleLeft, fontSize = 12 };
            _metricsMutedStyle ??= new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Italic };
        }

        // ============================ Toolbar + status ============================

        void DrawMetricsToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                RefreshMetrics();
                // El botón existe para releer disco; los hallazgos comparten esa foto.
                InvalidateHealthFindings();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// El titular: cuántos hay y si algo está mal, sin tener que leer nada más. Se tiñe de
        /// rojo cuando hay problemas.
        /// </summary>
        void DrawMetricsStatusStrip()
        {
            int problems = HealthFindings.Count;
            int inPool = 0, cursed = 0;
            foreach (var m in _metricsCache)
            {
                if (m.InPool) inPool++;
                if (m.IsCursed) cursed++;
            }

            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(26f));
            EditorGUI.DrawRect(rect, problems == 0
                ? new Color(0.20f, 0.28f, 0.22f)
                : new Color(0.30f, 0.20f, 0.18f));

            var text = $"{_metricsCache.Count} encantamientos   ·   {inPool} en el pool   ·   " +
                       $"{cursed} malditos   ·   " +
                       (problems == 0 ? "sin problemas" : $"{problems} problemas");

            GUI.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height), text, _metricsStatusStyle);
        }

        /// <summary>Cabecera de sección con su cuenta al costado. <paramref name="count"/> 0 = sin contador.</summary>
        bool MetricsSection(string title, int count, ref bool expanded)
        {
            EditorGUILayout.Space(8);
            var header = count > 0 ? $"{title}   ({count})" : title;
            bool next = EditorGUILayout.Foldout(expanded, header, true, _metricsSectionHeaderStyle);
            if (next != expanded) expanded = next;

            var line = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1f));
            EditorGUI.DrawRect(line, new Color(0.35f, 0.35f, 0.35f));
            EditorGUILayout.Space(3);
            return expanded;
        }

        // ============================ Salud del catálogo ============================

        /// <summary>
        /// Los hallazgos, más severos arriba: filas de una línea con ícono y barra de severidad,
        /// barribles de un vistazo, con Ping para saltar al asset.
        /// </summary>
        void DrawMetricsHealth(IReadOnlyList<EnchantmentQuery.CatalogFinding> findings)
        {
            if (findings.Count == 0)
            {
                EditorGUILayout.LabelField("Sin hallazgos — catálogo limpio.", EditorStyles.miniLabel);
                return;
            }

            var ordered = findings.OrderByDescending(f => f.Severity).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                var finding = ordered[i];
                var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(20f));
                if (i % 2 == 1) EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.03f));

                EditorGUI.DrawRect(
                    new Rect(rect.x, rect.y + 2f, 3f, rect.height - 4f),
                    MetricsSeverityColor(finding.Severity));

                var icon = MetricsSeverityIcon(finding.Severity);
                if (icon != null)
                    GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, 16f, 16f), icon);

                GUI.Label(new Rect(rect.x + 28f, rect.y, rect.width - 86f, rect.height),
                          finding.Message, EditorStyles.miniLabel);

                using (new EditorGUI.DisabledScope(finding.Asset == null))
                {
                    if (GUI.Button(new Rect(rect.xMax - 52f, rect.y + 1f, 48f, 17f), "Ping", EditorStyles.miniButton))
                        EditorGUIUtility.PingObject(finding.Asset);
                }
            }
        }

        static Color MetricsSeverityColor(EnchantmentQuery.FindingSeverity severity)
        {
            switch (severity)
            {
                case EnchantmentQuery.FindingSeverity.Error: return new Color(0.80f, 0.30f, 0.25f);
                case EnchantmentQuery.FindingSeverity.Warning: return new Color(0.85f, 0.68f, 0.30f);
                default: return new Color(0.45f, 0.65f, 0.85f);
            }
        }

        static GUIContent MetricsSeverityIcon(EnchantmentQuery.FindingSeverity severity)
        {
            switch (severity)
            {
                case EnchantmentQuery.FindingSeverity.Error:
                    return EditorGUIUtility.IconContent("console.erroricon.sml");
                case EnchantmentQuery.FindingSeverity.Warning:
                    return EditorGUIUtility.IconContent("console.warnicon.sml");
                default:
                    return EditorGUIUtility.IconContent("console.infoicon.sml");
            }
        }

        // ============================ Distribución ============================

        /// <summary>
        /// Cuántos caen en cada categoría, con barras del color de la categoría. La barra filtra
        /// la tabla: convierte el gráfico en navegación en vez de dato suelto.
        /// </summary>
        void DrawMetricsDistribution()
        {
            var counts = new Dictionary<EnchantmentCategory, int>();
            foreach (var m in _metricsCache)
                counts[m.Category] = counts.TryGetValue(m.Category, out int c) ? c + 1 : 1;

            if (counts.Count == 0)
            {
                EditorGUILayout.LabelField("—", EditorStyles.miniLabel);
                return;
            }

            int max = 1;
            foreach (var kv in counts) if (kv.Value > max) max = kv.Value;

            foreach (var kv in counts.OrderBy(kv => (int)kv.Key))
            {
                var category = kv.Key;
                int count = kv.Value;

                var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(16f));
                float labelW = Mathf.Min(120f, rect.width * 0.30f);

                EditorGUI.DrawRect(
                    new Rect(rect.x, rect.y + 4f, 3f, 9f), EnchantmentPalette.CategoryColor(category));
                GUI.Label(new Rect(rect.x + 8f, rect.y, labelW, rect.height),
                          CategoryLabelOf(category), EditorStyles.miniLabel);

                float trackX = rect.x + labelW + 8f;
                float trackW = Mathf.Max(20f, rect.width - labelW - 48f);
                EditorGUI.DrawRect(new Rect(trackX, rect.y + 4f, trackW, 8f), new Color(1f, 1f, 1f, 0.06f));

                Color fill = EnchantmentPalette.CategoryColor(category);
                fill.a = 0.85f;
                EditorGUI.DrawRect(new Rect(trackX, rect.y + 4f, trackW * (count / (float)max), 8f), fill);

                GUI.Label(new Rect(rect.xMax - 36f, rect.y, 32f, rect.height),
                          count.ToString(), EditorStyles.miniLabel);

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    _metricsFilter = new MetricsFilter(
                        "categoría: " + CategoryLabelOf(category), m => m.Category == category);
                    _metricsShowTable = true;
                }
            }
        }

        // ============================ Tabla comparable ============================

        // Reparto proporcional del ancho libre entre las 8 columnas, con mínimos para paneles
        // angostos. Compartido entre cabecera y filas para que queden alineadas.
        static float[] MetricsColumnWidths(float free)
        {
            var w = new float[8];
            w[0] = Mathf.Max(110f, free * 0.20f); // Nombre
            w[1] = Mathf.Max(70f, free * 0.11f);  // Categoría
            w[2] = Mathf.Max(44f, free * 0.06f);  // Peso
            w[3] = Mathf.Max(44f, free * 0.06f);  // MinFloor
            w[4] = Mathf.Max(90f, free * 0.19f);  // Eventos
            w[5] = Mathf.Max(90f, free * 0.19f);  // Combos
            w[6] = Mathf.Max(40f, free * 0.06f);  // FaceFilter
            w[7] = Mathf.Max(40f, free - w[0] - w[1] - w[2] - w[3] - w[4] - w[5] - w[6]); // Caps
            return w;
        }

        void DrawMetricsTable()
        {
            DrawMetricsTableControls();

            var rows = FilteredMetricsRows();
            if (rows.Count == 0)
            {
                EditorGUILayout.LabelField("Ningún encantamiento coincide.", EditorStyles.miniLabel);
                return;
            }

            DrawMetricsColumnHeaders();

            // Agrupado por categoría, en el orden del enum — el mismo orden que la lista de la
            // izquierda, para que las dos vistas cuenten la misma historia.
            var groups = rows
                .GroupBy(m => m.Category)
                .OrderBy(g => (int)g.Key);

            int row = 0;
            foreach (var group in groups)
            {
                EditorGUILayout.Space(4);

                var headerRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(16f));
                EditorGUI.DrawRect(
                    new Rect(headerRect.x, headerRect.y + 3f, 3f, 10f),
                    EnchantmentPalette.CategoryColor(group.Key));
                GUI.Label(new Rect(headerRect.x + 8f, headerRect.y, headerRect.width - 8f, headerRect.height),
                          CategoryLabelOf(group.Key).ToUpperInvariant(), EditorStyles.miniBoldLabel);

                foreach (var m in group.OrderBy(m => LabelOf(m.Asset), StringComparer.OrdinalIgnoreCase))
                    DrawMetricsRow(m, row++);
            }
        }

        /// <summary>Búsqueda y el chip del filtro que puso un clic en una barra.</summary>
        void DrawMetricsTableControls()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _metricsSearch = EditorGUILayout.TextField(
                    _metricsSearch, EditorStyles.toolbarSearchField, GUILayout.MaxWidth(180f));

                GUILayout.FlexibleSpace();

                if (_metricsFilter.Active)
                {
                    if (GUILayout.Button($"✕  {_metricsFilter.Label}", EditorStyles.miniButton))
                        _metricsFilter = default;
                }
            }
        }

        List<EnchantmentQuery.EnchantmentMetrics> FilteredMetricsRows()
        {
            var result = new List<EnchantmentQuery.EnchantmentMetrics>();
            foreach (var m in _metricsCache)
            {
                if (_metricsFilter.Active && !_metricsFilter.Predicate(m)) continue;
                if (!string.IsNullOrEmpty(_metricsSearch)
                    && LabelOf(m.Asset).IndexOf(_metricsSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                result.Add(m);
            }
            return result;
        }

        void DrawMetricsColumnHeaders()
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(18f));
            EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.05f));

            float x = rect.x + 14f;
            var w = MetricsColumnWidths(rect.width - 14f - 56f);
            var titles = new[]
                { "Nombre", "Categoría", "Peso", "Piso", "Eventos", "Combos", "Caras", "Sin cablear" };

            for (int i = 0; i < titles.Length; i++)
            {
                GUI.Label(new Rect(x, rect.y, w[i], rect.height), titles[i], EditorStyles.miniBoldLabel);
                x += w[i];
            }
        }

        void DrawMetricsRow(EnchantmentQuery.EnchantmentMetrics m, int index)
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(17f));

            bool isSelected = m.Asset != null && m.Asset == SelectedAsset;
            if (isSelected) EditorGUI.DrawRect(rect, new Color(0.45f, 0.75f, 1f, 0.18f));
            else if (index % 2 == 1) EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.03f));

            // La fila entera lleva a editar el asset — la tab dice qué está raro, la selección es
            // donde se arregla. Diferido: cambiar la selección re-lay-outea el panel en medio de
            // la pasada IMGUI de esta tabla.
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
            EditorGUI.DrawRect(new Rect(x, rect.y + 4f, 4f, 9f), EnchantmentPalette.CategoryColor(m.Category));
            x += 10f;

            var w = MetricsColumnWidths(rect.width - (x - rect.x) - 56f);

            GUI.Label(new Rect(x, rect.y, w[0], rect.height), LabelOf(m.Asset));
            x += w[0];

            var prevColor = GUI.color;
            GUI.color = EnchantmentPalette.CategoryColor(m.Category);
            GUI.Label(new Rect(x, rect.y, w[1], rect.height), CategoryLabelOf(m.Category), EditorStyles.miniLabel);
            GUI.color = prevColor;
            x += w[1];

            if (m.InPool)
                GUI.Label(new Rect(x, rect.y, w[2], rect.height), m.Weight.ToString("0.##"), EditorStyles.miniLabel);
            else
                GUI.Label(new Rect(x, rect.y, w[2], rect.height), "fuera", _metricsMutedStyle);
            x += w[2];

            GUI.Label(new Rect(x, rect.y, w[3], rect.height),
                      m.InPool ? m.MinFloorDepth.ToString() : "—", EditorStyles.miniLabel);
            x += w[3];

            GUI.Label(new Rect(x, rect.y, w[4], rect.height), MetricsEventsSummary(m), EditorStyles.miniLabel);
            x += w[4];

            GUI.Label(new Rect(x, rect.y, w[5], rect.height), MetricsCombosSummary(m), EditorStyles.miniLabel);
            x += w[5];

            GUI.Label(new Rect(x, rect.y, w[6], rect.height),
                      m.HasFaceFilter ? "Sí" : "—", EditorStyles.miniLabel);
            x += w[6];

            GUI.Label(new Rect(x, rect.y, w[7], rect.height),
                      m.UnwiredCapabilities > 0 ? m.UnwiredCapabilities.ToString() : "—",
                      m.UnwiredCapabilities > 0 ? EditorStyles.boldLabel : EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(m.Asset == null))
            {
                if (GUI.Button(new Rect(rect.xMax - 52f, rect.y, 48f, 16f), "Ping", EditorStyles.miniButton))
                    EditorGUIUtility.PingObject(m.Asset);
            }
        }

        static string MetricsEventsSummary(EnchantmentQuery.EnchantmentMetrics m) =>
            m.TriggerEvents == null || m.TriggerEvents.Count == 0
                ? "—"
                : string.Join(", ", m.TriggerEvents);

        /// <summary>Combos que gatean los triggers, en una línea. El detalle completo está en el grafo.</summary>
        static string MetricsCombosSummary(EnchantmentQuery.EnchantmentMetrics m)
        {
            if (m.ComboIds == null || m.ComboIds.Count == 0) return "—";
            return string.Join(", ", m.ComboIds.Select(id =>
                id == EnchantmentQuery.AnyComboSentinel ? "cualquiera" : id.Replace("combo.", "")));
        }
    }
}
