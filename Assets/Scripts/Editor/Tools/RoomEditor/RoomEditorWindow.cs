using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Rollgeon.Dungeon.Components;
using Rollgeon.EditorTools;
using Rollgeon.Grid;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    public sealed partial class RoomEditorWindow : OdinEditorWindow
    {
        // ============================ Tab paths ============================

        private const string Tabs = "Tabs";
        private const string TabTool = "Tool & Shortcuts";
        private const string TabRoom = "Room & Info";
        private const string TabPalette = "Palette & Settings";
        private const string TabDoors = "Doors";
        private const string TabSpawn = "Spawn Points";

        private const string GTool = Tabs + "/" + TabTool + "/Tool";
        private const string GShortcuts = Tabs + "/" + TabTool + "/Shortcuts";
        private const string GRoom = Tabs + "/" + TabRoom + "/Room";
        private const string GInfo = Tabs + "/" + TabRoom + "/Info";
        private const string GPalette = Tabs + "/" + TabPalette + "/Palette";
        private const string GSettings = Tabs + "/" + TabPalette + "/Settings";
        private const string GDoors = Tabs + "/" + TabDoors + "/Doors";
        private const string GSpawnTool = Tabs + "/" + TabSpawn + "/Tool";
        private const string GSpawnSets = Tabs + "/" + TabSpawn + "/Sets";
        private const string GSpawnList = Tabs + "/" + TabSpawn + "/List";
        private const string GSpawnValidation = Tabs + "/" + TabSpawn + "/Validation";

        // ============================ Section tints (used by header drawers) ============================

        private const float RoomR = 0.45f, RoomG = 0.70f, RoomB = 0.95f;
        private const float PaletteR = 0.95f, PaletteG = 0.70f, PaletteB = 0.35f;
        private const float SettingsR = 0.45f, SettingsG = 0.85f, SettingsB = 0.45f;
        private const float ToolR = 0.95f, ToolG = 0.55f, ToolB = 0.75f;
        private const float InfoR = 0.95f, InfoG = 0.85f, InfoB = 0.35f;
        private const float ShortcutsR = 0.65f, ShortcutsG = 0.65f, ShortcutsB = 0.90f;

        private static GUIStyle _sectionHeaderStyle;
        private static GUIStyle SectionHeaderStyle
        {
            get
            {
                if (_sectionHeaderStyle == null)
                {
                    _sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        padding = new RectOffset(8, 0, 3, 0),
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = new Color(0.08f, 0.08f, 0.08f) },
                    };
                }
                return _sectionHeaderStyle;
            }
        }

        private static void DrawSectionHeader(string title, Color color)
        {
            var rect = EditorGUILayout.GetControlRect(false, 22f);
            EditorGUI.DrawRect(rect, color);
            GUI.Label(rect, title, SectionHeaderStyle);
        }

        private static void DrawRectBorder(Rect r, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
        }

        // Tab order sentinel moved to RoomEditorWindow.0Tabs.cs — that filename is
        // enumerated first alphabetically, which is the only way to guarantee Odin
        // encounters the tab declarations before any partial-class tab-specific field.

        // ============================ Tab 1 — Tool ============================

        [TabGroup(Tabs, TabTool), BoxGroup(GTool, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawToolSectionHeader() => DrawSectionHeader("Tool", new Color(ToolR, ToolG, ToolB));

        [TabGroup(Tabs, TabTool), BoxGroup(GTool, false), PropertyOrder(0), OnInspectorGUI]
        private void DrawToolActiveBigToggle()
        {
            EditorGUILayout.Space(6);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = _toolActive
                ? new Color(0.45f, 0.95f, 0.55f)
                : new Color(0.72f, 0.72f, 0.72f);
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 34f,
                alignment = TextAnchor.MiddleCenter,
            };
            var label = _toolActive
                ? "● TOOL ACTIVE  —  click to deactivate"
                : "○ TOOL INACTIVE  —  click to activate";
            if (GUILayout.Button(label, style))
            {
                _toolActive = !_toolActive;
                if (_toolActive) _spawnToolActive = false; // mutually exclusive with spawn paint
                Repaint();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prev;
            EditorGUILayout.Space(4);
        }

        [TabGroup(Tabs, TabTool), BoxGroup(GTool, false), PropertyOrder(1), EnableIf("_toolActive")]
        [Title("Editing", bold: true, horizontalLine: true)]
        [LabelText("Erase Mode (Shift+click also erases)")]
        [SerializeField] private bool _eraseMode;

        [TabGroup(Tabs, TabTool), BoxGroup(GTool, false), PropertyOrder(2), EnableIf("_toolActive")]
        [LabelText("Multi-Paint (drag to paint many)")]
        [SerializeField] private bool _multiPaint;

        [TabGroup(Tabs, TabTool), BoxGroup(GTool, false), PropertyOrder(3), EnableIf("_toolActive")]
        [LabelText("Scale Tile To Tile Size")]
        [SerializeField] private bool _scaleToTileSize;

        [TabGroup(Tabs, TabTool), BoxGroup(GTool, false), PropertyOrder(4), EnableIf("_toolActive")]
        [Title("Visuals", bold: true, horizontalLine: true)]
        [PropertySpace(SpaceBefore = 4)]
        [LabelText("Show Grid Plane")]
        [SerializeField] private bool _showGrid = true;

        [TabGroup(Tabs, TabTool), BoxGroup(GTool, false), PropertyOrder(5), EnableIf("_toolActive")]
        [LabelText("Show Ghost Preview")]
        [SerializeField] private bool _showGhost = true;

        [TabGroup(Tabs, TabTool), BoxGroup(GTool, false), PropertyOrder(6), EnableIf("_toolActive"), MinValue(1)]
        [LabelText("Grid Extent (cells)")]
        [SerializeField] private int _gridExtent = 12;

        [TabGroup(Tabs, TabTool), BoxGroup(GTool, false), PropertyOrder(7)]
        [Title("NavGraph", bold: true, horizontalLine: true)]
        [PropertySpace(SpaceBefore = 4)]
        [LabelText("Show NavGraph Overlay (hold G to peek)")]
        [OnValueChanged(nameof(SyncNavGraphOverlay))]
        [SerializeField] private bool _showNavGraphOverlay;

        // ============================ Tab 1 — Shortcuts ============================

        [TabGroup(Tabs, TabTool), BoxGroup(GShortcuts, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawShortcutsSectionHeader() => DrawSectionHeader("Shortcuts", new Color(ShortcutsR, ShortcutsG, ShortcutsB));

        [TabGroup(Tabs, TabTool), BoxGroup(GShortcuts, false), PropertyOrder(0), OnInspectorGUI]
        private void DrawShortcuts()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "ROTATION\n" +
                "    R           Rotate Y\n" +
                "    Shift + R   Rotate X\n" +
                "    Ctrl + R    Rotate Z\n" +
                "\n" +
                "LAYER\n" +
                "    Q / E       Layer  -1 / +1\n" +
                "\n" +
                "PAINT\n" +
                "    LMB         Place\n" +
                "    Shift+LMB   Erase\n" +
                "    Esc         Deselect\n" +
                "\n" +
                "VIEW\n" +
                "    G (hold)    Peek NavGraph overlay",
                MessageType.None);
            EditorGUILayout.Space(2);
        }

        // ============================ Tab 2 — Room ============================

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawRoomSectionHeader() => DrawSectionHeader("Room", new Color(RoomR, RoomG, RoomB));

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(0)]
        [Title("Load Room", bold: true, horizontalLine: true)]
        [LabelText("Existing Room"), ValueDropdown(nameof(GetRoomPrefabs))]
        [OnValueChanged(nameof(OpenSelectedRoom))]
        [SerializeField] private GameObject _selectedRoomPrefab;

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(1)]
        [ShowInInspector, ReadOnly, LabelText("Active Target")]
        private string ActiveTargetName => _target != null ? _target.gameObject.name : "(none)";

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(2)]
        [Title("Create Room", bold: true, horizontalLine: true)]
        [PropertySpace(SpaceBefore = 4)]
        [LabelText("Rooms Folder")]
        [SerializeField] private string _roomsFolder = "Assets/Prefabs/Rooms";

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(3)]
        [LabelText("New Room Name")]
        [SerializeField] private string _newRoomName = "NewRoom";

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(4)]
        [Button("Create Room", ButtonSizes.Medium), GUIColor(0.55f, 1f, 0.55f)]
        private void CreateRoom()
        {
            var name = (_newRoomName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                EditorUtility.DisplayDialog("Create Room", "Room name is required.", "OK");
                return;
            }
            EnsureFolderExists(_roomsFolder);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{_roomsFolder}/{name}.prefab");

            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            go.AddComponent<RoomLayout>();

            GameObject prefab = null;
            try { prefab = PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { Object.DestroyImmediate(go); }

            if (prefab != null)
            {
                _selectedRoomPrefab = prefab;
                OpenSelectedRoom();
            }
        }

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(5)]
        [Title("Rename Room", bold: true, horizontalLine: true)]
        [PropertySpace(SpaceBefore = 4)]
        [LabelText("Rename To")]
        [SerializeField] private string _renameTo;

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(6)]
        [Button("Rename Selected Room"), EnableIf("@_selectedRoomPrefab != null && !string.IsNullOrWhiteSpace(_renameTo)")]
        private void RenameSelectedRoom()
        {
            if (_selectedRoomPrefab == null || string.IsNullOrWhiteSpace(_renameTo)) return;
            var path = AssetDatabase.GetAssetPath(_selectedRoomPrefab);
            if (string.IsNullOrEmpty(path)) return;
            var error = AssetDatabase.RenameAsset(path, _renameTo.Trim());
            if (!string.IsNullOrEmpty(error))
            {
                EditorUtility.DisplayDialog("Rename Failed", error, "OK");
                return;
            }
            AssetDatabase.SaveAssets();
            _renameTo = string.Empty;
            Repaint();
        }

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(7)]
        [Title("Bake Room", bold: true, horizontalLine: true)]
        [PropertySpace(SpaceBefore = 4)]
        [Button("Auto-Populate Door Slots", ButtonSizes.Medium), EnableIf("@_target != null")]
        private void AutoPopulateDoorSlots()
        {
            if (_target == null) return;
            _target.AutoPopulateDoorSlots();
        }

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(8)]
        [Button("Bake NavGraph", ButtonSizes.Medium), EnableIf("@_target != null")]
        private void BakeNavGraph()
        {
            if (_target == null) return;
            Undo.RecordObject(_target, "Bake NavGraph");
            _target.NavGraph = NavGraphBaker.Bake(_target.gameObject, _target.BakeSettings);
            EditorUtility.SetDirty(_target);

            int nodeCount = _target.NavGraph.NodeCount;
            int edgeCount = _target.NavGraph.Edges?.Count ?? 0;

            var allMarkers = _target.GetComponentsInChildren<TileMarker>(true);
            int total = allMarkers.Length;
            int blockerMarkers = 0;
            int floorMarkers = 0;
            foreach (var m in allMarkers)
            {
                if (m.IsBlocker) blockerMarkers++;
                if (m.Type == TileType.Floor) floorMarkers++;
            }

            Debug.Log($"[NavGraphBaker] Baked {nodeCount} nodes, {edgeCount} edges from {total} tiles ({floorMarkers} floors, {blockerMarkers} blockers).");
        }

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(9)]
        [ShowInInspector, ReadOnly, LabelText("NavGraph")]
        [ShowIf("@_target != null && _target.NavGraph != null && !_target.NavGraph.IsEmpty")]
        private string NavGraphInfo =>
            _target != null && _target.NavGraph != null && !_target.NavGraph.IsEmpty
                ? $"{_target.NavGraph.NodeCount} nodes, {_target.NavGraph.Edges.Count} edges"
                : "—";

        [TabGroup(Tabs, TabRoom), BoxGroup(GRoom, false), PropertyOrder(10)]
        [Button("Bake Wall Occluders", ButtonSizes.Medium), EnableIf("@_target != null")]
        private void BakeWallOccluders()
        {
            if (_target == null) return;

            // Collapse every EnsureOccluder Undo into one named group so Ctrl+Z
            // reverts the whole bake in a single step.
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(WallOccluderOps.UndoLabel);

            int added = 0, updated = 0, skipped = 0;
            var markers = _target.GetComponentsInChildren<TileMarker>(includeInactive: true);
            foreach (var m in markers)
            {
                if (m == null || m.Type != TileType.Wall) continue;
                var cell = new Vector3Int(m.Coord.X, m.Layer, m.Coord.Y);
                var result = WallOccluderOps.EnsureOccluder(m.gameObject, _target, cell);
                switch (result)
                {
                    case WallOccluderOps.BakeResult.Added: added++; break;
                    case WallOccluderOps.BakeResult.Updated: updated++; break;
                    case WallOccluderOps.BakeResult.Skipped: skipped++; break;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(_target);
            Debug.Log($"[WallOccluder] Baked: {added} added, {updated} updated, {skipped} ok.");
        }

        // ============================ Tab 2 — Info ============================

        [TabGroup(Tabs, TabRoom), BoxGroup(GInfo, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawInfoSectionHeader() => DrawSectionHeader("Info", new Color(InfoR, InfoG, InfoB));

        [TabGroup(Tabs, TabRoom), BoxGroup(GInfo, false), PropertyOrder(0)]
        [ShowInInspector, ReadOnly, LabelText("Tiles in target")]
        private string TileCount => _target != null
            ? _target.GetComponentsInChildren<TileMarker>(true).Length.ToString()
            : "—";

        [TabGroup(Tabs, TabRoom), BoxGroup(GInfo, false), PropertyOrder(1)]
        [ShowInInspector, ReadOnly, LabelText("Origin")]
        private string OriginText => _target != null ? _target.GetOrigin().ToString("F2") : "—";

        [TabGroup(Tabs, TabRoom), BoxGroup(GInfo, false), PropertyOrder(2)]
        [ShowInInspector, ReadOnly, LabelText("Selected")]
        private string SelectedTileLabel => SelectedEntry() is { } e
            ? $"{e.Label ?? e.Prefab?.name ?? "?"}  ({e.Type}{(e.IsBlocker ? ", blocker" : "")})"
            : "—";

        [TabGroup(Tabs, TabRoom), BoxGroup(GInfo, false), PropertyOrder(3)]
        [Button("Frame Target"), EnableIf("@_target != null")]
        private void FrameTarget()
        {
            Selection.activeObject = _target.gameObject;
            SceneView.FrameLastActiveSceneView();
        }

        // ============================ Tab 3 — Palette ============================

        [TabGroup(Tabs, TabPalette), BoxGroup(GPalette, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawPaletteSectionHeader() => DrawSectionHeader("Palette", new Color(PaletteR, PaletteG, PaletteB));

        [TabGroup(Tabs, TabPalette), BoxGroup(GPalette, false), PropertyOrder(0)]
        [Title("Asset", bold: true, horizontalLine: true)]
        [LabelText("Palette Asset")]
        [SerializeField] private TilePainterPaletteSO _palette;

        [TabGroup(Tabs, TabPalette), BoxGroup(GPalette, false), PropertyOrder(1), EnableIf("@_palette != null")]
        [Button("Use Palette Default Tile Size")]
        private void UsePaletteDefaultTileSize()
        {
            if (_palette != null) _tileSize = _palette.DefaultTileSize;
        }

        [TabGroup(Tabs, TabPalette), BoxGroup(GPalette, false), PropertyOrder(2)]
        [Title("Tiles", bold: true, horizontalLine: true)]
        [PropertySpace(SpaceBefore = 4)]
        [OnInspectorGUI]
        private void DrawPaletteGrid()
        {
            if (_palette == null)
            {
                EditorGUILayout.HelpBox("Assign a TilePainterPaletteSO to see tile choices.", MessageType.Info);
                return;
            }
            if (_palette.Entries == null || _palette.Entries.Count == 0)
            {
                EditorGUILayout.HelpBox("Palette has no entries. Add prefabs to it.", MessageType.Info);
                return;
            }

            GUILayout.Space(2);
            GUILayout.Label("Tiles", EditorStyles.boldLabel);
            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll, GUILayout.MinHeight(120), GUILayout.MaxHeight(220));
            const int cols = 3;
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < _palette.Entries.Count; i++)
            {
                if (i > 0 && i % cols == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }
                var entry = _palette.Entries[i];
                var content = entry.Icon != null
                    ? new GUIContent(entry.Icon, entry.Label)
                    : new GUIContent(string.IsNullOrEmpty(entry.Label)
                        ? (entry.Prefab != null ? entry.Prefab.name : "?")
                        : entry.Label);
                bool selected = _selectedEntryIndex == i;
                var prev = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(0.5f, 0.85f, 1f);
                if (GUILayout.Button(content, GUILayout.Height(48), GUILayout.MinWidth(72)))
                {
                    _selectedEntryIndex = i;
                }
                GUI.backgroundColor = prev;

                if (entry.IsBlocker && Event.current.type == EventType.Repaint)
                {
                    DrawRectBorder(GUILayoutUtility.GetLastRect(), new Color(0.95f, 0.25f, 0.25f), 2f);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        // ============================ Tab 3 — Settings ============================

        [TabGroup(Tabs, TabPalette), BoxGroup(GSettings, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawSettingsSectionHeader() => DrawSectionHeader("Settings", new Color(SettingsR, SettingsG, SettingsB));

        [TabGroup(Tabs, TabPalette), BoxGroup(GSettings, false), PropertyOrder(0)]
        [Title("Grid", bold: true, horizontalLine: true)]
        [LabelText("Tile Size")]
        [SerializeField] private Vector3 _tileSize = Vector3.one;

        [TabGroup(Tabs, TabPalette), BoxGroup(GSettings, false), PropertyOrder(1)]
        [LabelText("Grid Step")]
        [SerializeField] private Vector3 _gridStep = Vector3.one;

        [TabGroup(Tabs, TabPalette), BoxGroup(GSettings, false), PropertyOrder(2)]
        [LabelText("Current Layer (Y)")]
        [SerializeField] private int _currentLayer;

        [TabGroup(Tabs, TabPalette), BoxGroup(GSettings, false), PropertyOrder(3)]
        [Title("Rotation", bold: true, horizontalLine: true)]
        [PropertySpace(SpaceBefore = 4)]
        [LabelText("Rotation Step (deg)"), ValueDropdown(nameof(GetRotationStepOptions))]
        [SerializeField] private float _rotationStep = 90f;

        [TabGroup(Tabs, TabPalette), BoxGroup(GSettings, false), PropertyOrder(4)]
        [LabelText("Rotation (deg)"), OnValueChanged(nameof(OnRotationChanged))]
        [SerializeField] private Vector3 _rotationEuler;

        [TabGroup(Tabs, TabPalette), BoxGroup(GSettings, false), PropertyOrder(5)]
        [Button("Reset Rotation")]
        private void ResetRotation()
        {
            _rotationEuler = Vector3.zero;
            SceneView.RepaintAll();
        }

        // ============================ Hidden state ============================

        [HideInInspector, SerializeField] private RoomLayout _target;
        [HideInInspector, SerializeField] private int _selectedEntryIndex = -1;
        [HideInInspector, SerializeField] private bool _toolActive;

        [System.NonSerialized] private RoomEditorGhost _ghost;
        [System.NonSerialized] private Vector2 _paletteScroll;
        [System.NonSerialized] private Vector3Int? _hoverCell;
        [System.NonSerialized] private Vector3Int? _lastPaintedCell;
        [System.NonSerialized] private bool _hoverValid;
        [System.NonSerialized] private bool _navKeyHeld;

        // ============================ Lifecycle ============================

        [MenuItem("Tools/Rollgeon/Room Editor")]
        public static void Open()
        {
            var win = GetWindow<RoomEditorWindow>();
            win.titleContent = new GUIContent("Room Editor");
            win.minSize = new Vector2(460, 520);
            win.Show();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += Repaint;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
            _ghost = new RoomEditorGhost();
            SyncFromActivePrefabStage();
            SyncNavGraphOverlay();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= Repaint;
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
            _ghost?.Dispose();
            _ghost = null;
            _navKeyHeld = false;
            SceneView.RepaintAll();
        }

        // ============================ Helpers ============================

        private TilePainterPaletteEntry SelectedEntry()
        {
            if (_palette == null) return null;
            if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _palette.Entries.Count) return null;
            return _palette.Entries[_selectedEntryIndex];
        }

        private static IEnumerable<float> GetRotationStepOptions()
        {
            yield return 45f;
            yield return 90f;
        }

        private static IEnumerable<ValueDropdownItem<GameObject>> GetRoomPrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponent<RoomLayout>() != null)
                {
                    var label = path.StartsWith("Assets/") ? path.Substring("Assets/".Length) : path;
                    yield return new ValueDropdownItem<GameObject>(label, go);
                }
            }
        }

        private void OpenSelectedRoom()
        {
            if (_selectedRoomPrefab == null) return;
            var path = AssetDatabase.GetAssetPath(_selectedRoomPrefab);
            if (string.IsNullOrEmpty(path)) return;
            PrefabStageUtility.OpenPrefab(path);
        }

        private void OnRotationChanged() => SceneView.RepaintAll();

        private void SyncNavGraphOverlay() => SceneView.RepaintAll();

        private void UpdateNavGraphPeek(Event e)
        {
            if (e == null) return;
            bool changed = false;
            bool hasModifier = e.alt || e.shift || e.control || e.command;

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.G && !hasModifier)
            {
                if (!_navKeyHeld) { _navKeyHeld = true; changed = true; }
                e.Use();
            }
            else if (e.type == EventType.KeyUp && e.keyCode == KeyCode.G)
            {
                if (_navKeyHeld) { _navKeyHeld = false; changed = true; }
                e.Use();
            }

            if (changed) SyncNavGraphOverlay();
        }

        private static void EnsureFolderExists(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            var parts = folder.Split('/');
            if (parts.Length < 2 || parts[0] != "Assets") return;
            var current = "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private void OnPrefabStageOpened(PrefabStage stage)
        {
            if (stage == null || stage.prefabContentsRoot == null) return;
            var layout = stage.prefabContentsRoot.GetComponent<RoomLayout>();
            if (layout != null) _target = layout;
            Repaint();
        }

        private void OnPrefabStageClosing(PrefabStage stage)
        {
            if (stage == null || stage.prefabContentsRoot == null) return;
            if (_target != null && _target.transform.IsChildOf(stage.prefabContentsRoot.transform))
            {
                _target = null;
                Repaint();
            }
        }

        private void SyncFromActivePrefabStage()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) OnPrefabStageOpened(stage);
        }

        // ============================ SceneView input + rendering ============================

        private void OnSceneGUI(SceneView sv)
        {
            var e = Event.current;
            UpdateNavGraphPeek(e);

            if ((_showNavGraphOverlay || _navKeyHeld) && _target != null && e.type == EventType.Repaint)
            {
                NavGraphGizmoDrawer.DrawWithHandles(_target);
            }

            if (_target == null) return;

            // Spawn point gizmos render regardless of which tool is active (unless mode = Hide).
            if (e.type == EventType.Repaint)
            {
                RoomEditorSpawnGizmos.Draw(_target, _previewSetIndex, _spawnGizmoMode, _selectedSpawnPoint);
            }

            // Spawn tool short-circuits tile painting.
            if (_spawnToolActive)
            {
                int spControlId = GUIUtility.GetControlID(FocusType.Passive);
                if (_showGrid)
                    RoomEditorGizmos.DrawGridPlane(_target.GetOrigin(), _gridStep, _currentLayer, _gridExtent);
                if (e.type == EventType.Repaint)
                    RoomEditorGizmos.DrawDoorSlotArrows(_target);

                HandleLayerKeys(e);
                UpdateHover(e);
                HandleSpawnSceneInput(e, spControlId);

                if (_hoverCell.HasValue)
                {
                    var center = SpawnCellCenter(_hoverCell.Value);
                    var existing = FindSpawnAtCell(_hoverCell.Value);
                    var color = existing != null
                        ? new Color(1f, 0.55f, 0.55f, 0.9f)
                        : new Color(0.4f, 1f, 0.5f, 0.9f);
                    RoomEditorGizmos.DrawCellWire(center, _gridStep, Quaternion.identity, color);
                }

                if (e.type == EventType.Layout)
                    HandleUtility.AddDefaultControl(spControlId);

                sv.Repaint();
                return;
            }

            if (!_toolActive) return;

            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (_showGrid)
            {
                RoomEditorGizmos.DrawGridPlane(_target.GetOrigin(), _gridStep, _currentLayer, _gridExtent);
            }

            if (e.type == EventType.Repaint)
            {
                RoomEditorGizmos.DrawDoorSlotArrows(_target);
            }

            HandleKeyboard(e);
            UpdateHover(e);
            HandleMouse(e, controlId);

            if (_showGhost && _hoverCell.HasValue)
            {
                var entry = SelectedEntry();
                var center = CellCenter(_hoverCell.Value);
                var wireColor = _hoverValid
                    ? new Color(0.3f, 1f, 0.4f, 0.9f)
                    : new Color(1f, 0.3f, 0.3f, 0.9f);
                if (entry != null && entry.Prefab != null && !_eraseMode && e.type == EventType.Repaint)
                {
                    Vector3? scale = _scaleToTileSize ? _tileSize : (Vector3?)null;
                    var meshTint = _hoverValid
                        ? new Color(0.4f, 1f, 0.5f, 0.4f)
                        : new Color(1f, 0.4f, 0.4f, 0.4f);
                    _ghost.Render(entry.Prefab, center, Quaternion.Euler(_rotationEuler), scale, meshTint);
                }
                RoomEditorGizmos.DrawCellWire(center, _tileSize, Quaternion.Euler(_rotationEuler), wireColor);
            }

            if (e.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            sv.Repaint();
        }

        private void UpdateHover(Event e)
        {
            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            TileMarker hitMarker = null;
            bool stackOnTop = false;
            Vector3 fallbackPoint = default;
            bool havePoint = false;

            if (Physics.Raycast(ray, out var hit, 1000f))
            {
                var marker = hit.collider.GetComponentInParent<TileMarker>();
                if (marker != null && marker.transform.IsChildOf(_target.transform))
                {
                    hitMarker = marker;
                    stackOnTop = Vector3.Dot(hit.normal, Vector3.up) > 0.5f;
                }
                else
                {
                    fallbackPoint = hit.point;
                    havePoint = true;
                }
            }

            if (hitMarker == null && !havePoint)
            {
                if (TryProjectToLayerPlane(ray, out var p)) { fallbackPoint = p; havePoint = true; }
            }

            Vector3Int cell;
            if (hitMarker != null)
            {
                cell = new Vector3Int(hitMarker.Coord.X, hitMarker.Layer, hitMarker.Coord.Y);
                if (stackOnTop && !_eraseMode)
                {
                    var hitFp = SafeFootprint(hitMarker.Footprint);
                    cell.y += hitMarker.FootprintOffset.y + hitFp.y;
                }
            }
            else if (havePoint)
            {
                cell = WorldToCell(fallbackPoint);
                cell.y = _currentLayer;
            }
            else
            {
                _hoverCell = null;
                return;
            }

            _hoverCell = cell;
            if (_eraseMode)
            {
                _hoverValid = FindTileAt(cell) != null;
            }
            else
            {
                var entry = SelectedEntry();
                if (entry == null || !entry.IsBlocker)
                {
                    _hoverValid = true;
                }
                else
                {
                    CurrentFootprintAndOffset(out var fp, out var off);
                    _hoverValid = !HasBlockerOverlap(cell, fp, off);
                }
            }
        }

        private bool TryProjectToLayerPlane(Ray ray, out Vector3 point)
        {
            var origin = _target.GetOrigin();
            float planeY = origin.y + _currentLayer * _gridStep.y;
            var plane = new Plane(Vector3.up, new Vector3(0, planeY, 0));
            if (plane.Raycast(ray, out var dist))
            {
                point = ray.GetPoint(dist);
                return true;
            }
            point = default;
            return false;
        }

        private void HandleLayerKeys(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            switch (e.keyCode)
            {
                case KeyCode.Q:
                    _currentLayer -= 1;
                    e.Use();
                    Repaint();
                    break;
                case KeyCode.E:
                    _currentLayer += 1;
                    e.Use();
                    Repaint();
                    break;
                case KeyCode.Escape:
                    _selectedSpawnPoint = null;
                    e.Use();
                    Repaint();
                    break;
            }
        }

        private void HandleKeyboard(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            switch (e.keyCode)
            {
                case KeyCode.R:
                    if (e.shift) _rotationEuler.x = NormalizeAngle(_rotationEuler.x + _rotationStep);
                    else if (e.control || e.command) _rotationEuler.z = NormalizeAngle(_rotationEuler.z + _rotationStep);
                    else _rotationEuler.y = NormalizeAngle(_rotationEuler.y + _rotationStep);
                    e.Use();
                    Repaint();
                    SceneView.RepaintAll();
                    break;
                case KeyCode.Q:
                    _currentLayer -= 1;
                    e.Use();
                    Repaint();
                    break;
                case KeyCode.E:
                    _currentLayer += 1;
                    e.Use();
                    Repaint();
                    break;
                case KeyCode.Escape:
                    _selectedEntryIndex = -1;
                    e.Use();
                    Repaint();
                    break;
            }
        }

        private void HandleMouse(Event e, int controlId)
        {
            bool isMouseDown = e.type == EventType.MouseDown && e.button == 0 && !e.alt;
            bool isDragPaint = _multiPaint && e.type == EventType.MouseDrag && e.button == 0 && !e.alt;

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _lastPaintedCell = null;
                if (GUIUtility.hotControl == controlId) GUIUtility.hotControl = 0;
            }

            if (!isMouseDown && !isDragPaint) return;
            if (!_hoverCell.HasValue) return;

            var cell = _hoverCell.Value;
            if (_lastPaintedCell.HasValue && _lastPaintedCell.Value == cell)
            {
                e.Use();
                return;
            }

            bool erase = _eraseMode || e.shift;
            if (erase)
            {
                EraseAt(cell);
            }
            else
            {
                var entry = SelectedEntry();
                if (entry == null || entry.Prefab == null) return;
                PlaceAt(entry, cell);
            }

            _lastPaintedCell = cell;
            GUIUtility.hotControl = controlId;
            e.Use();
            Repaint();
        }

        // ============================ Placement ============================

        private void PlaceAt(TilePainterPaletteEntry entry, Vector3Int cell)
        {
            CurrentFootprintAndOffset(out var footprint, out var footprintOffset);
            if (entry.IsBlocker && HasBlockerOverlap(cell, footprint, footprintOffset)) return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab, _target.transform);
            if (instance == null) return;

            instance.transform.SetPositionAndRotation(CellCenter(cell), Quaternion.Euler(_rotationEuler));
            if (_scaleToTileSize) instance.transform.localScale = _tileSize;

            var marker = instance.GetComponent<TileMarker>();
            if (marker == null) marker = instance.AddComponent<TileMarker>();
            marker.Coord = new GridCoord(cell.x, cell.z);
            marker.Layer = cell.y;
            marker.Footprint = footprint;
            marker.FootprintOffset = footprintOffset;
            marker.Type = entry.Type;
            marker.IsBlocker = entry.IsBlocker;

            Undo.RegisterCreatedObjectUndo(instance, "Paint Tile");

            if (entry.Type == TileType.Door)
            {
                var controller = instance.GetComponentInChildren<DoorController>(true);
                if (controller != null)
                    RoomEditorDoorBinder.BindOnPlace(_target, controller, instance.transform.position);
            }
            else if (entry.Type == TileType.Wall)
            {
                WallOccluderOps.EnsureOccluder(instance, _target, cell);
            }

            EditorUtility.SetDirty(_target);
        }

        private void EraseAt(Vector3Int cell)
        {
            var m = FindTileAt(cell);
            if (m == null) return;

            if (m.Type == TileType.Door)
            {
                var controller = m.GetComponentInChildren<DoorController>(true);
                if (controller != null)
                    RoomEditorDoorBinder.RemoveSlot(_target, controller.Direction);
            }

            Undo.DestroyObjectImmediate(m.gameObject);
            EditorUtility.SetDirty(_target);
        }

        private bool HasBlockerOverlap(Vector3Int anchor, Vector3Int footprint, Vector3Int offset)
        {
            var newMin = anchor + offset;
            var newMax = newMin + footprint;
            var markers = _target.GetComponentsInChildren<TileMarker>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                if (!m.IsBlocker) continue;
                var mAnchor = new Vector3Int(m.Coord.X, m.Layer, m.Coord.Y);
                var mFp = SafeFootprint(m.Footprint);
                var mMin = mAnchor + m.FootprintOffset;
                var mMax = mMin + mFp;
                if (newMin.x < mMax.x && newMax.x > mMin.x
                    && newMin.y < mMax.y && newMax.y > mMin.y
                    && newMin.z < mMax.z && newMax.z > mMin.z)
                {
                    return true;
                }
            }
            return false;
        }

        private TileMarker FindTileAt(Vector3Int cell)
        {
            var markers = _target.GetComponentsInChildren<TileMarker>(true);
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                var mAnchor = new Vector3Int(m.Coord.X, m.Layer, m.Coord.Y);
                var mFp = SafeFootprint(m.Footprint);
                var mMin = mAnchor + m.FootprintOffset;
                var mMax = mMin + mFp;
                if (cell.x >= mMin.x && cell.x < mMax.x
                    && cell.y >= mMin.y && cell.y < mMax.y
                    && cell.z >= mMin.z && cell.z < mMax.z)
                {
                    return m;
                }
            }
            return null;
        }

        private void CurrentFootprintAndOffset(out Vector3Int size, out Vector3Int offset)
        {
            var r = Quaternion.Euler(_rotationEuler);
            var aabbHalf = RotatedAABBHalf(_tileSize, r);
            var tileCenterOffset = r * ((_tileSize - _gridStep) * 0.5f);
            var halfStep = _gridStep * 0.5f;
            var minRel = tileCenterOffset - aabbHalf + halfStep;
            var maxRel = tileCenterOffset + aabbHalf + halfStep;

            int minX = FloorCellsWithEpsilon(minRel.x, _gridStep.x);
            int minY = FloorCellsWithEpsilon(minRel.y, _gridStep.y);
            int minZ = FloorCellsWithEpsilon(minRel.z, _gridStep.z);
            int maxX = CeilCellsWithEpsilon(maxRel.x, _gridStep.x);
            int maxY = CeilCellsWithEpsilon(maxRel.y, _gridStep.y);
            int maxZ = CeilCellsWithEpsilon(maxRel.z, _gridStep.z);

            size = new Vector3Int(
                Mathf.Max(1, maxX - minX),
                Mathf.Max(1, maxY - minY),
                Mathf.Max(1, maxZ - minZ));
            offset = new Vector3Int(minX, minY, minZ);
        }

        private const float CellEpsilon = 1e-4f;

        private static int FloorCellsWithEpsilon(float value, float step)
        {
            float ratio = value / step;
            int rounded = Mathf.RoundToInt(ratio);
            if (Mathf.Abs(ratio - rounded) < CellEpsilon) return rounded;
            return Mathf.FloorToInt(ratio);
        }

        private static int CeilCellsWithEpsilon(float value, float step)
        {
            float ratio = value / step;
            int rounded = Mathf.RoundToInt(ratio);
            if (Mathf.Abs(ratio - rounded) < CellEpsilon) return rounded;
            return Mathf.CeilToInt(ratio);
        }

        private static Vector3Int SafeFootprint(Vector3Int fp)
        {
            return (fp.x > 0 && fp.y > 0 && fp.z > 0) ? fp : Vector3Int.one;
        }

        // ============================ Coord math ============================

        private Vector3Int WorldToCell(Vector3 world)
        {
            var origin = _target.GetOrigin();
            int x = Mathf.FloorToInt((world.x - origin.x) / _gridStep.x);
            int z = Mathf.FloorToInt((world.z - origin.z) / _gridStep.z);
            int y = Mathf.FloorToInt((world.y - origin.y) / _gridStep.y);
            return new Vector3Int(x, y, z);
        }

        private Vector3 CellCenter(Vector3Int cell)
        {
            var origin = _target.GetOrigin();
            var cellCenter = new Vector3(
                origin.x + (cell.x + 0.5f) * _gridStep.x,
                origin.y + (cell.y + 0.5f) * _gridStep.y,
                origin.z + (cell.z + 0.5f) * _gridStep.z);
            var rotation = Quaternion.Euler(_rotationEuler);
            return cellCenter + rotation * ((_tileSize - _gridStep) * 0.5f);
        }

        private static Vector3 RotatedAABBHalf(Vector3 size, Quaternion rotation)
        {
            var m = Matrix4x4.Rotate(rotation);
            Vector3 half = size * 0.5f;
            float x = Mathf.Abs(m.m00) * half.x + Mathf.Abs(m.m01) * half.y + Mathf.Abs(m.m02) * half.z;
            float y = Mathf.Abs(m.m10) * half.x + Mathf.Abs(m.m11) * half.y + Mathf.Abs(m.m12) * half.z;
            float z = Mathf.Abs(m.m20) * half.x + Mathf.Abs(m.m21) * half.y + Mathf.Abs(m.m22) * half.z;
            return new Vector3(x, y, z);
        }

        private static float NormalizeAngle(float a)
        {
            a %= 360f;
            if (a < 0f) a += 360f;
            return a;
        }
    }
}
