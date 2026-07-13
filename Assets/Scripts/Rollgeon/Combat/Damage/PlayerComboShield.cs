using System.Collections.Generic;
using Rollgeon.Dice;
using UnityEngine;

namespace Rollgeon.Combat.Damage
{
    /// <summary>
    /// Fórmula v2 del escudo de combo del jugador (Spec de Escudo, Santi, 2026-07-08):
    /// <code>
    /// escudo = min( escudo_combo_base × multi_dmg_combo , ShieldCap )
    /// </code>
    /// Tabla y fórmula completamente separadas de las de ataque: acá NO entra
    /// <c>Attack</c>, <c>bono_combo</c> ni multiplicadores de scratch/habilidad — reusar
    /// la tabla de daño fue la causa raíz del bug de escudo trivial.
    /// <c>escudo_combo_base</c> sale de <c>ContractSheet.GetShieldBase</c> (tabla por
    /// clase); <c>multi_dmg_combo</c> es la misma fórmula que en ataque
    /// (<see cref="PlayerComboDamage.ComputeMultiDmgCombo"/>) sobre los dados de la
    /// tirada que generó el escudo.
    /// </summary>
    public static class PlayerComboShield
    {
        /// <summary>
        /// ESCUDO_CAP — tope duro sobre el resultado final, post-multiplicación. Ni una
        /// Generala perfecta debe dejar al jugador inmune varios turnos.
        /// </summary>
        public const int ShieldCap = 8;

        public static int Resolve(int shieldBase, IReadOnlyList<DiceType> contributingDice)
        {
            if (shieldBase <= 0) return 0;
            float multi = PlayerComboDamage.ComputeMultiDmgCombo(contributingDice);
            int shield = Mathf.RoundToInt(shieldBase * multi);
            return Mathf.Min(shield, ShieldCap);
        }
    }
}
