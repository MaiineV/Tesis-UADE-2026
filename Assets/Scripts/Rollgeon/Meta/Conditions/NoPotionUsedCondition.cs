using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Por ejecución (#164): completar la run sin usar ninguna poción. Condición
    /// de consistencia total: solo evaluable al cierre de run y se invalida en el
    /// momento del primer uso.
    /// </summary>
    [Serializable]
    public sealed class NoPotionUsedCondition : IUnlockCondition
    {
        [ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("ItemIds que cuentan como poción. Lista vacía = cualquier item activo usado rompe la condición.")]
        public List<string> PotionItemIds = new List<string>();

        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx == null || !ctx.RunEnded) return false;
            return CountUses(ctx) == 0;
        }

        public bool IsInvalidated(UnlockEvaluationContext ctx)
        {
            return ctx != null && CountUses(ctx) > 0;
        }

        private int CountUses(UnlockEvaluationContext ctx)
        {
            if (ctx.UsedActiveItemIds == null) return 0;
            if (PotionItemIds == null || PotionItemIds.Count == 0) return ctx.UsedActiveItemIds.Count;

            int uses = 0;
            foreach (var used in ctx.UsedActiveItemIds)
            {
                if (PotionItemIds.Contains(used)) uses++;
            }
            return uses;
        }
    }
}
