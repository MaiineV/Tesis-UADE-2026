using System;
using System.Collections.Generic;
using Rollgeon.Combos;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Entities.Bosses;
using UnityEditor;

namespace Rollgeon.Editor.Tools.Enemy
{
    /// <summary>
    /// Chequeos de la ficha completa: identidad, visual, recompensas, debilidad, tiers, IA y
    /// coherencia entre la ficha declarativa (GDD) y lo que el árbol hace de verdad
    /// (<see cref="EnemyTreeSummary"/>). Puro: no toca assets ni dibuja.
    /// </summary>
    public static class EnemyDataValidator
    {
        public const string SecIdentity = "Identidad";
        public const string SecVisual = "Visual";
        public const string SecRewards = "Recompensas";
        public const string SecWeakness = "Debilidad";
        public const string SecTiers = "Tiers";
        public const string SecAI = "Árbol de IA";
        public const string SecSheet = "Ficha de diseño";
        public const string SecFootprint = "Tamaño en grilla";

        /// <summary>Lado a partir del cual pocas salas tienen lugar y el spawn puede caer al fallback 1×1.</summary>
        public const int LargeFootprintSide = 4;

        public static List<EnemyIssue> Validate(EnemyDataSO so, IReadOnlyList<EnemyDataSO> all,
                                                ComboCatalogSO catalogOrNull, EnemyTreeSummary summaryOrNull = null)
        {
            string builderMenu = so != null && BossBuilderRegistry.TryGetBuilder(so, out var menu) ? menu : null;
            return Validate(so, all, catalogOrNull, summaryOrNull, builderMenu);
        }

