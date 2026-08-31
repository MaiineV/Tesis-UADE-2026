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

            // Rango real de la ficha (atributo materializado por CreateRuntimeStats, con
            // buffs). Sin atributo o ≤ 0 (enemigos viejos, fakes) planea como melee de 1,
            // el comportamiento histórico.
            int attackRange = 1;
            var rangeAttr = context.Attributes?.GetAttribute<AttackRange>(context.SelfGuid);
            if (rangeAttr != null && rangeAttr.ModifiedValue > 0) attackRange = rangeAttr.ModifiedValue;

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
                AttackRange = attackRange,
                TargetHpPct = -1,
                Personality = context.Personality,
            };

            var plan = context.PathPlanner.PlanMove(request);
            if (!plan.HasMove) return false;

            if (plan.Path != null && context.Movement is IPathedMovementService pathed)
                return pathed.CommitPath(context.SelfGuid, plan.Path, applyPathFilter: true);

            // Sin path explícito: el Move clásico, con los mismos eventos.
            return context.Movement.Move(context.SelfGuid, plan.Destination);
        }
    }
}
