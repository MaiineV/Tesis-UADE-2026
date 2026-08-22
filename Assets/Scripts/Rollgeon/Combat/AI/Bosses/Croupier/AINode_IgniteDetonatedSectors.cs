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
    /// (<see cref="AINode_DetonateSungSectors"/>): daña a quien termine su turno adentro.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DurationRounds</c> pide un turno más de lo que arde en la mesa: el fuego nace en el turno del
    /// jefe y el jugador tiene el primer turno de cada ronda (CNF-006), así que <c>DurationRounds = 1</c>
    /// expira antes de que vuelva a jugar y el fuego no tickea nunca.
    /// </para>
    /// <para>
    /// <see cref="IHazardService.SkipNextTick"/> se arma sólo si el jugador estaba adentro al detonar:
    /// el flag se consume recién con un tick que hubiera pegado, así que armarlo a ciegas se tragaría
    /// el primer tick legítimo.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_IgniteDetonatedSectors : AIActionNode
    {
        [Tooltip("Fuego de paño de fase 1. Trigger = OnTurnEndInTile, Damage = 6, DurationRounds = 3 " +
                 "(= 'arde 2 rondas' para el jugador; ver remarks del nodo).")]
        public HazardDefinitionSO Fire;

        [Tooltip("Fuego de fase 2 — la misma llama con DurationRounds = 4 ('arde 3 rondas'). Vacío = " +
                 "usa la definición de fase 1 en las dos fases.")]
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
