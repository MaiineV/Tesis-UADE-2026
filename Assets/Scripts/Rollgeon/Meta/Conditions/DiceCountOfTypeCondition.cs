using System;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>Modo de comparación de <see cref="DiceCountOfTypeCondition"/>.</summary>
    public enum DiceCountComparison
    {
        /// <summary>La build tiene exactamente N dados del tipo.</summary>
        Exactly,

        /// <summary>La build tiene N o más dados del tipo (ej. "ganar con D8 en la build").</summary>
        AtLeast,
    }

    /// <summary>
    /// Por build (#164): la run se jugó con <see cref="Count"/> dados de tipo
    /// <see cref="Type"/> en la bolsa (exactos o como mínimo según
    /// <see cref="Comparison"/>). La build es fija desde el run start, así que es
    /// evaluable en cualquier momento; el filtro de outcome del
    /// <see cref="UnlockDefinitionSO"/> decide si además exige ganar/perder.
    /// </summary>
    [Serializable]
    public sealed class DiceCountOfTypeCondition : IUnlockCondition
    {
        [Tooltip("Tipo de dado a contar en la build.")]
        public DiceType Type = DiceType.D6;

        [MinValue(0)]
        [Tooltip("Cantidad de dados de ese tipo requerida en la build.")]
        public int Count = 5;

        [Tooltip("Exactly = exactamente N. AtLeast = N o más.")]
        public DiceCountComparison Comparison = DiceCountComparison.Exactly;

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx?.DiceBuild == null) return false;
            int found = 0;
            for (int i = 0; i < ctx.DiceBuild.Count; i++)
            {
                if (ctx.DiceBuild[i] == Type) found++;
            }
            return Comparison == DiceCountComparison.AtLeast ? found >= Count : found == Count;
        }
    }
}
