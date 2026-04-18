using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.PreConditions
{
    /// <summary>
    /// Modos de composición de <see cref="PCComposite"/>.
    /// <list type="bullet">
    ///   <item><c>And</c> — todas las hijas deben pasar (equivalente al default del grupo).</item>
    ///   <item><c>Or</c>  — al menos una hija pasa.</item>
    ///   <item><c>Not</c> — invierte el AND de las hijas (útil cuando sólo se lista una).</item>
    /// </list>
    /// </summary>
    public enum CompositeMode
    {
        And = 0,
        Or  = 1,
        Not = 2,
    }

    /// <summary>
    /// Único concrete de precondition que esta foundation implementa (plan §3.3).
    /// Permite lógica AND/OR/NOT a nivel de precondition sin cargar la composición en
    /// <see cref="Rollgeon.Effects.EffectData"/> — la lista del EffectData mantiene su
    /// semántica AND dura (§8.1), y cuando un autor necesita OR/NOT agrega un
    /// <c>PCComposite</c> como único elemento del grupo y pobla su lista interna.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class PCComposite : BasePreCondition
    {
        public override string ConditionName => $"Composite ({Mode})";

        [Tooltip("Cómo se combinan las precondiciones hijas.")]
        public CompositeMode Mode = CompositeMode.And;

        /// <summary>
        /// Lista polimórfica de hijas. Plan §3.3 + §13.6.1: <c>[OdinSerialize]</c> +
        /// <c>[SerializeReference]</c> para preservar el subtipo concreto al round-trip.
        /// </summary>
        [OdinSerialize, SerializeReference]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        public List<BasePreCondition> Children = new List<BasePreCondition>();

        public override bool Evaluate(PreConditionContext context)
        {
            // Lista vacía: And → true (vacuously), Or → false (no one to approve), Not → true (!false).
            switch (Mode)
            {
                case CompositeMode.And:
                    if (Children == null || Children.Count == 0) return true;
                    foreach (var c in Children)
                    {
                        if (c == null) continue;
                        if (!c.Evaluate(context)) return false;
                    }
                    return true;

                case CompositeMode.Or:
                    if (Children == null || Children.Count == 0) return false;
                    foreach (var c in Children)
                    {
                        if (c == null) continue;
                        if (c.Evaluate(context)) return true;
                    }
                    return false;

                case CompositeMode.Not:
                    // Semántica: Not == NAND. AND de las hijas, luego negación.
                    // Con 0 hijas, And == true (vacuously) → Not = !true = false.
                    if (Children == null || Children.Count == 0) return false;
                    {
                        bool allTrue = true;
                        foreach (var c in Children)
                        {
                            if (c == null) continue;
                            if (!c.Evaluate(context)) { allTrue = false; break; }
                        }
                        return !allTrue;
                    }

                default:
                    return false;
            }
        }
    }
}
