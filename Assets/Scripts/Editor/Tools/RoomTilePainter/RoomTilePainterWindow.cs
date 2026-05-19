using UnityEditor;
using UnityEngine;
using Rollgeon.Dungeon.Components;
using Rollgeon.Grid;

namespace Rollgeon.Editor.Tools.RoomTilePainter
{
    public sealed class RoomTilePainterWindow : EditorWindow
    {
        // ---- Tool state (serialized so it survives reloads) ----
        [SerializeField] private RoomLayout _target;
        [SerializeField] private TilePainterPaletteSO _palette;
        [SerializeField] private int _selectedEntryIndex = -1;
        [SerializeField] private Vector3 _tileSize = Vector3.one;
        [SerializeField] private float _rotationStep = 90f;
        [SerializeField] private Vector3 _rotationEuler = Vector3.zero;
        [SerializeField] private int _currentLayer;
        [SerializeField] private bool _toolActive;
        [SerializeField] private bool _eraseMode;
        [SerializeField] private bool _multiPaint;
        [SerializeField] private bool _showGrid = true;
        [SerializeField] private bool _showGhost = true;
        [SerializeField] private int _gridExtent = 12;

        [System.NonSerialized] private RoomTilePainterGhost _ghost;
        [System.NonSerialized] private Vector2 _paletteScroll;
        [System.NonSerialized] private Vector3Int? _hoverCell;
        [System.NonSerialized] private Vector3Int? _lastPaintedCell;
        [System.NonSerialized] private bool _hoverValid;

        private const float RotationStep45 = 45f;
        private const float RotationStep90 = 90f;

        [MenuItem("Tools/Rollgeon/Room Tile Painter")]
        public static void Open()
        {
            var win = GetWindow<RoomTilePainterWindow>();
            win.titleContent = new GUIContent("Room Tile Painter");
            win.minSize = new Vector2(460, 360);
            win.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += Repaint;
            _ghost = new RoomTilePainterGhost();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= Repaint;
            _ghost?.Dispose();
            _ghost = null;
        }

        // ---- Window UI ----

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            GUILayout.Space(8);
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(280));

            GUILayout.Label("Target", EditorStyles.boldLabel);
            _target = (RoomLayout)EditorGUILayout.ObjectField("Room Layout", _target, typeof(RoomLayout), true);
            _palette = (TilePainterPaletteSO)EditorGUILayout.ObjectField("Palette", _palette, typeof(TilePainterPaletteSO), false);

            if (_palette != null)
            {
                if (GUILayout.Button("Use Palette Default Tile Size", EditorStyles.miniButton))
                {
                    _tileSize = _palette.DefaultTileSize;
                }
                DrawPaletteGrid();
            }
            else
            {
                EditorGUILayout.HelpBox("Assign a TilePainterPaletteSO to see tile choices.", MessageType.Info);
            }

            GUILayout.Space(6);
            GUILayout.Label("Settings", EditorStyles.boldLabel);

            _tileSize = EditorGUILayout.Vector3Field("Tile Size", _tileSize);
            _tileSize.x = Mathf.Max(0.01f, _tileSize.x);
            _tileSize.y = Mathf.Max(0.01f, _tileSize.y);
            _tileSize.z = Mathf.Max(0.01f, _tileSize.z);

            int stepIdx = Mathf.Approximately(_rotationStep, RotationStep45) ? 0 : 1;
            stepIdx = EditorGUILayout.Popup("Rotation Step", stepIdx, new[] { "45°", "90°" });
            _rotationStep = stepIdx == 0 ? RotationStep45 : RotationStep90;

            _currentLayer = EditorGUILayout.IntField("Current Layer (Y)", _currentLayer);

            GUILayout.Space(4);
            _toolActive = EditorGUILayout.ToggleLeft("Tool Active (intercept SceneView)", _toolActive);
            using (new EditorGUI.DisabledScope(!_toolActive))
            {
                _eraseMode = EditorGUILayout.ToggleLeft("Erase Mode (Shift+click also erases)", _eraseMode);
                _multiPaint = EditorGUILayout.ToggleLeft("Multi-Paint (drag to paint many)", _multiPaint);
                _showGrid = EditorGUILayout.ToggleLeft("Show Grid Plane", _showGrid);
                _showGhost = EditorGUILayout.ToggleLeft("Show Ghost Preview", _showGhost);
                _gridExtent = Mathf.Max(1, EditorGUILayout.IntField("Grid Extent (cells)", _gridExtent));
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRightPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(220));

            GUILayout.Label("Info", EditorStyles.boldLabel);
            if (_target != null)
            {
                int count = _target.GetComponentsInChildren<TileMarker>(true).Length;
                EditorGUILayout.LabelField("Tiles", count.ToString());
                EditorGUILayout.LabelField("Origin", _target.GetOrigin().ToString("F2"));
            }
            else
            {
                EditorGUILayout.LabelField("Tiles", "—");
            }
            EditorGUILayout.LabelField("Rotation", _rotationEuler.ToString("F0"));
            EditorGUILayout.LabelField("Layer", _currentLayer.ToString());
            EditorGUILayout.LabelField("Selected", SelectedEntry()?.Label ?? "—");

