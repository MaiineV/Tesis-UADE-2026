using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Items.Active;
using Rollgeon.Items.Active.Choice;
using Rollgeon.Movement;
using Rollgeon.Tiles;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Probability Drive — banda positiva "Control improbable" (Feature#0085,
    /// Items_Activos_Redisenados.md §5, D4 cara 4). Sortea hasta 3 casillas seguras distintas de
    /// radio 0-4 alrededor del centro y le pide al jugador que elija una (§A5); con 1 sola
    /// opción teletransporta directo, sin abrir la elección.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffProbabilityChoice : BaseEffect
    {
        private const int MaxOptions = 3;

        /// <summary>RNG del sorteo de opciones. Público y no serializado: producción usa el
        /// default, los tests inyectan una seed fija.</summary>
        [NonSerialized]
        public System.Random Rng = new System.Random();

        public override string GetEffectName() => "Probability Choice";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null)
            {
                Debug.LogWarning("[EffProbabilityChoice] IGridManager no registrado — no-op.");
                return true;
            }
            if (!TryResolveCenter(context, grid, out var center)) return true;

            ServiceLocator.TryGetService<ISpecialTileService>(out var tiles);
            ServiceLocator.TryGetService<IThreatenedAreaService>(out var threats);

            var candidates = SafeTileQuery.CollectRing(center, 0, 4, grid, tiles, threats);
            if (candidates.Count == 0)
            {
                Debug.Log("[EffProbabilityChoice] sin ninguna casilla segura en radio 4 — no-op.");
                return true;
            }

            var options = SampleDistinct(candidates, Math.Min(MaxOptions, candidates.Count));
            var player = context.SourceGuid;

            if (options.Count == 1)
            {
                if (TryGetPathedMovement(out var pathedSingle)) pathedSingle.Teleport(player, options[0]);
                return true;
            }

            if (!ActiveItemRollTriggerContext.TryGet(context, out var rc) || rc.Choices == null)
            {
                // Sin ChoiceHost (ej. test que no ejercita el flujo de elección): degrada a un
                // destino al azar entre las opciones, nunca deja el estado sin resolver.
                if (TryGetPathedMovement(out var pathedFallback))
                    pathedFallback.Teleport(player, options[Rng.Next(options.Count)]);
                return true;
            }

            rc.Choices.RequestChoice(new ActiveItemChoiceRequest
            {
                Options = options,
                HighlightStyle = "range",
                OnChosen = chosen =>
                {
                    if (TryGetPathedMovement(out var pathedChosen)) pathedChosen.Teleport(player, chosen);
                },
                OnAbandoned = () =>
                {
                    if (TryGetPathedMovement(out var pathedAbandoned))
                        pathedAbandoned.Teleport(player, options[Rng.Next(options.Count)]);
                },
            });

            return true;
        }

        private List<GridCoord> SampleDistinct(List<GridCoord> source, int count)
        {
            var pool = new List<GridCoord>(source);
            var result = new List<GridCoord>();
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int idx = Rng.Next(pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }
            return result;
        }

        private static bool TryResolveCenter(EffectContext context, IGridManager grid, out GridCoord center)
        {
            if (context.SelectionResult?.FirstSelectedCoord is GridCoord selected)
            {
                center = selected;
                return true;
            }
            return grid.TryGetPosition(context.SourceGuid, out center);
        }

        private static bool TryGetPathedMovement(out IPathedMovementService pathed)
        {
            pathed = null;
            if (ServiceLocator.TryGetService<IMovementService>(out var movement) && movement != null)
                pathed = movement as IPathedMovementService;
            if (pathed == null) ServiceLocator.TryGetService<IPathedMovementService>(out pathed);
            return pathed != null;
        }
    }
}
