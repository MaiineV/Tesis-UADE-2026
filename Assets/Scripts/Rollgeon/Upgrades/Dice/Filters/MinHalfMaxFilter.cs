using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Filters
{
    /// <summary>
    /// Garantiza que el resultado mínimo del dado sea la mitad de <c>MaxFace</c>
    /// redondeada hacia arriba. Sobre un D6 deja <c>{3, 4, 5, 6}</c>; sobre un D4
    /// deja <c>{2, 3, 4}</c>.
    /// </summary>
    /// <remarks>
    /// El piso se calcula como <c>ceil(maxFace / 2) = (maxFace + 1) / 2</c> (división
    /// entera). Se mantienen caras donde <c>face &gt;= floor</c>. BUG-030b: reemplaza
    /// el bonus de daño compensatorio (<see cref="Readers.ReadCarrierRollDelta"/> con
    /// <c>ClampMinToHalfMax</c>) por la restricción real de caras — Afilado ahora
    /// impide directamente que salgan caras bajas en vez de compensarlas post-roll.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class MinHalfMaxFilter : IFaceFilter
    {
        public IReadOnlyCollection<int> GetAllowedFaces(DiceType type, IReadOnlyCollection<int> currentlyAllowed)
        {
            var result = new HashSet<int>();
            if (currentlyAllowed == null) return result;

            int maxFace = type.MaxFace();
            int floor = (maxFace + 1) / 2;

            foreach (var face in currentlyAllowed)
            {
                if (face >= floor) result.Add(face);
            }
            return result;
        }
    }
}
