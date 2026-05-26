using System.Collections.Generic;
using System.Linq;
using Rollgeon.Dungeon;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// Floor Editor — autorea <see cref="FloorLayoutSO"/>s con Slots por
    /// RoomType, count Fixed/Random y pool de RoomSO con search dropdown.
    /// Inspirado en <see cref="HeroClassEditorWindow"/>.
    /// </summary>
    public sealed class FloorEditorWindow : EditorWindow
    {
        const float LeftWidth = 220f;
        const float RightWidth = 320f;
        const float PreviewCell = 22f;
        const float PreviewMaxSize = 240f;

        static readonly RoomType[] AllTypes =
        {
            RoomType.Start, RoomType.Combat, RoomType.Shop, RoomType.Potion, RoomType.Boss, RoomType.Enchantment
        };

        readonly List<FloorLayoutSO> _floors = new List<FloorLayoutSO>();
        FloorLayoutSO _selected;
        Vector2 _leftScroll, _midScroll, _rightScroll;
        string _searchOpenForSlotKey;   // key = $"{floorInstanceId}:{slotIdx}"
        string _searchFilter = string.Empty;
        readonly HashSet<int> _collapsed = new HashSet<int>();

        // Room cache for the search dropdown.
        List<RoomSO> _allRooms = new List<RoomSO>();

        // Preview state
        int _previewSeed = 12345;
        FloorTopologyPlanner.Plan _previewPlan;
        FloorLayoutSO _previewLayoutRef;

        [MenuItem("Tools/Floor Editor")]
        static void Open()
        {
            var w = GetWindow<FloorEditorWindow>("Floor Editor");
            w.minSize = new Vector2(960f, 540f);
        }

        void OnEnable()
        {
            RefreshFloorList();
            RefreshRoomCache();
            Undo.undoRedoPerformed += OnUndo;
        }

        void OnDisable() => Undo.undoRedoPerformed -= OnUndo;

        void OnProjectChange()
        {
            RefreshFloorList();
            RefreshRoomCache();
            Repaint();
        }

        void OnUndo() => Repaint();

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            DrawLeft();
            Sep();
            DrawMiddle();
            Sep();
            DrawRight();
            EditorGUILayout.EndHorizontal();
        }

        // ── Left: floor list ────────────────────────────────────

        void DrawLeft()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftWidth));
            EditorGUILayout.LabelField("Floor Layouts", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ New", GUILayout.Height(22f))) CreateNewFloor();
            if (GUILayout.Button("⟳", GUILayout.Width(28f), GUILayout.Height(22f)))
            {
                RefreshFloorList();
                RefreshRoomCache();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            if (_floors.Count == 0)
            {
                EditorGUILayout.HelpBox("No FloorLayoutSO assets found.", MessageType.Info);
            }
            else
            {
                foreach (var floor in _floors)
                {
                    bool isSel = floor == _selected;
                    var prev = GUI.backgroundColor;
                    if (isSel) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);

                    string label = string.IsNullOrEmpty(floor.DisplayName) ? floor.name : floor.DisplayName;
                    int totalSlots = floor.Slots != null ? floor.Slots.Count : 0;
                    string sub = $"{totalSlots} slots";

                    EditorGUILayout.BeginVertical(GUI.skin.box);
                    if (GUILayout.Button(label, EditorStyles.boldLabel)) SelectFloor(floor);
                    EditorGUILayout.LabelField(sub, EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();

                    GUI.backgroundColor = prev;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ── Middle: slots ───────────────────────────────────────

        void DrawMiddle()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            _midScroll = EditorGUILayout.BeginScrollView(_midScroll);

            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select a floor from the left panel.", MessageType.Info);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                DrawIdentity();
                EditorGUILayout.Space(10);
                DrawSlots();
                if (EditorGUI.EndChangeCheck()) InvalidatePreview();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawIdentity()
        {
            EditorGUILayout.LabelField($"Floor: {_selected.name}", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string newId = EditorGUILayout.TextField("Floor Id", _selected.FloorId ?? string.Empty);
            string newName = EditorGUILayout.TextField("Display Name", _selected.DisplayName ?? string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_selected, "Edit Floor Identity");
                _selected.FloorId = newId;
                _selected.DisplayName = newName;
                EditorUtility.SetDirty(_selected);
            }
        }

        void InvalidatePreview() => _previewPlan = null;

        void DrawSlots()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Room Slots", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Slot", GUILayout.Width(100f)))
            {
                ShowAddSlotMenu();
            }
            EditorGUILayout.EndHorizontal();

            if (_selected.Slots == null || _selected.Slots.Count == 0)
            {
                EditorGUILayout.HelpBox("No slots — click '+ Add Slot' to begin.", MessageType.Info);
                return;
            }

            for (int i = 0; i < _selected.Slots.Count; i++)
            {
                DrawSlotCard(i);
            }
        }

        void DrawSlotCard(int idx)
        {
            var slot = _selected.Slots[idx];
            if (slot == null) return;
            if (slot.Count == null) slot.Count = new RoomCountSpec();

            var rect = EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4f, rect.height), TypeColor(slot.Type));

            // Header
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8f);
            bool collapsed = _collapsed.Contains(idx);
            if (GUILayout.Button(collapsed ? "▸" : "▾", GUILayout.Width(22f)))
            {
                if (collapsed) _collapsed.Remove(idx); else _collapsed.Add(idx);
            }
            EditorGUILayout.LabelField(slot.Type.ToString(), EditorStyles.boldLabel, GUILayout.Width(80f));

            // Mode toggle
            EditorGUI.BeginChangeCheck();
            int modeIdx = GUILayout.Toolbar(
                slot.Count.Mode == RoomCountMode.Fixed ? 0 : 1,
                new[] { "Fixed", "Random" }, GUILayout.Width(140f));
            var newMode = modeIdx == 0 ? RoomCountMode.Fixed : RoomCountMode.Random;
            if (EditorGUI.EndChangeCheck() && newMode != slot.Count.Mode)
            {
                Undo.RecordObject(_selected, "Change Slot Mode");
                slot.Count.Mode = newMode;
                EditorUtility.SetDirty(_selected);
            }

            // Stepper or Range
            EditorGUI.BeginChangeCheck();
            if (slot.Count.Mode == RoomCountMode.Fixed)
            {
                int v = EditorGUILayout.IntField(slot.Count.Fixed, GUILayout.Width(50f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selected, "Edit Slot Count");
                    slot.Count.Fixed = Mathf.Max(0, v);
                    EditorUtility.SetDirty(_selected);
                }
            }
            else
            {
                int mn = EditorGUILayout.IntField(slot.Count.Min, GUILayout.Width(40f));
                GUILayout.Label("..", GUILayout.Width(16f));
                int mx = EditorGUILayout.IntField(slot.Count.Max, GUILayout.Width(40f));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selected, "Edit Slot Range");
                    slot.Count.Min = Mathf.Max(0, mn);
                    slot.Count.Max = Mathf.Max(slot.Count.Min, mx);
                    EditorUtility.SetDirty(_selected);
                }
            }

            GUILayout.FlexibleSpace();
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("×", GUILayout.Width(24f)))
            {
                Undo.RecordObject(_selected, "Remove Slot");
                _selected.Slots.RemoveAt(idx);
                _collapsed.Remove(idx);
                EditorUtility.SetDirty(_selected);
                GUI.backgroundColor = oldBg;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            GUI.backgroundColor = oldBg;
            EditorGUILayout.EndHorizontal();

            // Pool body
            if (!collapsed)
            {
                EditorGUILayout.Space(4);
                DrawPool(slot, idx);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        void DrawPool(RoomTypeSlot slot, int slotIdx)
        {
            if (slot.Pool == null) slot.Pool = new List<RoomSO>();

            EditorGUILayout.LabelField($"Pool ({slot.Pool.Count} entries)",
                EditorStyles.miniBoldLabel);

            int removeIdx = -1;
            for (int i = 0; i < slot.Pool.Count; i++)
            {
                var room = slot.Pool[i];
                EditorGUILayout.BeginHorizontal();
                if (room == null)
                {
                    EditorGUILayout.LabelField("(missing room)", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.ObjectField(room, typeof(RoomSO), false);
                    EditorGUILayout.LabelField(room.Type.ToString(), EditorStyles.miniLabel, GUILayout.Width(60f));
                }
                if (GUILayout.Button("×", GUILayout.Width(22f))) removeIdx = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeIdx >= 0)
            {
                Undo.RecordObject(_selected, "Remove Room from Pool");
                slot.Pool.RemoveAt(removeIdx);
                EditorUtility.SetDirty(_selected);
            }

            EditorGUILayout.Space(4);
            string key = $"{_selected.GetInstanceID()}:{slotIdx}";
            bool isOpen = _searchOpenForSlotKey == key;

            if (!isOpen)
            {
                if (GUILayout.Button("+ Add Room…", GUILayout.Height(22f)))
                {
                    _searchOpenForSlotKey = key;
                    _searchFilter = string.Empty;
                    GUI.FocusControl(null);
                }
            }
            else
            {
                DrawSearchDropdown(slot);
            }
        }

        void DrawSearchDropdown(RoomTypeSlot slot)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("Cancel", GUILayout.Width(60f)))
            {
                _searchOpenForSlotKey = null;
            }
            EditorGUILayout.EndHorizontal();

            var matches = FilterRooms(slot);
            int shown = 0;
            foreach (var (room, sameType, alreadyInPool) in matches)
            {
                if (shown++ > 30) break;
                EditorGUILayout.BeginHorizontal();
                bool disabled = alreadyInPool;
                GUI.enabled = !disabled;
                var label = $"{room.name}  ({room.Type})";
                if (GUILayout.Button(label, sameType ? EditorStyles.boldLabel : EditorStyles.label))
                {
                    Undo.RecordObject(_selected, "Add Room to Pool");
                    slot.Pool.Add(room);
                    EditorUtility.SetDirty(_selected);
                    _searchOpenForSlotKey = null;
                }
                GUI.enabled = true;
                if (alreadyInPool) EditorGUILayout.LabelField("(in pool)", EditorStyles.miniLabel, GUILayout.Width(60f));
                else if (!sameType) EditorGUILayout.LabelField("(other type)", EditorStyles.miniLabel, GUILayout.Width(80f));
                EditorGUILayout.EndHorizontal();
            }
            if (shown == 0)
            {
                EditorGUILayout.LabelField("No rooms match.", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }

        IEnumerable<(RoomSO room, bool sameType, bool alreadyInPool)> FilterRooms(RoomTypeSlot slot)
        {
            string f = (_searchFilter ?? string.Empty).Trim().ToLowerInvariant();
            // 1) sameType first, 2) other type after
            var sameType = new List<(RoomSO, bool, bool)>();
            var otherType = new List<(RoomSO, bool, bool)>();

            foreach (var room in _allRooms)
            {
                if (room == null) continue;
                if (f.Length > 0)
                {
                    string nm = (room.name ?? string.Empty).ToLowerInvariant();
                    string id = (room.RoomId ?? string.Empty).ToLowerInvariant();
                    string dn = (room.DisplayName ?? string.Empty).ToLowerInvariant();
                    if (!nm.Contains(f) && !id.Contains(f) && !dn.Contains(f)) continue;
                }
                bool same = room.Type == slot.Type;
                bool inPool = slot.Pool.Contains(room);
                if (same) sameType.Add((room, true, inPool));
                else otherType.Add((room, false, inPool));
            }
            foreach (var r in sameType) yield return r;
            foreach (var r in otherType) yield return r;
        }

        void ShowAddSlotMenu()
        {
            var menu = new GenericMenu();
            foreach (var t in AllTypes)
            {
                menu.AddItem(new GUIContent(t.ToString()), false, () => AddSlot(t));
            }
            menu.ShowAsContext();
        }

        void AddSlot(RoomType type)
        {
            Undo.RecordObject(_selected, "Add Slot");
            if (_selected.Slots == null) _selected.Slots = new List<RoomTypeSlot>();
            _selected.Slots.Add(new RoomTypeSlot
            {
                Type = type,
                Count = new RoomCountSpec { Mode = RoomCountMode.Fixed, Fixed = 1 },
                Pool = new List<RoomSO>()
            });
            EditorUtility.SetDirty(_selected);
        }

        // ── Right: composition + validation ─────────────────────

        void DrawRight()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(RightWidth));
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_selected == null)
            {
                EditorGUILayout.HelpBox("Select a floor.", MessageType.Info);
            }
            else
            {
                DrawComposition();
                EditorGUILayout.Space(10);
                DrawPreview();
                EditorGUILayout.Space(10);
                DrawValidation();
                EditorGUILayout.Space(10);
                DrawActions();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawPreview()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel, GUILayout.Width(80f));
            GUILayout.Label("Seed", GUILayout.Width(38f));
            int newSeed = EditorGUILayout.IntField(_previewSeed, GUILayout.Width(80f));
            if (newSeed != _previewSeed) { _previewSeed = newSeed; _previewPlan = null; }
            if (GUILayout.Button("⟳", GUILayout.Width(28f)))
            {
                _previewSeed = UnityEngine.Random.Range(1, int.MaxValue);
                _previewPlan = null;
            }
            EditorGUILayout.EndHorizontal();

            // Regenerar plan si cambió el layout seleccionado o se invalidó.
            if (_previewPlan == null || _previewLayoutRef != _selected)
            {
                _previewLayoutRef = _selected;
                try { _previewPlan = FloorTopologyPlanner.Generate(_selected, _previewSeed); }
                catch { _previewPlan = null; }
            }

            if (_previewPlan == null || _previewPlan.Cells.Count == 0)
            {
                EditorGUILayout.HelpBox("Nothing to preview — add slots with count > 0.", MessageType.Info);
                return;
            }

            DrawPreviewGrid(_previewPlan);

            // Resumen del roll concreto.
            EditorGUILayout.Space(4);
            int total = _previewPlan.Cells.Count;
            var sb = new System.Text.StringBuilder();
            sb.Append($"Rolled {total} rooms · ");
            bool first = true;
            foreach (var kv in _previewPlan.ResolvedCounts)
            {
                if (!first) sb.Append(", ");
                sb.Append($"{kv.Value} {kv.Key}");
                first = false;
            }
            EditorGUILayout.LabelField(sb.ToString(), EditorStyles.miniLabel);

            if (_previewPlan.Warnings.Count > 0)
            {
                foreach (var w in _previewPlan.Warnings)
                    StatusLine($"⚠ {w}", new Color(1f, 0.7f, 0.2f));
            }

            // Botón explícito para forzar re-roll sin cambiar el seed (útil si
            // el usuario tocó pools y quiere repintar contra el mismo seed).
            if (GUILayout.Button("Refresh", GUILayout.Height(20f)))
            {
                _previewPlan = null;
            }
        }

        void DrawPreviewGrid(FloorTopologyPlanner.Plan plan)
        {
            // Calcular bounds de cells para layout.
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in plan.Cells)
            {
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.y < minY) minY = c.y;
                if (c.y > maxY) maxY = c.y;
            }
            int cols = (maxX - minX) + 1;
            int rows = (maxY - minY) + 1;

            float cell = Mathf.Min(PreviewCell, PreviewMaxSize / Mathf.Max(cols, rows));
            float gridW = cols * cell;
            float gridH = rows * cell;

            // Centrar el grid horizontalmente en el panel.
            Rect area = GUILayoutUtility.GetRect(gridW + 8f, gridH + 8f,
                GUILayout.ExpandWidth(true));
            float originX = area.x + (area.width - gridW) * 0.5f;
            float originY = area.y + 4f;

            // Fondo
            EditorGUI.DrawRect(new Rect(originX - 4f, originY - 4f, gridW + 8f, gridH + 8f),
                new Color(0.1f, 0.1f, 0.1f));

            var cellMap = new Dictionary<Vector2Int, RoomType>(plan.Types);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    var c = new Vector2Int(minX + x, minY + y);
                    if (!cellMap.TryGetValue(c, out var type)) continue;

                    // Y invertido — y=0 es abajo en el grid; queremos que el (0,0)
                    // start aparezca centrado y crezca hacia arriba.
                    float rx = originX + x * cell;
                    float ry = originY + (rows - 1 - y) * cell;
                    var r = new Rect(rx + 1f, ry + 1f, cell - 2f, cell - 2f);
                    EditorGUI.DrawRect(r, TypeColor(type));

                    // Start outline
                    if (c == Vector2Int.zero)
                    {
                        DrawOutline(r, Color.white, 2f);
                    }
                }
            }
        }

        static void DrawOutline(Rect r, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, thickness, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
        }

        void DrawComposition()
        {
            EditorGUILayout.LabelField("Composition", EditorStyles.boldLabel);
            if (_selected.Slots == null || _selected.Slots.Count == 0)
            {
                EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
                return;
            }
            int minSum = 0, maxSum = 0;
            foreach (var slot in _selected.Slots)
            {
                if (slot == null || slot.Count == null) continue;
                int lo = slot.Count.Mode == RoomCountMode.Fixed ? slot.Count.Fixed : slot.Count.Min;
                int hi = slot.Count.Mode == RoomCountMode.Fixed ? slot.Count.Fixed : slot.Count.Max;
                minSum += lo; maxSum += hi;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"● {slot.Type}", GUILayout.Width(90f));
                EditorGUILayout.LabelField(slot.Count.Describe(),
                    slot.Count.Mode == RoomCountMode.Fixed ? EditorStyles.label : EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("Total", minSum == maxSum ? $"{minSum}" : $"{minSum}..{maxSum}",
                EditorStyles.boldLabel);
        }

        void DrawValidation()
        {
            EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
            if (_selected.Slots == null) return;

            bool hasStart = false;
            int startCount = 0;
            foreach (var slot in _selected.Slots)
            {
                if (slot == null || slot.Count == null) continue;
                bool poolEmpty = slot.Pool == null || slot.Pool.Count == 0;
                bool countZero = slot.Count.IsZero();

                if (poolEmpty && !countZero)
                {
                    StatusLine($"✗ {slot.Type}: count > 0 but pool empty", Color.red);
                }
                else if (slot.Count.Mode == RoomCountMode.Random && slot.Count.Min > slot.Count.Max)
                {
                    StatusLine($"✗ {slot.Type}: Min > Max", Color.red);
                }
                else if (countZero && !poolEmpty)
                {
                    StatusLine($"⚠ {slot.Type}: count = 0 (pool reserved)", new Color(1f, 0.7f, 0.2f));
                }
                else
                {
                    StatusLine($"✓ {slot.Type}: ok", new Color(0.55f, 0.85f, 0.45f));
                }

                if (slot.Type == RoomType.Start)
                {
                    hasStart = true;
                    if (slot.Count.Mode == RoomCountMode.Fixed) startCount += slot.Count.Fixed;
                    else startCount += slot.Count.Min;
                }
            }

            if (!hasStart)
            {
                StatusLine("⚠ no Start slot — spawn cell will use Combat fallback",
                    new Color(1f, 0.7f, 0.2f));
            }
            else if (startCount != 1)
            {
                StatusLine($"⚠ Start count = {startCount} (expected 1)",
                    new Color(1f, 0.7f, 0.2f));
            }
        }

        void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save", GUILayout.Height(24f)))
            {
                EditorUtility.SetDirty(_selected);
                AssetDatabase.SaveAssets();
            }
            if (GUILayout.Button("Duplicate", GUILayout.Height(24f)))
            {
                DuplicateSelected();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("Delete Asset", GUILayout.Height(22f)))
            {
                if (EditorUtility.DisplayDialog("Delete FloorLayout",
                    $"Delete '{_selected.name}'? This cannot be undone.", "Delete", "Cancel"))
                {
                    DeleteSelected();
                }
            }
            GUI.backgroundColor = prev;
        }

        // ── Helpers ─────────────────────────────────────────────

        void SelectFloor(FloorLayoutSO floor)
        {
            _selected = floor;
            _collapsed.Clear();
            _searchOpenForSlotKey = null;
        }

        void RefreshFloorList()
        {
            _floors.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:FloorLayoutSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var floor = AssetDatabase.LoadAssetAtPath<FloorLayoutSO>(path);
                if (floor != null) _floors.Add(floor);
            }
            if (_selected != null && !_floors.Contains(_selected)) _selected = null;
        }

        void RefreshRoomCache()
        {
            _allRooms.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:RoomSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var room = AssetDatabase.LoadAssetAtPath<RoomSO>(path);
                if (room != null) _allRooms.Add(room);
            }
            _allRooms = _allRooms.OrderBy(r => r.Type).ThenBy(r => r.name).ToList();
        }

        void CreateNewFloor()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Floor Layout", "FloorLayout", "asset",
                "Choose where to save the new FloorLayoutSO.");
            if (string.IsNullOrEmpty(path)) return;

            var floor = ScriptableObject.CreateInstance<FloorLayoutSO>();
            floor.Slots = new List<RoomTypeSlot>();
            AssetDatabase.CreateAsset(floor, path);
            AssetDatabase.SaveAssets();
            RefreshFloorList();
            SelectFloor(floor);
        }

        void DuplicateSelected()
        {
            if (_selected == null) return;
            string path = AssetDatabase.GetAssetPath(_selected);
            string newPath = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CopyAsset(path, newPath);
            AssetDatabase.SaveAssets();
            RefreshFloorList();
            var copy = AssetDatabase.LoadAssetAtPath<FloorLayoutSO>(newPath);
            if (copy != null) SelectFloor(copy);
        }

        void DeleteSelected()
        {
            if (_selected == null) return;
            string path = AssetDatabase.GetAssetPath(_selected);
            AssetDatabase.DeleteAsset(path);
            _selected = null;
            RefreshFloorList();
        }

        static Color TypeColor(RoomType t) => t switch
        {
            RoomType.Start  => new Color(0.36f, 0.72f, 0.36f),
            RoomType.Combat => new Color(0.85f, 0.33f, 0.30f),
            RoomType.Shop   => new Color(0.94f, 0.68f, 0.31f),
            RoomType.Potion => new Color(0.36f, 0.75f, 0.87f),
            RoomType.Boss   => new Color(0.72f, 0.36f, 1.00f),
            RoomType.Enchantment   => new Color(0.12f, 0.36f, 1.00f),
            _               => Color.gray,
        };

        static void StatusLine(string text, Color color)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = color;
            EditorGUILayout.LabelField(text);
            GUI.contentColor = prev;
        }

        static void Sep()
        {
            var r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.12f));
        }
    }
}
