using System;
using System.Collections.Generic;
using Rollgeon.Dice;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Filters
{
    /// <summary>
    /// Restringe el pool de caras a un set específico autoreado por el diseñador.
    /// Útil para encantamientos puntuales tipo "este dado siempre saca {3, 7, 13}".
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class SpecificValuesFilter : IFaceFilter
    {
        [Tooltip("Caras permitidas. Las que NO estén acá se eliminan del pool del dado.")]
        [ListDrawerSettings(ShowFoldout = false, DefaultExpandedState = true)]
        public List<int> AllowedFaces = new List<int>();

        public IReadOnlyCollection<int> GetAllowedFaces(DiceType type, IReadOnlyCollection<int> currentlyAllowed)
        {
            var result = new HashSet<int>();
            if (currentlyAllowed == null) return result;
            if (AllowedFaces == null || AllowedFaces.Count == 0) return result;

            var allow = new HashSet<int>(AllowedFaces);
            foreach (var face in currentlyAllowed)
            {
                if (allow.Contains(face)) result.Add(face);
            }
            return result;
        }
    }
}
