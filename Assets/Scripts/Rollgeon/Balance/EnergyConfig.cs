using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Balance
{
    /// <summary>
    /// Sub-seccion de <see cref="RulesetSO"/> con las perillas de energia del FP.
    /// GDD #100: EnergyMax=4, EnergyAtRunStart=2, EnergyRegenBase=2.
    /// </summary>
    /// <remarks>
    /// Archivo independiente para permitir que Balance#0101 lo consuma sin
    /// tocar <see cref="RulesetSO"/>. Los 3 campos cubren el DoD del bullet
    /// "Energia inicial / Energia maxima / Regen (base)" del issue #101.
    /// </remarks>
    [Serializable]
    public sealed class EnergyConfig
    {
        [Tooltip("Energia maxima acumulable. GDD: 4.")]
        [Range(0, 20)]
        public int EnergyMax = 4;

        [Tooltip("Energia al iniciar la run. GDD: 2 de 4.")]
        [MinValue(0)]
        public int EnergyAtRunStart = 2;

        [Tooltip("Energia que se regenera como base al finalizar el turno. GDD: 2. " +
                 "La energia no utilizada del turno se suma a esto, clampeando a EnergyMax.")]
        [MinValue(0)]
        public int EnergyRegenBase = 2;

        /// <summary>Clamp defensivo si el SO tiene configuracion invalida.</summary>
        public void Validate()
        {
            if (EnergyMax < 0) EnergyMax = 0;
            if (EnergyRegenBase < 0) EnergyRegenBase = 0;
            if (EnergyAtRunStart < 0) EnergyAtRunStart = 0;
            if (EnergyAtRunStart > EnergyMax) EnergyAtRunStart = EnergyMax;
        }
    }
}
