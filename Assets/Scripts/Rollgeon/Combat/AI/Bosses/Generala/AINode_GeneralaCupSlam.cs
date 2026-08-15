using System;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Generala
{
    /// <summary>
    /// El cubilete de La Generala: cuando tira, baja la copa sobre quien esté pegado a ella y le
    /// cobra <see cref="Damage"/> directos. Ficha de diseño "La Generala" (piso 3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Es el precio de romper de cerca.</b> Los cinco dados son la mano del jefe y romperlos es
    /// la jugada que le borra categorías, pero romperlos es acercarse. Sin este golpe la mesa se
    /// desarma gratis: el resto de su daño viaja por telegraphs avisados una ronda antes, o sea
    /// esquivables sin renunciar a nada.
    /// </para>
    /// <para>
    /// <b>Directo, no avisado.</b> No marca área ni pinta overlay — el aviso es la distancia, que el
    /// jugador controla entero. Por eso el nodo tiene que ir envuelto en un <c>Selector[nodo, Wait]</c>:
    /// con el jugador lejos devuelve <see cref="AIResult.Failed"/> y sin la envoltura le cancelaría
    /// al jefe el resto del turno, el telegraph de la mano incluido.
    /// </para>
    /// <para>
    /// <b>Manhattan por default.</b> Es el mismo alcance con el que el jugador la ataca a ella
    /// (<c>Base Attack</c>: Range 1, RangeMode Manhattan), así que la regla se lee de una: si podés
    /// pegarle, te alcanza. Con <see cref="DistanceMetric.Chebyshev"/> el cubilete recupera el 3×3
    /// completo — incluidas las diagonales, desde donde el jugador no puede atacar.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_GeneralaCupSlam : AIActionNode
    {
        [Tooltip("Daño directo del cubilete. No se avisa: cobra en el acto, el mismo turno en que tira.")]
        [MinValue(0)]
        public int Damage = 18;

        [Tooltip("Alcance en casillas. 1 = pegado.")]
        [MinValue(1)]
        public int Range = 1;

        [Tooltip("Métrica de distancia al jugador. Manhattan = las cuatro casillas desde las que el " +
                 "jugador puede atacarla. Chebyshev = el 3×3 entero, diagonales incluidas.")]
        public DistanceMetric Metric = DistanceMetric.Manhattan;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        public override string NodeName => $"Generala — Cubilete ({Damage} melee)";

        public override AIResult Tick(AIContext context)
        {
            if (context?.Grid == null) return AIResult.Failed;
            if (context.SelfGuid == Guid.Empty || context.PlayerGuid == Guid.Empty) return AIResult.Failed;
            if (context.DamagePipeline == null || Damage <= 0) return AIResult.Failed;

            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;

            if (Distance(selfCoord, playerCoord) > Mathf.Max(1, Range)) return AIResult.Failed;

            context.DamagePipeline.Resolve(new DamageContext
            {
                SourceId = context.SelfGuid,
                TargetId = context.PlayerGuid,
                BaseDamage = Damage,
                Kind = Kind,
            });

            return AIResult.Succeeded;
        }

        private int Distance(GridCoord from, GridCoord to) => Metric == DistanceMetric.Manhattan
            ? from.Manhattan(to)
            : from.Chebyshev(to);
    }
}
