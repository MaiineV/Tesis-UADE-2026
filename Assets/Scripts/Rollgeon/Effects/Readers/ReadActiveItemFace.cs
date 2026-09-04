using System;
using Rollgeon.Items.Active;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Readers
{
    /// <summary>
    /// Lee la cara resuelta (o la magnitud) de la activacion de un item activo en curso
    /// (<see cref="ActiveItemRollTriggerContext"/>). Para "Justa de Justicia": el daño
    /// literal es la cara del D12, autorado como <c>EffDealDamage { FromReader = new
    /// ReadActiveItemFace() }</c>. Feature#0085 §A3.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadActiveItemFace : EffectIntReader
    {
        private const string LogPrefix = "[ReadActiveItemFace] ";

        [Tooltip("false: cara final resuelta (1..Faces). true: Magnitude — igual a la cara " +
                 "en items Gradient/Hierarchy, 0 en Bands/Binary (no tienen magnitud continua).")]
        public bool UseMagnitude;

        public override int Read(EffectContext context)
        {
            if (!ActiveItemRollTriggerContext.TryGet(context, out var rollContext))
            {
                Debug.LogWarning(LogPrefix + "sin ActiveItemRollTriggerContext — devuelve 0.");
                return 0;
            }

            return UseMagnitude ? rollContext.Magnitude : rollContext.Face;
        }
    }
}
