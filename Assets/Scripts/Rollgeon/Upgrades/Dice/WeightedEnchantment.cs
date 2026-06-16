using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Entry del <see cref="EnchantmentPoolSO"/>: un encantamiento + su peso en
    /// el roll aleatorio. Mismo patrón que <c>WeightedShopItem</c>.
    /// </summary>
    /// <remarks>
    /// El peso vive acá (no en el <see cref="EnchantmentSO"/>) porque un mismo
    /// asset puede entrar a múltiples pools con pesos distintos (ej. común en
    /// pool de floor 1, raro en pool de floor 3).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public class WeightedEnchantment
    {
        [Required]
        [Tooltip("El encantamiento elegible. Required.")]
        public EnchantmentSO Enchantment;

        [MinValue(0f)]
        [Tooltip("Peso relativo en el pool. 0 = nunca se rolea (se puede usar para deshabilitar " +
                 "temporalmente sin borrar la entry).")]
        public float Weight = 1f;

        [MinValue(0)]
        [Tooltip("Floor mínimo desde el que esta entry es elegible. 0 = disponible desde el primer floor.")]
        public int MinFloorDepth = 0;
    }
}
