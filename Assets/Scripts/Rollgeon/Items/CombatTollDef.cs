using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// Perilla de <see cref="ItemSO"/> para "pagar para saltear un combate" (GDD: Peaje).
    /// La oferta la hace <c>CombatTollService</c> al entrar a una sala Combat estándar;
    /// acá vive solo el costo y su escalado por piso.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class CombatTollDef
    {
        [Tooltip("Activa la oferta de peaje al entrar a salas de combate estándar.")]
        public bool Enabled;

        [ShowIf(nameof(Enabled))]
        [MinValue(0)]
        [Tooltip("Costo base en oro. Peaje: 15.")]
        public int BaseCost = 15;

        [ShowIf(nameof(Enabled))]
        [MinValue(0)]
        [Tooltip("Cuánto suma por piso (piso 1 = primer piso de la run). Peaje: 10 → piso 1 = 25.")]
        public int CostPerFloor = 10;

        /// <summary><paramref name="floorIndex"/> es zero-based (IRunContextService.FloorIndex).</summary>
        public int CostFor(int floorIndex)
            => BaseCost + CostPerFloor * (Mathf.Max(0, floorIndex) + 1);
    }
}
