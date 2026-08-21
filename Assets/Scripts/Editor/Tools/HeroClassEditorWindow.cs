using System.Collections.Generic;
using Rollgeon.Editor.Tools.Polymorphic;
using Rollgeon.Heroes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    public sealed class HeroClassEditorWindow : EditorWindow
    {
        const float LeftWidth = 200f;
        const float MiddleWidth = 340f;

        readonly List<ClassHeroSO> _heroes = new List<ClassHeroSO>();
        readonly PolymorphicAuthoringContext _ctx = new PolymorphicAuthoringContext();
        ClassHeroSO _selected;
        int _behaviorIdx = -1;

        Vector2 _leftScroll, _midScroll, _rightScroll;

        // Search + validation (quick wins port de la CardDatabase tool de Bot-Game).
        string _search = string.Empty;
        readonly List<(MessageType severity, string message)> _issues
            = new List<(MessageType, string)>();
        bool _validated;

        [MenuItem("Tools/Hero Class Editor")]
        static void Open()
        {
            var w = GetWindow<HeroClassEditorWindow>("Hero Class Editor");
            w.minSize = new Vector2(920f, 500f);
        }

        void OnEnable()
        {
            RefreshList();
            Undo.undoRedoPerformed += OnUndo;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            DisposeTree();
        }

        void OnProjectChange()
        {
            RefreshList();
            Repaint();
        }

        void OnUndo()
        {
            if (_selected != null)
                RebuildTree();
            Repaint();
        }

        void OnGUI()
        {
            _ctx.UpdateTree();

            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawLeft();
            Sep();
            DrawMiddle();
            Sep();
            DrawRight();
            EditorGUILayout.EndHorizontal();

            _ctx.ApplyChanges();
        }

        // ── Toolbar: search + CRUD + validate ───────────────────

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Búsqueda live (sin submit) — filtra la lista de la izquierda.
            _search = GUILayout.TextField(
                _search, EditorStyles.toolbarSearchField, GUILayout.Width(LeftWidth - 8f));

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                RefreshList();
            }

            GUILayout.Space(8f);

            var prev = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
            if (GUILayout.Button("+ Create", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                CreateHeroAsset();
            GUI.backgroundColor = prev;

            using (new EditorGUI.DisabledScope(_selected == null))
            {
                if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(68f)))
                    DuplicateHeroAsset();

                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                if (GUILayout.Button("Delete", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                    DeleteHeroAsset();
                GUI.backgroundColor = prev;

                GUILayout.Space(8f);
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    RunValidation();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label($"{_heroes.Count} hero class(es)", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        void CreateHeroAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Hero Class", "CH_NewHero", "asset",
                "Ubicación del nuevo ClassHeroSO.", "Assets/Rollgeon/Classes");
            if (string.IsNullOrEmpty(path)) return;

            var hero = ScriptableObject.CreateInstance<ClassHeroSO>();
            // Defaults que respetan el Spec Daño v2 (dmg_base_PJ nunca 0).
            hero.BaseAttack = 1;
            AssetDatabase.CreateAsset(hero, path);
            AssetDatabase.SaveAssets();

            RefreshList();
            SelectHero(hero);
        }

        void DuplicateHeroAsset()
        {
            if (_selected == null) return;
            string srcPath = AssetDatabase.GetAssetPath(_selected);
            if (string.IsNullOrEmpty(srcPath)) return;

            string dstPath = AssetDatabase.GenerateUniqueAssetPath(
                srcPath.Replace(".asset", "_Copy.asset"));
            if (!AssetDatabase.CopyAsset(srcPath, dstPath)) return;
            AssetDatabase.SaveAssets();

            RefreshList();
            var copy = AssetDatabase.LoadAssetAtPath<ClassHeroSO>(dstPath);
            if (copy != null) SelectHero(copy);
        }

        void DeleteHeroAsset()
        {
            if (_selected == null) return;
            string path = AssetDatabase.GetAssetPath(_selected);
            if (!EditorUtility.DisplayDialog(
                    "Delete Hero Class",
                    $"¿Borrar '{_selected.name}'?\n\n{path}\n\nEsta acción no tiene undo.",
                    "Delete", "Cancel"))
                return;

            _selected = null;
            _behaviorIdx = -1;
            DisposeTree();
            AssetDatabase.DeleteAsset(path);
            RefreshList();
        }

        // ── Validation (versión mínima del CardValidationService de Bot-Game) ──

        void RunValidation()
        {
            _issues.Clear();
            _validated = true;
            if (_selected == null) return;

            if (string.IsNullOrEmpty(_selected.EntityId))
                _issues.Add((MessageType.Warning, "EntityId vacío — screens y catálogos lo usan como key."));
            if (string.IsNullOrEmpty(_selected.DisplayName))
                _issues.Add((MessageType.Warning, "DisplayName vacío — la UI de selección de clase muestra el nombre del asset."));

            if (_selected.BaseAttack <= 0)
                _issues.Add((MessageType.Error,
                    $"BaseAttack={_selected.BaseAttack}. Spec Daño v2 — dmg_base_PJ nunca debería ser 0."));

            if (_selected.Sheet == null)
            {
                _issues.Add((MessageType.Error, "Sheet (ContractSheet) es null."));
            }
            else
            {
                if (!_selected.Sheet.Validate(out var sheetError))
                    _issues.Add((MessageType.Error, $"ContractSheet: {sheetError}"));

                ValidateBaseDamageTable(_selected.Sheet);
                ValidateShieldBaseTable(_selected.Sheet);
                ValidateHealBaseTable(_selected.Sheet);
            }

            if (_issues.Count == 0)
                _issues.Add((MessageType.Info, "Sin problemas — la clase valida OK."));
        }

        void ValidateBaseDamageTable(ContractSheet sheet)
        {
            var table = sheet.BaseDamageTable;
            if (table == null || table.Count == 0) return;

            var seen = new HashSet<string>();
            foreach (var entry in table)
            {
                if (string.IsNullOrEmpty(entry.ComboId))
                {
                    _issues.Add((MessageType.Error, "BaseDamageTable: entrada con ComboId vacío."));
                    continue;
                }
                if (!seen.Add(entry.ComboId))
                    _issues.Add((MessageType.Error,
                        $"BaseDamageTable: ComboId duplicado '{entry.ComboId}' — gana la primera entrada."));

                bool inContract = sheet.Combos != null
                    && sheet.Combos.Exists(c => c != null && c.ComboId == entry.ComboId);
                if (!inContract)
                    _issues.Add((MessageType.Warning,
                        $"BaseDamageTable: '{entry.ComboId}' no está en el contrato de esta clase — la entrada no tiene efecto."));

                if (entry.BaseDamage <= 0)
                    _issues.Add((MessageType.Warning,
                        $"BaseDamageTable: '{entry.ComboId}' con BaseDamage={entry.BaseDamage}."));
            }
        }

        void ValidateShieldBaseTable(ContractSheet sheet)
        {
            var table = sheet.ShieldBaseTable;
            if (table == null || table.Count == 0) return;

            var seen = new HashSet<string>();
            foreach (var entry in table)
            {
                if (string.IsNullOrEmpty(entry.ComboId))
                {
                    _issues.Add((MessageType.Error, "ShieldBaseTable: entrada con ComboId vacío."));
                    continue;
                }
                if (!seen.Add(entry.ComboId))
                    _issues.Add((MessageType.Error,
                        $"ShieldBaseTable: ComboId duplicado '{entry.ComboId}' — gana la primera entrada."));

                bool inContract = sheet.Combos != null
                    && sheet.Combos.Exists(c => c != null && c.ComboId == entry.ComboId);
                if (!inContract)
                    _issues.Add((MessageType.Warning,
                        $"ShieldBaseTable: '{entry.ComboId}' no está en el contrato de esta clase — la entrada no tiene efecto."));

                if (entry.ShieldBase <= 0)
                    _issues.Add((MessageType.Warning,
                        $"ShieldBaseTable: '{entry.ComboId}' con ShieldBase={entry.ShieldBase} — " +
                        "entrada redundante, sin entrada ya es 0."));
            }
        }

        void ValidateHealBaseTable(ContractSheet sheet)
        {
            var table = sheet.HealBaseTable;
            if (table == null || table.Count == 0) return;

            var seen = new HashSet<string>();
            foreach (var entry in table)
            {
                if (string.IsNullOrEmpty(entry.ComboId))
                {
                    _issues.Add((MessageType.Error, "HealBaseTable: entrada con ComboId vacío."));
                    continue;
                }
                if (!seen.Add(entry.ComboId))
                    _issues.Add((MessageType.Error,
                        $"HealBaseTable: ComboId duplicado '{entry.ComboId}' — gana la primera entrada."));

                bool inContract = sheet.Combos != null
                    && sheet.Combos.Exists(c => c != null && c.ComboId == entry.ComboId);
                if (!inContract)
                    _issues.Add((MessageType.Warning,
                        $"HealBaseTable: '{entry.ComboId}' no está en el contrato de esta clase — la entrada no tiene efecto."));

                if (entry.HealBase <= 0)
                    _issues.Add((MessageType.Warning,
                        $"HealBaseTable: '{entry.ComboId}' con HealBase={entry.HealBase} — " +
                        "entrada redundante, sin entrada ya es 0."));
            }
        }

        void DrawIssues()
        {
            if (!_validated || _selected == null) return;

            Header("Validation");
            foreach (var (severity, message) in _issues)
                EditorGUILayout.HelpBox(message, severity);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Ping asset", GUILayout.Width(80f)))
                EditorGUIUtility.PingObject(_selected);
            if (GUILayout.Button("Re-validate", GUILayout.Width(80f)))
                RunValidation();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(8);
        }

        // ── Left: hero class list ───────────────────────────────

        void DrawLeft()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftWidth));

            EditorGUILayout.LabelField("Hero Classes", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            if (_heroes.Count == 0)
            {
                EditorGUILayout.HelpBox("No ClassHeroSO assets found in project.", MessageType.Info);
            }
            else
            {
                foreach (var h in _heroes)
                {
                    if (!MatchesSearch(h)) continue;
                    bool isSel = h == _selected;
                    var prev = GUI.backgroundColor;
                    if (isSel) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);

                    string label = string.IsNullOrEmpty(h.DisplayName) ? h.name : h.DisplayName;
                    if (GUILayout.Button(label, GUILayout.Height(28f)))
                        SelectHero(h);

                    GUI.backgroundColor = prev;
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ── Middle: identity + behaviors ─────────────────────────

        void DrawMiddle()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(MiddleWidth));
            _midScroll = EditorGUILayout.BeginScrollView(_midScroll);

            if (_selected == null || _ctx.Tree == null)
            {
                EditorGUILayout.HelpBox("Select a hero class from the left panel.", MessageType.Info);
            }
            else
            {
                DrawIssues();
                DrawIdentity();
                EditorGUILayout.Space(12);
                DrawBehaviorList();

                if (ValidIdx())
                {
                    EditorGUILayout.Space(12);
                    DrawBehaviorConfig();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawIdentity()
        {
            Header("Identity");
            Prop("EntityId");
            Prop("DisplayName");
            Prop("Description");
            Prop("Portrait");

            EditorGUILayout.Space(8);
            Header("Base Stats");
            Prop("BaseMaxHp");
            Prop("BaseSpeed");
            // dmg_base_PJ (Spec Daño v2) — piso garantizado del turno, nunca 0.
            Prop("BaseAttack");

            EditorGUILayout.Space(8);
            Header("Contract (§5.3)");
            Prop("Sheet");

            EditorGUILayout.Space(8);
            Header("Extras");
            Prop("DiceBagPool");
            Prop("Passive");
        }

        void DrawBehaviorList()
        {
            Header("Behaviors");

            var behaviors = _selected.PhaseBehaviors;
            if (behaviors == null || behaviors.Count == 0)
            {
                EditorGUILayout.HelpBox("No behaviors defined.", MessageType.Info);
                return;
            }

            for (int i = 0; i < behaviors.Count; i++)
            {
                var b = behaviors[i];
                if (b == null) continue;

                string label = b.ActionName ?? "(unnamed)";
                if (b.IsBaseBehavior)
                    label += $"  [{b.Slot}]";

                bool isSel = i == _behaviorIdx;
                var prev = GUI.backgroundColor;
                if (isSel) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);

                if (GUILayout.Button(label, GUILayout.Height(24f)))
                    _behaviorIdx = isSel ? -1 : i;

                GUI.backgroundColor = prev;
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ Add Behavior", GUILayout.Height(22f)))
            {
                Undo.RecordObject(_selected, "Add HeroActionBehavior");
                _selected.PhaseBehaviors.Add(new HeroActionBehavior());
                EditorUtility.SetDirty(_selected);
                RebuildTree();
            }
        }

        void DrawBehaviorConfig()
        {
            string bp = BehaviorPath();

            Header("Action Config");
            Prop($"{bp}.ActionName");
            Prop($"{bp}.IsBaseBehavior");
            Prop($"{bp}.Slot");
            Prop($"{bp}.BlockOnRepeat");

            EditorGUILayout.Space(8);
            Header("Dice");
            Prop($"{bp}.NeedsDiceRoll");
            Prop($"{bp}.AllowsReroll");

            EditorGUILayout.Space(8);
            Header("Show Conditions");
            Prop($"{bp}.ShowConditions");

            EditorGUILayout.Space(12);
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("Remove Behavior", GUILayout.Height(22f)))
            {
                Undo.RecordObject(_selected, "Remove HeroActionBehavior");
                _selected.PhaseBehaviors.RemoveAt(_behaviorIdx);
                EditorUtility.SetDirty(_selected);
                _behaviorIdx = -1;
                RebuildTree();
            }
            GUI.backgroundColor = oldColor;
        }

        // ── Right: effect pipeline ──────────────────────────────

        void DrawRight()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (!ValidIdx())
            {
                EditorGUILayout.HelpBox(
                    "Select a behavior from the middle panel to view its effect pipeline.",
                    MessageType.Info);
            }
            else
            {
                var b = _selected.PhaseBehaviors[_behaviorIdx];

                Header($"Effect Pipeline — {b.ActionName}");
                EditorGUILayout.Space(4);

                if (b.Effects == null) b.Effects = new List<Rollgeon.Effects.EffectData>();
                if (b.Effects.Count == 0)
                    EditorGUILayout.HelpBox("No effect groups defined.", MessageType.Info);

                // Was: Prop("...Effects.$i"), which handed the whole EffectData to Odin's stock
                // drawer — and Odin hides the picker for BasePreCondition, so preconditions could
                // never be authored here. The shared drawer supplies the pickers Odin drops, and
                // recurses into EffChain phases.
                PolymorphicBlockDrawer.DrawEffectDataList(
                    _ctx, b.Effects, $"{BehaviorPath()}.Effects",
                    PolymorphicBlockDrawer.Options.Default);

                EditorGUILayout.Space(4);
                PolymorphicBlockDrawer.DrawAddButton(
                    _ctx, "Effect Group", typeof(Rollgeon.Effects.EffectData), b.Effects);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ── Helpers ─────────────────────────────────────────────

        void Prop(string path) => _ctx.Draw(path);

        string BehaviorPath() => $"PhaseBehaviors.${_behaviorIdx}";

        bool ValidIdx()
        {
            if (_selected == null || _behaviorIdx < 0) return false;
            if (_selected.PhaseBehaviors == null || _behaviorIdx >= _selected.PhaseBehaviors.Count)
            {
                _behaviorIdx = -1;
                return false;
            }
            return true;
        }

        void SelectHero(ClassHeroSO hero)
        {
            if (_selected == hero) return;
            _selected = hero;
            _behaviorIdx = -1;
            _validated = false;
            _issues.Clear();
            RebuildTree();
        }

        bool MatchesSearch(ClassHeroSO hero)
        {
            if (string.IsNullOrEmpty(_search)) return true;
            var needle = _search.Trim();
            if (needle.Length == 0) return true;
            return Contains(hero.name, needle)
                || Contains(hero.DisplayName, needle)
                || Contains(hero.EntityId, needle);
        }

        static bool Contains(string haystack, string needle)
            => !string.IsNullOrEmpty(haystack)
               && haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;

        void RefreshList()
        {
            _heroes.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:ClassHeroSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var hero = AssetDatabase.LoadAssetAtPath<ClassHeroSO>(path);
                if (hero != null)
                    _heroes.Add(hero);
            }

            if (_selected != null && !_heroes.Contains(_selected))
            {
                _selected = null;
                DisposeTree();
                _behaviorIdx = -1;
            }
        }

        void RebuildTree() => _ctx.Bind(_selected);

        void DisposeTree() => _ctx.Dispose();

        static void Header(string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        static void Sep()
        {
            var r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(r, new Color(0.12f, 0.12f, 0.12f));
        }
    }
}
