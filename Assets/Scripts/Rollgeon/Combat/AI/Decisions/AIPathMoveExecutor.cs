using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Pathing;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Camino compartido de <c>AINode_Move</c> / <c>AINode_KeepDistance</c> cuando hay
    /// planner: arma el request, planea, y ejecuta. Un path explícito se camina con
    /// <see cref="IPathedMovementService.CommitPath"/> — la ruta que esquiva el charco
    /// tiene que ser la que se pisa, no un recálculo A* uniforme.
    /// </summary>
    internal static class AIPathMoveExecutor
    {
        public static bool TryPlanAndMove(AIContext context, GridCoord targetCoord,
            int maxSteps, int desiredRange, MoveIntent intent)
        {
            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return false;

            int currentHp = context.SelfMaxHp;
            var health = context.Attributes?.GetAttribute<Health>(context.SelfGuid);
            if (health != null) currentHp = health.Value;

            var request = new AIPathRequest
            {
                SelfGuid = context.SelfGuid,
                Origin = selfCoord,
                TargetCoord = targetCoord,
                MaxSteps = maxSteps,
                DesiredRange = desiredRange,
                Intent = intent,
                CurrentHp = currentHp,
                MaxHp = Mathf.Max(1, context.SelfMaxHp),
                // #158: BaseAttackRange sigue reservado — wirearlo cuando el targeting con
                // rango exista. Hoy todo el combate es melee-adyacente.
                AttackRange = 1,
                TargetHpPct = -1,
                Personality = context.Personality,
            };

            var plan = context.PathPlanner.PlanMove(request);
            if (!plan.HasMove) return false;

            if (plan.Path != null && context.Movement is IPathedMovementService pathed)
                return pathed.CommitPath(context.SelfGuid, plan.Path, applyPathFilter: true);

            // Sin path explícito (fast-path legacy): el Move clásico, mismos eventos que hoy.
            return context.Movement.Move(context.SelfGuid, plan.Destination);
        }
    }
}
