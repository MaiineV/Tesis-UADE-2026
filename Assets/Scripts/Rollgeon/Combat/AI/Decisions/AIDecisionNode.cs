using System;
using System.Collections;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Base polimorfica de los nodos del AI tree. TECHNICAL.md §7.5.
    /// </summary>
    /// <remarks>
    /// Serializada via <c>[SerializeReference]</c> en <c>EnemyDataSO.AIRoot</c> + Odin
    /// para inspector autorable. Ver regla §13.6.1.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public abstract class AIDecisionNode
    {
        /// <summary>
        /// Identidad de AUTORÍA — la key estable del sidecar de layout del Editor de
        /// enemigos. El runtime jamás la lee. Null en árboles viejos (Odin deserializa
        /// default): el editor la asigna lazy al guardar el layout. Viaja en el blob Odin
        /// del SO, así sobrevive reloads, builders que regeneran el árbol y edición externa.
        /// </summary>
        [OdinSerialize, HideInInspector]
        public string EditorNodeId;

        public virtual string NodeName => GetType().Name;

        public abstract AIResult Tick(AIContext context);

        /// <summary>
        /// Variante coroutine de <see cref="Tick"/>. Default: llama <c>Tick()</c> y, si
        /// retorna <see cref="AIResult.Running"/> con <see cref="AIContext.PendingWait"/>
        /// seteado, lo drena y promueve a <see cref="AIResult.Succeeded"/>. Los nodos de
        /// control flow overridean para propagar la semántica coroutine a sus hijos.
        /// </summary>
        public virtual IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            var result = Tick(context);
            if (result == AIResult.Running && context?.PendingWait != null)
            {
                var wait = context.PendingWait;
                context.PendingWait = null;
                while (wait.MoveNext()) yield return wait.Current;
                result = AIResult.Succeeded;
            }
            onResult?.Invoke(result);
        }
    }
}
