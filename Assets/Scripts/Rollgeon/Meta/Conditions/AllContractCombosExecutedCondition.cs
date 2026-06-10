using System;

namespace Rollgeon.Meta.Conditions
{
    /// <summary>
    /// Por combos (#164): todos los combos del Contrato de la clase jugada se
    /// ejecutaron al menos una vez en la run. Monotónica — puede completarse
    /// mid-run apenas cae el último combo pendiente.
    /// </summary>
    [Serializable]
    public sealed class AllContractCombosExecutedCondition : IUnlockCondition
    {
        public bool Evaluate(UnlockEvaluationContext ctx)
        {
            if (ctx?.ContractComboIds == null || ctx.ContractComboIds.Count == 0) return false;
            foreach (var comboId in ctx.ContractComboIds)
            {
                if (ctx.GetComboCount(comboId) < 1) return false;
            }
            return true;
        }
    }
}
