using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Por clase (#164): haber jugado N clases distintas. Lee el set persistente
    /// <see cref="UnlockEvaluationContext.ClassesPlayed"/>, contador de acumulación
    /// entre runs que <b>NO se resetea al morir</b>. Se actualiza al cierre de run
    /// (la clase cuenta como "jugada" cuando la run termina).
    /// </summary>
    [Serializable]
    public sealed class DistinctClassesPlayedCondition : IUnlockCondition
    {
        [MinValue(1)]
        [Tooltip("Cantidad de clases distintas que deben haberse jugado.")]
        public int Count = 2;

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx?.ClassesPlayed == null) return false;
            return ctx.ClassesPlayed.Count >= Count;
        }
    }
}
