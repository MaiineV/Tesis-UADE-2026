using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Acción de "ataque telegráfico — turno N": marca un área centrada en el jugador, la resalta
    /// con estilo de advertencia y guarda el estado en <see cref="IThreatenedAreaService"/> para
    /// ejecutarla el próximo turno del Boss (<see cref="AINode_ExecuteTelegraph"/>). <b>No inflige
    /// daño este turno.</b> Sistemas prerequisito Bosses §1.
    /// </summary>
    /// <remarks>
    /// La <see cref="Shape"/> distingue a los tres Bosses: Boss 1 = <see cref="ThreatShape.SquareAroundPlayer"/>
    /// (3×3 con <see cref="Size"/>=1), Boss 2 = <see cref="ThreatShape.Row"/>/<see cref="ThreatShape.Column"/>
    /// (franja), Boss 3 = <see cref="ThreatShape.HalfRoom"/> (media sala). El daño y el ancho/radio
    /// salen del Inspector del nodo — nada hardcoded.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_TelegraphMark : AIActionNode
    {
        [Tooltip("Forma del área. Square=Boss1 (3×3), Row/Column=Boss2 (franja), HalfRoom=Boss3 (media sala), " +
                 "SquareAroundSelf=Boss1 (área centrada en el propio boss).")]
        public ThreatShape Shape = ThreatShape.SquareAroundPlayer;

        [Tooltip("Radio para Square/SquareAroundSelf (1 ⇒ 3×3), ancho en casillas de la franja para Row/Column " +
                 "(1 ⇒ línea del jugador), medio-ancho de la banda perpendicular para DirectionalBand " +
                 "(1 ⇒ 3 casillas de ancho), ancho de cada cuadrado para ScatteredSquares (2 ⇒ 2×2), o el " +
                 "índice de celda (1-based) para GridPartition. Ignorado en HalfRoom.")]
        [MinValue(0)]
        public int Size = 1;

        [Tooltip("Profundidad (en casillas) de la banda direccional, arrancando pegada al boss. Solo para DirectionalBand.")]
        [MinValue(1)]
        [ShowIf(nameof(Shape), ThreatShape.DirectionalBand)]
        public int Depth = 2;

        [Tooltip("Cantidad de cuadrados independientes, anclados al azar en la sala. Solo para ScatteredSquares.")]
        [MinValue(1)]
        [ShowIf(nameof(Shape), ThreatShape.ScatteredSquares)]
        public int Count = 3;

        [Tooltip("Columnas de la partición. Solo para GridPartition.")]
        [MinValue(1)]
        [ShowIf(nameof(Shape), ThreatShape.GridPartition)]
        public int Columns = 3;

        [Tooltip("Filas de la partición. Solo para GridPartition.")]
        [MinValue(1)]
        [ShowIf(nameof(Shape), ThreatShape.GridPartition)]
        public int Rows = 2;

        [Tooltip("Eje de corte para HalfRoom: Vertical ⇒ izquierda/derecha, Horizontal ⇒ abajo/arriba.")]
        [ShowIf(nameof(Shape), ThreatShape.HalfRoom)]
        public HalfRoomAxis HalfAxis = HalfRoomAxis.Vertical;

        [Tooltip("Daño que aplicará el ataque el próximo turno si el jugador sigue en el área.")]
        [MinValue(0)]
        public int Damage = 100;

        [Tooltip("Tipo de ataque del DamageContext al ejecutar.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        public override string NodeName => $"Telegraph Mark ({Shape}, dmg {Damage})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            var grid = context.Grid;
            if (grid == null) return AIResult.Failed;

            HashSet<GridCoord> tiles;
            if (Shape == ThreatShape.DirectionalBand)
            {
                if (!grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;
                if (!grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;
                tiles = ThreatAreaShape.ComputeDirectionalBand(grid, selfCoord, playerCoord, Size, Depth);
            }
            else if (Shape == ThreatShape.ScatteredSquares)
            {
                var rng = context.Rng ?? new System.Random();
                tiles = ThreatAreaShape.ComputeScatteredSquares(grid, rng, Count, Size);
            }
            // GridPartition necesita 3 parámetros (columnas, filas, índice de celda) y no entra
            // en el `size` único de Compute — mismo motivo por el que DirectionalBand/ScatteredSquares
            // tampoco pasan por ahí. Ni el jugador ni el boss son el centro, así que no hace falta
            // resolver ninguna posición.
            else if (Shape == ThreatShape.GridPartition)
            {
                tiles = ThreatAreaShape.ComputeGridPartition(grid, Columns, Rows, Size);
            }
            // AnchorsOnSelf en vez de comparar contra una shape puntual: el criterio es de la forma,
            // no del nodo, así que una shape nueva anclada en el jefe (ColumnAroundSelf) no depende
            // de acordarse de agregar otra rama acá.
            else if (ThreatAreaShape.AnchorsOnSelf(Shape))
            {
                if (!grid.TryGetPosition(context.SelfGuid, out var selfCoord)) return AIResult.Failed;
                tiles = ThreatAreaShape.Compute(grid, selfCoord, Shape, Size, HalfAxis);
            }
            else
            {
                if (!grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;
                tiles = ThreatAreaShape.Compute(grid, playerCoord, Shape, Size, HalfAxis);
            }
            if (tiles.Count == 0)
            {
                Debug.LogWarning($"[AINode_TelegraphMark] Área vacía (shape={Shape}) — ¿grafo sin bounds? No se marca nada.");
                return AIResult.Failed;
            }

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
            {
                Debug.LogError("[AINode_TelegraphMark] IThreatenedAreaService no registrado. " +
                               "Agrega ThreatenedAreaServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            threat.Mark(context.SelfGuid, tiles, Damage, Kind);

            // Overlay de sprites independiente del tinte del piso: el highlight de
            // move/path del jugador pinta y limpia sus tiles a su antojo y antes se
            // llevaba puesto el warning (quedaba azul y después default con el
            // telegraph todavía pendiente).
            ThreatTelegraphOverlay.ResolveOrCreate().Show(context.SelfGuid, tiles);

            return AIResult.Succeeded;
        }
    }
}
