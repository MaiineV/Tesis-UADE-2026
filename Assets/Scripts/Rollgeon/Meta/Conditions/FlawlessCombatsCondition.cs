using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Por ejecución (#164): completar la run sin recibir daño en N o más combates.
    /// Monotónica — el conteo de combates flawless nunca decrece, así que puede
    /// completarse mid-run.
    /// </summary>
    [Serializable]
    public sealed class FlawlessCombatsCondition : IUnlockCondition
    {
        [MinValue(1)]
        [Tooltip("Cantidad mínima de combates ganados sin recibir daño.")]
        public int MinCombats = 3;

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx == null) return false;
            return ctx.FlawlessCombats >= MinCombats;
        }
    }
}
