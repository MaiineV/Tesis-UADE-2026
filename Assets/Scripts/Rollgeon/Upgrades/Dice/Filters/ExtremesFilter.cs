using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Filters
{
    /// <summary>
    /// Inverso de <see cref="CenterQuartersFilter"/>: mantiene solo el cuarto
    /// inferior y superior de las caras (los extremos). Sobre un D8 deja
    /// <c>{1, 2, 7, 8}</c>; sobre un D12 deja <c>{1, 2, 3, 10, 11, 12}</c>.
    /// </summary>
    /// <remarks>
    /// Los límites se calculan igual que <see cref="CenterQuartersFilter"/>:
    /// <c>lowerBound = floor(maxFace / 4)</c>,
    /// <c>upperBound = maxFace - floor(maxFace / 4)</c>. Se mantienen caras
    /// donde <c>face &lt;= lowerBound OR face &gt; upperBound</c>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ExtremesFilter : IFaceFilter
    {
        public IReadOnlyCollection<int> GetAllowedFaces(DiceType type, IReadOnlyCollection<int> currentlyAllowed)
        {
            var result = new HashSet<int>();
            if (currentlyAllowed == null) return result;

            int maxFace = type.MaxFace();
            int quarter = maxFace / 4;
            int lowerBound = quarter;
            int upperBound = maxFace - quarter;

            foreach (var face in currentlyAllowed)
            {
                if (face <= lowerBound || face > upperBound) result.Add(face);
            }
            return result;
        }
    }
}