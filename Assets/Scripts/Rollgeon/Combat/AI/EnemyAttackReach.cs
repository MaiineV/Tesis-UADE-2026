using System;
using System.Collections.Generic;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Effects.Concretes;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// El alcance del arma de un enemigo leído del árbol sin tickearlo: las celdas desde las que
    /// su ataque pega, medidas desde donde está parado y sin contar su movimiento.
    /// </summary>
    /// <remarks>
    /// Es la lectura estática de los gates de rango que el árbol ya declara — el
    /// <see cref="PcTargetInRange"/> o <see cref="PCEntityInRange"/> de un
    /// <see cref="AINode_If"/> que envuelve un ataque, o el auto-gate de un
    /// <see cref="AINode_RangedShot"/> — nunca una simulación del turno.
    /// Sobre-aproxima siempre a favor del jugador: la línea de visión y las condiciones hermanas
    /// del If se ignoran (un blocker puede morir o correrse dentro del turno del jugador), así
    /// que puede pintar de más pero nunca de menos. No baja por <c>AINode_Random</c> (límite de
    /// <see cref="AIIntentWalker.CollectNodes{T}"/>, compartido con otros lectores del árbol).
    /// El alcance viaja por acá y no por <see cref="AIIntent.Tiles"/>: Tiles es dónde cae,
    /// y su invariante ("vacío = no se sabe") sigue intacto.
    /// </remarks>
    public static class EnemyAttackReach
    {
        /// <summary>
        /// Un gate de rango replicado del árbol. <see cref="FootprintAware"/> separa la
        /// matemática del If (rect-a-celda, como <see cref="PcTargetInRange"/>) de la del
        /// disparo (ancla 1×1, como <c>AINode_RangedShot.CanFire</c>): en un jefe multi-celda
        /// no miden lo mismo.
        /// </summary>
        private readonly struct Gate
        {
            public readonly int Range;
            public readonly DistanceMetric Metric;
            public readonly TargetAlignment Alignment;
            public readonly bool FootprintAware;

            public Gate(int range, DistanceMetric metric, TargetAlignment alignment,
                        bool footprintAware)
            {
                Range = range;
                Metric = metric;
                Alignment = alignment;
                FootprintAware = footprintAware;
            }
        }

        /// <summary>
        /// Llena <paramref name="into"/> con la unión de alcances de los ataques del árbol.
        /// Si la unión cubre todas las casillas de la sala se degrada a vacío: un aviso que es
        /// toda la sala no informa nada y cuesta un quad por casilla. El set se limpia antes.
        /// </summary>
        public static void Collect(AIDecisionNode root, AIContext context, HashSet<GridCoord> into)
        {
            if (into == null) return;
            into.Clear();

            if (root == null || context == null) return;
            var grid = context.Grid;
            if (grid == null) return;
            if (!grid.TryGetPosition(context.SelfGuid, out var anchor)) return;

            var descriptors = CollectDescriptors(root, context);
            if (descriptors.Count == 0) return;

            var footprint = grid.GetFootprint(context.SelfGuid);
            var selfCells = new HashSet<GridCoord>(grid.OccupiedCells(context.SelfGuid));

            int candidates = 0;
            foreach (var cell in ThreatAreaShape.RoomTiles(grid))
            {
                if (selfCells.Contains(cell)) continue;
                candidates++;
                if (AnyDescriptorReaches(descriptors, grid, context, anchor, footprint, cell))
                    into.Add(cell);
            }

            if (candidates > 0 && into.Count == candidates) into.Clear();
        }

        /// <summary>
        /// Un descriptor por ataque: los gates de un mismo If se exigen todos (AND, como los
        /// evalúa el tick); entre descriptores la celda entra con que uno la alcance (unión).
        /// </summary>
        private static List<List<Gate>> CollectDescriptors(AIDecisionNode root, AIContext context)
        {
            var descriptors = new List<List<Gate>>();

            var shots = new List<AINode_RangedShot>();
            AIIntentWalker.CollectNodes(root, shots);
            foreach (var shot in shots)
            {
                if (shot == null || shot.Damage <= 0) continue;
                descriptors.Add(new List<Gate>
                {
                    new Gate(Mathf.Max(1, shot.Range), shot.Metric, TargetAlignment.Any,
                             footprintAware: false),
                });
            }

            var branches = new List<AINode_If>();
            AIIntentWalker.CollectNodes(root, branches);
            foreach (var branch in branches)
            {
                if (branch?.Conditions == null) continue;

                // El heal del healer y la fuga del Croupier también viven detrás de un
                // PcTargetInRange: sólo aporta alcance el gate cuyo Then de verdad pega. Un
                // RangedShot en el Then tampoco cuenta acá — ya se auto-gatea con su propio
                // Range por ancla, y sumarle el gate footprint-aware del If pintaría de más
                // en jefes multi-celda.
                if (!SubtreeHasGateableAttack(branch.Then)) continue;

                List<Gate> gates = null;
                foreach (var condition in branch.Conditions)
                {
                    if (condition is PcTargetInRange pc)
                    {
                        gates ??= new List<Gate>();
                        gates.Add(new Gate(EffectiveRange(pc, context), pc.Metric, pc.Alignment,
                                           footprintAware: true));
                    }
                    // El bestiario melee (y el mímico) gatean con PCEntityInRange: ancla-a-ancla,
                    // sin ficha, sin alineación y sin línea de visión — réplica de su Evaluate.
                    else if (condition is PCEntityInRange entityGate)
                    {
                        gates ??= new List<Gate>();
                        gates.Add(new Gate(entityGate.MaxRange, entityGate.Metric,
                                           TargetAlignment.Any, footprintAware: false));
                    }
                }
                if (gates != null) descriptors.Add(gates);
            }

            return descriptors;
        }

        private static bool SubtreeHasGateableAttack(AIDecisionNode then)
        {
            if (then == null) return false;

            var behaviors = new List<AINode_Behavior>();
            AIIntentWalker.CollectNodes(then, behaviors);
            foreach (var node in behaviors)
            {
                var groups = node?.Behavior?.Effects;
                if (groups == null) continue;
                foreach (var group in groups)
                {
                    if (group?.Effects == null) continue;
                    foreach (var effect in group.Effects)
                        if (effect is EffDealDamage) return true;
                }
            }

            // Sniper, artillery y mago atacan con un telegraph dentro del gate: el aviso con
            // daño es su golpe, aunque se cobre al turno siguiente.
            var marks = new List<AINode_TelegraphMark>();
            AIIntentWalker.CollectNodes(then, marks);
            foreach (var mark in marks)
                if (mark != null && mark.Damage > 0) return true;

            return false;
        }

        // Réplica de PcTargetInRange.Evaluate: la ficha del owner si el designer lo pidió y
        // existe; si no, el campo Range del gate.
        private static int EffectiveRange(PcTargetInRange pc, AIContext context)
        {
            int range = pc.Range;
            if (pc.UseOwnerAttackRange && context.Attributes != null)
            {
                int fromSheet = context.Attributes
                    .GetAttributeModifiedValue<AttackRange, int>(context.SelfGuid);
                if (fromSheet > 0) range = fromSheet;
            }
            return range;
        }

        private static bool AnyDescriptorReaches(List<List<Gate>> descriptors, IGridManager grid,
                                                 AIContext context, GridCoord anchor,
                                                 Vector2Int footprint, GridCoord cell)
        {
            foreach (var gates in descriptors)
            {
                bool all = true;
                foreach (var gate in gates)
                {
                    if (Reaches(gate, grid, context, anchor, footprint, cell)) continue;
                    all = false;
                    break;
                }
                if (all) return true;
            }
            return false;
        }

        private static bool Reaches(in Gate gate, IGridManager grid, AIContext context,
                                    GridCoord anchor, Vector2Int footprint, GridCoord cell)
        {
            int distance;
            if (gate.FootprintAware)
            {
                distance = gate.Metric == DistanceMetric.Manhattan
                    ? GridFootprint.ManhattanDistance(anchor, footprint, cell)
                    : GridFootprint.ChebyshevDistance(anchor, footprint, cell);
            }
            else
            {
                distance = gate.Metric == DistanceMetric.Manhattan
                    ? anchor.Manhattan(cell)
                    : anchor.Chebyshev(cell);
            }

            if (distance > gate.Range) return false;
            if (gate.Alignment == TargetAlignment.Any) return true;

            // Como PcTargetInRange: la alineación se mide contra la celda propia más cercana a
            // la celda target (first-wins en el orden de OccupiedCells), no contra el ancla.
            var nearest = anchor;
            int best = int.MaxValue;
            foreach (var own in grid.OccupiedCells(context.SelfGuid))
            {
                int d = own.Manhattan(cell);
                if (d < best)
                {
                    best = d;
                    nearest = own;
                }
            }

            int dx = cell.X - nearest.X;
            int dy = cell.Y - nearest.Y;
            bool ortho = (dx == 0) != (dy == 0);
            bool diag = dx != 0 && Math.Abs(dx) == Math.Abs(dy);

            return gate.Alignment == TargetAlignment.SameRowOrColumn ? ortho : diag;
        }
    }
}
