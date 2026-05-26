using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>
    /// IMGUI panel that draws an <see cref="EnemyDataSO"/> with grouped sections.
    /// Mirrors HeroClassEditorWindow's PropertyTree-by-path approach so we don't pollute
    /// the SO with editor-only attributes.
    /// </summary>
    public sealed class EnemyDataPanel
    {
        EnemyDataSO _so;
        PropertyTree _tree;
        int _behaviorIdx = -1;
        Vector2 _scroll;

        // Foldout state
        bool _visualOpen = true;
        bool _statsOpen = true;
        bool _weaknessOpen = true;
        bool _rewardsOpen = false;

        public void Bind(EnemyDataSO so)
        {
            if (_so == so) return;
            _so = so;
            _behaviorIdx = -1;
            RebuildTree();
        }

        public void RebuildTree()
        {
            _tree?.Dispose();
            _tree = _so != null ? PropertyTree.Create(_so) : null;
        }

        public void Dispose()
        {
            _tree?.Dispose();
            _tree = null;
        }

        // ---- IMGUI draw ---------------------------------------------------

        public void Draw()
        {
            if (_so == null || _tree == null)
            {
                EditorGUILayout.HelpBox("Select an enemy from the left panel.", MessageType.Info);
                return;
            }

            _tree.UpdateTree();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawIdentity();
            EditorGUILayout.Space(8);
            DrawVisual();
            EditorGUILayout.Space(8);
            DrawStats();
            EditorGUILayout.Space(8);
            DrawWeakness();
            EditorGUILayout.Space(8);
            DrawRewards();
            EditorGUILayout.Space(12);
            DrawBehaviors();

            EditorGUILayout.EndScrollView();

            _tree.ApplyChanges();
        }

        // ---- sections -----------------------------------------------------

        void DrawIdentity()
        {
            Header("Identity");
            Prop("EntityId");
            Prop("DisplayName");
            Prop("Description");
        }

        void DrawVisual()
        {
            _visualOpen = EditorGUILayout.Foldout(_visualOpen, "Visual", toggleOnLabelClick: true);
            if (!_visualOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                // ObjectField directo (no via PropertyTree) — Odin pierde el
                // drag-drop de UnityEngine.Object cuando vive dentro de un
                // IMGUIContainer (UIToolkit), así que escribimos al SO a mano.
                EditorGUI.BeginChangeCheck();
                var newPrefab = (GameObject)EditorGUILayout.ObjectField(
                    new GUIContent("Visual Prefab",
                        "Prefab que se instancia como pawn visual del enemigo."),
                    _so.VisualPrefab,
                    typeof(GameObject),
                    allowSceneObjects: false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_so, "Set Visual Prefab");
                    _so.VisualPrefab = newPrefab;
                    EditorUtility.SetDirty(_so);
                }
            }
        }

        void DrawStats()
        {
            _statsOpen = EditorGUILayout.Foldout(_statsOpen, "Base Stats", toggleOnLabelClick: true);
            if (!_statsOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                Prop("BaseHP");
                Prop("BaseAttack");
                Prop("BaseHealStrength");
                Prop("BaseSpeed");
                Prop("MaxEnergy");
            }
        }

        void DrawWeakness()
        {
            _weaknessOpen = EditorGUILayout.Foldout(_weaknessOpen, "Weakness (§5)", toggleOnLabelClick: true);
            if (!_weaknessOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                Prop("WeaknessComboId");
                Prop("WeaknessMultiplierOverride");
            }
        }

        void DrawRewards()
        {
            _rewardsOpen = EditorGUILayout.Foldout(_rewardsOpen, "Rewards", toggleOnLabelClick: true);
            if (!_rewardsOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                Prop("MinGoldDrop");
                Prop("MaxGoldDrop");
            }
        }

        void DrawBehaviors()
        {
            Header("Behaviors");

            var list = _so.Behaviors;
            if (list == null || list.Count == 0)
            {
                EditorGUILayout.HelpBox("No behaviors. Use the palette below to add one.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var b = list[i];
                    string label = b != null ? b.BehaviorName : "(null)";
                    string typeChip = b != null ? "  [" + b.GetType().Name + "]" : "";
                    bool isSel = i == _behaviorIdx;

                    var prev = GUI.backgroundColor;
                    if (isSel) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(label + typeChip, GUILayout.Height(22f)))
                        _behaviorIdx = isSel ? -1 : i;
                    if (GUILayout.Button("✕", GUILayout.Width(24f), GUILayout.Height(22f)))
                    {
                        Undo.RecordObject(_so, "Remove Behavior");
                        list.RemoveAt(i);
                        EditorUtility.SetDirty(_so);
                        _behaviorIdx = -1;
                        RebuildTree();
                        EditorGUILayout.EndHorizontal();
                        GUI.backgroundColor = prev;
                        return;
                    }
                    EditorGUILayout.EndHorizontal();
                    GUI.backgroundColor = prev;
                }

                if (_behaviorIdx >= 0 && _behaviorIdx < list.Count)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    Prop($"Behaviors.${_behaviorIdx}");
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("+ Add Behavior", GUILayout.Height(24f)))
            {
                EnemyBehaviorPalette.Show(template =>
                {
                    Undo.RecordObject(_so, "Add Behavior");
                    _so.Behaviors.Add(template);
                    EditorUtility.SetDirty(_so);
                    _behaviorIdx = _so.Behaviors.Count - 1;
                    RebuildTree();
                });
            }
        }

        // ---- helpers ------------------------------------------------------

        void Prop(string path)
        {
            var p = _tree.GetPropertyAtPath(path);
            if (p != null) p.Draw();
        }

        static void Header(string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
