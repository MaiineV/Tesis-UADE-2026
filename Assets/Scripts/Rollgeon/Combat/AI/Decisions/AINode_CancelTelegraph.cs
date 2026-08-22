using System;
using Patterns;
using Rollgeon.Combat.Threat;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Descarta el aviso pendiente de un canal sin ejecutarlo: saca el área de
    /// <see cref="IThreatenedAreaService"/> y apaga su overlay. El paso que le da a un jefe la
    /// forma de <i>reemplazar</i> lo que tenía anunciado en vez de sumarle un segundo anuncio.
    /// </summary>
    /// <remarks>
    /// Descarta la telegrafía —el área marcada y su dibujo—, no lo que el jefe ya hizo este turno.
    /// Nada pendiente no es un fallo: el aviso a cancelar puede no existir, y un <c>Failed</c> ahí
    /// cortaría la Sequence del jefe.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_CancelTelegraph : AIActionNode
    {
        [Tooltip("Canal del aviso a descartar. Vacío = la marca principal del propio jefe, la que " +
                 "levanta un AINode_TelegraphMark sin canal. Tiene que coincidir con el ChannelId " +
                 "del paso que la marcó.")]
        public string ChannelId;

        public override string NodeName => string.IsNullOrEmpty(ChannelId)
            ? "Cancel Telegraph (canal principal)"
            : $"Cancel Telegraph [{ChannelId}]";

        public override AIResult Tick(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
            {
                Debug.LogError("[AINode_CancelTelegraph] IThreatenedAreaService no registrado.");
                return AIResult.Failed;
            }

            // La misma derivación que usa el paso que marca: el canal es un guid derivado, no una
            // key aparte, así que cancelar por el guid del jefe cuando el aviso vive en un canal
            // dejaría el área pendiente y su overlay intactos.
            var source = AINode_TelegraphMark.SourceKey(context.SelfGuid, ChannelId);

            if (!threat.HasPending(source)) return AIResult.Succeeded;

            threat.Clear(source);

            // El área y su dibujo se prenden y se apagan juntos: sin esto queda un aviso pintado que
            // nunca va a detonar.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(source);

            Debug.Log($"[AINode_CancelTelegraph] Aviso descartado ({NodeName}): lo reemplaza el " +
                      "paso que sigue.");

            return AIResult.Succeeded;
        }
    }
}
