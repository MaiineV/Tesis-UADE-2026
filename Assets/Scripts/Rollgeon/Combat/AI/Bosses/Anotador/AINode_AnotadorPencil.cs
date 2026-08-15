using System;
using Rollgeon.Combat.Pipelines;
using Rollgeon.PreConditions.Concretes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// El lápiz del Anotador (piso 2): 12 de daño melee <b>directo</b> —sin marca y sin área— contra
    /// el jugador que esté pegado cuando le toca el turno. Ficha de diseño "El Anotador".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por qué directo y no telegrafiado.</b> El lápiz era un anillo 3×3 avisado un turno antes por
    /// canal auxiliar. Eso ponía un tercer overlay en un piso que ya pinta la franja de fila/columna y
    /// la estela de hielo, y el tercero era justo el que menos decisión cambiaba: 12 de daño que sólo
    /// cobran si el jugador sigue pegado. Cobrado en el acto, el peaje de acercarse se lee sin overlay
    /// y el piso queda para las dos amenazas que sí se esquivan moviéndose.
    /// </para>
    /// <para>
    /// <b>Va antes del repliegue.</b> "Estar a 1 cuando le toca" se mide al empezar su turno, sobre la
    /// posición que el jugador eligió. Después de <see cref="AINode_KeepDistance"/> el boss ya está a
    /// distancia 4 y el lápiz no cobraría nunca, salvo en el caso raro de que el repliegue falle. El
    /// anillo telegrafiado sí tenía que ir después —su área se ancla en la casilla final del boss—,
    /// pero un golpe sin área no arrastra esa restricción.
    /// </para>
    /// <para>
    /// <b>Manhattan y no Chebyshev.</b> El rango del jugador se mide en Manhattan
    /// (<c>SelectionSettings</c>), así que las casillas a Manhattan 1 son exactamente las que tiene
    /// que ocupar para pegarle de melee. El lápiz cobra el peaje de esa casilla, no el de una diagonal
    /// desde la que nadie ataca.
    /// </para>
    /// <para>
    /// <b>La paridad la decide el árbol.</b> El nodo no se auto-gatea por ronda: la alternancia
    /// fila/columna/lápiz es una propiedad del ciclo de turno del jefe y vive en un solo lugar (el
    /// <see cref="AINode_If"/> que lo cuelga). Un <c>Failed</c> por estar lejos es el caso mayoritario,
    /// así que en el árbol va dentro de un <c>Selector[…, Wait]</c> como el resto.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_AnotadorPencil : AIActionNode
    {
        [Tooltip("Daño del lápiz. Va directo al pipeline: no pasa por telegraph ni por área amenazada.")]
        [MinValue(0)]
        public int Damage = 12;

        [Tooltip("Alcance en casillas. 1 = pegado.")]
        [MinValue(1)]
        public int Range = 1;

        [Tooltip("Métrica de distancia al jugador. Manhattan = las casillas desde las que el jugador " +
                 "puede pegarle a él.")]
        public DistanceMetric Metric = DistanceMetric.Manhattan;

        [Tooltip("Tipo de ataque del DamageContext.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        public override string NodeName => $"Anotador — Lápiz ({Damage})";

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
