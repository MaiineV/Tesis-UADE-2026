using System;
using Rollgeon.Items.Active;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// Compara la cara resuelta (o la magnitud) de la activacion de un item activo en
    /// curso contra <see cref="Value"/>. Lee <c>PreConditionContext.Effect.TriggerContext</c>
    /// (<see cref="ActiveItemRollTriggerContext"/>) — sin ese contexto (fuera de un
    /// dispatch de item activo, o en un arbol de IA) devuelve <c>false</c>: no hay nada
    /// que comparar, no se puede decir que "pasa" por default. Feature#0085 §A3.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcActiveItemFaceCompare : BasePreCondition, IReadsTriggerEffect
    {
        [Tooltip("Operador de comparacion.")]
        public IntComparison Comparison = IntComparison.GreaterOrEqual;

        [Tooltip("Valor contra el que se compara la cara (o la magnitud).")]
        public int Value;

        [Tooltip("false: cara final resuelta (1..Faces). true: Magnitude (Gradient/Hierarchy).")]
        public bool UseMagnitude;

        public override string ConditionName => "Cara del item activo";

        public override bool Evaluate(PreConditionContext context)
        {
            if (!ActiveItemRollTriggerContext.TryGet(context?.Effect, out var rollContext)) return false;

            int actual = UseMagnitude ? rollContext.Magnitude : rollContext.Face;

            switch (Comparison)
            {
                case IntComparison.Equal: return actual == Value;
                case IntComparison.NotEqual: return actual != Value;
                case IntComparison.Less: return actual < Value;
                case IntComparison.LessOrEqual: return actual <= Value;
                case IntComparison.Greater: return actual > Value;
                case IntComparison.GreaterOrEqual: return actual >= Value;
                default: return false;
            }
        }
    }
}
