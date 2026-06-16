using System;
using Rollgeon.Upgrades.Character;
using UnityEngine;

namespace Rollgeon.Upgrades
{
    /// <summary>
    /// Boost permanente (Run-lifetime) a un stat del jugador. Es el "canal único" de stat grants
    /// que comparten los rewards de personaje y las pasivas/ítems de tienda: cada
    /// <see cref="UpgradeSO"/> expone una lista de <see cref="StatGrant"/> y
    /// <see cref="PlayerStatGrants"/> los aplica al adquirir el upgrade.
    /// </summary>
    [Serializable]
    public sealed class StatGrant
    {
        [Tooltip("Stat del jugador a aumentar. Attack = daño base del PJ.")]
        public CharacterRewardTargetStat Stat = CharacterRewardTargetStat.Attack;

        [Tooltip("Monto sumado al stat (operación Add, dura toda la run). 0 = no-op.")]
        public int Amount;
    }
}
