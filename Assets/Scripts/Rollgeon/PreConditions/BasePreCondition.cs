using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions
{
    /// <summary>
    /// Base polimórfica de las precondiciones. TECHNICAL.md §8.2.
    /// <para>
    /// Regla §13.6.1: <c>abstract</c> + <c>[Serializable, HideReferenceObjectPicker]</c>. Los
    /// contenedores (<see cref="Rollgeon.Effects.EffectData.PreConditions"/>) usan
    /// <c>[OdinSerialize]</c> + <c>[SerializeReference]</c>.
    /// </para>
    /// <para>
    /// <b>Catálogo §8.2 previsto pero NO implementado en esta foundation</b>:
    /// <c>PCHasIntAttribute</c>, <c>PCHasModifier</c>, <c>PCCurrentPhase</c>, <c>PCFirstRollOfCombat</c>,
    /// <c>PCComboAvailable</c>, <c>PCEntityInRange</c>. Cada sistema downstream implementa los
    /// suyos en su propio archivo bajo <c>Assets/Scripts/Rollgeon/PreConditions/Concretes/</c>.
    /// Único concrete acá: <see cref="PCComposite"/> (plan §3.3).
    /// </para>
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public abstract class BasePreCondition
    {
        /// <summary>Nombre visible en inspector / logs.</summary>
        public abstract string ConditionName { get; }

        /// <summary>Evalúa la condición contra el contexto dado.</summary>
        public abstract bool Evaluate(PreConditionContext context);

        /// <summary>
        /// Flag §8.2 — true si la condición se resuelve a valor constante (literal en inspector),
        /// false si depende de state runtime. Consumido por herramientas de editor.
        /// </summary>
        [SerializeField]
        protected bool _isConstantValue = true;

        /// <summary>
        /// Helper estático reutilizable por cualquier sistema que quiera AND-evaluar un set
        /// de precondiciones. TECHNICAL.md §8.1 AND semántico.
        /// <para>Retorna <c>true</c> si la lista es null o vacía (no-op = pasa).</para>
        /// </summary>
        public static bool EvaluateAll(IEnumerable<BasePreCondition> conditions, PreConditionContext context)
        {
            if (conditions == null) return true;
            foreach (var pc in conditions)
            {
                if (pc == null) continue;
                if (!pc.Evaluate(context)) return false;
            }
            return true;
        }
    }
}
