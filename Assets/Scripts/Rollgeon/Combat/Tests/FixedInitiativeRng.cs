using System.Collections.Generic;
using Rollgeon.Combat.Random;

namespace Rollgeon.Combat.Tests
{
    /// <summary>
    /// RNG de tests que devuelve una secuencia predefinida de valores. Permite
    /// escribir asserts exactos sobre el orden producido por
    /// <c>DefaultInitiativeProvider</c>.
    /// </summary>
    /// <remarks>
    /// Respeta el contrato <c>[min, max)</c> de <see cref="IInitiativeRng.Next"/>:
    /// si el siguiente valor cae fuera del rango pedido, se clamp-ea al rango
    /// para mantener al test robusto ante ajustes de <c>SpeedDieMin/Max</c>.
    /// </remarks>
    internal sealed class FixedInitiativeRng : IInitiativeRng
    {
        private readonly Queue<int> _values;
        private readonly int _fallback;

        public FixedInitiativeRng(int fallback, params int[] values)
        {
            _fallback = fallback;
            _values = new Queue<int>(values ?? System.Array.Empty<int>());
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            int v = _values.Count > 0 ? _values.Dequeue() : _fallback;
            if (v < minInclusive)
            {
                v = minInclusive;
            }
            if (v >= maxExclusive)
            {
                v = maxExclusive - 1;
            }
            return v;
        }
    }
}
