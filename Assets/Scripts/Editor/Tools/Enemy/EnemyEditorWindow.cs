using System.Collections.Generic;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>
    /// Single editor window with a left enemy browser and a tabbed right panel:
    /// "Enemy Data" (Odin-driven IMGUI) and "AI Tree" (GraphView).
    /// Pattern mirrors HeroClassEditorWindow but extends it with UIToolkit + GraphView.
    /// </summary>
    public sealed class EnemyEditorWindow : EditorWindow
    {
        const float LEFT_WIDTH = 220f;

        readonly List<EnemyDataSO> _enemies = new List<EnemyDataSO>();
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

        Vector2 _leftScroll;

        [MenuItem("Tools/Enemy Editor")]
        static void Open()
        {
            var w = GetWindow<EnemyEditorWindow>("Enemy Editor");
            w.minSize = new Vector2(960f, 540f);
        }

        void OnEnable()
        {
            BuildUI();
            RefreshList();
            Undo.undoRedoPerformed += OnUndo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
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
            _tabData = MakeTab("Enemy Data", () => SwitchTab(0));
            _tabTree = MakeTab("AI Tree",    () => SwitchTab(1));
            tabBar.Add(_tabData);
            tabBar.Add(_tabTree);
            rightCol.Add(tabBar);

            _rightHost = new VisualElement { style = { flexGrow = 1 } };
            rightCol.Add(_rightHost);

            _dataPanel = new EnemyDataPanel();
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
        }

        // ---- Left list ----------------------------------------------------

        void DrawLeft()
        {
            EditorGUILayout.LabelField("Enemies", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            if (_enemies.Count == 0)
                EditorGUILayout.HelpBox("No EnemyDataSO assets found.", MessageType.Info);

            foreach (var e in _enemies)
            {
                bool isSel = e == _selected;
                var prev = GUI.backgroundColor;
                if (isSel) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);

                string label = string.IsNullOrEmpty(e.DisplayName) ? e.name : e.DisplayName;
                if (GUILayout.Button(label, GUILayout.Height(26f)))
                    Select(e);

                GUI.backgroundColor = prev;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space(8);

            if (GUILayout.Button("+ Create Enemy", GUILayout.Height(24f)))
                CreateNewEnemy();

            if (_selected != null)
            {
                EditorGUILayout.Space(4);
                if (GUILayout.Button("Ping in Project", GUILayout.Height(20f)))
                    EditorGUIUtility.PingObject(_selected);
            }
        }

        void Select(EnemyDataSO so)
        {
            if (_selected == so) return;
            _selected = so;
            _dataPanel.Bind(so);
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

            if (_selected != null && !_enemies.Contains(_selected))
            {
                _selected = null;
                _dataPanel?.Bind(null);
                _graphView?.Bind(null);
            }
        }

        void CreateNewEnemy()
        {
            string folder = "Assets/Rollgeon/Enemies";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                System.IO.Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/ED_NewEnemy.asset");
            var so = ScriptableObject.CreateInstance<EnemyDataSO>();
            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();

            RefreshList();
            Select(so);

            PingCatalogToHelpRegistration();
        }

        /// <summary>
        /// EnemyCatalogSO uses Odin's <c>[OdinSerialize]</c> on a private list, which is
        /// invisible to <see cref="SerializedObject"/>. Auto-registering would require
        /// reflection that's brittle across schema changes — instead we ping the catalog so
        /// the user drags the new asset in via the standard inspector.
        /// </summary>
        void PingCatalogToHelpRegistration()
        {
            var guids = AssetDatabase.FindAssets("t:EnemyCatalogSO");
            if (guids.Length == 0) return;
            var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
            EditorGUIUtility.PingObject(catalog);
        }
    }
}
