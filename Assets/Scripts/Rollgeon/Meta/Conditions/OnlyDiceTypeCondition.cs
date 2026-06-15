using System;
using Rollgeon.Dice;
using UnityEngine;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Por build (#164): la run se jugó usando <b>solo</b> dados de tipo
    /// <see cref="Type"/> (build no vacía, todos los slots del mismo tipo).
    /// </summary>
    [Serializable]
    public sealed class OnlyDiceTypeCondition : IUnlockCondition
    {
        [Tooltip("Único tipo de dado permitido en la build.")]
        public DiceType Type = DiceType.D6;

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx?.DiceBuild == null || ctx.DiceBuild.Count == 0) return false;
            for (int i = 0; i < ctx.DiceBuild.Count; i++)
            {
                if (ctx.DiceBuild[i] != Type) return false;
            }
            return true;
        }
    }
}
