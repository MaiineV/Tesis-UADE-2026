using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;

namespace Rollgeon.Upgrades.Dice.Filters
{
    /// <summary>
    /// Bloquea el cuarto inferior y superior de las caras, manteniendo solo el
    /// 50% central. Sobre un D8 deja <c>{3, 4, 5, 6}</c>; sobre un D12 deja
    /// <c>{4, 5, 6, 7, 8, 9}</c>.
    /// </summary>
    /// <remarks>
    /// Los límites se calculan como <c>lowerBound = floor(maxFace / 4)</c> y
    /// <c>upperBound = maxFace - floor(maxFace / 4)</c>. Se mantienen caras
    /// donde <c>face &gt; lowerBound AND face &lt;= upperBound</c>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class CenterQuartersFilter : IFaceFilter
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
                if (face > lowerBound && face <= upperBound) result.Add(face);
            }
            return result;
        }
    }
}