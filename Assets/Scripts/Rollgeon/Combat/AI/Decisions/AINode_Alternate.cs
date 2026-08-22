using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Rota determinísticamente entre los <see cref="Children"/>, uno por invocación
    /// (turno): 0, 1, 2, ..., 0, 1, 2, ... A diferencia de <see cref="AINode_Random"/>
    /// (azar independiente por turno, puede repetir la misma opción varias veces
    /// seguidas por pura probabilidad), este nodo garantiza que nunca se repita antes
    /// de recorrer todo el ciclo.
    /// </summary>
    /// <remarks>
    /// El índice es <c>[NonSerialized]</c>: vive en la instancia runtime (copia fresca por combate
    /// vía <c>EnemyDataSO.CreateRuntimeAIRoot</c>), nunca se serializa al asset y arranca en 0 en
    /// cada pelea nueva.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Alternate : AIQuestionNode
    {
        [OdinSerialize]
        public List<AIDecisionNode> Children = new List<AIDecisionNode>();

        [NonSerialized] private int _index;

        public override string NodeName => "Alternate";

        public override AIResult Tick(AIContext context)
        {
            if (Children == null || Children.Count == 0) return AIResult.Failed;

            var child = Children[_index % Children.Count];
            _index++;

            return child?.Tick(context) ?? AIResult.Failed;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (Children == null || Children.Count == 0) { onResult?.Invoke(AIResult.Failed); yield break; }

            var child = Children[_index % Children.Count];
            _index++;

            if (child == null) { onResult?.Invoke(AIResult.Failed); yield break; }

            var co = child.TickCoroutine(context, onResult);
            while (co.MoveNext()) yield return co.Current;
        }
    }
}
