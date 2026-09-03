using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.PreConditions.Concretes
{
    /// <summary>
    /// True si la acción identificada por <see cref="ActionKey"/> ya se ejecutó este turno
    /// (<c>AIContext.HasExecuted</c>, vía <see cref="PreConditionContext.HasExecutedAction"/>).
    /// Con <see cref="Negate"/>, invierte el resultado — "esta acción TODAVÍA no se ejecutó".
    /// </summary>
    /// <remarks>
    /// Nace del caso del Healer: intenta reposicionarse para curar
    /// (<c>AINode_Move.ActionKey</c>) y, si ese intento no logró moverlo (bloqueado sin
    /// alternativa — ej. un mimic tapando el único paso), cae a su comportamiento de
    /// combate solo en vez de terminar el turno sin hacer nada. Sin
    /// <see cref="PreConditionContext.HasExecutedAction"/> (callers que no vienen de un
    /// árbol de IA), permisivo: no veta.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class PcActionExecuted : BasePreCondition
    {
        [Tooltip("Action key a chequear — ej. AINode_Move.ActionKey ('__move').")]
        public string ActionKey;

        [Tooltip("Invierte: true = pasa cuando la acción TODAVÍA no se ejecutó.")]
        public bool Negate;

        public override string ConditionName =>
            Negate ? $"Action '{ActionKey}' NOT executed yet" : $"Action '{ActionKey}' executed this turn";

        public override bool Evaluate(PreConditionContext context)
        {
            if (context == null || string.IsNullOrEmpty(ActionKey)) return true;
            if (context.HasExecutedAction == null) return true; // sin dato → permisivo, no veta

            bool executed = context.HasExecutedAction(ActionKey);
            return Negate ? !executed : executed;
        }
    }
}
