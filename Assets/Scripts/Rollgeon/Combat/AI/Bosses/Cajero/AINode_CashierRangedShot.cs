using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Entities.Visuals;
using Rollgeon.PreConditions.Concretes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// El disparo del Cajero: <see cref="Damage"/> directos al jugador a distancia
    /// <see cref="Range"/> o menos, sin área y sin telegráfico. Es lo que hace en los turnos en
    /// que no marca columna. Ficha de diseño "El Cajero" (piso 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué existe.</b> Es la mitad de la tenaza que le da presión al jefe. La columna sola
    /// se esquiva con un paso — el jugador salía del área, volvía a pegarle y el Cajero medía 0%
    /// de vida perdida en la mediana de 3000 peleas simuladas. Con el disparo, salir del área ya
    /// no alcanza: para golpearlo hay que estar a distancia 1, y distancia 1 está dentro del rango
    /// del disparo. Esquivar la columna es gratis; dejar de pagar el disparo es dejar de atacar.
    /// </para>
    /// <para>
    /// <b>Se auto-gatea por rango</b> en vez de depender de un <c>PcTargetInRange</c> en el árbol:
    /// el nodo devuelve Failed si el jugador está lejos y el <c>Selector[Shot, Wait]</c> del árbol
    /// lo absorbe. Un rewire que se olvide la condición no puede convertirlo en un ataque de
    /// alcance infinito. Mismo criterio que <c>AINode_TahurPoke</c>.
    /// </para>
    /// <para>
    /// <b>Resuelve por <see cref="IDamagePipeline"/> directo</b>, no por
    /// <c>AINode_Behavior → EnemyActionBehavior → EffDealDamage</c>: el daño base de
    /// <c>EffDealDamage</c> es un campo privado sin setter, así que un builder de editor no puede
    /// autorar los 12 de la ficha — quedaría clavado en el default de 10. El camino directo es el
    /// que ya usan <c>AINode_ExecuteTelegraph</c> y el poke del Tahúr, y pasa por el mismo pipeline
    /// (debilidades, escudo, número flotante).
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CashierRangedShot : AIActionNode
    {
        [Tooltip("Daño directo del disparo. Ficha: 12.")]
        [MinValue(0)]
        public int Damage = 12;

        [Tooltip("Alcance en casillas. Ficha: 4 — el mismo número que la distancia a la que kitea, " +
                 "para que replegarse no lo saque de su propio rango.")]
        [MinValue(1)]
        public int Range = 4;

        [Tooltip("Métrica de distancia al jugador. Manhattan, igual que AINode_KeepDistance: si " +
                 "difirieran, el jefe se replegaría fuera de su propio alcance.")]
        public DistanceMetric Metric = DistanceMetric.Manhattan;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        public override string NodeName => $"Cajero — Disparo ({Damage} a ≤ {Range})";

        public override AIResult Tick(AIContext context)
        {
            if (context?.Grid == null) return AIResult.Failed;
            if (context.SelfGuid == Guid.Empty || context.PlayerGuid == Guid.Empty) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;

            int distance = Metric == DistanceMetric.Manhattan
                ? selfCoord.Manhattan(playerCoord)
                : selfCoord.Chebyshev(playerCoord);
            if (distance > Mathf.Max(1, Range)) return AIResult.Failed;

            if (context.DamagePipeline == null || Damage <= 0) return AIResult.Failed;

            FaceTarget(context);

            context.DamagePipeline.Resolve(new DamageContext
            {
                SourceId = context.SelfGuid,
                TargetId = context.PlayerGuid,
                BaseDamage = Damage,
                Kind = Kind,
            });

            return AIResult.Succeeded;
        }

        /// <summary>
        /// Gira al jefe hacia el jugador antes de tirar la ficha. Sin esto dispara mirando hacia
        /// donde kiteó el turno anterior, que en un enemigo que se aleja es justo el lado opuesto.
        /// No-op sin capa visual (EditMode).
        /// </summary>
        private static void FaceTarget(AIContext context)
        {
            if (!ServiceLocator.TryGetService<IEntityVisualService>(out var visuals) || visuals == null) return;
            if (!visuals.TryGetPawn(context.SelfGuid, out var pawn) || pawn == null) return;
            if (!context.Grid.TryGetPosition(context.SelfGuid, out var from)) return;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var to)) return;
            pawn.FaceCoord(from, to);
        }
    }
}
