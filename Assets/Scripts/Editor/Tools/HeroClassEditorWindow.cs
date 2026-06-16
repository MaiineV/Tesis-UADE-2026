using System.Collections.Generic;
using Rollgeon.Heroes;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools
{
    public sealed class HeroClassEditorWindow : EditorWindow
    {
        const float LeftWidth = 200f;
        const float MiddleWidth = 340f;

        readonly List<ClassHeroSO> _heroes = new List<ClassHeroSO>();
        ClassHeroSO _selected;
        PropertyTree _tree;
        int _behaviorIdx = -1;

        Vector2 _leftScroll, _midScroll, _rightScroll;

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
            _tree?.UpdateTree();

            EditorGUILayout.BeginHorizontal();
            DrawLeft();
            Sep();
            DrawMiddle();
            Sep();
            DrawRight();
            EditorGUILayout.EndHorizontal();

            _tree?.ApplyChanges();
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

            if (_selected == null || _tree == null)
            {
                EditorGUILayout.HelpBox("Select a hero class from the left panel.", MessageType.Info);
            }
            else
            {
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
            Header("Base Stats (Stub)");
            Prop("BaseMaxHp");
            Prop("BaseSpeed");

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
            Prop($"{bp}.EnergyCost");
            Prop($"{bp}.BlockOnRepeat");

            EditorGUILayout.Space(8);
            Header("Dice");
            Prop($"{bp}.NeedsDiceRoll");
            Prop($"{bp}.FreeRollCount");
            Prop($"{bp}.AllowsReroll");
            Prop($"{bp}.AllowsEnergyReroll");

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

                if (b.Effects == null || b.Effects.Count == 0)
                {
                    EditorGUILayout.HelpBox("No effect groups defined.", MessageType.Info);
                }
                else
                {
                    for (int i = 0; i < b.Effects.Count; i++)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        Prop($"{BehaviorPath()}.Effects.${i}");
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space(8);
                    }
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("+ Add Effect Group", GUILayout.Height(22f)))
                {
                    Undo.RecordObject(_selected, "Add EffectData");
                    b.Effects.Add(new Rollgeon.Effects.EffectData());
                    EditorUtility.SetDirty(_selected);
                    RebuildTree();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ── Helpers ─────────────────────────────────────────────

        void Prop(string path)
        {
            if (_tree == null) return;
            var p = _tree.GetPropertyAtPath(path);
            if (p != null) p.Draw();
        }

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
            RebuildTree();
        }

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
