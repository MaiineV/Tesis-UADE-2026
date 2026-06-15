using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rollgeon.Meta;
using Rollgeon.Meta.Conditions;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    /// <summary>
    /// <b>Unlock Condition Tool</b> (#164). Permite definir el elemento a
    /// desbloquear y su categoría, armar la condición con bloques condicionales
    /// (combinables con AND/OR), configurar si aplica en run ganada/perdida/ambas
    /// y asignar el texto de pista visible al jugador.
    /// Mismo patrón IMGUI + Odin <see cref="PropertyTree"/> que
    /// <see cref="HeroClassEditorWindow"/>.
    /// </summary>
    public sealed class UnlockConditionToolWindow : EditorWindow
    {
        const float LeftWidth = 240f;
        const string DefaultAssetFolder = "Assets/Rollgeon/Meta/Unlocks";

        readonly List<UnlockDefinitionSO> _unlocks = new List<UnlockDefinitionSO>();
        UnlockDefinitionSO _selected;
        PropertyTree _tree;

        Vector2 _leftScroll, _rightScroll;

        [MenuItem("Tools/Unlock Condition Tool")]
        static void Open()
        {
            var w = GetWindow<UnlockConditionToolWindow>("Unlock Condition Tool");
            w.minSize = new Vector2(760f, 480f);
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
            _tree?.UpdateTree();

            EditorGUILayout.BeginHorizontal();
            DrawLeft();
            Sep();
            DrawRight();
            EditorGUILayout.EndHorizontal();

            _tree?.ApplyChanges();
        }

        // ── Left: unlock list ───────────────────────────────────

        void DrawLeft()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftWidth));

            EditorGUILayout.LabelField("Unlocks", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);

            if (_unlocks.Count == 0)
            {
                EditorGUILayout.HelpBox("No UnlockDefinitionSO assets found in project.", MessageType.Info);
            }
            else
            {
                foreach (var group in _unlocks.GroupBy(u => u.Category).OrderBy(g => g.Key))
                {
                    EditorGUILayout.LabelField(group.Key.ToString(), EditorStyles.miniBoldLabel);
                    foreach (var unlock in group)
                    {
                        bool isSel = unlock == _selected;
                        var prev = GUI.backgroundColor;
                        if (isSel) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);

                        string label = string.IsNullOrEmpty(unlock.DisplayName) ? unlock.name : unlock.DisplayName;
                        if (GUILayout.Button(label, GUILayout.Height(26f)))
                            Select(unlock);

                        GUI.backgroundColor = prev;
                    }
                    EditorGUILayout.Space(4);
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("+ New Unlock", GUILayout.Height(26f)))
                CreateNewUnlock();

            EditorGUILayout.EndVertical();
        }

        // ── Right: definition editor ────────────────────────────

        void DrawRight()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_selected == null || _tree == null)
            {
                EditorGUILayout.HelpBox("Select an unlock from the left panel, or create a new one.", MessageType.Info);
            }
            else
            {
                Header("Identity");
                Prop("UnlockId");
                Prop("DisplayName");

                EditorGUILayout.Space(8);
                Header("Target — qué se desbloquea");
                Prop("Category");
                Prop("TargetId");

                EditorGUILayout.Space(8);
                Header("Player-facing text");
                Prop("Description");
                Prop("HintText");

                EditorGUILayout.Space(8);
                Header("Condition");
                Prop("AppliesTo");
                EditorGUILayout.Space(4);
                DrawConditionControls();
                Prop("Condition");

                EditorGUILayout.Space(8);
                DrawValidation();

                EditorGUILayout.Space(12);
                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
                if (GUILayout.Button("Delete Unlock Asset", GUILayout.Height(22f)) &&
                    EditorUtility.DisplayDialog("Delete unlock",
                        $"¿Borrar el asset '{_selected.name}'?", "Borrar", "Cancelar"))
                {
                    var path = AssetDatabase.GetAssetPath(_selected);
                    _selected = null;
                    DisposeTree();
                    AssetDatabase.DeleteAsset(path);
                    RefreshList();
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = oldColor;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Botonera de bloques condicionales: setea la condición raíz, o la envuelve
        /// en un composite AND/OR para componer sin perder lo ya armado.
        /// </summary>
        void DrawConditionControls()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Set Root Block…", GUILayout.Height(20f)))
            {
                ShowConditionTypeMenu(condition =>
                {
                    Undo.RecordObject(_selected, "Set unlock condition");
                    _selected.Condition = condition;
                    EditorUtility.SetDirty(_selected);
                    RebuildTree();
                });
            }

            using (new EditorGUI.DisabledScope(_selected.Condition == null))
            {
                if (GUILayout.Button("Wrap in AND", GUILayout.Height(20f)))
                    WrapRoot(new AndCondition());
                if (GUILayout.Button("Wrap in OR", GUILayout.Height(20f)))
                    WrapRoot(new OrCondition());
            }

            EditorGUILayout.EndHorizontal();
        }

        void WrapRoot(IUnlockCondition composite)
        {
            Undo.RecordObject(_selected, "Wrap unlock condition");
            switch (composite)
            {
                case AndCondition and:
                    and.Children.Add(_selected.Condition);
                    break;
                case OrCondition or:
                    or.Children.Add(_selected.Condition);
                    break;
            }
            _selected.Condition = composite;
            EditorUtility.SetDirty(_selected);
            RebuildTree();
        }

        void ShowConditionTypeMenu(Action<IUnlockCondition> onPicked)
        {
            var menu = new GenericMenu();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IUnlockCondition>()
                         .Where(t => !t.IsAbstract && !t.IsInterface)
                         .OrderBy(t => t.Name))
            {
                var captured = type;
                menu.AddItem(new GUIContent(captured.Name), false, () =>
                {
                    onPicked((IUnlockCondition)Activator.CreateInstance(captured));
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }

        void DrawValidation()
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(_selected.UnlockId)) problems.Add("UnlockId vacío.");
            if (string.IsNullOrEmpty(_selected.TargetId)) problems.Add("TargetId vacío — no gatea ningún elemento.");
            if (_selected.Condition == null) problems.Add("Sin condición — el unlock nunca se cumple.");
            if (string.IsNullOrEmpty(_selected.HintText)) problems.Add("Sin pista — la pantalla de desbloqueos muestra el candado sin texto.");
            if (_unlocks.Any(u => u != _selected && u.UnlockId == _selected.UnlockId && !string.IsNullOrEmpty(u.UnlockId)))
                problems.Add($"UnlockId duplicado: '{_selected.UnlockId}'.");

            if (problems.Count == 0)
            {
                EditorGUILayout.HelpBox("Definición válida.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(string.Join("\n", problems), MessageType.Warning);
            }
        }

        // ── Helpers ─────────────────────────────────────────────

        void CreateNewUnlock()
        {
            Directory.CreateDirectory(DefaultAssetFolder);

            var asset = CreateInstance<UnlockDefinitionSO>();
            asset.UnlockId = "unlock.new";
            asset.AppliesTo = UnlockOutcomeFilter.Won;

            var path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultAssetFolder}/Unlock_New.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            RefreshList();
            Select(asset);
            EditorGUIUtility.PingObject(asset);
        }

        void Prop(string path)
        {
            if (_tree == null) return;
            var p = _tree.GetPropertyAtPath(path);
            if (p != null) p.Draw();
        }

        void Select(UnlockDefinitionSO unlock)
        {
            if (_selected == unlock) return;
            _selected = unlock;
            RebuildTree();
        }

        void RefreshList()
        {
            _unlocks.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:UnlockDefinitionSO"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var unlock = AssetDatabase.LoadAssetAtPath<UnlockDefinitionSO>(path);
                if (unlock != null)
                    _unlocks.Add(unlock);
            }

            if (_selected != null && !_unlocks.Contains(_selected))
            {
                _selected = null;
                DisposeTree();
            }
        }

        void RebuildTree()
        {
            DisposeTree();
            if (_selected != null)
                _tree = PropertyTree.Create(_selected);
        }

        void DisposeTree()
        {
            _tree?.Dispose();
            _tree = null;
        }

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
