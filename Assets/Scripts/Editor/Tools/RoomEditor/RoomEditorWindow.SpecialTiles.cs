using System.Collections.Generic;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Rollgeon.Tiles.Authoring;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.RoomEditor
{
    public sealed partial class RoomEditorWindow
    {
        // ============================ Tab — Special Tiles ============================

        private const float SpecialR = 0.55f, SpecialG = 0.75f, SpecialB = 0.95f;

        public enum SpecialPaintMode
        {
            Permanent,
            Slot,
            PortalPair
        }

        // -------- State (persisted via SerializeField) --------

        [HideInInspector, SerializeField] private bool _specialToolActive;
        [HideInInspector, SerializeField] private SpecialPaintMode _specialPaintMode = SpecialPaintMode.Permanent;
        [HideInInspector, SerializeField] private bool _specialGizmoHidden;
        [HideInInspector, SerializeField] private int _selectedSpecialDefIndex = -1;

        // -------- Transient state — GridCoord? no es serializable por Unity de forma nativa,
        // y perder selección/pendiente entre reloads del editor es aceptable (mismo criterio
        // que _draggingSpawnPoint en el tab de Spawn Points). --------

        [System.NonSerialized] private GridCoord? _pendingPortalA;
        [System.NonSerialized] private GridCoord? _selectedSpecialCoord;
        [System.NonSerialized] private GridCoord? _specialHoverCoord;
        [System.NonSerialized] private Vector3 _specialHoverWorld;
        [System.NonSerialized] private List<SpecialTileDefinitionSO> _cachedSpecialDefs;
        [System.NonSerialized] private Vector2 _specialPaletteScroll;
        [System.NonSerialized] private Vector2 _specialListScroll;

        // ============================ Tool section ============================

        [TabGroup(Tabs, TabSpecial), BoxGroup(GSpecialTool, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawSpecialToolSectionHeader() => DrawSectionHeader("Special Tile Tool", new Color(SpecialR, SpecialG, SpecialB));

        [TabGroup(Tabs, TabSpecial), BoxGroup(GSpecialTool, false), PropertyOrder(0), OnInspectorGUI]
        private void DrawSpecialToolToggle()
        {
            if (_target == null)
            {
                EditorGUILayout.HelpBox(
                    "Open or create a room prefab to manage special tiles.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = _specialToolActive
                ? new Color(0.5f, 0.7f, 0.95f)
                : new Color(0.72f, 0.72f, 0.72f);
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 34f,
                alignment = TextAnchor.MiddleCenter,
            };
            var label = _specialToolActive
                ? "● SPECIAL TILES ACTIVE  —  click to deactivate"
                : "○ SPECIAL TILES INACTIVE  —  click to activate";
            if (GUILayout.Button(label, style))
            {
                _specialToolActive = !_specialToolActive;
                if (_specialToolActive)
                {
                    _toolActive = false; // mutually exclusive with tile paint
                    _spawnToolActive = false; // mutually exclusive with spawn paint
                }
                _pendingPortalA = null; // salir de la herramienta cancela cualquier par a medio armar
                Repaint();
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = prev;
            EditorGUILayout.Space(4);

            int newModeIdx = GUILayout.Toolbar((int)_specialPaintMode, new[] { "Permanent", "Slot", "Portal Pair" });
            var newMode = (SpecialPaintMode)newModeIdx;
            if (newMode != _specialPaintMode)
            {
                _specialPaintMode = newMode;
                _pendingPortalA = null; // cambiar de modo cancela un par a medio colocar
                SceneView.RepaintAll();
            }
            EditorGUILayout.Space(4);

            EditorGUILayout.HelpBox(SpecialModeHelpText(_specialPaintMode), MessageType.None);

            if (_pendingPortalA.HasValue)
            {
                EditorGUILayout.HelpBox(
                    $"Extremo A pendiente en {_pendingPortalA.Value}. Click en OTRA celda para cerrar el par, Esc para cancelar.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            _specialGizmoHidden = EditorGUILayout.ToggleLeft("Hide placed entries in Scene", _specialGizmoHidden);
            if (GUI.changed) SceneView.RepaintAll();
        }

        private static string SpecialModeHelpText(SpecialPaintMode mode)
        {
            switch (mode)
            {
                case SpecialPaintMode.Permanent:
                    return "LMB en celda libre        → coloca la definición seleccionada\n" +
                           "LMB sobre existente        → selecciona\n" +
                           "Shift+LMB / RMB            → borra";
                case SpecialPaintMode.Slot:
                    return "LMB en celda libre        → crea un slot (editá sus opciones abajo)\n" +
                           "LMB sobre existente        → selecciona\n" +
                           "Shift+LMB / RMB            → borra";
                case SpecialPaintMode.PortalPair:
                    return "1er LMB en celda libre    → fija el extremo A (pendiente, sin guardar)\n" +
                           "2do LMB en OTRA celda     → crea el par completo\n" +
                           "Esc                        → cancela el extremo pendiente\n" +
                           "Shift+LMB / RMB            → borra el PAR entero";
                default:
                    return string.Empty;
            }
        }

        // ============================ Palette section ============================

        [TabGroup(Tabs, TabSpecial), BoxGroup(GSpecialPalette, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawSpecialPaletteSectionHeader() => DrawSectionHeader("Definitions", new Color(SpecialR, SpecialG, SpecialB));

        [TabGroup(Tabs, TabSpecial), BoxGroup(GSpecialPalette, false), PropertyOrder(0), OnInspectorGUI]
        private void DrawSpecialPaletteGrid()
        {
            if (_specialPaintMode == SpecialPaintMode.Slot)
            {
                EditorGUILayout.HelpBox(
                    "Los slots no usan una definición fija de paleta — sus opciones se editan " +
                    "inline en cada card, abajo.",
                    MessageType.None);
                return;
            }

            var defs = SpecialDefs();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                    RefreshSpecialDefs();
            }

            if (defs.Count == 0)
            {
                EditorGUILayout.HelpBox("No se encontraron SpecialTileDefinitionSO en el proyecto.", MessageType.Info);
                return;
            }

            const int cols = 3;
            _specialPaletteScroll = EditorGUILayout.BeginScrollView(_specialPaletteScroll, GUILayout.MinHeight(90), GUILayout.MaxHeight(200));
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < defs.Count; i++)
            {
                if (i > 0 && i % cols == 0)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                }

                var def = defs[i];
                var content = def.EditorIcon != null
                    ? new GUIContent(def.EditorIcon, def.DisplayName)
                    : new GUIContent(string.IsNullOrEmpty(def.DisplayName) ? def.name : def.DisplayName);

                bool selected = _selectedSpecialDefIndex == i;
                var prevBg = GUI.backgroundColor;
                if (selected) GUI.backgroundColor = new Color(0.5f, 0.85f, 1f);
                if (GUILayout.Button(content, GUILayout.Height(44), GUILayout.MinWidth(72)))
                    _selectedSpecialDefIndex = i;
                GUI.backgroundColor = prevBg;

                if (Event.current.type == EventType.Repaint)
                    DrawRectBorder(GUILayoutUtility.GetLastRect(), RoomEditorSpecialTileGizmos.ColorForDefinition(def), 2f);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        // ============================ List section ============================

        [TabGroup(Tabs, TabSpecial), BoxGroup(GSpecialList, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawSpecialListSectionHeader() => DrawSectionHeader("Placed Entries", new Color(SpecialR, SpecialG, SpecialB));

        [TabGroup(Tabs, TabSpecial), BoxGroup(GSpecialList, false), PropertyOrder(0), OnInspectorGUI]
        private void DrawSpecialList()
        {
            if (_target == null) return;

            _specialListScroll = EditorGUILayout.BeginScrollView(_specialListScroll, GUILayout.MinHeight(150), GUILayout.MaxHeight(420));

            EditorGUILayout.LabelField("Permanentes", EditorStyles.boldLabel);
            if (_target.SpecialTilePlacements == null || _target.SpecialTilePlacements.Count == 0)
            {
                EditorGUILayout.HelpBox("Sin casillas permanentes.", MessageType.None);
            }
            else
            {
                for (int i = 0; i < _target.SpecialTilePlacements.Count; i++)
                {
                    var p = _target.SpecialTilePlacements[i];
                    if (p == null) continue;
                    DrawPermanentCard(p);
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Slots", EditorStyles.boldLabel);
            if (_target.SpecialTileSlots == null || _target.SpecialTileSlots.Count == 0)
            {
                EditorGUILayout.HelpBox("Sin slots.", MessageType.None);
            }
            else
            {
                for (int i = 0; i < _target.SpecialTileSlots.Count; i++)
                {
                    var s = _target.SpecialTileSlots[i];
                    if (s == null) continue;
                    DrawSlotCard(s);
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Portales", EditorStyles.boldLabel);
            if (_target.PortalPairs == null || _target.PortalPairs.Count == 0)
            {
                EditorGUILayout.HelpBox("Sin portales.", MessageType.None);
            }
            else
            {
                for (int i = 0; i < _target.PortalPairs.Count; i++)
                {
                    var pp = _target.PortalPairs[i];
                    if (pp == null) continue;
                    DrawPortalCard(pp);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPermanentCard(SpecialTilePlacement p)
        {
            bool isSelected = _selectedSpecialCoord.HasValue && _selectedSpecialCoord.Value == p.Coord;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var label = p.Definition != null
                        ? (string.IsNullOrEmpty(p.Definition.DisplayName) ? p.Definition.TileId : p.Definition.DisplayName)
                        : "(sin definición)";
                    var nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
                    if (isSelected) nameStyle.normal.textColor = new Color(0.5f, 0.85f, 1f);
                    GUILayout.Label(label, nameStyle, GUILayout.MinWidth(100));
                    GUILayout.Label($"celda {p.Coord}", EditorStyles.miniLabel);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Frame", GUILayout.Width(56)))
                        FrameSpecialCoord(p.Coord);
                    if (GUILayout.Button(isSelected ? "✓ Sel" : "Select", GUILayout.Width(60)))
                    {
                        _selectedSpecialCoord = p.Coord;
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = new Color(0.95f, 0.55f, 0.55f);
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        SpecialTileOps.RemoveAt(_target, p.Coord);
                        if (_selectedSpecialCoord.HasValue && _selectedSpecialCoord.Value == p.Coord) _selectedSpecialCoord = null;
                        GUI.backgroundColor = Color.white;
                        Repaint();
                        SceneView.RepaintAll();
                        return;
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            EditorGUILayout.Space(2);
        }

        private void DrawSlotCard(SpecialTileSlot s)
        {
            bool isSelected = _selectedSpecialCoord.HasValue && _selectedSpecialCoord.Value == s.Coord;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("SlotId", GUILayout.Width(46));

                    EditorGUI.BeginChangeCheck();
                    var newId = EditorGUILayout.TextField(s.SlotId, GUILayout.MinWidth(70));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(_target, SpecialTileOps.UndoLabel);
                        s.SlotId = newId;
                        EditorUtility.SetDirty(_target);
                    }

                    bool idEmpty = string.IsNullOrEmpty(s.SlotId);
                    bool idDup = !idEmpty && IsSlotIdDuplicate(s.SlotId, s);
                    if (idEmpty || idDup)
                    {
                        GUI.contentColor = new Color(1f, 0.5f, 0.5f);
                        GUILayout.Label(idEmpty ? "vacío" : "duplicado", EditorStyles.miniBoldLabel, GUILayout.Width(58));
                        GUI.contentColor = Color.white;
                    }

                    GUILayout.FlexibleSpace();
                    GUILayout.Label($"celda {s.Coord}", EditorStyles.miniLabel);

                    if (GUILayout.Button("Frame", GUILayout.Width(56)))
                        FrameSpecialCoord(s.Coord);
                    if (GUILayout.Button(isSelected ? "✓ Sel" : "Select", GUILayout.Width(60)))
                    {
                        _selectedSpecialCoord = s.Coord;
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = new Color(0.95f, 0.55f, 0.55f);
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        SpecialTileOps.RemoveAt(_target, s.Coord);
                        if (_selectedSpecialCoord.HasValue && _selectedSpecialCoord.Value == s.Coord) _selectedSpecialCoord = null;
                        GUI.backgroundColor = Color.white;
                        Repaint();
                        SceneView.RepaintAll();
                        return;
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUI.BeginChangeCheck();
                var newGroup = (SpecialTileOptionGroupSO)EditorGUILayout.ObjectField(
                    "Group", s.Group, typeof(SpecialTileOptionGroupSO), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_target, SpecialTileOps.UndoLabel);
                    s.Group = newGroup;
                    EditorUtility.SetDirty(_target);
                }

                using (new EditorGUI.DisabledScope(s.Group != null))
                {
                    EditorGUILayout.LabelField(
                        s.Group != null ? "Inline Options (el Group gana)" : "Inline Options",
                        EditorStyles.miniBoldLabel);

                    if (s.InlineOptions == null) s.InlineOptions = new List<SpecialTileDefinitionSO>();
                    for (int i = 0; i < s.InlineOptions.Count; i++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUI.BeginChangeCheck();
                            var next = (SpecialTileDefinitionSO)EditorGUILayout.ObjectField(
                                s.InlineOptions[i], typeof(SpecialTileDefinitionSO), false);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(_target, SpecialTileOps.UndoLabel);
                                s.InlineOptions[i] = next;
                                EditorUtility.SetDirty(_target);
                            }
                            if (GUILayout.Button("×", GUILayout.Width(24)))
                            {
                                Undo.RecordObject(_target, SpecialTileOps.UndoLabel);
                                s.InlineOptions.RemoveAt(i);
                                EditorUtility.SetDirty(_target);
                                break; // la lista mutó: cortamos el loop de este frame para no desalinear el layout
                            }
                        }
                    }
                    if (GUILayout.Button("+ Add Option", GUILayout.Width(110)))
                    {
                        Undo.RecordObject(_target, SpecialTileOps.UndoLabel);
                        s.InlineOptions.Add(null);
                        EditorUtility.SetDirty(_target);
                    }
                }

                EditorGUI.BeginChangeCheck();
                var canResolveEmpty = EditorGUILayout.ToggleLeft("Can Resolve Empty", s.CanResolveEmpty);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_target, SpecialTileOps.UndoLabel);
                    s.CanResolveEmpty = canResolveEmpty;
                    EditorUtility.SetDirty(_target);
                }

                int effCount = 0;
                var eff = s.EffectiveOptions;
                if (eff != null) foreach (var o in eff) if (o != null) effCount++;
                EditorGUILayout.LabelField(
                    $"{effCount} opción(es) efectiva(s)" + (s.CanResolveEmpty ? " + vacío" : ""),
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.Space(2);
        }

        private void DrawPortalCard(PortalPairPlacement pp)
        {
            bool isSelected = _selectedSpecialCoord.HasValue &&
                (_selectedSpecialCoord.Value == pp.CoordA || _selectedSpecialCoord.Value == pp.CoordB);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var label = pp.PortalDefinition != null
                        ? (string.IsNullOrEmpty(pp.PortalDefinition.DisplayName) ? pp.PortalDefinition.TileId : pp.PortalDefinition.DisplayName)
                        : "(sin definición)";
                    var nameStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
                    if (isSelected) nameStyle.normal.textColor = new Color(0.5f, 0.85f, 1f);
                    GUILayout.Label(label, nameStyle, GUILayout.MinWidth(100));
                    GUILayout.Label($"A {pp.CoordA}  ↔  B {pp.CoordB}", EditorStyles.miniLabel);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Frame", GUILayout.Width(56)))
                        FrameSpecialCoord(pp.CoordA);
                    if (GUILayout.Button(isSelected ? "✓ Sel" : "Select", GUILayout.Width(60)))
                    {
                        _selectedSpecialCoord = pp.CoordA;
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = new Color(0.95f, 0.55f, 0.55f);
                    if (GUILayout.Button("×", GUILayout.Width(24)))
                    {
                        // Borra el PAR entero — nunca queda un extremo huérfano.
                        SpecialTileOps.RemoveAt(_target, pp.CoordA);
                        if (isSelected) _selectedSpecialCoord = null;
                        GUI.backgroundColor = Color.white;
                        Repaint();
                        SceneView.RepaintAll();
                        return;
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            EditorGUILayout.Space(2);
        }

        // ============================ Validation section ============================

        [TabGroup(Tabs, TabSpecial), BoxGroup(GSpecialValidation, false), PropertyOrder(-100), OnInspectorGUI]
        private void DrawSpecialValidationSectionHeader() => DrawSectionHeader("Validation", new Color(SpecialR, SpecialG, SpecialB));

        [TabGroup(Tabs, TabSpecial), BoxGroup(GSpecialValidation, false), PropertyOrder(0), OnInspectorGUI]
        private void DrawSpecialValidation()
        {
            if (_target == null) return;

            var messages = SpecialTileOps.Validate(_target);
            if (messages.Count == 0)
            {
                EditorGUILayout.HelpBox("Sin problemas detectados.", MessageType.Info);
                return;
            }

            foreach (var m in messages)
                EditorGUILayout.HelpBox(m, m.StartsWith("ERROR") ? MessageType.Error : MessageType.Warning);
        }

        // ============================ Scene input ============================

        private void UpdateSpecialHover(Event e)
        {
            if (_target == null) { _specialHoverCoord = null; return; }

            var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            var origin = _target.GetOrigin();
            var plane = new Plane(Vector3.up, origin);
            if (!plane.Raycast(ray, out var dist))
            {
                _specialHoverCoord = null;
                return;
            }

            var point = ray.GetPoint(dist);
            _specialHoverWorld = point;
            _specialHoverCoord = WorldToGridCoord(point);
        }

        private void HandleSpecialSceneInput(Event e, int controlId)
        {
            if (_target == null) return;

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && _pendingPortalA.HasValue)
            {
                _pendingPortalA = null;
                e.Use();
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            if (!_specialHoverCoord.HasValue) return;
            if (e.type != EventType.MouseDown || e.alt) return;

            var coord = _specialHoverCoord.Value;

            // Right-click or shift+left-click → delete (borra el par entero si es un extremo de portal).
            bool isDelete = e.button == 1 || (e.button == 0 && e.shift);
            if (isDelete)
            {
                if (SpecialTileOps.RemoveAt(_target, coord))
                {
                    if (_selectedSpecialCoord.HasValue && _selectedSpecialCoord.Value == coord) _selectedSpecialCoord = null;
                    if (_pendingPortalA.HasValue && _pendingPortalA.Value == coord) _pendingPortalA = null;
                    GUIUtility.hotControl = controlId;
                    e.Use();
                    Repaint();
                    SceneView.RepaintAll();
                }
                return;
            }

            if (e.button != 0) return;

            switch (_specialPaintMode)
            {
                case SpecialPaintMode.Permanent:
                    HandleSpecialPermanentClick(coord, controlId, e);
                    break;
                case SpecialPaintMode.Slot:
                    HandleSpecialSlotClick(coord, controlId, e);
                    break;
                case SpecialPaintMode.PortalPair:
                    HandleSpecialPortalClick(coord, controlId, e);
                    break;
            }
        }

        private void HandleSpecialPermanentClick(GridCoord coord, int controlId, Event e)
        {
            if (!SpecialTileOps.IsCellFree(_target, coord))
            {
                SelectAndConsume(coord, controlId, e);
                return;
            }

            var def = SelectedSpecialDef();
            if (def == null) return;

            SpecialTileOps.AddPermanent(_target, def, coord);
            SelectAndConsume(coord, controlId, e);
        }

        private void HandleSpecialSlotClick(GridCoord coord, int controlId, Event e)
        {
            if (!SpecialTileOps.IsCellFree(_target, coord))
            {
                SelectAndConsume(coord, controlId, e);
                return;
            }

            SpecialTileOps.AddSlot(_target, coord);
            SelectAndConsume(coord, controlId, e);
        }

        private void HandleSpecialPortalClick(GridCoord coord, int controlId, Event e)
        {
            if (!_pendingPortalA.HasValue)
            {
                if (!SpecialTileOps.IsCellFree(_target, coord))
                {
                    SelectAndConsume(coord, controlId, e);
                    return;
                }

                var def = SelectedSpecialDef();
                if (def == null) return;

                // Solo fija el extremo A en memoria — NADA se serializa hasta el segundo click.
                _pendingPortalA = coord;
                GUIUtility.hotControl = controlId;
                e.Use();
                Repaint();
                SceneView.RepaintAll();
                return;
            }

            if (coord == _pendingPortalA.Value) return; // mismo punto: esperar otro click
            if (!SpecialTileOps.IsCellFree(_target, coord)) return;

            var portalDef = SelectedSpecialDef();
            if (portalDef == null)
            {
                _pendingPortalA = null;
                return;
            }

            SpecialTileOps.AddPortalPair(_target, portalDef, _pendingPortalA.Value, coord);
            _pendingPortalA = null;
            SelectAndConsume(coord, controlId, e);
        }

        private void SelectAndConsume(GridCoord coord, int controlId, Event e)
        {
            _selectedSpecialCoord = coord;
            GUIUtility.hotControl = controlId;
            e.Use();
            Repaint();
            SceneView.RepaintAll();
        }

        // ============================ Helpers ============================

        private List<SpecialTileDefinitionSO> SpecialDefs()
        {
            if (_cachedSpecialDefs == null) RefreshSpecialDefs();
            return _cachedSpecialDefs;
        }

        private void RefreshSpecialDefs()
        {
            _cachedSpecialDefs = new List<SpecialTileDefinitionSO>();
            var guids = AssetDatabase.FindAssets("t:SpecialTileDefinitionSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(path);
                if (def != null) _cachedSpecialDefs.Add(def);
            }
            _cachedSpecialDefs.Sort((a, b) => string.Compare(
                a.DisplayName ?? a.name, b.DisplayName ?? b.name, System.StringComparison.OrdinalIgnoreCase));
        }

        private SpecialTileDefinitionSO SelectedSpecialDef()
        {
            var defs = SpecialDefs();
            if (_selectedSpecialDefIndex < 0 || _selectedSpecialDefIndex >= defs.Count) return null;
            return defs[_selectedSpecialDefIndex];
        }

        private bool IsSlotIdDuplicate(string id, SpecialTileSlot self)
        {
            if (_target?.SpecialTileSlots == null) return false;
            foreach (var s in _target.SpecialTileSlots)
                if (s != null && s != self && s.SlotId == id) return true;
            return false;
        }

        /// <summary>Mismo cálculo que <see cref="RoomEditorSpecialTileGizmos.CellCenter"/> — ver esa nota para el porqué.</summary>
        private Vector3 SpecialCellCenter(GridCoord c) => RoomEditorSpecialTileGizmos.CellCenter(_target, c);

        private GridCoord WorldToGridCoord(Vector3 world)
        {
            var origin = _target.GetOrigin();
            float ts = Mathf.Max(_target.TileSize, 0.01f);
            int x = Mathf.FloorToInt((world.x - origin.x) / ts);
            int y = Mathf.FloorToInt((world.z - origin.z) / ts);
            return new GridCoord(x, y);
        }

        private void FrameSpecialCoord(GridCoord c)
        {
            var view = SceneView.lastActiveSceneView;
            if (view != null) view.LookAt(SpecialCellCenter(c));
        }
    }
}
