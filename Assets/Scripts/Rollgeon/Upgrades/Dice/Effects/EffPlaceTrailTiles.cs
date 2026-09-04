using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Effects;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Rollgeon.Upgrades;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Effects
{
    /// <summary>
    /// Rastro del dado de Movimiento (Incendiario, Rastro tóxico, Sendero de espinas): deja
    /// una casilla especial en cada celda que el jugador ABANDONÓ en el movimiento voluntario
    /// que disparó <c>PlayerMoved</c> (el path del contexto sin el destino). Owner = el
    /// jugador, así <c>OwnerAndAlliesImmune</c> de la definición lo protege a él y a sus aliados.
    /// </summary>
    /// <remarks>
    /// Stacking GDD: varias copias no duplican el rastro — solo la primera copia viva coloca,
    /// y cada copia extra suma <see cref="ExtraRoundsPerCopy"/> a la duración. Las celdas ya
    /// ocupadas por otra casilla especial se saltean (<c>CreateRuntime</c> las rechaza).
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffPlaceTrailTiles : BaseEffect, IRequiresTriggerContext<ScratchTriggerContext>
    {
        [Required]
        [Tooltip("Definición de la casilla que queda en el rastro (fuego, veneno, espinas).")]
        public SpecialTileDefinitionSO Definition;

        [MinValue(0)]
        [Tooltip("Rondas que dura cada casilla. 0 = DefaultDurationRounds de la definición.")]
        public int DurationRounds = 2;

        [MinValue(0)]
        [Tooltip("Rondas extra por cada copia adicional del encantamiento en el dado.")]
        public int ExtraRoundsPerCopy = 1;

        [Tooltip("Si también deja casilla en el destino (por default solo en las abandonadas).")]
        public bool IncludeDestination;

        protected override bool ShowSelection => false;

        public override string GetEffectName() => "Place Trail Tiles";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null || Definition == null) return false;
            if (!context.TryGetTriggerContext<ScratchTriggerContext>(out var trig) || trig.Slot == null)
                return false;
            var path = trig.Path;
            if (path == null || path.Count < 2) return true;

            int copies = MovementLaneCopies.Count(trig.Slot.Value, out bool isFirstCopy);
            if (!isFirstCopy) return true;

            if (!ServiceLocator.TryGetService<ISpecialTileService>(out var tiles) || tiles == null)
            {
                Debug.LogWarning("[EffPlaceTrailTiles] ISpecialTileService no registrado — sin rastro.");
                return false;
            }

            int baseRounds = DurationRounds > 0 ? DurationRounds : Definition.DefaultDurationRounds;
            int rounds = baseRounds + ExtraRoundsPerCopy * Math.Max(0, copies - 1);
            var request = new RuntimeTileRequest
            {
                Owner = context.SourceGuid,
                DurationRounds = rounds,
            };

            int last = IncludeDestination ? path.Count - 1 : path.Count - 2;
            for (int i = 0; i <= last; i++)
            {
                // Una celda ya ocupada por otra casilla especial se saltea: el rastro no pisa.
                tiles.CreateRuntime(Definition, path[i], request, out _);
            }
            return true;
        }
    }
}
