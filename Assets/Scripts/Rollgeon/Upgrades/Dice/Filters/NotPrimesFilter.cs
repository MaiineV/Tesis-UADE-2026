using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Filters
{
    /// <summary>
    /// Inverso de <see cref="OnlyPrimesFilter"/>: mantiene solo caras que
    /// <b>no</b> son números primos (incluye el 1). Sobre un D8 deja
    /// <c>{1, 4, 6, 8}</c>; sobre un D12 deja <c>{1, 4, 6, 8, 9, 10, 12}</c>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class NotPrimesFilter : IFaceFilter
    {
        public IReadOnlyCollection<int> GetAllowedFaces(DiceType type, IReadOnlyCollection<int> currentlyAllowed)
        {
            var result = new HashSet<int>();
            if (currentlyAllowed == null) return result;
            foreach (var face in currentlyAllowed)
            {
                if (!IsPrime(face)) result.Add(face);
            }
            return result;
        }

        // Trial-division, suficiente para D20 (max face = 20).
        private static bool IsPrime(int n)
        {
            if (n < 2) return false;
            if (n == 2) return true;
            if (n % 2 == 0) return false;
            for (int i = 3; i * i <= n; i += 2)
            {
                if (n % i == 0) return false;
            }
            return true;
        }
    }
}