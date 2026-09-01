using System;
using Patterns;
using Rollgeon.Combat.TurnState;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Compara las casillas recorridas por el jugador en el turno actual
    /// (<see cref="IPlayerTurnStateService.TilesMovedThisTurn"/>) contra
    /// <see cref="Value"/>. "Piedra de Guardia" (GDD): <c>Equal 0</c> — bonus solo si
    /// el jugador NO se movió antes de atacar.
    /// </summary>
    /// <remarks>
    /// Sin servicio registrado → <c>false</c> (veta, criterio <c>PcGoldCompare</c>):
    /// una comparación que no se puede afirmar no habilita bonos.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcTilesMovedThisTurn : BasePreCondition
    {
        [Tooltip("Operador contra las casillas recorridas este turno.")]
        public IntComparison Comparison = IntComparison.Equal;

        [MinValue(0)]
        public int Value;

        public PcTilesMovedThisTurn() { _isConstantValue = false; }

        public override string ConditionName => $"TilesMovedThisTurn {Comparison} {Value}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (!ServiceLocator.TryGetService<IPlayerTurnStateService>(out var state) || state == null)
                return false;

            int tiles = state.TilesMovedThisTurn;
            switch (Comparison)
            {
                case IntComparison.Equal: return tiles == Value;
                case IntComparison.NotEqual: return tiles != Value;
                case IntComparison.Less: return tiles < Value;
                case IntComparison.LessOrEqual: return tiles <= Value;
                case IntComparison.Greater: return tiles > Value;
                case IntComparison.GreaterOrEqual: return tiles >= Value;
                default: return false;
            }
        }
    }
}
