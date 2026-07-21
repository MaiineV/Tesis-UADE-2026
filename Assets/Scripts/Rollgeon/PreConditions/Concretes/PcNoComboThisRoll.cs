using System;
using Sirenix.OdinInspector;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Pasa cuando el roll del evento NO matcheó ningún combo (lee
    /// <c>PreConditionContext.Effect.ComboResult</c>). Típico con el hook RollResolved:
    /// "oro de consuelo cuando no armaste nada" (Ench_GoldOnRoll legacy).
    /// </summary>
    /// <remarks>
    /// Mismo predicado que <c>ModifyResourceTrigger.PassesCondition(NoComboMatched)</c>:
    /// sin contexto o sin ComboResult se considera "no hubo combo" → pasa.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcNoComboThisRoll : BasePreCondition
    {
        public override string ConditionName => "Sin combo en este roll";

        public override bool Evaluate(PreConditionContext context)
        {
            var combo = context?.Effect?.ComboResult;
            return !(combo.HasValue && combo.Value.IsMatch);
        }
    }
}
