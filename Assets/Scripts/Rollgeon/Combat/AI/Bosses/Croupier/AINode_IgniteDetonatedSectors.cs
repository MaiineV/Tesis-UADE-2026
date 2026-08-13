using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Croupier
{
    /// <summary>
    /// Prende fuego el/los sector(es) que detonaron en este turno del jefe
    /// (<see cref="AINode_DetonateSungSectors"/>): 6 por turno a quien termine su turno adentro. Mata
    /// la lectura de que el bloque recién explotado es el lugar más seguro del paño.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Duración por fase = dos definiciones.</b> <see cref="IHazardService.Activate(HazardDefinitionSO, IEnumerable{GridCoord})"/>
    /// toma la duración de la definición, así que la fase no puede sobreescribirla desde el nodo sin
    /// tocar el servicio: en vez de eso hay una def por fase (<see cref="Fire"/> /
    /// <see cref="FirePhase2"/>) y el nodo elige. Dejar <see cref="FirePhase2"/> vacío = el fuego dura
    /// lo mismo en las dos fases.
    /// </para>
    /// <para>
    /// <b>Ojo con <c>DurationRounds</c>: pide un turno más de lo que dice la ficha.</b> El fuego nace
    /// en el turno del jefe y el jugador tiene el primer turno de cada ronda (CNF-006), así que la
    /// ronda en la que se enciende ya no tiene ningún cierre de turno del jugador por delante.
    /// <c>DurationRounds = 1</c> expira en el próximo <c>OnTurnQueueBuilt</c>, antes de que el jugador
    /// vuelva a jugar: el fuego no llegaría a tickear nunca. "Dura 1 turno" (fase 1) se autora como
    /// <c>DurationRounds = 2</c> y "dura 2" (fase 2) como <c>3</c>.
    /// </para>
    /// <para>
    /// <b>La detonación consume la llama.</b> Si el jugador estaba adentro cuando el sector explotó, ya
    /// pagó por esa casilla este turno: se arma <see cref="IHazardService.SkipNextTick"/> sobre la
    /// instancia para que el fuego no le cobre 6 encima, y el peor caso de la costura sigue siendo 24.
    /// El skip se arma <b>sólo</b> si el jugador estaba adentro, y no siempre: el flag se consume
    /// recién con un tick que hubiera pegado, así que armarlo a ciegas se quedaría esperando y se
    /// tragaría el primer tick legítimo — que en fase 1 es el único que el fuego llega a dar.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_IgniteDetonatedSectors : AIActionNode
    {
        [Tooltip("Fuego de paño de fase 1. Trigger = OnTurnEndInTile, Damage = 6, DurationRounds = 2 " +
                 "(= 'un turno' del jugador; ver remarks del nodo).")]
        public HazardDefinitionSO Fire;

        [Tooltip("Fuego de fase 2 — la misma llama con DurationRounds = 3 ('dos turnos'). Vacío = usa " +
                 "la definición de fase 1 en las dos fases.")]
        public HazardDefinitionSO FirePhase2;

        [Tooltip("Si el jugador se comió la detonación, el fuego de ese sector se saltea su primer tick " +
                 "(la explosión consume la llama). Apagalo para que la detonación y el fuego se sumen.")]
        public bool BlastConsumesFlame = true;

        public override string NodeName => "Ignite Detonated Sectors (Croupier)";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;
            if (Fire == null) return AIResult.Failed;

            var wheel = CroupierWheelService.ResolveOrCreate();
            if (wheel == null) return AIResult.Failed;

            var sectors = wheel.DetonatedSectors;
            // Turno 1: cantó pero todavía no detonó nada. No hay nada que prender y no es un fallo.
            if (sectors == null || sectors.Count == 0) return AIResult.Succeeded;

            var pending = new List<int>(sectors);
            wheel.ClearDetonated();

            if (!ServiceLocator.TryGetService<IHazardService>(out var hazard) || hazard == null)
            {
                Debug.LogError("[AINode_IgniteDetonatedSectors] IHazardService no registrado. " +
                               "Agrega HazardServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            var grid = context.Grid;
            if (grid == null) ServiceLocator.TryGetService<IGridManager>(out grid);
            if (grid == null) return AIResult.Failed;

            var definition = (wheel.PhaseIndex >= 2 && FirePhase2 != null) ? FirePhase2 : Fire;
            bool playerPlaced = grid.TryGetPosition(context.PlayerGuid, out var playerCoord);

            bool ignitedAny = false;
            foreach (var sector in pending)
            {
                var tiles = ThreatAreaShape.ComputeRoomSector(grid, sector);
                if (tiles.Count == 0) continue;

                var instanceId = hazard.Activate(definition, tiles);
                if (instanceId == Guid.Empty) continue;
                ignitedAny = true;

                if (BlastConsumesFlame && playerPlaced && tiles.Contains(playerCoord))
                    hazard.SkipNextTick(instanceId);
            }

            return ignitedAny ? AIResult.Succeeded : AIResult.Failed;
        }
    }
}
