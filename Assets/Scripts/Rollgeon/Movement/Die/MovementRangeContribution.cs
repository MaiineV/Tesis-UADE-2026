using UnityEngine;

namespace Rollgeon.Movement.Die
{
    /// <summary>
    /// Aporte de un item al rango del dado de Movimiento (§6.6): el asset que lo explica
    /// (hoy siempre un <c>ItemSO</c>) y cuánto suma o resta. Es solo para que la UI lo
    /// muestre — el rango real sigue saliendo de <c>MoveRange.ModifiedValue</c>.
    /// </summary>
    public readonly struct MovementRangeContribution
    {
        public readonly Object SourceAsset;
        public readonly int Delta;

        public MovementRangeContribution(Object sourceAsset, int delta)
        {
            SourceAsset = sourceAsset;
            Delta = delta;
        }
    }
}
