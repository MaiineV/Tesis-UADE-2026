using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Filters
{
    /// <summary>Mitad del dado seleccionada por <see cref="RelativeHalfFilter"/>.</summary>
    public enum HalfSide
    {
        Upper,
        Lower,
    }

    /// <summary>
    /// Mantiene la mitad superior o inferior de las caras del dado relativo a
    /// <c>MaxFace</c>. Sobre un D6: <c>Upper</c> → <c>{4, 5, 6}</c>,
    /// <c>Lower</c> → <c>{1, 2, 3}</c>. Sobre un D12: <c>Upper</c> →
    /// <c>{7..12}</c>, <c>Lower</c> → <c>{1..6}</c>.
    /// </summary>
    /// <remarks>
    /// El corte se calcula como <c>floor(maxFace / 2)</c>. Lower incluye
    /// caras &lt;= corte; Upper incluye caras &gt; corte.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class RelativeHalfFilter : IFaceFilter
    {
        [Tooltip("Upper mantiene caras por encima de la mitad; Lower las de abajo.")]
        public HalfSide Side = HalfSide.Upper;

        public IReadOnlyCollection<int> GetAllowedFaces(DiceType type, IReadOnlyCollection<int> currentlyAllowed)
        {
            var result = new HashSet<int>();
            if (currentlyAllowed == null) return result;

            int midpoint = type.MaxFace() / 2;

            foreach (var face in currentlyAllowed)
            {
                bool keep = Side == HalfSide.Upper
                    ? face > midpoint
                    : face <= midpoint;
                if (keep) result.Add(face);
            }
            return result;
        }
    }
}