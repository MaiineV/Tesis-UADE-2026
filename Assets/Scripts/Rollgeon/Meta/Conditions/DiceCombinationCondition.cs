using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Por build (#164): la run se jugó con una combinación específica de dados —
    /// igualdad de multiset entre <see cref="Combination"/> y la build (el orden
    /// no importa, las repeticiones sí).
    /// </summary>
    [Serializable]
    public sealed class DiceCombinationCondition : IUnlockCondition
    {
        [ListDrawerSettings(ShowFoldout = false)]
        [Tooltip("Combinación exacta requerida, ej. [D4, D4, D6, D6, D8]. El orden no importa.")]
        public List<DiceType> Combination = new List<DiceType>();

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx?.DiceBuild == null || Combination == null) return false;
            if (ctx.DiceBuild.Count != Combination.Count || Combination.Count == 0) return false;

            var counts = new Dictionary<DiceType, int>();
            foreach (var die in Combination)
            {
                counts.TryGetValue(die, out var c);
                counts[die] = c + 1;
            }
            foreach (var die in ctx.DiceBuild)
            {
                if (!counts.TryGetValue(die, out var c) || c == 0) return false;
                counts[die] = c - 1;
            }
            return true;
        }
    }
}
