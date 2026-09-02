using System;
using Patterns;
using Rollgeon.Combat.Rolls;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Compara los rolls disponibles del owner (<see cref="IRollPoolService.GetCurrent"/>)
    /// contra <see cref="Value"/>. "Última Carta" (GDD): <c>Equal 0</c> — bonus solo si el
    /// jugador ataca sin rerolls disponibles. Como el pool se cobra al tirar, leído en el
    /// dispatch de ComboPlayed refleja exactamente lo que le quedó al confirmar la acción.
    /// </summary>
    /// <remarks>
    /// Sin servicio, sin owner o fuera de combate (el pool devuelve 0 pero no hay
    /// combate) → <c>false</c> (veta, criterio <c>PcTilesMovedThisTurn</c>): una
    /// comparación que no se puede afirmar no habilita bonos.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcRollPoolCompare : BasePreCondition
    {
        [Tooltip("Operador contra los rolls disponibles del owner.")]
        public IntComparison Comparison = IntComparison.Equal;

        [MinValue(0)]
        public int Value;

        public PcRollPoolCompare() { _isConstantValue = false; }

        public override string ConditionName => $"RollPool {Comparison} {Value}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || context.OwnerGuid == Guid.Empty) return false;
            if (!ServiceLocator.TryGetService<IRollPoolService>(out var rolls) || rolls == null)
                return false;
            if (!rolls.IsCombatActive) return false;

            int current = rolls.GetCurrent(context.OwnerGuid);
            switch (Comparison)
            {
                case IntComparison.Equal: return current == Value;
                case IntComparison.NotEqual: return current != Value;
                case IntComparison.Less: return current < Value;
                case IntComparison.LessOrEqual: return current <= Value;
                case IntComparison.Greater: return current > Value;
                case IntComparison.GreaterOrEqual: return current >= Value;
                default: return false;
            }
        }
    }
}
