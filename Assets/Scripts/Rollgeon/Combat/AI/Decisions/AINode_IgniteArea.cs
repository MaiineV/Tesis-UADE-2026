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
    /// pendiente y planta <see cref="Definition"/> encima.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Consume la marca en vez de recalcular la forma: la banda está anclada en el jefe y apunta al
    /// jugador, así que recalcularla en el turno de la ignición la movería y el fuego caería donde el
    /// jugador no lo vio venir.
    /// </para>
    /// <para>
    /// <see cref="DurationRounds"/> = 0 significa PERMANENTE en <see cref="ISpecialTileService.Place"/>,
    /// no "sin duración", y un campo nuevo nace en 0 en todo <c>ED_Boss_*.asset</c> ya serializado
    /// (Odin no corre field initializers al deserializar): por eso un 0 se cae al
    /// <c>DefaultDurationRounds</c> del SO en vez de pasarse al servicio.
    /// </para>
    /// <para>
    /// Las duraciones piden una ronda más de lo que dicen: el fuego nace en el turno del jefe y el
    /// jugador tiene el primer turno de cada ronda (CNF-006), así que "arde N rondas" se autora como
    /// <c>N + 1</c>.
    /// </para>
    /// <para>
    /// Una casilla es un fuego: planta sólo donde todavía no arde la misma definición (ver
    /// <c>AlreadyBurning</c>), porque el motor de triggers dispara una vez por instancia y una casilla
    /// cubierta por dos cobra dos veces.
    /// </para>
    /// <para>
    /// Con <see cref="AnnounceTurns"/> en 0 la marca se consume en el mismo tick en que se levantó y,
    /// sin yield entre el <c>Show</c> del telegraph y el <c>Clear</c> de acá, el overlay del aviso no
    /// se dibuja nunca. Con 1 o más el aviso sobrevive el turno, y entonces la ignición tiene que ir
    /// suelta —sin <c>AINode_Once</c> ni gate que la saltee— porque cuenta sus propias activaciones y
    /// saltearla deja la marca pendiente para siempre; el aviso, en cambio, tiene que salir de un paso
    /// que se dispara una vez por ciclo, ya que el servicio guarda una marca por fuente y re-marcar
    /// antes de detonar reemplaza la que el jugador venía leyendo.
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

        [Tooltip("Canal de la marca a consumir. Vacío = la marca del propio jefe. " +
                 "Tiene que coincidir con el ChannelId del AINode_TelegraphMark que la levanta.")]
        public string ChannelId;

        [Tooltip("Turnos que la marca se queda avisada antes de prender. 0 = marca y prende en el " +
                 "mismo tick. 1 = el turno del aviso deja el overlay puesto y prende al turno " +
                 "siguiente.")]
        [MinValue(0)]
        public int AnnounceTurns;

        [Tooltip("Apaga las instancias PROPIAS de esta definición que el área nueva tapa por " +
                 "completo, para que el terreno compartido arranque con el reloj nuevo. Off = la " +
                 "instancia vieja se queda con su reloj y el área nueva sólo prende lo que no ardía.")]
        public bool RetireFullyReplaced;

        [Tooltip("Devuelve Failed cuando la ignición no tenía NADA nuevo que prender, para que el " +
                 "Selector que envuelve al paso pueda hacer otra cosa. Off = Succeeded. El aviso al " +
                 "log sale igual en los dos casos.")]
        public bool FailWhenNothingToBurn;

        /// <summary>
        /// Turnos que la marca lleva avisada sin prender. <c>[NonSerialized]</c> para que viva en la
        /// copia runtime del árbol y no en el asset: una pelea nueva arranca sin aviso a medias.
        /// </summary>
        [NonSerialized] private int _turnsAnnounced;

        public override string NodeName => Definition == null
            ? "Ignite Area (sin definición)"
            : $"Ignite Area ({Definition.name})";

        /// <summary>
        /// Camino síncrono (EditMode / escenas sin <c>CoroutineHost</c>). No hay presentación que
        /// esperar: las casillas traen su propio visual y VFX.
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

            // Se resuelve ANTES de consumir: consumir primero deja el aviso cobrado, el overlay
            // apagado y el piso sin prender.
            if (!ServiceLocator.TryGetService<ISpecialTileService>(out var special) || special == null)
            {
                Debug.LogError("[AINode_IgniteArea] ISpecialTileService no registrado. Agrega " +
                               "SpecialTileServiceBootstrap a ServiceBootstrap.ExtraServices.");
                return AIResult.Failed;
            }

            var source = AINode_TelegraphMark.SourceKey(context.SelfGuid, ChannelId);

            // Sin marca pendiente no es un fallo: el primer turno del jefe telegrafía y todavía no
            // hay nada que encender. Failed acá abortaría la Sequence del árbol.
            if (!threat.HasPending(source))
            {
                _turnsAnnounced = 0;
                return AIResult.Succeeded;
            }

            if (_turnsAnnounced < AnnounceTurns)
            {
                _turnsAnnounced++;

                // Se sale sin consumir y sin tocar el overlay: la marca y su dibujo son el aviso, y
                // el aviso tiene que sobrevivir el turno.
                return AIResult.Succeeded;
            }
            _turnsAnnounced = 0;

            if (!threat.TryConsume(source, out var area)) return AIResult.Succeeded;

            ClearOverlay(source);

            // El golpe de la ignicion, antes de plantar: la casilla cobra por pisarla y por arrancar
            // el turno adentro, pero no por prenderse debajo de quien ya estaba parado ahi.
            ChargeOnIgnition(context, area);

            var tiles = area.Tiles;
            if (tiles == null || tiles.Count == 0) return AIResult.Succeeded;

            // Primer filtro, la sala: la forma telegrafiada pudo marcar casillas que el grafo
            // horneado no tiene, y plantar fuera de la grilla deja instancias que nadie puede pisar
            // ni ver expirar.
            var inRoom = new List<GridCoord>(tiles.Count);
            var grid = context.Grid;
            if (grid == null) ServiceLocator.TryGetService<IGridManager>(out grid);
            foreach (var coord in tiles)
            {
                if (grid != null && !grid.IsWalkable(coord)) continue;
                inRoom.Add(coord);
            }

            if (RetireFullyReplaced) RetireReplaced(special, context.SelfGuid, inRoom);

            // El segundo es el solape: una casilla, un fuego.
            var burning = AlreadyBurning(special, inRoom);
            var placeable = new List<GridCoord>(inRoom.Count);
            foreach (var coord in inRoom)
            {
                if (burning.Contains(coord)) continue;
                placeable.Add(coord);
            }
            if (placeable.Count == 0) return NothingToBurn(tiles.Count, inRoom.Count);

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
        /// La ignición consumió la marca y no plantó nada. Avisa al log y decide con qué resultado
        /// salir.
        /// </summary>
        /// <remarks>
        /// El default es <c>Succeeded</c> porque el nodo también se monta desnudo dentro de una
        /// Sequence, donde un <c>Failed</c> la corta y deja a <c>AINode_Once</c> sin latchear —el
        /// evento de cambio de fase se volvería a emitir cada turno—; el <c>Failed</c> se prende sólo
        /// donde hay un Selector que lo absorba.
        /// </remarks>
        private AIResult NothingToBurn(int markedCount, int inRoomCount)
        {
            string reason = inRoomCount == 0
                ? $"ninguna de las {markedCount} casillas marcadas existe en la sala"
                : $"las {inRoomCount} casillas de la sala ya ardían con {Definition.name}";

            Debug.LogWarning(
                $"[AINode_IgniteArea] Marca consumida sin plantar nada: {reason}. El turno de quema " +
                "se va en blanco (ni fuego, ni movimiento, ni ataque), así que el jefe se ve " +
                $"congelado. Resultado: {(FailWhenNothingToBurn ? "Failed" : "Succeeded")}.");

            return FailWhenNothingToBurn ? AIResult.Failed : AIResult.Succeeded;
        }

        /// <summary>
        /// Retira las instancias de <see cref="Definition"/> plantadas por <paramref name="owner"/>
        /// que <paramref name="area"/> reemplaza por completo — las que no cubren ni una casilla
        /// fuera de ella.
        /// </summary>
        /// <remarks>
        /// Se retira en vez de plantar encima porque dos instancias sobre una casilla cobran dos veces
        /// (<c>ResolveStand</c> y <c>ResolveEntries</c> disparan una por instancia y <c>Place</c> no
        /// valida solapamiento). Sólo las que quedan enteras adentro: una que asoma afuera conserva su
        /// reloj, o el corredor por el que el jefe huye quedaría encendido para siempre.
        /// </remarks>
        private void RetireReplaced(ISpecialTileService special, Guid owner, List<GridCoord> area)
        {
            if (area.Count == 0) return;

            var covered = new HashSet<GridCoord>(area);

            // ActiveInstances viene materializado, así que Remove a mitad de la enumeración es seguro.
            foreach (var existing in special.ActiveInstances())
            {
                if (existing.Definition != Definition || existing.Coords == null) continue;

                // Sólo las suyas: apagar fuego que plantó la sala u otra entidad no es asunto de
                // este paso.
                if (existing.OwnerGuid != owner) continue;

                bool intersects = false, escapes = false;
                foreach (var coord in existing.Coords)
                {
                    if (covered.Contains(coord)) intersects = true;
                    else escapes = true;
                }

                if (intersects && !escapes) special.Remove(existing.InstanceId);
            }
        }

        /// <summary>
        /// Las casillas de <paramref name="area"/> que ya están cubiertas por una instancia de
        /// <see cref="Definition"/>.
        /// </summary>
        /// <remarks>
        /// Se enumera instancia por instancia y no con <c>TryGetTileAt</c>: ése devuelve la PRIMERA
        /// instancia que cubre la casilla, sea de la definición que sea, así que una casilla que ya
        /// ardía contesta "libre" cuando el hielo o el veneno de la sala se enumeran antes y el fuego
        /// se replanta encima cobrando doble. El filtro es sólo contra la misma definición: dos
        /// sustancias distintas en una casilla son dos efectos, no uno duplicado.
        /// </remarks>
        private HashSet<GridCoord> AlreadyBurning(ISpecialTileService special, List<GridCoord> area)
        {
            var burning = new HashSet<GridCoord>();
            if (area.Count == 0) return burning;

            var covered = new HashSet<GridCoord>(area);
            foreach (var existing in special.ActiveInstances())
            {
                if (existing.Definition != Definition || existing.Coords == null) continue;

                foreach (var coord in existing.Coords)
                    if (covered.Contains(coord)) burning.Add(coord);
            }

            return burning;
        }

        /// <summary>
        /// Cobra el <c>Damage</c> de la marca consumida a quien este dentro del area.
        /// </summary>
        /// <remarks>
        /// El numero lo trae la marca y no este nodo: <see cref="AINode_TelegraphMark.Damage"/> ya
        /// viaja dentro de <see cref="ThreatenedArea"/>, asi que cada ignicion decide si pega desde el
        /// paso que la telegrafio. Solo al jugador: el fuego que planta ya cobra por su cuenta a todo
        /// lo que pise o arranque el turno encima.
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

        /// <remarks>
        /// La fuente y no <c>context.SelfGuid</c>: con un canal declarado el overlay del aviso se
        /// pintó bajo el guid derivado, y limpiar el del jefe apagaría el telegraph de otro paso.
        /// </remarks>
        private static void ClearOverlay(Guid source)
        {
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(source);
        }
    }
}
