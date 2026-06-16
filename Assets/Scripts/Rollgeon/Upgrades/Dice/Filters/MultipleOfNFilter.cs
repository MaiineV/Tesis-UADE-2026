using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Filters
{
    /// <summary>
    /// Restringe el pool de caras a múltiplos de <see cref="N"/>. Sobre un D12
    /// con N=3 deja <c>{3, 6, 9, 12}</c>; sobre un D6 con N=3 deja <c>{3, 6}</c>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class MultipleOfNFilter : IFaceFilter
    {
        [MinValue(2)]
        [Tooltip("Solo se permiten caras divisibles por este valor.")]
        public int N = 3;

        public IReadOnlyCollection<int> GetAllowedFaces(DiceType type, IReadOnlyCollection<int> currentlyAllowed)
        {
            var result = new HashSet<int>();
            if (currentlyAllowed == null) return result;
            foreach (var face in currentlyAllowed)
            {
                if (face % N == 0) result.Add(face);
            }
            return result;
        }
    }
}