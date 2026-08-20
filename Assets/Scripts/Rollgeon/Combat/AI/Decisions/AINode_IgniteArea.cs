using System;
using System.Collections;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
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
    /// <b>Contra <c>AINode_ExecuteTelegraph</c>:</b> ése cobra el golpe y se va. Este deja el piso
    /// encendido, y el grueso del daño lo cobran las casillas con sus dos números propios
    /// (<c>EnterDamage</c> por casilla pisada, <c>TurnStartDamage</c> por arrancar el turno adentro).
    /// El <c>Damage</c> de la marca, si viene con algo, se cobra igual en el momento de prender — ver
    /// <c>ChargeOnIgnition</c>. Por eso el jefe puede quemar sin tocar al jugador y el castigo llega
    /// igual.
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
    /// <b>Este nodo no apaga las bandas anteriores</b>, así que la relación entre la duración y el
    /// intervalo entre igniciones es toda la diferencia entre un fuego que se rodea y un piso que se
    /// achica. Duración igual al intervalo: cada banda se apaga justo cuando nace la siguiente y
    /// nunca conviven dos. Por encima: se apilan ronda a ronda hasta que no queda dónde plantarse.
    /// Cuál de las dos se quiere es una decisión de diseño por jefe, no un default de este nodo.
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

            // El golpe de la ignicion, antes de plantar. La casilla cobra por pisarla y por
            // arrancar el turno adentro, pero no por prenderse debajo de quien ya estaba parado
            // ahi: sin este paso, prender el paño entero encima del jugador no le hace nada hasta
            // su proximo turno, y el momento mas grande de la pelea pasa sin acuse de recibo.
            ChargeOnIgnition(context, area);

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

        /// <summary>
        /// Cobra el <c>Damage</c> de la marca consumida a quien este dentro del area.
        /// </summary>
        /// <remarks>
        /// <para>
        /// El numero lo trae la marca, no este nodo: <see cref="AINode_TelegraphMark.Damage"/> ya
        /// viaja dentro de <see cref="ThreatenedArea"/>, asi que cada ignicion decide si pega o no
        /// desde el paso que la telegrafio. Una banda que se marco un turno antes puede dejarlo en
        /// 0 --el jugador tuvo su turno para salirse-- y una que marca y prende en el mismo tick
        /// puede cobrar, porque nadie tuvo la chance de moverse.
        /// </para>
        /// <para>
        /// Solo el jugador. <c>AINode_ExecuteTelegraph</c> ademas rompe el cofre que caiga adentro
        /// (GDD §22); esto no lo hace porque el fuego que planta ya cobra por su cuenta a todo lo
        /// que pise o arranque el turno encima, y sumar el cofre aca lo cobraria dos veces.
        /// </para>
        /// </remarks>
        private static void ChargeOnIgnition(AIContext context, ThreatenedArea area)
        {
            if (area.Damage <= 0 || context.DamagePipeline == null) return;

            var grid = context.Grid;
            if (grid == null) return;
            if (!grid.TryGetPosition(context.PlayerGuid, out var playerCoord)) return;
            if (!area.Contains(playerCoord)) return;

            context.DamagePipeline.Resolve(new DamageContext
            {
                SourceId = context.SelfGuid,
                TargetId = context.PlayerGuid,
                BaseDamage = area.Damage,
                Kind = area.Kind,
            });
        }

        private static void ClearOverlay(AIContext context)
        {
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(context.SelfGuid);
        }
    }
}
