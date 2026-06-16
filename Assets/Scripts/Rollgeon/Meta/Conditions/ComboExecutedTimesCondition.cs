using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Por combos (#164): el combo <see cref="ComboId"/> se ejecutó al menos
    /// <see cref="Times"/> veces en la run. Monotónica — puede completarse
    /// mid-run (el contador nunca decrece).
    /// </summary>
    [Serializable]
    public sealed class ComboExecutedTimesCondition : IUnlockCondition
    {
        [Tooltip("ComboId del catálogo (ej. 'combo.generala').")]
        public string ComboId;

        [MinValue(1)]
        [Tooltip("Cantidad mínima de ejecuciones requeridas.")]
        public int Times = 1;

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx == null || string.IsNullOrEmpty(ComboId)) return false;
            return ctx.GetComboCount(ComboId) >= Times;
        }
    }
}
