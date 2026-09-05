using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.Items.Active;
using Rollgeon.Items.Active.Targeting;
using Rollgeon.Tiles;
using Rollgeon.Tiles.Forced;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Cómo responde el empuje tras el impacto de la carga de Justa de Justicia — un valor
    /// distinto autorado por banda (Feature#0085, Items_Activos_Redisenados.md §4).
    /// </summary>
    public enum JoustPushMode
    {
        /// <summary>Banda negativa (1-4, "Carga turbulenta"): empuja 1 casilla en una cardinal
        /// libre elegida al azar.</summary>
        RandomAdjacent,

        /// <summary>Banda mixta (5-8, "Carga controlada"): empuja 1 casilla en la dirección de
        /// la carga.</summary>
        OneForward,

        /// <summary>Banda positiva (9-12, "Carga perfecta"): empuja 2 en la dirección de la
        /// carga; si el empuje queda bloqueado, inflige el daño de la carga otra vez.</summary>
        TwoForwardWithCollision,
    }

    /// <summary>
    /// Justa de Justicia (D12 Bandas, dirección — Items_Activos_Redisenados.md §4). Carga hasta
    /// <c>Face</c> casillas, cortada antes de una casilla dañina; si la carga llega entera hasta
    /// un enemigo vivo, inflige <c>Face</c> de daño y lo empuja según <see cref="PushMode"/>
    /// (autorado distinto por banda por el builder de autoría).
    /// </summary>
    /// <remarks>
    /// Uso directo de <see cref="IForcedMovementService"/> a propósito, no
    /// <c>ClassSkillPushResolver</c>: ese resolver siempre stunea contra un choque sólido, y acá
    /// el GDD no lo pide — la colisión de la banda positiva repite daño, no aturde.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffJoustCharge : BaseEffect, IDirectionTargetedEffect
    {
        private const int MaxAcquireRange = 12;

        private static readonly Cardinal[] AllCardinals =
        {
            Cardinal.North, Cardinal.East, Cardinal.South, Cardinal.West,
        };

        [Title("Justa de Justicia")]
        [Tooltip("Cómo empuja tras el impacto — un valor distinto por banda (autoría).")]
        public JoustPushMode PushMode = JoustPushMode.OneForward;

        // La dirección sale del trigger context (§A4), no de un picking propio.
        protected override bool ShowSelection => false;

        /// <summary>RNG del empuje aleatorio (banda negativa). Público y no serializado:
        /// producción usa el default, los tests inyectan una seed fija.</summary>
        [NonSerialized]
        private System.Random _rng;

        /// <remarks>
        /// Propiedad perezosa y no un campo inicializado: Odin instancia los efectos del
        /// asset sin correr constructores ni inicializadores de campo, asi que un
        /// <c>= new System.Random()</c> queda en null en runtime (Probability Drive
        /// tiraba NullReference al resolver — ronda de testers 2026-09-04).
        /// </remarks>
        public System.Random Rng
        {
            get { return _rng ?? (_rng = new System.Random()); }
            set { _rng = value; }
        }

        public override string GetEffectName() => "Jousting Charge";

        public IReadOnlyList<GridCoord> PreviewTrajectory(Guid owner, GridCoord origin, Cardinal dir)
        {
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
                return Array.Empty<GridCoord>();

            var trace = GridLineTrace.Trace(grid, origin, dir, MaxAcquireRange, owner);
            bool hasAdjacentOccupant = trace.Stop == LineTraceStop.Occupant;

            // Sin ninguna celda libre y sin un ocupante adyacente: no hay nada que cargar en
            // esta dirección — inválida.
            if (trace.FreeCells.Count == 0 && !hasAdjacentOccupant)
                return Array.Empty<GridCoord>();

            var result = new List<GridCoord>(trace.FreeCells);
            if (trace.Stop != LineTraceStop.MaxReached) result.Add(trace.HitCoord);
            return result;
        }

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;
            if (!ActiveItemRollTriggerContext.TryGet(context, out var rc) || rc.Direction == null)
            {
                Debug.LogWarning("[EffJoustCharge] sin ActiveItemRollTriggerContext/Direction — no-op.");
                return true;
            }

            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null
                || !ServiceLocator.TryGetService<IForcedMovementService>(out var forced) || forced == null)
            {
                Debug.LogWarning("[EffJoustCharge] IGridManager/IForcedMovementService no registrados — no-op.");
                return true;
            }

            var player = context.SourceGuid;
            var dir = rc.Direction.Value;
            var origin = rc.Origin;

            // La carga real nunca supera la cara — el trace ya viene acotado a Face.
            var trace = GridLineTrace.Trace(grid, origin, dir, rc.Face, player);
            var chargeCells = CutBeforeHarmful(trace.FreeCells);
            if (chargeCells.Count > 0) forced.Push(player, dir, chargeCells.Count, player);

            // Impacto solo si la carga llegó ENTERA hasta el ocupante (nada la cortó antes de
            // una tile dañina) — si se cortó, el jugador ni siquiera llegó a tocarlo.
            bool reachedOccupant = trace.Stop == LineTraceStop.Occupant
                                    && chargeCells.Count == trace.FreeCells.Count;
            if (!reachedOccupant) return true;

            var enemy = trace.Occupant;
            if (!CombatantQuery.LiveEnemiesOf(player).Contains(enemy)) return true; // prop/cofre, no pega

            DealDamage(player, enemy, rc.Face);

            if (!CombatantQuery.IsMovable(enemy)) return true; // daño sin empuje

            switch (PushMode)
            {
                case JoustPushMode.RandomAdjacent:
                    PushRandomAdjacent(grid, forced, player, enemy);
                    break;

                case JoustPushMode.OneForward:
                    forced.Push(enemy, dir, 1, player);
                    break;

                case JoustPushMode.TwoForwardWithCollision:
                    var result = forced.Push(enemy, dir, 2, player);
                    if (result.BlockedByWall || result.BlockedByEntity)
                        DealDamage(player, enemy, rc.Face);
                    break;
            }

            return true;
        }

        private void PushRandomAdjacent(IGridManager grid, IForcedMovementService forced, Guid player, Guid enemy)
        {
            if (!grid.TryGetPosition(enemy, out var enemyCoord)) return;

            var options = new List<Cardinal>();
            foreach (var c in AllCardinals)
            {
                var dest = c.Step(enemyCoord);
                if (grid.InBounds(dest) && grid.IsWalkable(dest) && grid.IsFree(dest)) options.Add(c);
            }
            if (options.Count == 0) return; // sin destino válido: se queda en su lugar

            var pick = options[Rng.Next(options.Count)];
            forced.Push(enemy, pick, 1, player);
        }

        private static void DealDamage(Guid source, Guid target, int amount)
        {
            if (amount <= 0) return;
            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) || pipeline == null) return;

            pipeline.Resolve(new DamageContext
            {
                SourceId = source,
                TargetId = target,
                BaseDamage = amount,
                Kind = AttackKind.ScriptedAbility,
            });
        }

        private static List<GridCoord> CutBeforeHarmful(IReadOnlyList<GridCoord> freeCells)
        {
            var result = new List<GridCoord>();
            foreach (var cell in freeCells)
            {
                if (HarmfulTileQuery.IsHarmfulAt(cell)) break;
                result.Add(cell);
            }
            return result;
        }
    }
}
