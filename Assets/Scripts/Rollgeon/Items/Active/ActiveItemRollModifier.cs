using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Items.Active
{
    /// <summary>
    /// Ajuste que un encantamiento aplica sobre el resultado crudo de la tirada, antes de
    /// determinar la banda. GDD "Ítems Activos" §14, orden de operaciones.
    /// </summary>
    /// <remarks>
    /// <b>El clamp no es opcional.</b> El GDD prohibe que un encantamiento saque el
    /// resultado del rango del dado, y usa "Calibración: +1 al resultado, máximo 5" como
    /// ejemplo de que el tope va declarado en el propio encantamiento. Quien aplica el
    /// modifier igual clampea a <c>[1, faces]</c> como red de seguridad.
    /// </remarks>
    [HideReferenceObjectPicker]
    public abstract class ActiveItemRollModifier
    {
        /// <summary>
        /// <c>true</c> si este ajuste corresponde para <paramref name="roll"/>. Un
        /// modifier que no aplica no consume uso.
        /// </summary>
        public abstract bool AppliesTo(int roll, int faces);

        /// <summary>Resultado ajustado. Solo se llama si <see cref="AppliesTo"/> dio true.</summary>
        public abstract int Apply(int roll, int faces);

        /// <summary>Texto corto para el tooltip de la ficha.</summary>
        public abstract string Describe();
    }

    /// <summary>
    /// Suma fijo al resultado, con tope propio. Es la "Calibración" del GDD: <c>+1 al
    /// resultado, máximo 5</c> — el tope por debajo del maximo del dado es lo que impide
    /// que el encantamiento garantice la mejor banda.
    /// </summary>
    [System.Serializable]
    public sealed class RollFlatBonus : ActiveItemRollModifier
    {
        [Tooltip("Cuanto suma al resultado. Puede ser negativo.")]
        public int Amount = 1;

        [Tooltip("Tope del resultado ajustado. 0 = sin tope propio (igual se clampea al " +
                 "maximo del dado). Ponerlo por debajo del maximo es lo que conserva el " +
                 "riesgo — el GDD prohibe eliminar la banda mala.")]
        [MinValue(0)]
        public int MaxResult = 0;

        public override bool AppliesTo(int roll, int faces) => Amount != 0;

        public override int Apply(int roll, int faces)
        {
            int adjusted = roll + Amount;
            if (MaxResult > 0 && adjusted > MaxResult) adjusted = MaxResult;
            return adjusted;
        }

        public override string Describe()
            => MaxResult > 0
                ? $"{(Amount >= 0 ? "+" : "")}{Amount} al resultado, máximo {MaxResult}"
                : $"{(Amount >= 0 ? "+" : "")}{Amount} al resultado";
    }

    /// <summary>
    /// Piso parcial: un resultado igual o menor a <see cref="Threshold"/> se trata como
    /// <see cref="TreatAs"/>. Es el "Seguro flojo" del GDD (<c>si sacás 1, tratá como
    /// 2</c>) — el unico tipo de proteccion contra mala suerte que el doc permite, porque
    /// es limitada y parcial: no elimina la banda negativa, solo suaviza su piso.
    /// </summary>
    [System.Serializable]
    public sealed class RollFloor : ActiveItemRollModifier
    {
        [Tooltip("Resultados iguales o menores a esto se elevan.")]
        [MinValue(1)]
        public int Threshold = 1;

        [Tooltip("Valor al que se elevan.")]
        [MinValue(1)]
        public int TreatAs = 2;

        public override bool AppliesTo(int roll, int faces) => roll <= Threshold;

        public override int Apply(int roll, int faces) => TreatAs;

        public override string Describe() => $"si sacás {Threshold} o menos, cuenta como {TreatAs}";
    }
}
