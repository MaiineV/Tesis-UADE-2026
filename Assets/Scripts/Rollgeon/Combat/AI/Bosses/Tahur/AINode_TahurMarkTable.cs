using System;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Bosses.Tahur
{
    /// <summary>
    /// "Pone la mesa": pinta el 3×3 alrededor del Tahúr en cian, daño 0 — el único lugar desde
    /// donde el jugador cobra el pozo.
    /// </summary>
    /// <remarks>
    /// La mesa vive en <see cref="ITahurWagerService.TableTiles"/> y su overlay usa una key propia
    /// (<see cref="TahurWagerService.TableOverlayGuid"/>): <see cref="IThreatenedAreaService"/> se
    /// indexa por el guid de la fuente y una segunda marca del mismo jefe pisaría al Castigo.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TahurMarkTable : AIActionNode
    {
        [Tooltip("Radio de La Mesa (1 ⇒ 3×3 centrado en el jefe).")]
        [MinValue(0)]
        public int Size = 1;

        [Tooltip("Color del overlay. Cian: sin un estado de color propio, La Mesa y el Castigo se " +
                 "leen del mismo naranja y el jefe es ilegible por construcción.")]
        public Color Tint = new Color(0f, 0.85f, 1f, 1f);

        public override string NodeName => "Tahúr — Mark Table (su 3×3, daño 0)";

        public override AIResult Tick(AIContext context)
        {
            if (context?.Grid == null) return AIResult.Failed;
            if (!context.Grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;

            var tiles = ThreatAreaShape.Compute(
                context.Grid, selfCoord, ThreatShape.SquareAroundSelf, Size, HalfRoomAxis.Vertical);
            if (tiles.Count == 0) return AIResult.Failed;

            var wager = TahurWagerService.ResolveOrCreate();
            wager.SetTable(tiles);

            ThreatTelegraphOverlay.ResolveOrCreate()
                .Show(TahurWagerService.TableOverlayGuid, tiles, ThreatOverlayState.Safe, Tint);

            return AIResult.Succeeded;
        }
    }
}
