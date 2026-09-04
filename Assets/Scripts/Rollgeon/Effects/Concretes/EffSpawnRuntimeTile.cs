using System;
using System.Collections.Generic;
using System.Linq;
using Patterns;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Coloca <see cref="Count"/> casillas runtime de <see cref="Definition"/> alrededor de un
    /// ancla (Bottle'o Thunder: charcos eléctricos cerca del objetivo). Genérico a propósito —
    /// cualquier item que necesite "tirar N casillas cerca de X" lo reusa sin código nuevo.
    /// </summary>
    /// <remarks>
    /// Nunca aborta la cadena: sin servicio o sin <see cref="Definition"/> el roll ya se cobró,
    /// así que degrada a warning + no-op en vez de <c>false</c>.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffSpawnRuntimeTile : BaseEffect
    {
        [Title("Casilla a colocar")]
        [Required]
        [Tooltip("Definición de la casilla especial que se instancia alrededor del ancla.")]
        public SpecialTileDefinitionSO Definition;

        [MinValue(1)]
        [Tooltip("Cantidad de casillas a colocar (celdas más cercanas al ancla primero).")]
        public int Count = 2;

        [MinValue(0)]
        [Tooltip("Rondas de vida de cada casilla creada. 0 = permanente vía DefaultDurationRounds no aplica acá; usar el override.")]
        public int DurationRounds = 2;

        [MinValue(1)]
        [Tooltip("Radio Manhattan máximo en el que busca celdas libres para colocar.")]
        public int MaxRadius = 3;

        /// <summary>
        /// Generador de aleatoriedad del shuffle por anillo. Público y no serializado a propósito:
        /// producción usa el default, los tests inyectan una seed fija para determinismo.
        /// </summary>
        [NonSerialized]
        public System.Random Rng = new System.Random();

        // El ancla sale del target/source del contexto, no de un picking propio del efecto.
        protected override bool ShowSelection => false;

        public override string GetEffectName() => "Spawn Runtime Tile";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            if (Definition == null)
            {
                Debug.LogWarning("[EffSpawnRuntimeTile] Sin Definition — no coloca nada.");
                return true;
            }

            if (!ServiceLocator.TryGetService<ISpecialTileService>(out var tiles) || tiles == null)
            {
                Debug.LogWarning("[EffSpawnRuntimeTile] ISpecialTileService no registrado — sin casillas.");
                return true;
            }
            if (!ServiceLocator.TryGetService<IGridManager>(out var grid) || grid == null) return true;

            var anchor = ResolveAnchor(context, grid);
            var request = new RuntimeTileRequest
            {
                Owner = context.SourceGuid,
                DurationRounds = DurationRounds,
            };

            int placed = 0;
            for (int radius = 1; radius <= MaxRadius && placed < Count; radius++)
            {
                var ring = CollectRingCells(anchor, radius);
                Shuffle(ring);

                foreach (var cell in ring)
                {
                    if (placed >= Count) break;
                    var instanceId = tiles.CreateRuntime(Definition, cell, request, out _);
                    if (instanceId != Guid.Empty) placed++;
                }
            }

            return true;
        }

        /// <summary>
        /// Coord del primer ocupante seleccionado (mismo patrón de resolución que
        /// <c>EffGridPush.ResolveTargetGuids</c>, pero devolviendo la celda y no el guid);
        /// sin selección, cae a la posición del source.
        /// </summary>
        private static GridCoord ResolveAnchor(EffectContext context, IGridManager grid)
        {
            if (context.SelectionResult?.SelectedTargets != null)
            {
                var occupants = grid.DistinctOccupants(
                    context.SelectionResult.SelectedTargets.Select(t => t.Coord));
                if (occupants.Count > 0 && grid.TryGetPosition(occupants[0], out var occupantCoord))
                    return occupantCoord;
            }

            grid.TryGetPosition(context.SourceGuid, out var sourceCoord);
            return sourceCoord;
        }

        // Row-major (Y ascendente, X ascendente) — mismo criterio que SafeTileQuery.CollectRing,
        // para que el orden "pre-shuffle" sea predecible en tests.
        private static List<GridCoord> CollectRingCells(GridCoord center, int radius)
        {
            var result = new List<GridCoord>();
            for (int y = center.Y - radius; y <= center.Y + radius; y++)
            {
                for (int x = center.X - radius; x <= center.X + radius; x++)
                {
                    var c = new GridCoord(x, y);
                    if (center.Manhattan(c) == radius) result.Add(c);
                }
            }
            return result;
        }

        private void Shuffle(List<GridCoord> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
