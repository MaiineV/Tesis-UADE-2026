using System;
using System.Collections.Generic;
using Rollgeon.Grid;

namespace Rollgeon.Combat.AI.Pathing
{
    /// <summary>Qué está intentando el movimiento: acercarse a una banda o kitear.</summary>
    public enum MoveIntent
    {
        /// <summary>Minimizar |dist(target) − DesiredRange| (AINode_Move).</summary>
        Approach = 0,

        /// <summary>Maximizar min(dist, DesiredRange) (AINode_KeepDistance).</summary>
        Kite = 1,
    }

    /// <summary>Un pedido de movimiento de la IA, con todo el contexto que el planner necesita.</summary>
    public struct AIPathRequest
    {
        public Guid SelfGuid;
        public GridCoord Origin;
        public GridCoord TargetCoord;

        /// <summary>Presupuesto REAL de movimiento en tiles — el costo IA nunca lo reduce.</summary>
        public int MaxSteps;

        /// <summary>Approach: banda deseada. Kite: distancia ideal.</summary>
        public int DesiredRange;

        public MoveIntent Intent;
        public int CurrentHp;
        public int MaxHp;

        /// <summary>Para Fortaleza/PrimaryGain ("puede atacar desde ahí"). Default 1.</summary>
        public int AttackRange;

        /// <summary>HP del target en % (0-100), −1 = desconocido (el ContextBonus lo ignora).</summary>
        public int TargetHpPct;

        public AIPersonalityProfile Personality;
    }

    /// <summary>Resultado del plan. <see cref="Path"/> null = ejecutar con el Move clásico
    /// (fast-path legacy: no hay ruta especial que respetar).</summary>
    public readonly struct AIPathPlanResult
    {
        public readonly bool HasMove;
        public readonly GridCoord Destination;
        public readonly IReadOnlyList<GridCoord> Path;

        public AIPathPlanResult(bool hasMove, GridCoord destination, IReadOnlyList<GridCoord> path)
        {
            HasMove = hasMove;
            Destination = destination;
            Path = path;
        }

        public static AIPathPlanResult NoMove => default;
    }

    /// <summary>
    /// Planner de movimiento IA con conciencia de Casillas Especiales: filtro de
    /// supervivencia, costo de tránsito y score de destino (GDD, Fórmula de Pathing IA).
    /// Sin casillas en la sala su resultado es idéntico al scoring legacy de los nodos.
    /// </summary>
    public interface IAIPathPlanner
    {
        AIPathPlanResult PlanMove(in AIPathRequest request);
    }
}
