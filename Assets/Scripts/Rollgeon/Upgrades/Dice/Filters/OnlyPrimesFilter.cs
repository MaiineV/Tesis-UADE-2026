using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Filters
{
    /// <summary>
    /// Restringe el pool de caras a números primos. Sobre un D20 deja
    /// <c>{2, 3, 5, 7, 11, 13, 17, 19}</c>. Sobre un D3 deja <c>{2, 3}</c>.
    /// </summary>
    /// <remarks>
    /// <b>Cuidado balance.</b> En dados chicos (D3/D4) reduce el pool a 1-2
    /// caras — la validación de la sala (Phase 4) puede rechazarlo según
    /// <c>EnchantmentConfigSO.MinFacesAfterApply</c>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class OnlyPrimesFilter : IFaceFilter
    {
        public IReadOnlyCollection<int> GetAllowedFaces(DiceType type, IReadOnlyCollection<int> currentlyAllowed)
        {
            var result = new HashSet<int>();
            if (currentlyAllowed == null) return result;
            foreach (var face in currentlyAllowed)
            {
                if (IsPrime(face)) result.Add(face);
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
