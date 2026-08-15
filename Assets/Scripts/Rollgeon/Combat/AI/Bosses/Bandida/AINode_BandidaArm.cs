using System;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// El brazo de La Bandida: 12 de daño melee directo a quien haya terminado su turno pegado a la
    /// máquina. Sin marca y sin área. Ficha de diseño "La Bandida" (piso 2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué dejó de ser un <c>TelegraphMark</c>.</b> Como marca de 3×3 sobre el jefe, el brazo
    /// avisaba un turno antes y se esquivaba con un paso: era daño que nunca entraba y una amenaza
    /// más compitiendo por lectura con el número del jackpot. Como golpe directo es el precio fijo de
    /// desarmar de cerca — los rodillos están en el anillo del jefe, así que romperlos es quedar a su
    /// alcance, y esa es la decisión que la pelea quiere cobrar.
    /// </para>
    /// <para>
    /// <b>"Termina el turno pegado" se resuelve mirando el presente.</b> El jefe actúa después del
    /// jugador (CNF-006), así que la posición que se lee en su turno ES la posición en la que el
    /// jugador cerró el suyo: no hace falta recordar nada. Mismo patrón que
    /// <c>AINode_TahurPoke</c>.
    /// </para>
    /// <para>
    /// <b>Se auto-gatea.</b> El árbol ya lo envuelve en un <c>If(PcTargetInRange)</c>, pero el nodo
    /// vuelve a medir la distancia: un rewire que se olvide la condición no puede convertir el brazo
    /// en un golpe a distancia, que es justo lo que el jefe atornillado a la pared no puede tener.
    /// El <see cref="Metric"/> tiene que ser el mismo que el del gate o una de las dos mitades
    /// miente sobre las diagonales.
    /// </para>
    /// <para>
    /// <b>Sin <c>FaceTarget</c>, a diferencia de <c>AINode_CashierRangedShot</c>.</b> Los dos jefes
    /// que giran hacia el jugador antes de pegar se mueven por la sala y pueden quedar de espaldas;
    /// esta máquina está atornillada y su ataque es una palanca que baja, no un puño que apunta.
    /// Girarla sería mueble rotando contra la pared.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_BandidaArm : AIActionNode
    {
        [Tooltip("Daño del brazo. Directo, sin marca previa.")]
        [MinValue(0)]
        public int Damage = 12;

        [Tooltip("Alcance en casillas. 1 = pegado a la máquina.")]
        [MinValue(1)]
        public int Range = 1;

        [Tooltip("Métrica de distancia al jugador. Chebyshev incluye las diagonales — tiene que " +
                 "coincidir con la del PcTargetInRange que gatea el nodo.")]
        public DistanceMetric Metric = DistanceMetric.Chebyshev;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        public override string NodeName => $"Bandida — Arm ({Damage})";

        public override AIResult Tick(AIContext context)
        {
            if (context?.Grid == null) return AIResult.Failed;
            if (context.DamagePipeline == null || Damage <= 0) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;

            int distance = Metric == DistanceMetric.Manhattan
                ? selfCoord.Manhattan(playerCoord)
                : selfCoord.Chebyshev(playerCoord);
            if (distance > Mathf.Max(1, Range)) return AIResult.Failed;

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
