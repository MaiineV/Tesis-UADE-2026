using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Balance
{
    /// <summary>
    /// Sub-seccion de <see cref="RulesetSO"/> con las perillas del Pool de Rolls
    /// (GDD "Turn System", rediseño que reemplaza al sistema de Energía).
    /// Cada tirada de dado cuesta 1 roll; el pool acumula entre turnos dentro
    /// del combate y no existe en exploración.
    /// </summary>
    [Serializable]
    public sealed class RollPoolConfig
    {
        [Tooltip("Rolls con los que arranca el pool al entrar a un combate. GDD: 5.")]
        [MinValue(0)]
        public int RollsAtCombatStart = 5;

        [Tooltip("Rolls fijos otorgados al finalizar cada turno del jugador. GDD: 5.")]
        [MinValue(0)]
        public int RollsPerTurn = 5;

        [Tooltip("Tope máximo de acumulación del pool. GDD: 15 (a probar en balance).")]
        [Range(0, 50)]
        public int RollPoolCap = 15;

        /// <summary>Clamp defensivo si el SO tiene configuracion invalida.</summary>
        public void Validate()
        {
            if (RollPoolCap < 0) RollPoolCap = 0;
            if (RollsPerTurn < 0) RollsPerTurn = 0;
            if (RollsAtCombatStart < 0) RollsAtCombatStart = 0;
            if (RollsAtCombatStart > RollPoolCap) RollsAtCombatStart = RollPoolCap;
        }
    }
}
