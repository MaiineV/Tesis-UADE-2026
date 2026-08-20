using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Combat.AI.Decisions
{
    /// <summary>
    /// Convierte en casillas especiales el área que este jefe ya telegrafió: consume la marca
    /// pendiente y planta <see cref="Definition"/> encima. El puente entre la telegrafía (que piensa
    /// en formas) y el sistema de casillas especiales (que piensa en coordenadas).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>No recalcula la forma, consume la marca.</b> Recalcular la banda en el turno de la
    /// ignición la movería: está anclada en el jefe y apunta al jugador, y los dos se movieron desde
    /// que se marcó. El fuego tiene que caer exactamente donde el jugador lo vio venir — si no
    /// coincide, la telegrafía deja de ser una promesa y pasa a ser una sugerencia.
    /// </para>
    /// <para>
    /// <b>Contra <c>AINode_ExecuteTelegraph</c>:</b> ése cobra el golpe y se va. Este no pega nada
    /// en el momento: deja el piso encendido y el daño lo cobran las casillas, con sus dos números
    /// propios (<c>EnterDamage</c> por casilla pisada, <c>TurnStartDamage</c> por arrancar el turno
    /// adentro). Por eso el jefe puede quemar sin tocar al jugador y el castigo llega igual.
    /// </para>
    /// <para>
    /// <b><see cref="DurationRounds"/> = 0 significa PERMANENTE</b> en
    /// <see cref="ISpecialTileService.Place"/>, no "sin duración". Un campo nuevo nace en 0 en todo
    /// <c>ED_Boss_*.asset</c> ya serializado (Odin no corre field initializers al deserializar), así
    /// que un 0 que llegue al servicio dejaría una banda de fuego encendida para siempre y la pelea
    /// invivible. Se cae al <c>DefaultDurationRounds</c> del SO en vez de pasar el 0.
    /// </para>
    /// <para>
    /// <b>Las duraciones piden una ronda más de lo que dicen.</b> El fuego nace en el turno del jefe
    /// y el jugador tiene el primer turno de cada ronda (CNF-006), así que la ronda en la que se
    /// enciende ya no tiene cierre de turno del jugador por delante. "Arde N rondas" se autora como
    /// <c>N + 1</c>.
    /// </para>
    /// <para>
    /// <b>Las bandas se acumulan.</b> Este nodo no apaga las anteriores. Si la duración supera el
    /// intervalo entre igniciones, el piso libre se achica ronda a ronda — que es justamente para lo
    /// que existe el fuego. Si la duración es menor o igual al intervalo, nunca conviven dos bandas
    /// y el efecto se pierde.
    /// </para>
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class AINode_IgniteArea : AIActionNode
    {
        [Required]
        [Tooltip("Definición de la casilla a plantar. Sin esto el nodo falla: es el daño entero del " +
                 "paso.")]
        public SpecialTileDefinitionSO Definition;

        [Tooltip("Duración en rondas, override del DefaultDurationRounds del SO. 0 o menos = usar el " +
                 "del SO. Ojo: pide una ronda más de lo que dura para el jugador (ver remarks).")]
        [MinValue(0)]
        public int DurationRounds;

        public override string NodeName => Definition == null
            ? "Ignite Area (sin definición)"
            : $"Ignite Area ({Definition.name})";

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>). No hay presentación que
        /// esperar: las casillas traen su propio visual y VFX, así que los dos caminos hacen lo mismo.
        /// </summary>
        public override AIResult Tick(AIContext context) => Ignite(context);

        /// <inheritdoc />
        public override IEnumerator TickCoroutine(AIContext context, Action<AIResult> onResult)
        {
            onResult?.Invoke(Ignite(context));
            yield break;
        }

        private AIResult Ignite(AIContext context)
        {
            if (context == null || context.SelfGuid == Guid.Empty) return AIResult.Failed;
            if (Definition == null)
            {
                Debug.LogError("[AINode_IgniteArea] Sin Definition: el paso no tiene nada que plantar.");
                return AIResult.Failed;
            }

            if (!ServiceLocator.TryGetService<IThreatenedAreaService>(out var threat) || threat == null)
            {
                Debug.LogError("[AINode_IgniteArea] IThreatenedAreaService no registrado.");
                return AIResult.Failed;
            }

            // Sin marca pendiente no es un fallo: el primer turno del jefe telegrafía y todavía no
            // hay nada que encender. Failed acá abortaría la Sequence del árbol.
            if (!threat.TryConsume(context.SelfGuid, out var area)) return AIResult.Succeeded;

            ClearOverlay(context);

            var tiles = area.Tiles;
            if (tiles == null || tiles.Count == 0) return AIResult.Succeeded;

            if (!ServiceLocator.TryGetService<ISpecialTileService>(out var special) || special == null)
            {
                Debug.LogError("[AINode_IgniteArea] ISpecialTileService no registrado. Agrega " +
                               "SpecialTileServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            // Se filtra contra la sala: la forma telegrafiada pudo marcar casillas que el grafo
            // horneado no tiene, y plantar fuera de la grilla deja instancias que nadie puede pisar
            // ni ver expirar.
            var placeable = new List<GridCoord>(tiles.Count);
            var grid = context.Grid;
            if (grid == null) ServiceLocator.TryGetService<IGridManager>(out grid);
            foreach (var coord in tiles)
            {
                if (grid != null && !grid.IsWalkable(coord)) continue;
                placeable.Add(coord);
            }
            if (placeable.Count == 0) return AIResult.Succeeded;

            var instance = special.Place(Definition, placeable, new TilePlacementOptions
            {
                // El dueño es lo que hace valer OwnerBossImmune: sin esto el jefe se quema con su
                // propio fuego, y este nodo lo usa un jefe que huye pegado a la banda que prendió.
                Owner = context.SelfGuid,
                DurationRounds = DurationRounds > 0 ? DurationRounds : 0,
            });

            return instance == Guid.Empty ? AIResult.Failed : AIResult.Succeeded;
        }

        private static void ClearOverlay(AIContext context)
        {
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(context.SelfGuid);
        }
    }
}
