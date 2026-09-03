using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// Perilla de <see cref="ItemSO"/> para la categoría "bono permanente al combo menos
    /// usado" (GDD: Rezagado). El combo se elige UNA vez al adquirir el item — lo hace
    /// <c>LeastUsedComboService</c> mirando los contadores de la run — y desde ahí ese combo
    /// suma <see cref="MultiplierBonus"/> al canal aditivo de M en cada ataque.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class LeastUsedComboBonusDef
    {
        [Tooltip("Activa la asignación al adquirir el item.")]
        public bool Enabled;

        [ShowIf(nameof(Enabled))]
        [MinValue(0f)]
        [Tooltip("Cuánto suma al canal aditivo de M cuando se juega el combo asignado. " +
                 "M = (1 + Σadd) × Πmult: 0.5 = +50%. Rezagado: 0.5.")]
        public float MultiplierBonus = 0.5f;
    }
}
