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
        [Tooltip("Forma del área. Square=Boss1 (3×3), Row/Column=Boss2 (franja), HalfRoom=Boss3 (media sala).")]
        public ThreatShape Shape = ThreatShape.SquareAroundPlayer;

        [Tooltip("Radio para Square (1 ⇒ 3×3) o ancho en casillas de la franja para Row/Column (1 ⇒ línea del jugador). Ignorado en HalfRoom.")]
        [MinValue(0)]
        public int Size = 1;

        [Tooltip("Eje de corte para HalfRoom: Vertical ⇒ izquierda/derecha, Horizontal ⇒ abajo/arriba.")]
        [ShowIf(nameof(Shape), ThreatShape.HalfRoom)]
        public HalfRoomAxis HalfAxis = HalfRoomAxis.Vertical;

        [Tooltip("Daño que aplicará el ataque el próximo turno si el jugador sigue en el área.")]
        [MinValue(0)]
        public int Damage = 10;

        [Tooltip("Tipo de ataque del DamageContext al ejecutar.")]
        public AttackKind Kind = AttackKind.BasicAttack;

        [Tooltip("Estilo de highlight (clave de TileHighlightService). Default 'warning'.")]
        public string HighlightStyle = "warning";

        public override string NodeName => $"Telegraph Mark ({Shape}, dmg {Damage})";

        public override AIResult Tick(AIContext context)
        {
            if (context == null) return AIResult.Failed;

            var grid = context.Grid;
            if (grid == null) return AIResult.Failed;
            if (!grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return AIResult.Failed;

            var tiles = ThreatAreaShape.Compute(grid, playerCoord, Shape, Size, HalfAxis);
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

            if (ServiceLocator.TryGetService<ITileHighlightService>(out var highlight) && highlight != null)
                highlight.Highlight(tiles, HighlightStyle);

            return AIResult.Succeeded;
        }
    }
}
