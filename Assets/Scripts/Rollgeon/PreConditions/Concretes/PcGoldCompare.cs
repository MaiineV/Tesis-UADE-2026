using System;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.Effects.Readers;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Compara el oro actual del jugador (<c>IEconomyService.CurrentGold</c>) contra
    /// <see cref="Value"/>. Gating de los encantamientos gold-gated ("si no alcanza,
    /// bloqueá el combo" / "pagá y ganá bonus").
    /// </summary>
    /// <remarks>
    /// Sin <c>IEconomyService</c> devuelve <c>false</c> — NO permisivo, a diferencia de
    /// <c>PcOwnerStatCompare</c>: los triggers legacy hacían early-return sin aplicar
    /// nada cuando faltaba la economía, y una comparación de oro que no se puede
    /// afirmar no debe habilitar gastos ni bloqueos.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcGoldCompare : BasePreCondition
    {
        public IntComparison Comparison = IntComparison.GreaterOrEqual;

        [Tooltip("Valor contra el que se compara el oro actual. Admite readers.")]
        [OdinSerialize, SerializeReference]
        public EffectIntReader Value = new ReadConstantInt();

        public override string ConditionName => $"Gold {Symbol(Comparison)} {Value?.GetType().Name}";

        public override bool Evaluate(PreConditionContext context)
        {
            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null)
                return false;

            int value = Value?.Read(context?.Effect) ?? 0;
            return Apply(economy.CurrentGold, Comparison, value);
        }

        private static bool Apply(int a, IntComparison op, int b) => op switch
        {
            IntComparison.Equal          => a == b,
            IntComparison.NotEqual       => a != b,
            IntComparison.Less           => a <  b,
            IntComparison.LessOrEqual    => a <= b,
            IntComparison.Greater        => a >  b,
            IntComparison.GreaterOrEqual => a >= b,
            _                            => false,
        };

        private static string Symbol(IntComparison op) => op switch
        {
            IntComparison.Equal          => "==",
            IntComparison.NotEqual       => "!=",
            IntComparison.Less           => "<",
            IntComparison.LessOrEqual    => "<=",
            IntComparison.Greater        => ">",
            IntComparison.GreaterOrEqual => ">=",
            _                            => "?",
        };
    }
}
