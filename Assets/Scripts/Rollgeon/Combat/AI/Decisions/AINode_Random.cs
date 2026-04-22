using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Selecciona un hijo al azar con pesos y lo ejecuta. TECHNICAL.md §7.5.
    /// </summary>
    /// <remarks>
    /// Usa <see cref="AIContext.Rng"/> si está seteado; sino, <see cref="UnityEngine.Random"/>.
    /// Suma de weights inválida (≤0 o lista vacía) → <see cref="AIResult.Failed"/>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Random : AIQuestionNode
    {
        [Serializable]
        public struct Option
        {
            [MinValue(0f)]
            public float Weight;
            [OdinSerialize, SerializeReference]
            public AIDecisionNode Node;
        }

        [OdinSerialize]
        public List<Option> Options = new List<Option>();

        public override string NodeName => "Random";

        public override AIResult Tick(AIContext context)
        {
            if (Options == null || Options.Count == 0) return AIResult.Failed;

            float total = 0f;
            foreach (var o in Options) total += Mathf.Max(0f, o.Weight);
            if (total <= 0f) return AIResult.Failed;

            double roll = context?.Rng != null
                ? context.Rng.NextDouble() * total
                : UnityEngine.Random.Range(0f, total);

            double cursor = 0;
            foreach (var o in Options)
            {
                cursor += Mathf.Max(0f, o.Weight);
                if (roll <= cursor)
                {
                    return o.Node?.Tick(context) ?? AIResult.Failed;
                }
            }

            // Edge: floating-point jitter — ejecutar el último.
            return Options[Options.Count - 1].Node?.Tick(context) ?? AIResult.Failed;
        }
    }
}
