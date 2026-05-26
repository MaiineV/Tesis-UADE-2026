using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Character
{
    /// <summary>
    /// Entry del <see cref="CharacterRewardPoolSO"/>.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class WeightedCharacterReward
    {
        [Required]
        public CharacterRewardSO Reward;

        [MinValue(0f)]
        [Tooltip("Peso relativo. 0 = no se rolea.")]
        public float Weight = 1f;

        [MinValue(0)]
        [Tooltip("Floor mínimo en el que la entry es elegible.")]
        public int MinFloorDepth = 0;
    }
}
