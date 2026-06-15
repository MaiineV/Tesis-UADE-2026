using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Por clase (#164): N runs ganadas consecutivas sin perder. Lee el contador
    /// persistente <see cref="UnlockEvaluationContext.ConsecutiveWins"/>, que el
    /// <c>IMetaProgressionService</c> incrementa al ganar y <b>resetea al morir</b>
    /// (contador de consistencia). Solo evaluable al cierre de run, después de
    /// actualizar el contador.
    /// </summary>
    [Serializable]
    public sealed class ConsecutiveWinsCondition : IUnlockCondition
    {
        [MinValue(1)]
        [Tooltip("Cantidad de victorias consecutivas requeridas.")]
        public int Wins = 2;

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx == null || !ctx.RunEnded) return false;
            return ctx.ConsecutiveWins >= Wins;
        }
    }
}
