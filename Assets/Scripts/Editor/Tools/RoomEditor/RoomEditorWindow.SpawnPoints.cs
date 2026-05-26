using System.Collections.Generic;
using Rollgeon.Dungeon.Components;
using Rollgeon.Entities;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    public sealed partial class RoomEditorWindow
    {
        // ============================ Tab — Spawn Points ============================

        private const float SpawnR = 1.0f, SpawnG = 0.45f, SpawnB = 0.55f;

        public enum SpawnGizmoMode
        {
            PreviewSet,
            ColorPerSet,
            Hide
        }

        // -------- State (persisted via SerializeField) --------

        [HideInInspector, SerializeField] private bool _spawnToolActive;
        [HideInInspector, SerializeField] private int _previewSetIndex;
        [HideInInspector, SerializeField] private SpawnGizmoMode _spawnGizmoMode = SpawnGizmoMode.PreviewSet;
        [HideInInspector, SerializeField] private Transform _selectedSpawnPoint;

        [System.NonSerialized] private Transform _draggingSpawnPoint;
        [System.NonSerialized] private Vector3Int? _draggingFromCell;

        private static readonly Color[] SetColors =
        {
            new Color(0.35f, 0.66f, 1f),    // blue   — Set 0
            new Color(0.94f, 0.72f, 0.37f), // orange — Set 1
            new Color(0.55f, 0.82f, 0.49f), // green  — Set 2
            new Color(0.72f, 0.36f, 1f),    // purple — Set 3
            new Color(1f, 0.45f, 0.55f),    // pink   — Set 4
            new Color(0.46f, 0.88f, 0.88f), // cyan   — Set 5
        };

        internal static Color ColorForSet(int index)
        {
            if (index < 0) return Color.white;
            return SetColors[index % SetColors.Length];
        }

        // ============================ Tool section ============================

        [TabGroup(Tabs, TabSpawn), BoxGroup(GSpawnTool, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawSpawnToolSectionHeader() => DrawSectionHeader("Spawn Tool", new Color(SpawnR, SpawnG, SpawnB));

        [TabGroup(Tabs, TabSpawn), BoxGroup(GSpawnTool, false), PropertyOrder(0), OnInspectorGUI]
        private void DrawSpawnToolToggle()
        {
            if (_target == null)
            {
                EditorGUILayout.HelpBox(
                    "Open or create a room prefab to manage spawn points.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = _spawnToolActive
                ? new Color(0.95f, 0.45f, 0.55f)
                : new Color(0.72f, 0.72f, 0.72f);
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 34f,
                alignment = TextAnchor.MiddleCenter,
            };
            var label = _spawnToolActive
                ? "● SPAWN PAINT ACTIVE  —  click to deactivate"
                : "○ SPAWN PAINT INACTIVE  —  click to activate";
            if (GUILayout.Button(label, style))
            {
                _spawnToolActive = !_spawnToolActive;
                if (_spawnToolActive) _toolActive = false; // mutually exclusive with tile paint
                _draggingSpawnPoint = null;
                _draggingFromCell = null;
                Repaint();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prev;
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(
                "LMB on empty cell      → create new spawn point\n" +
                "LMB on existing SP     → select it (drag to move)\n" +
                "Shift+LMB / RMB on SP  → delete\n" +
                "Q / E                  → change layer",
                MessageType.None);
        }

        // ============================ Sets section ============================

        [TabGroup(Tabs, TabSpawn), BoxGroup(GSpawnSets, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawSpawnSetsSectionHeader() => DrawSectionHeader("Sets", new Color(SpawnR, SpawnG, SpawnB));

        [TabGroup(Tabs, TabSpawn), BoxGroup(GSpawnSets, false), PropertyOrder(0), OnInspectorGUI]
        private void DrawSpawnSetsControls()
        {
            if (_target == null) return;

            int setCount = SpawnPointOps.GetMaxSetCount(_target);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Set Count", GUILayout.Width(160));
                EditorGUILayout.LabelField(setCount.ToString(), EditorStyles.boldLabel, GUILayout.Width(40));

                GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);
                if (GUILayout.Button("+ Add Set", GUILayout.Width(90)))
                {
                    SpawnPointOps.AddSetSlot(_target);
                    Repaint();
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = new Color(0.95f, 0.55f, 0.55f);
                if (GUILayout.Button("− Remove Set", GUILayout.Width(110)))
                {
                    if (setCount > 0)
                    {
                        SpawnPointOps.RemoveSetSlot(_target, setCount - 1);
                        _previewSetIndex = Mathf.Clamp(_previewSetIndex, 0, Mathf.Max(0, setCount - 2));
                        Repaint();
                        SceneView.RepaintAll();
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Preview Set", GUILayout.Width(160));
                int clampedMax = Mathf.Max(0, setCount - 1);
                int newIdx = EditorGUILayout.IntSlider(_previewSetIndex, 0, clampedMax);
                if (newIdx != _previewSetIndex)
                {
                    _previewSetIndex = newIdx;
                    SceneView.RepaintAll();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Gizmo Mode", GUILayout.Width(160));
                var newMode = (SpawnGizmoMode)EditorGUILayout.EnumPopup(_spawnGizmoMode);
                if (newMode != _spawnGizmoMode)
                {
                    _spawnGizmoMode = newMode;
                    SceneView.RepaintAll();
                }
            }
        }

        // ============================ List section ============================

        [TabGroup(Tabs, TabSpawn), BoxGroup(GSpawnList, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawSpawnListSectionHeader() => DrawSectionHeader("Spawn Points", new Color(SpawnR, SpawnG, SpawnB));

        [HideInInspector, SerializeField] private Vector2 _spawnListScroll;

        [TabGroup(Tabs, TabSpawn), BoxGroup(GSpawnList, false), PropertyOrder(0), OnInspectorGUI]
        private void DrawSpawnPointsList()
        {
            if (_target == null) return;

            var sps = _target.EnemySpawnPoints;
            if (sps == null || sps.Count == 0)
            {
                EditorGUILayout.HelpBox("No spawn points yet. Activate the Spawn Tool and click cells in the scene.", MessageType.None);
                return;
            }

            EditorGUILayout.LabelField($"{sps.Count} spawn point(s)", EditorStyles.miniBoldLabel);

            int setCount = SpawnPointOps.GetMaxSetCount(_target);
            _spawnListScroll = EditorGUILayout.BeginScrollView(_spawnListScroll, GUILayout.MinHeight(150), GUILayout.MaxHeight(420));

            for (int i = 0; i < sps.Count; i++)
            {
                var sp = sps[i];
                if (sp == null)
                {
                    DrawNullSpawnRow(i);
                    continue;
                }
                DrawSpawnCard(sp, i, setCount);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawNullSpawnRow(int index)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUI.contentColor = new Color(1f, 0.6f, 0.6f);
                EditorGUILayout.LabelField($"[{index}] (null reference)", EditorStyles.boldLabel);
                GUI.contentColor = Color.white;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Remove", GUILayout.Width(80)))
                {
                    SpawnPointOps.RemoveNullAt(_target, index);
                    Repaint();
                    SceneView.RepaintAll();
                }
            }
        }

        private void DrawSpawnCard(Transform sp, int index, int setCount)
        {
            bool isSelected = sp == _selectedSpawnPoint;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
                    if (isSelected) nameStyle.normal.textColor = new Color(0.5f, 0.85f, 1f);
                    GUILayout.Label(sp.name, nameStyle, GUILayout.MinWidth(100));

                    var local = _target.transform.InverseTransformPoint(sp.position);
                    var cell = WorldToCellNoRotation(sp.position);
                    GUILayout.Label($"cell ({cell.x}, {cell.y}, {cell.z})  ·  local ({local.x:F2}, {local.y:F2}, {local.z:F2})",
                        EditorStyles.miniLabel);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Frame", GUILayout.Width(56)))
                    {
                        Selection.activeGameObject = sp.gameObject;
                        SceneView.FrameLastActiveSceneView();
                    }
                    if (GUILayout.Button(isSelected ? "✓ Sel" : "Select", GUILayout.Width(60)))
                    {
                        _selectedSpawnPoint = sp;
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = new Color(0.95f, 0.55f, 0.55f);
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        SpawnPointOps.RemoveSpawnPoint(_target, sp);
                        if (_selectedSpawnPoint == sp) _selectedSpawnPoint = null;
                        Repaint();
                        SceneView.RepaintAll();
                        GUI.backgroundColor = Color.white;
                        return;
                    }
                    GUI.backgroundColor = Color.white;
                }

                var config = sp.GetComponent<SpawnPointConfig>();
                if (config == null)
                {
                    EditorGUILayout.HelpBox("Missing SpawnPointConfig component.", MessageType.Warning);
                    if (GUILayout.Button("Add SpawnPointConfig"))
                    {
                        Undo.AddComponent<SpawnPointConfig>(sp.gameObject);
                        Repaint();
                    }
                    return;
                }

                if (setCount == 0)
                {
                    EditorGUILayout.HelpBox("No sets defined. Press '+ Add Set' above.", MessageType.None);
                    return;
                }

                var so = new SerializedObject(config);
                var listProp = so.FindProperty(nameof(SpawnPointConfig.EnemySets));
                if (listProp == null) return;

                for (int s = 0; s < setCount; s++)
                {
                    while (listProp.arraySize <= s)
                        listProp.InsertArrayElementAtIndex(listProp.arraySize);

                    var element = listProp.GetArrayElementAtIndex(s);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var swatchRect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
                        EditorGUI.DrawRect(swatchRect, ColorForSet(s));
                        EditorGUILayout.LabelField($"Set {s}", GUILayout.Width(48));

                        EditorGUI.BeginChangeCheck();
                        var next = EditorGUILayout.ObjectField(element.objectReferenceValue, typeof(EnemyDataSO), allowSceneObjects: false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            element.objectReferenceValue = next;
                        }
                    }
                }
                so.ApplyModifiedProperties();
            }
            EditorGUILayout.Space(2);
        }

        // ============================ Validation section ============================

        [TabGroup(Tabs, TabSpawn), BoxGroup(GSpawnValidation, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawSpawnValidationSectionHeader() => DrawSectionHeader("Validation", new Color(SpawnR, SpawnG, SpawnB));

        [TabGroup(Tabs, TabSpawn), BoxGroup(GSpawnValidation, false), PropertyOrder(0), OnInspectorGUI]
        private void DrawSpawnValidation()
        {
            if (_target == null) return;

            var report = SpawnPointOps.Validate(_target);

            if (report.Issues.Count == 0)
            {
                EditorGUILayout.HelpBox("All spawn points are well-formed.", MessageType.Info);
            }
            else
            {
                foreach (var issue in report.Issues)
                    EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Re-sync EnemySpawnPoints list"))
                {
                    int added = SpawnPointOps.ResyncSpawnPointList(_target);
                    Debug.Log($"[RoomEditor] Re-sync: added {added} missing spawn point reference(s).");
                    Repaint();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Normalize SetCount"))
                {
                    int padded = SpawnPointOps.NormalizeSetCount(_target);
                    Debug.Log($"[RoomEditor] Normalize: padded {padded} set slot(s) across configs.");
                    Repaint();
                    SceneView.RepaintAll();
                }
            }
        }

        // ============================ Scene input ============================

        private void HandleSpawnSceneInput(Event e, int controlId)
        {
            if (_target == null) return;

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _draggingSpawnPoint = null;
                _draggingFromCell = null;
if (GUIUtility.hotControl == controlId) GUIUtility.hotControl = 0;
            }

            if (!_hoverCell.HasValue) return;
            var cell = _hoverCell.Value;

            // Drag-move (active drag continues even off-cell since we sample fresh each frame).
            if (e.type == EventType.MouseDrag && e.button == 0 && _draggingSpawnPoint != null && !e.alt)
            {
                var target = SpawnCellCenter(cell);
                if (_draggingSpawnPoint.position != target)
                {
                    SpawnPointOps.MoveSpawnPoint(_draggingSpawnPoint, target);
                    Repaint();
                    SceneView.RepaintAll();
                }
                GUIUtility.hotControl = controlId;
                e.Use();
                return;
            }

            if (e.type != EventType.MouseDown || e.alt) return;

            var existingSp = FindSpawnAtCell(cell);

            // Right-click or shift+left-click → delete.
            bool isDelete = e.button == 1 || (e.button == 0 && e.shift);
            if (isDelete)
            {
                if (existingSp != null)
                {
                    SpawnPointOps.RemoveSpawnPoint(_target, existingSp);
                    if (_selectedSpawnPoint == existingSp) _selectedSpawnPoint = null;
                    GUIUtility.hotControl = controlId;
                    e.Use();
                    Repaint();
                    SceneView.RepaintAll();
                }
                return;
            }

            if (e.button != 0) return;

            if (existingSp != null)
            {
                _selectedSpawnPoint = existingSp;
                _draggingSpawnPoint = existingSp;
                _draggingFromCell = cell;
GUIUtility.hotControl = controlId;
                e.Use();
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            // Empty cell → create a new SP.
            int setCount = SpawnPointOps.GetMaxSetCount(_target);
            var created = SpawnPointOps.AddSpawnPoint(_target, SpawnCellCenter(cell), setCount);
            _selectedSpawnPoint = created;
            GUIUtility.hotControl = controlId;
            e.Use();
            Repaint();
            SceneView.RepaintAll();
        }

        // ============================ Helpers ============================

        private Vector3 SpawnCellCenter(Vector3Int cell)
        {
            var origin = _target.GetOrigin();
            return new Vector3(
                origin.x + (cell.x + 0.5f) * _gridStep.x,
                origin.y + (cell.y + 0.5f) * _gridStep.y,
                origin.z + (cell.z + 0.5f) * _gridStep.z);
        }

        private Vector3Int WorldToCellNoRotation(Vector3 world)
        {
            var origin = _target.GetOrigin();
            return new Vector3Int(
                Mathf.FloorToInt((world.x - origin.x) / _gridStep.x),
                Mathf.FloorToInt((world.y - origin.y) / _gridStep.y),
                Mathf.FloorToInt((world.z - origin.z) / _gridStep.z));
        }

        private Transform FindSpawnAtCell(Vector3Int cell)
        {
            if (_target == null || _target.EnemySpawnPoints == null) return null;
            foreach (var sp in _target.EnemySpawnPoints)
            {
                if (sp == null) continue;
                if (WorldToCellNoRotation(sp.position) == cell) return sp;
            }
            return null;
        }
    }
}
