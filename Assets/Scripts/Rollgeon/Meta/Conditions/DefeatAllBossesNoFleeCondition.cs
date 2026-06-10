using System;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Por ejecución (#164): completar la run derrotando al Boss de cada piso sin
    /// huir de ningún combate. Condición de consistencia: se invalida en el momento
    /// de la primera huida (<c>CombatOutcome.Aborted</c>) y solo puede confirmarse
    /// al cierre de run.
    /// </summary>
    [Serializable]
    public sealed class DefeatAllBossesNoFleeCondition : IUnlockCondition
    {
        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx == null || !ctx.RunEnded) return false;
            if (ctx.CombatsFled > 0) return false;
            return ctx.FloorsVisited > 0 && ctx.BossesDefeated >= ctx.FloorsVisited;
        }

        public bool IsInvalidated(UnlockEvaluationContext ctx)
        {
            return ctx != null && ctx.CombatsFled > 0;
        }
    }
}
