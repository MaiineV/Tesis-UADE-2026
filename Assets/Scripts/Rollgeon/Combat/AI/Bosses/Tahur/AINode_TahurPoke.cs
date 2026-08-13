using System;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// El poke del Tahúr: 12 de daño melee, solo en ronda limpia. Es el precio fijo de cobrar,
    /// porque cobrar es estar en su cara. Ficha de diseño "El Tahúr" (piso 3).
    /// </summary>
    /// <remarks>
    /// <b>Exclusivo de la rama de marcar.</b> El poke y el Castigo nunca resuelven la misma ronda:
    /// 12 + 45 rompe el techo de 45 por golpe del piso 3. El árbol ya lo gatea con
    /// <c>PcTahurCleanRound</c>, pero el nodo se auto-gatea igual — un rewire que se olvide la
    /// condición no puede convertirse en un golpe de 57.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TahurPoke : AIActionNode
    {
        [Tooltip("Daño del poke.")]
        [MinValue(0)]
        public int Damage = 12;

        [Tooltip("Alcance en casillas (Manhattan). 1 = pegado.")]
        [MinValue(1)]
        public int Range = 1;

        [Tooltip("Métrica de distancia al jugador.")]
        public DistanceMetric Metric = DistanceMetric.Manhattan;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Tooltip("Exigir ronda limpia (que la liquidación de este turno no haya marcado Castigo). " +
                 "Apagarlo permite poke + Castigo en la misma ronda y rompe el techo de daño.")]
        public bool RequireCleanRound = true;

        public override string NodeName => $"Tahúr — Poke ({Damage})";

        public override AIResult Tick(AIContext context)
        {
            if (context?.Grid == null) return AIResult.Failed;

            if (RequireCleanRound)
            {
                var wager = TahurWagerService.ResolveOrCreate();
                if (wager.MarkedPunishmentThisTurn) return AIResult.Failed;
            }

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;

            int distance = Metric == DistanceMetric.Manhattan
                ? selfCoord.Manhattan(playerCoord)
                : selfCoord.Chebyshev(playerCoord);
            if (distance > Mathf.Max(1, Range)) return AIResult.Failed;

            if (context.DamagePipeline == null || Damage <= 0) return AIResult.Failed;

            context.DamagePipeline.Resolve(new DamageContext
            {
                SourceId = context.SelfGuid,
                TargetId = context.PlayerGuid,
                BaseDamage = Damage,
                Kind = Kind,
            });

            return AIResult.Succeeded;
        }
    }
}
