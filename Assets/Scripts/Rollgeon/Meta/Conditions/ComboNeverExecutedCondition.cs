using System;
using UnityEngine;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Por combos (#164): completar la run <b>sin</b> ejecutar el combo
    /// <see cref="ComboId"/>. Condición de consistencia total: solo evaluable al
    /// cierre de run, y se invalida en el momento en que el combo se ejecuta.
    /// </summary>
    [Serializable]
    public sealed class ComboNeverExecutedCondition : IUnlockCondition
    {
        [Tooltip("ComboId que NO debe ejecutarse en toda la run.")]
        public string ComboId;

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx == null || !ctx.RunEnded || string.IsNullOrEmpty(ComboId)) return false;
            return ctx.GetComboCount(ComboId) == 0;
        }

        public bool IsInvalidated(UnlockEvaluationContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ComboId)) return false;
            return ctx.GetComboCount(ComboId) > 0;
        }
    }
}
