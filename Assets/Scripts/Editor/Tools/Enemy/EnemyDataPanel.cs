using System;
using System.Collections.Generic;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Entities.Bosses;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>
    /// IMGUI panel that draws an <see cref="EnemyDataSO"/> with grouped sections.
    /// Mirrors HeroClassEditorWindow's PropertyTree-by-path approach so we don't pollute
    /// the SO with editor-only attributes. Los diagnósticos (problemas + resumen del árbol)
    /// los calcula la ventana y los empuja con <see cref="SetDiagnostics"/>: el panel nunca
    /// valida dentro de <see cref="Draw"/>.
    /// </summary>
    public sealed class EnemyDataPanel
    {
        EnemyDataSO _so;
        PropertyTree _tree;
        int _behaviorIdx = -1;
        int _tierIdx = -1;
        Vector2 _scroll;

        List<EnemyIssue> _issues = new List<EnemyIssue>();
        EnemyTreeSummary _summary;
        string _builderMenu;

        // Foldout state
        bool _issuesOpen = true;
        bool _sheetOpen = true;
        bool _summaryOpen = true;
        bool _visualOpen = true;
        bool _statsOpen = true;
        bool _tiersOpen = true;
        bool _weaknessOpen = true;
        bool _traitsOpen = true;
        bool _rewardsOpen = false;
        bool _bossOpen = true;

        /// <summary>Se dispara tras cualquier edición que llegó al asset (Odin o manual).</summary>
        public event Action Changed;

        public void Bind(EnemyDataSO so)
        {
            if (_so == so) return;
            _so = so;
            _behaviorIdx = -1;
            _tierIdx = -1;
            _builderMenu = so != null && BossBuilderRegistry.TryGetBuilder(so, out var menu) ? menu : null;
            RebuildTree();
        }

        public void SetDiagnostics(List<EnemyIssue> issues, EnemyTreeSummary summary)
        {
            _issues = issues ?? new List<EnemyIssue>();
            _summary = summary;
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
                EditorGUILayout.HelpBox("Elegí un enemigo en la lista de la izquierda.", MessageType.Info);
                return;
            }

            _tree.UpdateTree();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_builderMenu != null)
            {
                EditorGUILayout.HelpBox("⚙ " + BossBuilderRegistry.BannerText(_builderMenu), MessageType.Warning);
                EditorGUILayout.Space(4);
            }

            DrawIssues();
            EditorGUILayout.Space(8);
            DrawIdentity();
            EditorGUILayout.Space(8);
            DrawDesignSheet();
            EditorGUILayout.Space(8);
            DrawTreeSummary();
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

            if (_tree.ApplyChanges()) RaiseChanged();
        }

        // ---- diagnostics ---------------------------------------------------

        void DrawIssues()
        {
            int errors = EnemyDataValidator.Count(_issues, EnemyIssueSeverity.Error);
            int warnings = EnemyDataValidator.Count(_issues, EnemyIssueSeverity.Warning);
            int infos = EnemyDataValidator.Count(_issues, EnemyIssueSeverity.Info);
            string title = _issues.Count == 0
                ? "Problemas — ninguno"
                : $"Problemas — {errors} errores · {warnings} avisos · {infos} info";

            _issuesOpen = EditorGUILayout.Foldout(_issuesOpen, title, toggleOnLabelClick: true);
            if (!_issuesOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                if (_issues.Count == 0)
                {
                    EditorGUILayout.HelpBox("La ficha está completa y el árbol no tiene avisos.", MessageType.Info);
                    return;
                }
                DrawIssueGroup(EnemyIssueSeverity.Error);
                DrawIssueGroup(EnemyIssueSeverity.Warning);
                DrawIssueGroup(EnemyIssueSeverity.Info);
            }
        }

        void DrawIssueGroup(EnemyIssueSeverity severity)
        {
            var lines = new List<string>();
            foreach (var i in _issues)
                if (i.Severity == severity) lines.Add($"• [{i.Section}] {i.Message}");
            if (lines.Count == 0) return;
            EditorGUILayout.HelpBox(string.Join("\n", lines), EnemyIssue.ToMessageType(severity));
        }

        void DrawTreeSummary()
        {
            _summaryOpen = EditorGUILayout.Foldout(_summaryOpen, "Resumen del árbol (derivado)", toggleOnLabelClick: true);
            if (!_summaryOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                var s = _summary;
                if (s == null || !s.HasTree)
                {
                    EditorGUILayout.HelpBox("Sin árbol de IA: en combate usa BasicEnemyAI (siempre ataca). Armalo en la pestaña Árbol de IA.", MessageType.None);
                    return;
                }

                Row("Nodos", s.NodeCount + (s.DetachedCount > 0 ? $" (+{s.DetachedCount} sueltos)" : ""));
                Row("Movimiento", EnemyTreeSummary.Names(s.MovementNodes));
                Row("Telegraph", s.ShapesText());
                Row("Disparo a distancia", s.HasRangedShot ? "sí" : "no");
                Row("Condiciones (PC)", EnemyTreeSummary.Names(s.PreConditionTypes));
                Row("Efectos (Eff)", EnemyTreeSummary.Names(s.EffectTypes));
                Row("Cura / buff", !s.HasHeal && !s.HasBuff ? "no" : (s.HasHeal ? "cura" : "") + (s.HasHeal && s.HasBuff ? " · " : "") + (s.HasBuff ? "buff" : ""));
                if (s.SpawnsReinforcements) Row("Refuerzos", "sí");
                if (s.UsesBehaviorsList) Row("Behaviors de ficha", "sí (lista de abajo)");
            }
        }

        static void Row(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel, GUILayout.Width(150f));
            EditorGUILayout.LabelField(value, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndHorizontal();
        }

        // ---- sections -----------------------------------------------------

        void DrawIdentity()
        {
            Header("Identidad");
            Prop("EntityId", "Id de entidad");
            Prop("DisplayName", "Nombre visible");
            Prop("Description", "Descripción");

            // ObjectField a mano por la misma razón que Visual Prefab (ver DrawVisual): Odin
            // pierde el drag-drop de UnityEngine.Object dentro de un IMGUIContainer.
            EditorGUI.BeginChangeCheck();
            var newPortrait = (Sprite)EditorGUILayout.ObjectField(
                new GUIContent("Retrato",
                    "Sprite identificatorio para UI (cola de turnos, barra de jefe, bestiario)."),
                _so.Portrait,
                typeof(Sprite),
                allowSceneObjects: false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_so, "Cambiar retrato");
                _so.Portrait = newPortrait;
                EditorUtility.SetDirty(_so);
                RaiseChanged();
            }
        }

        void DrawDesignSheet()
        {
            _sheetOpen = EditorGUILayout.Foldout(_sheetOpen, "Ficha de diseño (GDD)", toggleOnLabelClick: true);
            if (!_sheetOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                if (_so.Design == null)
                {
                    EditorGUILayout.HelpBox("La ficha está vacía (asset viejo). Guardá el asset para inicializarla.", MessageType.Warning);
                    return;
                }
                EditorGUILayout.HelpBox(
                    "Metadata del GDD 'Patrones de Ataque': no cambia el juego, sirve para filtrar y para " +
                    "que el panel de problemas avise si el árbol no hace lo que la ficha declara.",
                    MessageType.None);
                Prop("Design.Archetype", "Arquetipo");
                Prop("Design.Pattern", "Patrón geométrico");
                Prop("Design.Timing", "Timing");
                Prop("Design.Notes", "Notas (movimiento, selección, payload…)");
            }
        }

        void DrawTraits()
        {
            _traitsOpen = EditorGUILayout.Foldout(_traitsOpen, "Rasgos de unidad", toggleOnLabelClick: true);
            if (!_traitsOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                Prop("IsFlying", "Voladora");
                Prop("IsBoss", "Jefe");
                Prop("Personality", "Personalidad");
                Prop("KamikazeIgnoresSurvival", "Kamikaze ignora supervivencia");
                Prop("Footprint", "Tamaño en grilla (ancho × alto)");
            }
        }

        void DrawBoss(BossFloorManagerSO boss)
        {
            _bossOpen = EditorGUILayout.Foldout(_bossOpen, "Jefe de piso", toggleOnLabelClick: true);
            if (!_bossOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                Prop("ComboBlockIntervalTurns", "Bloqueo de combo — cada N turnos");
                Prop("ComboBlockDurationTurns", "Bloqueo de combo — duración");
                Prop("BossEnergyMax", "Energía máxima");
                Prop("BossEnergyGainPerTurn", "Energía por turno");
                Prop("DoubleDamageChanceDefault", "Chance de daño doble");
                Prop("DoubleDamageChanceWhenEnergyFull", "Chance de daño doble (energía llena)");
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
                    new GUIContent("Prefab visual",
                        "Prefab que se instancia como pawn visual del enemigo."),
                    _so.VisualPrefab,
                    typeof(GameObject),
                    allowSceneObjects: false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_so, "Cambiar prefab visual");
                    _so.VisualPrefab = newPrefab;
                    EditorUtility.SetDirty(_so);
                    RaiseChanged();
                }
            }
        }

        void DrawStats()
        {
            _statsOpen = EditorGUILayout.Foldout(_statsOpen, "Stats base (Tier 1)", toggleOnLabelClick: true);
            if (!_statsOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                Prop("BaseHP", "Vida");
                Prop("BaseAttack", "Ataque");
                Prop("BaseAttackRange", "Rango de ataque");
                Prop("BaseHealStrength", "Potencia de curación");
                Prop("BaseSpeed", "Velocidad (iniciativa)");
                Prop("MaxEnergy", "Energía máxima");
            }
        }

        void DrawTiers()
        {
            _tiersOpen = EditorGUILayout.Foldout(_tiersOpen, "Tiers por piso", toggleOnLabelClick: true);
            if (!_tiersOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.HelpBox(
                    "En spawn se usa el tier más alto cuyo 'desde piso' <= piso actual. " +
                    "Los tiers cambian solo stats — nunca behaviors ni árbol de IA.",
                    MessageType.None);

                EditorGUILayout.LabelField("Tier 1 — Stats base (desde piso 1)", EditorStyles.miniBoldLabel);

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
                            : $"desde piso {effective} — legacy, 'desde piso' sin autorar";
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
                            Undo.RecordObject(_so, "Quitar tier");
                            tiers.RemoveAt(i);
                            EditorUtility.SetDirty(_so);
                            _tierIdx = -1;
                            RebuildTree();
                            RaiseChanged();
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
                }

                EditorGUILayout.Space(6);
                DrawResolvedTierTable();

                EditorGUILayout.Space(6);
                if (GUILayout.Button("+ Agregar tier", GUILayout.Height(24f)))
                {
                    Undo.RecordObject(_so, "Agregar tier");
                    if (_so.ExtraTiers == null) _so.ExtraTiers = new List<EnemyTier>();
                    _so.ExtraTiers.Add(_so.CreateNextTierTemplate());
                    EditorUtility.SetDirty(_so);
                    _tierIdx = _so.ExtraTiers.Count - 1;
                    RebuildTree();
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Valores finales por tier con la misma resolución que <c>CreateRuntimeStats(tier)</c>
        /// (<see cref="TierStat.Resolve"/> sobre el base), sin instanciar stats.
        /// </summary>
        void DrawResolvedTierTable()
        {
            EditorGUILayout.LabelField("Valores resueltos", EditorStyles.miniBoldLabel);
            var w = GUILayout.Width(58f);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Tier", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
            EditorGUILayout.LabelField("Desde piso", EditorStyles.miniBoldLabel, GUILayout.Width(70f));
            foreach (var h in new[] { "Vida", "Ataque", "Veloc.", "Energía", "Cura", "Rango" })
                EditorGUILayout.LabelField(h, EditorStyles.miniBoldLabel, w);
            EditorGUILayout.EndHorizontal();

            int count = _so.TierCount;
            for (int tier = 1; tier <= count; tier++)
            {
                var t = _so.GetTier(tier);
                int hp = t == null ? _so.BaseHP : t.HP.Resolve(_so.BaseHP);
                int atk = t == null ? _so.BaseAttack : t.Attack.Resolve(_so.BaseAttack);
                int spd = t == null ? _so.BaseSpeed : t.Speed.Resolve(_so.BaseSpeed);
                int en = t == null ? _so.MaxEnergy : t.Energy.Resolve(_so.MaxEnergy);
                int heal = t == null ? _so.BaseHealStrength : t.HealStrength.Resolve(_so.BaseHealStrength);
                int range = t == null ? _so.BaseAttackRange : t.AttackRange.Resolve(_so.BaseAttackRange);
                string name = t != null && !string.IsNullOrEmpty(t.Label) ? $"{tier} · {t.Label}" : tier.ToString();
                string floor = tier == 1 ? "1" : _so.EffectiveMinFloor(tier).ToString() + (t != null && t.MinFloor == 0 ? " (legacy)" : "");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(name, EditorStyles.miniLabel, GUILayout.Width(70f));
                EditorGUILayout.LabelField(floor, EditorStyles.miniLabel, GUILayout.Width(70f));
                foreach (var v in new[] { hp, atk, spd, en, heal, range })
                    EditorGUILayout.LabelField(v.ToString(), EditorStyles.miniLabel, w);
                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawWeakness()
        {
            _weaknessOpen = EditorGUILayout.Foldout(_weaknessOpen, "Debilidad", toggleOnLabelClick: true);
            if (!_weaknessOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                Prop("WeaknessComboId", "Combo al que es débil");
                Prop("WeaknessMultiplierOverride", "Multiplicador (0 = global)");
            }
        }

        void DrawRewards()
        {
            _rewardsOpen = EditorGUILayout.Foldout(_rewardsOpen, "Recompensas", toggleOnLabelClick: true);
            if (!_rewardsOpen) return;
            using (new EditorGUI.IndentLevelScope())
            {
                Prop("MinGoldDrop", "Oro mínimo");
                Prop("MaxGoldDrop", "Oro máximo");
            }
        }

        void DrawBehaviors()
        {
            Header("Behaviors de ficha");
            EditorGUILayout.HelpBox(
                "En combate solo se leen desde acá las inmunidades de jefe (BossComboImmunity). " +
                "Las acciones viven en el árbol de IA (nodo Behavior).",
                MessageType.None);

            var list = _so.Behaviors;
            if (list == null || list.Count == 0)
            {
                EditorGUILayout.HelpBox("Sin behaviors. Usá la paleta de abajo para agregar uno.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var b = list[i];
                    string label = b != null ? b.BehaviorName : "(vacío)";
                    string typeChip = b != null ? "  [" + b.GetType().Name + "]" : "";
                    bool isSel = i == _behaviorIdx;

                    var prev = GUI.backgroundColor;
                    if (isSel) GUI.backgroundColor = new Color(0.45f, 0.75f, 1f);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(label + typeChip, GUILayout.Height(22f)))
                        _behaviorIdx = isSel ? -1 : i;
                    if (GUILayout.Button("✕", GUILayout.Width(24f), GUILayout.Height(22f)))
                    {
                        Undo.RecordObject(_so, "Quitar behavior");
                        list.RemoveAt(i);
                        EditorUtility.SetDirty(_so);
                        _behaviorIdx = -1;
                        RebuildTree();
                        RaiseChanged();
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
            if (GUILayout.Button("+ Agregar behavior", GUILayout.Height(24f)))
            {
                EnemyBehaviorPalette.Show(template =>
                {
                    Undo.RecordObject(_so, "Agregar behavior");
                    _so.Behaviors.Add(template);
                    EditorUtility.SetDirty(_so);
                    _behaviorIdx = _so.Behaviors.Count - 1;
                    RebuildTree();
                    RaiseChanged();
                });
            }
        }

        // ---- helpers ------------------------------------------------------

        void Prop(string path, string label = null)
        {
            var p = _tree.GetPropertyAtPath(path);
            if (p == null) return;
            if (label == null) p.Draw();
            else p.Draw(new GUIContent(label));
        }

        static void Header(string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        void RaiseChanged() => Changed?.Invoke();
    }
}
