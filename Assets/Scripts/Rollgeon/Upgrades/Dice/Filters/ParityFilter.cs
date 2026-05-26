using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Filters
{
    /// <summary>Paridad permitida por <see cref="ParityFilter"/>.</summary>
    public enum Parity
    {
        Even,
        Odd,
    }

    /// <summary>
    /// Restringe el pool de caras a solo pares o solo impares. Sobre un D20:
    /// <c>Even</c> → <c>{2,4,6,8,10,12,14,16,18,20}</c>; <c>Odd</c> → impares.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ParityFilter : IFaceFilter
    {
        [Tooltip("Si Even, solo se permiten caras pares. Si Odd, solo impares.")]
        public Parity Allowed = Parity.Even;

        public IReadOnlyCollection<int> GetAllowedFaces(DiceType type, IReadOnlyCollection<int> currentlyAllowed)
        {
            var result = new HashSet<int>();
            if (currentlyAllowed == null) return result;
            bool wantEven = Allowed == Parity.Even;
            foreach (var face in currentlyAllowed)
            {
                bool isEven = (face % 2) == 0;
                if (isEven == wantEven) result.Add(face);
            }
            return result;
        }
    }
}
