using System;
using Patterns;
using Rollgeon.Combat.TurnState;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Multiplicador que arranca en <see cref="Start"/> al empezar el combate y baja
    /// <see cref="DecayPerAttack"/> por cada ataque YA ejecutado
    /// (<see cref="IPlayerTurnStateService.AttacksPlayedThisCombat"/>), con piso en
    /// <see cref="Min"/>. Para "Eco Menguante" (GDD: x5.0 → -0.1 por ataque → mínimo x1.0).
    /// Pensado como <c>MultiplierReader</c> de <c>EffMultiplyComboDamage</c>.
    /// </summary>
    /// <remarks>
    /// El contador del servicio excluye el ataque en curso (commit diferido), así que el
    /// primer golpe del combate lee exactamente <see cref="Start"/>. Sin servicio →
    /// <see cref="Start"/>. <see cref="Read"/> floorea para consumidores int.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadAttackDecayMultiplier : EffectIntReader
    {
        [MinValue(0f)]
        [Tooltip("Multiplicador al primer ataque del combate. Eco Menguante: 5.")]
        public float Start = 5f;

        [MinValue(0f)]
        [Tooltip("Cuánto baja por cada ataque ejecutado. Eco Menguante: 0.1.")]
        public float DecayPerAttack = 0.1f;

        [MinValue(0f)]
        [Tooltip("Piso del multiplicador. Eco Menguante: 1.")]
        public float Min = 1f;

        public override int Read(EffectContext context)
            => Mathf.FloorToInt(ReadFloat(context));

        public override float ReadFloat(EffectContext context)
        {
            int attacks = 0;
            if (ServiceLocator.TryGetService<IPlayerTurnStateService>(out var state) && state != null)
                attacks = state.AttacksPlayedThisCombat;
            return Mathf.Max(Min, Start - attacks * DecayPerAttack);
        }
    }
}
