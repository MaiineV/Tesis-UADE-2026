using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Filters
{
    /// <summary>
    /// Restringe las caras válidas a un rango cerrado <c>[Min, Max]</c>.
    /// Ej. Min=1 / Max=6 sobre un D20 → solo se permiten caras 1..6.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class FaceRangeFilter : IFaceFilter
    {
        [HorizontalGroup("range"), MinValue(1), LabelText("Min")]
        [Tooltip("Cara mínima inclusiva permitida.")]
        public int Min = 1;

        [HorizontalGroup("range"), MinValue(1), LabelText("Max")]
        [Tooltip("Cara máxima inclusiva permitida. Si excede MaxFace del dado, se trunca al rango real.")]
        public int Max = 6;

        public IReadOnlyCollection<int> GetAllowedFaces(DiceType type, IReadOnlyCollection<int> currentlyAllowed)
        {
            var result = new HashSet<int>();
            if (currentlyAllowed == null) return result;
            foreach (var face in currentlyAllowed)
            {
                if (face >= Min && face <= Max) result.Add(face);
            }
            return result;
        }
    }
}
