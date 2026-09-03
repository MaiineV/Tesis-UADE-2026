using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Items
{
    /// <summary>
    /// Bloque de autoría de <see cref="ItemSO"/> para items cuyo multiplicador de daño
    /// arranca alto al adquirirse y DECAE con cada combo jugado en la run, hasta romperse
    /// (Eco Menguante — decisión GD 2026-09-03: x5.0, −0.2 por combo, se rompe al llegar a
    /// x1.0). Lo aplica <c>DecayingMultiplierService</c>: multiplica solo ATAQUES, descuenta
    /// con cualquier combo de combate (ataque/defensa/cura), persiste el contador en el save.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class DecayingMultiplierDef
    {
        [Tooltip("Activa el multiplicador decreciente mientras el item esté en el inventario.")]
        public bool Enabled;

        [ShowIf(nameof(Enabled))]
        [MinValue(0f)]
        [Tooltip("Multiplicador del primer ataque tras adquirir el item. Eco Menguante: 5.")]
        public float Start = 5f;

        [ShowIf(nameof(Enabled))]
        [MinValue(0f)]
        [Tooltip("Cuánto baja por cada combo de combate jugado (ataque, defensa o cura). Eco Menguante: 0.2.")]
        public float DecayPerCombo = 0.2f;

        [ShowIf(nameof(Enabled))]
        [MinValue(0f)]
        [Tooltip("Piso del multiplicador. Eco Menguante: 1.")]
        public float Min = 1f;

        [ShowIf(nameof(Enabled))]
        [Tooltip("Si está activo, al llegar al piso el item se remueve del inventario (con toast). Eco Menguante: sí.")]
        public bool BreakAtMin = true;

        /// <summary>Multiplicador tras <paramref name="combosPlayed"/> combos descontados.</summary>
        public float MultiplierAfter(int combosPlayed)
            => Mathf.Max(Min, Start - Mathf.Max(0, combosPlayed) * DecayPerCombo);

        /// <summary>True si con <paramref name="combosPlayed"/> combos ya tocó el piso.</summary>
        public bool ReachedMin(int combosPlayed)
            => Start - Mathf.Max(0, combosPlayed) * DecayPerCombo <= Min + 1e-4f;
    }
}
