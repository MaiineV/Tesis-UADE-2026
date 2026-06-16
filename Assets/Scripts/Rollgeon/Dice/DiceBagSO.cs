using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Dice
{
    /// <summary>
    /// Bolsa de 5 dados que llevan el héroe. TECHNICAL.md §6.2.
    /// </summary>
    /// <remarks>
    /// Reglas del GD: exactamente 5 dados; cada tipo respeta
    /// <see cref="DiceTypeExt.MaxPerBag"/>. <see cref="OnValidate"/> emite warnings
    /// no-bloqueantes para que el editor pueda autorear bolsas WIP sin romper.
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Dice Bag", fileName = "DiceBag")]
    public class DiceBagSO : ScriptableObject
    {
        /// <summary>Tamaño canónico de una bolsa per §6.2.</summary>
        public const int RequiredSize = 5;

        [ListDrawerSettings(ShowFoldout = false)]
        [Tooltip("Cinco dados — orden no importa para el roller, pero se respeta en el slot index.")]
        public List<DiceType> Dice = new();

        /// <summary>
        /// Devuelve <c>true</c> si la bolsa cumple las reglas; en caso contrario,
        /// <paramref name="error"/> describe el primer fallo encontrado.
        /// </summary>
        public bool Validate(out string error)
        {
            if (Dice == null || Dice.Count != RequiredSize)
            {
                error = $"DiceBag debe tener {RequiredSize} dados (tiene {Dice?.Count ?? 0}).";
                return false;
            }

            foreach (var group in Dice.GroupBy(d => d))
            {
                int max = group.Key.MaxPerBag();
                if (group.Count() > max)
                {
                    error = $"{group.Key} excede máximo ({group.Count()}/{max}).";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Devuelve un nuevo <see cref="DiceBagSO"/> independiente con los mismos
        /// dados. El clon vive en memoria — no se persiste como asset.
        /// </summary>
        public DiceBagSO Clone()
        {
            var copy = CreateInstance<DiceBagSO>();
            copy.name = name + " (Clone)";
            copy.Dice = Dice != null ? new List<DiceType>(Dice) : new List<DiceType>();
            return copy;
        }

        private void OnValidate()
        {
            if (Dice == null) return;
            if (Dice.Count != RequiredSize)
            {
                Debug.LogWarning($"{name}: DiceBag debe tener {RequiredSize} dados (tiene {Dice.Count}).", this);
            }

            foreach (var group in Dice.GroupBy(d => d))
            {
                int max = group.Key.MaxPerBag();
                if (group.Count() > max)
                {
                    Debug.LogWarning($"{name}: {group.Key} excede máximo ({group.Count()}/{max}).", this);
                }
            }
        }
    }
}
