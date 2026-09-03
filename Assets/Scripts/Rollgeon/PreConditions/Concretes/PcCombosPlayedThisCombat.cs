using System;
using Patterns;
using Rollgeon.Combat.TurnState;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Compara la cantidad de combos de combate jugados en el combate actual
    /// (<see cref="IPlayerTurnStateService.CombosPlayedThisCombat"/>, que INCLUYE el combo
    /// en curso cuando se evalúa desde un hook de ComboPlayed). Para "Piedra Angular"
    /// (GDD: el primer combo del combate) = <c>Equal 1</c>. Sin servicio → false.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcCombosPlayedThisCombat : BasePreCondition
    {
        [Tooltip("Operador contra los combos jugados este combate (incluye el actual).")]
        public IntComparison Comparison = IntComparison.Equal;

        [MinValue(0)]
        [Tooltip("Piedra Angular: 1 (el combo en curso es el primero del combate).")]
        public int Value = 1;

        public PcCombosPlayedThisCombat() { _isConstantValue = false; }

        public override string ConditionName => $"CombosPlayedThisCombat {Comparison} {Value}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (!ServiceLocator.TryGetService<IPlayerTurnStateService>(out var state) || state == null)
                return false;

            int combos = state.CombosPlayedThisCombat;
            switch (Comparison)
            {
                case IntComparison.Equal: return combos == Value;
                case IntComparison.NotEqual: return combos != Value;
                case IntComparison.Less: return combos < Value;
                case IntComparison.LessOrEqual: return combos <= Value;
                case IntComparison.Greater: return combos > Value;
                case IntComparison.GreaterOrEqual: return combos >= Value;
                default: return false;
            }
        }
    }
}
