using System.Collections.Generic;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Entities.Bosses;
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
        int _tierIdx = -1;
        Vector2 _scroll;

        // Foldout state
        bool _visualOpen = true;
        bool _statsOpen = true;
        bool _tiersOpen = true;
        bool _weaknessOpen = true;
        bool _traitsOpen = true;
        bool _rewardsOpen = false;
        bool _bossOpen = true;

        public void Bind(EnemyDataSO so)
        {
            if (_so == so) return;
            _so = so;
            _behaviorIdx = -1;
            _tierIdx = -1;
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
            DrawTiers();
            EditorGUILayout.Space(8);
            DrawWeakness();
            EditorGUILayout.Space(8);
            DrawTraits();
            EditorGUILayout.Space(8);
            DrawRewards();
            if (_so is BossFloorManagerSO boss)
            {
                EditorGUILayout.Space(8);
                DrawBoss(boss);
            }
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

            // ObjectField a mano por la misma razón que Visual Prefab (ver DrawVisual): Odin
            // pierde el drag-drop de UnityEngine.Object dentro de un IMGUIContainer.
            EditorGUI.BeginChangeCheck();
            var newPortrait = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Portrait",
                    "Sprite identificatorio para UI (cola de turnos, barra de jefe, bestiario)."),
                _so.Portrait,
                typeof(Sprite),
                allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_so, "Set Portrait");
                _so.Portrait = newPortrait;
                EditorUtility.SetDirty(_so);
            }
        }

        void DrawTraits()
        {
            _traitsOpen = EditorGUILayout.Foldout(_traitsOpen, "Rasgos de unidad", toggleOnLabelClick: true);
            if (!_traitsOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                Prop("IsFlying");
                Prop("IsBoss");
                Prop("Personality");
                Prop("KamikazeIgnoresSurvival");
            }
        }

        void DrawBoss(BossFloorManagerSO boss)
        {
            _bossOpen = EditorGUILayout.Foldout(_bossOpen, "Jefe", toggleOnLabelClick: true);
            if (!_bossOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                if (!boss.IsBoss)
                {
                    EditorGUILayout.HelpBox(
                        "Esta ficha es un BossFloorManagerSO pero no tiene 'IsBoss' marcado: las " +
                        "inmunidades y el kill credit de jefe no aplican.",
                        MessageType.Warning);
                }
                Prop("ComboBlockIntervalTurns");
                Prop("ComboBlockDurationTurns");
                Prop("BossEnergyMax");
                Prop("BossEnergyGainPerTurn");
                Prop("DoubleDamageChanceDefault");
                Prop("DoubleDamageChanceWhenEnergyFull");
            }
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

        void DrawTiers()
        {
            _tiersOpen = EditorGUILayout.Foldout(_tiersOpen, "Tiers", toggleOnLabelClick: true);
            if (!_tiersOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.HelpBox(
                    "En spawn se usa el tier más alto cuyo 'desde piso' <= piso actual. " +
                    "Los tiers cambian solo stats — nunca behaviors ni AI tree.",
                    MessageType.None);

                EditorGUILayout.LabelField("Tier 1 — Base Stats (desde piso 1)", EditorStyles.miniBoldLabel);

                var tiers = _so.ExtraTiers;
                if (tiers != null)
                {
                    for (int i = 0; i < tiers.Count; i++)
                    {
                        int tierNumber = i + 2;
                        var t = tiers[i];
                        int effective = _so.EffectiveMinFloor(tierNumber);
                        string floorText = t != null && t.MinFloor > 0
                            ? $"desde piso {effective}"
                            : $"desde piso {effective} — legacy, MinFloor sin autorar";
                        string name = t != null && !string.IsNullOrEmpty(t.Label)
                            ? $"Tier {tierNumber} — {t.Label}"
                            : $"Tier {tierNumber}";
                        bool isSel = i == _tierIdx;

                        var prev = GUI.backgroundColor;
                        if (isSel) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button($"{name}  ({floorText})", GUILayout.Height(22f)))
                            _tierIdx = isSel ? -1 : i;
                        if (GUILayout.Button("✕", GUILayout.Width(24f), GUILayout.Height(22f)))
                        {
                            Undo.RecordObject(_so, "Remove Tier");
                            tiers.RemoveAt(i);
                            EditorUtility.SetDirty(_so);
                            _tierIdx = -1;
                            RebuildTree();
                            EditorGUILayout.EndHorizontal();
                            GUI.backgroundColor = prev;
                            return;
                        }
                        EditorGUILayout.EndHorizontal();
                        GUI.backgroundColor = prev;
                    }

                    if (_tierIdx >= 0 && _tierIdx < tiers.Count)
                    {
                        EditorGUILayout.Space(6);
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        Prop($"ExtraTiers.${_tierIdx}");
                        EditorGUILayout.EndVertical();
                    }

                    // Un tier con 'desde piso' efectivo <= al del anterior nunca deja
                    // usarse al anterior (siempre gana el más alto elegible).
                    for (int tierNumber = 2; tierNumber <= tiers.Count + 1; tierNumber++)
                    {
                        if (_so.EffectiveMinFloor(tierNumber) <= _so.EffectiveMinFloor(tierNumber - 1))
                        {
                            EditorGUILayout.HelpBox(
                                "Los 'desde piso' efectivos no son estrictamente crecientes — " +
                                "algún tier queda inalcanzable.",
                                MessageType.Warning);
                            break;
                        }
                    }
                }

                EditorGUILayout.Space(6);
                if (GUILayout.Button("+ Add Tier", GUILayout.Height(24f)))
                {
                    Undo.RecordObject(_so, "Add Tier");
                    if (_so.ExtraTiers == null) _so.ExtraTiers = new List<EnemyTier>();
                    _so.ExtraTiers.Add(_so.CreateNextTierTemplate());
                    EditorUtility.SetDirty(_so);
                    _tierIdx = _so.ExtraTiers.Count - 1;
                    RebuildTree();
                }
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
