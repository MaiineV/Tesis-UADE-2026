using System;
using System.Collections.Generic;
using Rollgeon.Combos;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>
    /// Single editor window with a left enemy browser and a tabbed right panel:
    /// "Ficha" (Odin-driven IMGUI) and "Árbol de IA" (GraphView).
    /// Pattern mirrors HeroClassEditorWindow but extends it with UIToolkit + GraphView.
    /// La ventana es dueña de los diagnósticos: valida cada ficha fuera del draw (al cambiar
    /// el proyecto, al deshacer o al editar) y se los empuja al panel y a la lista.
    /// </summary>
    public sealed class EnemyEditorWindow : EditorWindow
    {
        const float LEFT_WIDTH = 240f;
        static readonly string[] KindFilters = { "Todos", "Jefes", "Regulares" };

        readonly List<EnemyDataSO> _enemies = new List<EnemyDataSO>();
        readonly Dictionary<EnemyDataSO, List<EnemyIssue>> _issues = new Dictionary<EnemyDataSO, List<EnemyIssue>>();
        readonly Dictionary<EnemyDataSO, EnemyTreeSummary> _summaries = new Dictionary<EnemyDataSO, EnemyTreeSummary>();
        ComboCatalogSO _comboCatalog;
        EnemyDataSO _selected;
        EnemyDataPanel _dataPanel;
        AIDecisionTreeGraphView _graphView;
        VisualElement _treeTabContainer; // wraps graph + inspector horizontally
        IMGUIContainer _leftPanel;
        IMGUIContainer _dataPanelContainer;
        VisualElement _rightHost;
        Button _tabData;
        Button _tabTree;
        int _tabIndex; // 0 = data, 1 = tree

        string _search = string.Empty;
        int _kindFilter;
        int _archetypeFilter; // 0 = todos; i > 0 = EnemyArchetype (i - 1)
        string[] _archetypeFilters;
        Vector2 _leftScroll;

        [MenuItem("Tools/Enemy Editor")]
        static void Open()
        {
            var w = GetWindow<EnemyEditorWindow>("Editor de enemigos");
            w.minSize = new Vector2(960f, 540f);
        }

        void OnEnable()
        {
            var labels = EnemyEditorVocab.LabelsOf<EnemyArchetype>();
            _archetypeFilters = new string[labels.Length + 1];
            _archetypeFilters[0] = "Todos los arquetipos";
            Array.Copy(labels, 0, _archetypeFilters, 1, labels.Length);

            BuildUI();
            RefreshList();
            Undo.undoRedoPerformed += OnUndo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            if (_dataPanel != null) _dataPanel.Changed -= OnPanelChanged;
            _dataPanel?.Dispose();
            _graphView?.DisposeViews();
        }

        void OnProjectChange()
        {
            RefreshList();
            _leftPanel?.MarkDirtyRepaint();
        }

        void OnUndo()
        {
            _dataPanel?.RebuildTree();
            if (_tabIndex == 1 && _selected != null) _graphView.Bind(_selected);
            if (_selected != null) RecomputeOne(_selected);
            _leftPanel?.MarkDirtyRepaint();
        }

        void OnPanelChanged()
        {
            if (_selected != null) RecomputeOne(_selected);
            _leftPanel?.MarkDirtyRepaint();
        }

        // ---- UI construction ---------------------------------------------

        void BuildUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;

            // Left
            _leftPanel = new IMGUIContainer(DrawLeft) { style = { width = LEFT_WIDTH, marginRight = 4 } };
            root.Add(_leftPanel);

            // Right side: column with tab bar on top + content
            var rightCol = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column } };
            root.Add(rightCol);

            var tabBar = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 28, marginBottom = 4 } };
            _tabData = MakeTab("Ficha", () => SwitchTab(0));
            _tabTree = MakeTab("Árbol de IA", () => SwitchTab(1));
            tabBar.Add(_tabData);
            tabBar.Add(_tabTree);
            rightCol.Add(tabBar);

            _rightHost = new VisualElement { style = { flexGrow = 1 } };
            rightCol.Add(_rightHost);

            _dataPanel = new EnemyDataPanel();
            _dataPanel.Changed += OnPanelChanged;
            _dataPanelContainer = new IMGUIContainer(_dataPanel.Draw) { style = { flexGrow = 1 } };

            _graphView = new AIDecisionTreeGraphView(this) { style = { flexGrow = 1 } };
            _treeTabContainer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexGrow = 1 },
            };
            _treeTabContainer.Add(_graphView);
            _treeTabContainer.Add(BuildResizeGrip(_graphView.Inspector.Root));
            _treeTabContainer.Add(_graphView.Inspector.Root);

            SwitchTab(0);
        }

        /// <summary>
        /// 6-px wide draggable splitter that resizes the inspector panel to the right.
        /// Mouse capture is used so the drag continues even if the cursor leaves the grip.
        /// </summary>
        VisualElement BuildResizeGrip(VisualElement target)
        {
            var grip = new VisualElement
            {
                style =
                {
                    width = 6,
                    flexShrink = 0,
                    backgroundColor = new Color(0.12f, 0.12f, 0.12f),
                },
            };
            // Hover hint
            grip.RegisterCallback<MouseEnterEvent>(_ => grip.style.backgroundColor = new Color(0.35f, 0.55f, 0.85f));
            grip.RegisterCallback<MouseLeaveEvent>(_ => grip.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f));

            bool dragging = false;
            float startX = 0f;
            float startWidth = 0f;

            grip.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                dragging = true;
                startX = evt.mousePosition.x;
                startWidth = target.resolvedStyle.width;
                grip.CaptureMouse();
                evt.StopPropagation();
            });
            grip.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!dragging) return;
                float delta = evt.mousePosition.x - startX;
                // Dragging right shrinks panel, left grows it (panel sits on the right edge).
                float w = Mathf.Clamp(startWidth - delta, 220f, 800f);
                target.style.width = w;
                evt.StopPropagation();
            });
            grip.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (!dragging) return;
                dragging = false;
                grip.ReleaseMouse();
                evt.StopPropagation();
            });

            return grip;
        }

        Button MakeTab(string label, System.Action onClick)
        {
            var b = new Button(onClick) { text = label };
            b.style.flexGrow = 1;
            b.style.height = 26;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            return b;
        }

        void SwitchTab(int index)
        {
            _tabIndex = index;
            _rightHost.Clear();
            _rightHost.Add(index == 0 ? (VisualElement)_dataPanelContainer : _treeTabContainer);

            _tabData.style.backgroundColor = index == 0 ? new Color(0.30f, 0.40f, 0.55f) : new Color(0.20f, 0.20f, 0.20f);
            _tabTree.style.backgroundColor = index == 1 ? new Color(0.30f, 0.40f, 0.55f) : new Color(0.20f, 0.20f, 0.20f);

            if (index == 1 && _selected != null) _graphView.Bind(_selected);
            // Volver a la ficha después de editar el árbol: los avisos del árbol cambian.
            if (index == 0 && _selected != null) RecomputeOne(_selected);
        }

        // ---- Left list ----------------------------------------------------

        void DrawLeft()
        {
            EditorGUILayout.LabelField("Enemigos", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string next = GUILayout.TextField(_search, EditorStyles.toolbarSearchField);
                if (next != _search) _search = next;
            }
            _kindFilter = EditorGUILayout.Popup(_kindFilter, KindFilters);
            _archetypeFilter = EditorGUILayout.Popup(_archetypeFilter, _archetypeFilters);
            EditorGUILayout.Space(4);

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            if (_enemies.Count == 0)
                EditorGUILayout.HelpBox("No hay EnemyDataSO en el proyecto.", MessageType.Info);

            int shown = 0;
            foreach (var e in _enemies)
            {
                if (!Matches(e)) continue;
                shown++;
                bool isSel = e == _selected;
                var prev = GUI.backgroundColor;
                if (isSel) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);

                if (GUILayout.Button(RowContent(e), GUILayout.Height(26f)))
                    Select(e);

                GUI.backgroundColor = prev;
            }
            if (shown == 0 && _enemies.Count > 0)
                EditorGUILayout.LabelField("Ningún enemigo coincide con el filtro.", EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(8);

            if (GUILayout.Button("+ Nuevo enemigo", GUILayout.Height(24f)))
            {
                var so = EnemyAssetOps.CreateNew();
                RefreshList();
                Select(so);
            }

            using (new EditorGUI.DisabledScope(_selected == null))
            {
                if (GUILayout.Button("Duplicar", GUILayout.Height(22f)))
                {
                    var copy = EnemyAssetOps.Duplicate(_selected);
                    RefreshList();
                    if (copy != null) Select(copy);
                }
                if (GUILayout.Button("Mostrar en Project", GUILayout.Height(20f)))
                    EditorGUIUtility.PingObject(_selected);
            }
        }

        static string LabelOf(EnemyDataSO e) => string.IsNullOrEmpty(e.DisplayName) ? e.name : e.DisplayName;

        GUIContent RowContent(EnemyDataSO e)
        {
            string text = LabelOf(e);
            var archetype = e.Design != null ? e.Design.Archetype : EnemyArchetype.Unspecified;
            string chip = EnemyEditorVocab.Chip(archetype);
            if (!string.IsNullOrEmpty(chip)) text += $"  [{chip}]";
            if (BossBuilderRegistry.TryGetBuilder(e, out _)) text += "  ⚙";

            string tooltip = null;
            if (_issues.TryGetValue(e, out var issues) && issues.Count > 0)
            {
                int errors = EnemyDataValidator.Count(issues, EnemyIssueSeverity.Error);
                int warnings = EnemyDataValidator.Count(issues, EnemyIssueSeverity.Warning);
                if (errors > 0) text += $"  ✕{errors}";
                if (warnings > 0) text += $"  ⚠{warnings}";
                var lines = new List<string>();
                for (int i = 0; i < issues.Count && i < 3; i++) lines.Add(issues[i].ToString());
                if (issues.Count > 3) lines.Add($"… y {issues.Count - 3} más");
                tooltip = string.Join("\n", lines);
            }
            return new GUIContent(text, tooltip);
        }

        bool Matches(EnemyDataSO e)
        {
            if (_kindFilter == 1 && !e.IsBoss) return false;
            if (_kindFilter == 2 && e.IsBoss) return false;

            var archetype = e.Design != null ? e.Design.Archetype : EnemyArchetype.Unspecified;
            if (_archetypeFilter > 0 && (int)archetype != _archetypeFilter - 1) return false;

            if (string.IsNullOrWhiteSpace(_search)) return true;
            string q = _search.Trim();
            return Contains(e.DisplayName, q) || Contains(e.name, q) || Contains(e.EntityId, q)
                || Contains(EnemyEditorVocab.LabelOf(archetype), q);
        }

        static bool Contains(string haystack, string needle)
            => !string.IsNullOrEmpty(haystack) && haystack.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0;

        void Select(EnemyDataSO so)
        {
            if (_selected == so) return;
            _selected = so;
            _dataPanel.Bind(so);
            PushDiagnostics(so);
            if (_tabIndex == 1) _graphView.Bind(so);
            _dataPanelContainer.MarkDirtyRepaint();
        }

        void RefreshList()
        {
            _enemies.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDataSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
                if (asset != null) _enemies.Add(asset);
            }
            _enemies.Sort((a, b) => string.Compare(LabelOf(a), LabelOf(b), StringComparison.CurrentCultureIgnoreCase));

            if (_selected != null && !_enemies.Contains(_selected))
            {
                _selected = null;
                _dataPanel?.Bind(null);
                _graphView?.Bind(null);
            }

            RecomputeAll();
        }

        // ---- diagnostics ---------------------------------------------------

        void RecomputeAll()
        {
            _comboCatalog = EnemyDataValidator.FindComboCatalog();
            _issues.Clear();
            _summaries.Clear();
            foreach (var e in _enemies) Compute(e);
            if (_selected != null) PushDiagnostics(_selected);
        }

        void RecomputeOne(EnemyDataSO so)
        {
            if (so == null) return;
            Compute(so);
            if (so == _selected) PushDiagnostics(so);
        }

        void Compute(EnemyDataSO so)
        {
            var summary = EnemyTreeSummary.Build(so);
            _summaries[so] = summary;
            _issues[so] = EnemyDataValidator.Validate(so, _enemies, _comboCatalog, summary);
        }

        void PushDiagnostics(EnemyDataSO so)
        {
            if (so == null || _dataPanel == null) return;
            _issues.TryGetValue(so, out var issues);
            _summaries.TryGetValue(so, out var summary);
            _dataPanel.SetDiagnostics(issues, summary);
            _dataPanelContainer?.MarkDirtyRepaint();
        }
    }
}