        /// <param name="builderMenuOrNull">
        /// Menú del builder que genera este asset (ver <see cref="BossBuilderRegistry"/>), o null si
        /// se autora a mano. Cambia la severidad del prefab faltante: los jefes en pausa tienen el
        /// prefab borrado a propósito y el builder lo regenera.
        /// </param>
        public static List<EnemyIssue> Validate(EnemyDataSO so, IReadOnlyList<EnemyDataSO> all,
                                                ComboCatalogSO catalogOrNull, EnemyTreeSummary summaryOrNull,
                                                string builderMenuOrNull)
        {
            var issues = new List<EnemyIssue>();
            if (so == null) return issues;
            var summary = summaryOrNull ?? EnemyTreeSummary.Build(so);

            // ---- identidad ---------------------------------------------------
            if (string.IsNullOrWhiteSpace(so.EntityId))
                issues.Add(new EnemyIssue(EnemyIssueSeverity.Error, SecIdentity, "EntityId vacío: el catálogo y los pools no pueden referenciarlo."));
            else if (all != null)
            {
                foreach (var other in all)
                {
                    if (other == null || other == so) continue;
                    if (other.EntityId == so.EntityId)
                    {
                        issues.Add(new EnemyIssue(EnemyIssueSeverity.Error, SecIdentity,
                            $"EntityId '{so.EntityId}' repetido en '{other.name}'."));
                        break;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(so.DisplayName))
                issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecIdentity, "Sin nombre visible: la UI muestra el nombre del asset."));

            // ---- visual ------------------------------------------------------
            if (so.VisualPrefab == null)
            {
                if (builderMenuOrNull != null)
                    issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecVisual,
                        $"Sin Visual Prefab: jefe en pausa (prefab borrado a propósito). Correr {builderMenuOrNull} lo regenera."));
                else
                    issues.Add(new EnemyIssue(EnemyIssueSeverity.Error, SecVisual, "Sin Visual Prefab: spawnea sin pawn visual (EntityVisualService loguea error)."));
            }
            if (so.Portrait == null)
                issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecVisual, "Sin retrato: la cola de turnos y la barra de jefe muestran un placeholder."));

            // ---- tamaño en grilla ---------------------------------------------
            if (so.Footprint.x < 1 || so.Footprint.y < 1)
                issues.Add(new EnemyIssue(EnemyIssueSeverity.Error, SecFootprint,
                    $"Tamaño {so.Footprint.x}×{so.Footprint.y}: cada lado tiene que ser ≥ 1 (el runtime lo clampea a 1, corregilo)."));
            else if (so.HasMultiCellFootprint)
            {
                var fp = so.EffectiveFootprint;
                issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecFootprint,
                    $"Tamaño {fp.x}×{fp.y}: se mueve, ataca y se empuja como rectángulo (Fase B), pero el " +
                    "targeting del jugador, el AoE y las casillas especiales lo tratan como su ancla (Fase C pendiente)."));
                if (Math.Max(fp.x, fp.y) >= LargeFootprintSide)
                    issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecFootprint,
                        $"Tamaño {fp.x}×{fp.y}: pocas salas tienen lugar; si no cabe cerca del spawn se registra 1×1."));
            }

            // ---- recompensas -------------------------------------------------
            if (so.MinGoldDrop > so.MaxGoldDrop)
                issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecRewards,
                    $"Oro mínimo ({so.MinGoldDrop}) mayor que el máximo ({so.MaxGoldDrop}): siempre dropea el mínimo."));

            // ---- debilidad ---------------------------------------------------
            if (!string.IsNullOrEmpty(so.WeaknessComboId))
            {
                if (catalogOrNull != null)
                {
                    if (!catalogOrNull.Contains(so.WeaknessComboId))
                        issues.Add(new EnemyIssue(EnemyIssueSeverity.Error, SecWeakness,
                            $"El combo '{so.WeaknessComboId}' no está en ComboCatalog: la debilidad nunca aplica."));
                }
                else
                {
                    issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecWeakness,
                        "No hay ComboCatalogSO en el proyecto: no se puede verificar la debilidad."));
                }
            }

            // ---- tiers -------------------------------------------------------
            var tiers = so.ExtraTiers;
            if (tiers != null)
            {
                for (int tierNumber = 2; tierNumber <= tiers.Count + 1; tierNumber++)
                {
                    if (so.EffectiveMinFloor(tierNumber) <= so.EffectiveMinFloor(tierNumber - 1))
                    {
                        var t = tiers[tierNumber - 2];
                        string label = t != null && !string.IsNullOrEmpty(t.Label) ? $"Tier {tierNumber} — {t.Label}" : $"Tier {tierNumber}";
                        issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecTiers,
                            $"{label} arranca en un piso ≤ al del tier anterior: el anterior queda inalcanzable."));
                        break;
                    }
                }
            }

            // ---- IA ----------------------------------------------------------
            if (so.AIRoot == null)
                issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecAI, "Sin árbol de IA: usa el fallback BasicEnemyAI (siempre ataca)."));
            else
            {
                var snap = AITreeSerializer.Load(so.AIRoot, so.AIDetachedNodes);
                foreach (var issue in AITreeValidator.Validate(snap)) issues.Add(FromTreeIssue(issue));
            }
            if (so is BossFloorManagerSO && !so.IsBoss)
                issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecAI,
                    "Es un BossFloorManagerSO sin 'Jefe' marcado: las inmunidades y el kill credit de jefe no aplican."));

            // ---- coherencia ficha ↔ árbol ------------------------------------
            var design = so.Design;
            if (design != null && summary.HasTree)
            {
                if (design.Timing == AttackTiming.Telegraph && !summary.HasTelegraph)
                    issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecSheet, "Timing = Telegraph pero el árbol no marca ninguna área (TelegraphMark / IgniteArea)."));
                if (design.Timing == AttackTiming.Instant && summary.HasTelegraph)
                    issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecSheet, "Timing = Instantáneo pero el árbol telegrafía áreas."));

                switch (design.Archetype)
                {
                    case EnemyArchetype.Support:
                        if (!summary.HasHeal && !summary.HasBuff)
                            issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecSheet, "Arquetipo = Apoyo pero no cura ni buffea a nadie."));
                        break;
                    case EnemyArchetype.Ranged:
                        if (!summary.HasRangedShot && !summary.KeepsDistance && !summary.HasTelegraph)
                            issues.Add(new EnemyIssue(EnemyIssueSeverity.Warning, SecSheet, "Arquetipo = A distancia pero no dispara, no telegrafía ni mantiene distancia."));
                        break;
                    case EnemyArchetype.Melee:
                        if (!summary.HasMovement)
                            issues.Add(new EnemyIssue(EnemyIssueSeverity.Info, SecSheet, "Arquetipo = Cuerpo a cuerpo sin nodos de movimiento (válido si es estático)."));
                        break;
                }
            }

            issues.Sort((a, b) => b.Severity.CompareTo(a.Severity)); // Error primero
            return issues;
        }

        /// <summary>Único punto de mapeo con el validador del árbol: si cambia su tipo, cambia acá.</summary>
        static EnemyIssue FromTreeIssue(ValidationIssue issue)
        {
            EnemyIssueSeverity sev;
            switch (issue.Severity)
            {
                case IssueSeverity.Error:   sev = EnemyIssueSeverity.Error; break;
                case IssueSeverity.Warning: sev = EnemyIssueSeverity.Warning; break;
                default:                    sev = EnemyIssueSeverity.Info; break;
            }
            string msg = issue.Node != null ? $"{issue.Node.NodeName}: {issue.Message}" : issue.Message;
            return new EnemyIssue(sev, SecAI, msg);
        }

        public static int Count(List<EnemyIssue> issues, EnemyIssueSeverity severity)
        {
            int c = 0;
            if (issues == null) return 0;
            foreach (var i in issues) if (i.Severity == severity) c++;
            return c;
        }

        public static ComboCatalogSO FindComboCatalog()
        {
            var guids = AssetDatabase.FindAssets("t:ComboCatalogSO");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<ComboCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