            GUILayout.Space(8);
            GUILayout.Label("Shortcuts (tool active)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("R", "Rotate Y");
            EditorGUILayout.LabelField("Shift+R", "Rotate X");
            EditorGUILayout.LabelField("Ctrl+R", "Rotate Z");
            EditorGUILayout.LabelField("Q / E", "Layer −1 / +1");
            EditorGUILayout.LabelField("LMB", "Place (Shift = erase)");
            EditorGUILayout.LabelField("Esc", "Deselect");

            GUILayout.Space(8);
            using (new EditorGUI.DisabledScope(_target == null))
            {
                if (GUILayout.Button("Frame target"))
                {
                    Selection.activeObject = _target.gameObject;
                    SceneView.FrameLastActiveSceneView();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPaletteGrid()
        {
            GUILayout.Space(4);
            GUILayout.Label("Palette", EditorStyles.boldLabel);
            if (_palette.Entries == null || _palette.Entries.Count == 0)
            {
                EditorGUILayout.HelpBox("Palette has no entries. Add prefabs to the TilePainterPaletteSO asset.", MessageType.Info);
                return;
            }

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
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private TilePainterPaletteEntry SelectedEntry()
        {
            if (_palette == null) return null;
            if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _palette.Entries.Count) return null;
            return _palette.Entries[_selectedEntryIndex];
        }

        // ---- SceneView input + rendering ----

        private void OnSceneGUI(SceneView sv)
        {
            if (!_toolActive || _target == null)
            {
                _ghost?.Hide();
                return;
            }

            var e = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (_showGrid)
            {
                RoomTilePainterGizmos.DrawGridPlane(_target.GetOrigin(), _tileSize, _currentLayer, _gridExtent);
            }

            UpdateHover(e);

            if (_showGhost && _hoverCell.HasValue)
            {
                var entry = SelectedEntry();
                var center = CellCenter(_hoverCell.Value);
                if (entry != null && entry.Prefab != null && !_eraseMode)
                {
                    _ghost.UpdatePreview(entry.Prefab, center, Quaternion.Euler(_rotationEuler));
                }
                else
                {
                    _ghost.Hide();
                }
                var color = _hoverValid
                    ? new Color(0.3f, 1f, 0.4f, 0.9f)
                    : new Color(1f, 0.3f, 0.3f, 0.9f);
                RoomTilePainterGizmos.DrawCellWire(center, _tileSize, color);
            }
            else
            {
                _ghost.Hide();
            }

            HandleKeyboard(e);
            HandleMouse(e, controlId);

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
                if (stackOnTop && !_eraseMode) cell.y += 1;
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
            _hoverValid = _eraseMode ? IsOccupied(cell) : !IsOccupied(cell);
        }

        private bool TryProjectToLayerPlane(Ray ray, out Vector3 point)
        {
            var origin = _target.GetOrigin();
            float planeY = origin.y + _currentLayer * _tileSize.y;
            var plane = new Plane(Vector3.up, new Vector3(0, planeY, 0));
            if (plane.Raycast(ray, out var dist))
            {
                point = ray.GetPoint(dist);
                return true;
            }
            point = default;
            return false;
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

        // ---- Placement ----

        private void PlaceAt(TilePainterPaletteEntry entry, Vector3Int cell)
        {
            if (IsOccupied(cell)) return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab, _target.transform);
            if (instance == null) return;

            instance.transform.SetPositionAndRotation(CellCenter(cell), Quaternion.Euler(_rotationEuler));

            var marker = instance.GetComponent<TileMarker>();
            if (marker == null) marker = instance.AddComponent<TileMarker>();
            marker.Coord = new GridCoord(cell.x, cell.z);
            marker.Layer = cell.y;

            Undo.RegisterCreatedObjectUndo(instance, "Paint Tile");
            EditorUtility.SetDirty(_target);
        }

        private void EraseAt(Vector3Int cell)
        {
            var markers = _target.GetComponentsInChildren<TileMarker>(true);
            foreach (var m in markers)
            {
                if (m.Coord.X == cell.x && m.Coord.Y == cell.z && m.Layer == cell.y)
                {
                    Undo.DestroyObjectImmediate(m.gameObject);
                    EditorUtility.SetDirty(_target);
                    return;
                }
            }
        }

        private bool IsOccupied(Vector3Int cell)
        {
            var markers = _target.GetComponentsInChildren<TileMarker>(true);
            foreach (var m in markers)
            {
                if (m.Coord.X == cell.x && m.Coord.Y == cell.z && m.Layer == cell.y) return true;
            }
            return false;
        }

        // ---- Coord math ----

        private Vector3Int WorldToCell(Vector3 world)
        {
            var origin = _target.GetOrigin();
            int x = Mathf.RoundToInt((world.x - origin.x) / _tileSize.x);
            int z = Mathf.RoundToInt((world.z - origin.z) / _tileSize.z);
            int y = Mathf.RoundToInt((world.y - origin.y) / _tileSize.y);
            return new Vector3Int(x, y, z);
        }

        private Vector3 CellCenter(Vector3Int cell)
        {
            var origin = _target.GetOrigin();
            return new Vector3(
                origin.x + cell.x * _tileSize.x,
                origin.y + cell.y * _tileSize.y,
                origin.z + cell.z * _tileSize.z);
        }

        private static float NormalizeAngle(float a)
        {
            a %= 360f;
            if (a < 0f) a += 360f;
            return a;
        }
    }
}
