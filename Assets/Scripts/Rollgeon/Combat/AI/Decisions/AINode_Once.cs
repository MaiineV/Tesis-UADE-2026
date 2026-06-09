using System;
using System.Collections;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Decorador que ejecuta su hijo <b>una sola vez</b> en toda la vida de la instancia. La
    /// primera vez que el hijo devuelve <see cref="AIResult.Succeeded"/> queda "latcheado"; a
    /// partir de ahí el nodo es transparente (devuelve <see cref="AIResult.Succeeded"/> sin
    /// re-ejecutar al hijo). Si el hijo falla, NO latchea — se reintenta el próximo tick.
    /// </summary>
    /// <remarks>
    /// Pieza del esquema ad-hoc de Fase 2 (decisión de diseño): se combina con
    /// <c>AINode_If(PcOwnerHpBelow)</c> para disparar el setup de fase una única vez al cruzar el
    /// umbral — p.ej. <c>If(HP&lt;umbral) Then Once(ApplyStatModifier)</c> — sin que el cambio
    /// permanente se vuelva a aplicar (y stackee) cada turno mientras el HP siga bajo.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_Once : AIDecisionNode
    {
        [OdinSerialize]
        [Tooltip("Nodo a ejecutar una sola vez (típicamente el setup de Fase 2).")]
        public AIDecisionNode Child;

        [NonSerialized] private bool _done;

        public override string NodeName => "Once";

        public override AIResult Tick(AIContext context)
        {
            if (_done) return AIResult.Succeeded;
            if (Child == null) return AIResult.Failed;

            var result = Child.Tick(context);
            if (result == AIResult.Succeeded) _done = true;
            return result;
        }

        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            if (_done) { onResult?.Invoke(AIResult.Succeeded); yield break; }
            if (Child == null) { onResult?.Invoke(AIResult.Failed); yield break; }

            AIResult captured = AIResult.Failed;
            var co = Child.TickCoroutine(context, r => captured = r);
            while (co.MoveNext()) yield return co.Current;

            if (captured == AIResult.Succeeded) _done = true;
            onResult?.Invoke(captured);
        }
    }
}
