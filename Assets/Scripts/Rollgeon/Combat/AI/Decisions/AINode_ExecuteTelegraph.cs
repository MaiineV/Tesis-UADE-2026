using System;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Acción de "ataque telegráfico — turno N+1": consume el área que el Boss marcó el turno
    /// anterior (<see cref="AINode_TelegraphMark"/>). Si el jugador sigue en alguna casilla
    /// marcada → aplica el daño guardado vía <see cref="IDamagePipeline"/>; si se movió fuera →
    /// el ataque falla sin daño. En ambos casos limpia el resaltado y el estado. Sistemas
    /// prerequisito Bosses §1.
    /// </summary>
    /// <remarks>
    /// Pensado para ir como <b>primer</b> hijo del sequence del Boss (se resuelve al inicio del
    /// turno del Boss, antes de elegir su acción del pool). Siempre retorna
    /// <see cref="AIResult.Succeeded"/> — no es un gate, es una resolución de inicio de turno;
    /// "no había nada pendiente" o "el jugador esquivó" no deben cortar el sequence.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_ExecuteTelegraph : AIActionNode
    {
        public override string NodeName => "Execute Telegraph (turn N+1)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Succeeded;

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
                return AIResult.Succeeded;

            // Apagar el overlay de advertencia siempre que ejecutemos (haya o no
            // impacto). Antes esto era TileHighlightService.ClearAll(), que además
            // se llevaba puesto cualquier highlight ajeno al telegraph.
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(context.SelfGuid);

            if (!threat.TryConsume(context.SelfGuid, out var area))
                return AIResult.Succeeded;

            bool hit = false;
            var grid = context.Grid;
            if (grid != null
                && grid.TryGetPosition(context.PlayerGuid, out var playerCoord)
                && area.Contains(playerCoord))
            {
                hit = true;
                if (context.DamagePipeline != null && area.Damage > 0)
                {
                    context.DamagePipeline.Resolve(new DamageContext
                    {
                        SourceId = context.SelfGuid,
                        TargetId = context.PlayerGuid,
                        BaseDamage = area.Damage,
                        Kind = area.Kind,
                    });
                }
            }

            EventManager.Trigger(EventName.OnThreatenedAreaResolved, context.SelfGuid, hit);
            return AIResult.Succeeded;
        }
    }
}
