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
    /// <b>Las bandas se acumulan en superficie pero nunca se solapan: una casilla es un fuego.</b>
    /// Planta sólo donde todavía no arde la misma definición (ver <c>AlreadyBurning</c>) — sin ese
    /// filtro, una casilla cubierta por dos instancias cobra dos veces, porque el motor de triggers
    /// dispara una vez por instancia. Con <see cref="RetireFullyReplaced"/> además apaga la
    /// instancia anterior que quede <b>entera</b> adentro del área nueva.
    /// </para>
    /// <para>
    /// <b>Con <see cref="AnnounceTurns"/> el aviso dura más de un frame.</b> Con 0 —el default— este
    /// nodo consume la marca en el mismo tick en que se levantó, y como no hay yield entre el
    /// <c>Show</c> del telegraph y el <c>Clear</c> de acá, el overlay de un aviso marcado y prendido
    /// en el mismo turno <b>no se dibuja nunca</b>. Con 1, el turno del aviso sale sin consumir: la
    /// marca queda pendiente y su overlay puesto, y la ignición cae al turno siguiente.
    /// </para>
    /// <para>
    /// <b>Y entonces este paso tiene que poder tickear el turno siguiente.</b> El nodo cuenta sus
    /// propias activaciones, así que un <c>AINode_Once</c> o un gate que lo saltee después del turno
    /// del aviso deja la marca pendiente para siempre: el aviso se queda pintado y nunca prende. La
    /// marca se levanta donde corresponda (ahí sí puede ir latcheada), pero la ignición va suelta y
    /// se ejecuta todos los turnos — sin marca pendiente es un no-op barato.
    /// </para>
    /// <para>
    /// <b>Y el canal del aviso no puede re-marcarse todos los turnos.</b> El servicio guarda una
    /// marca por fuente y la sobrescribe, así que un aviso levantado de nuevo antes de detonar
    /// reemplaza al que el jugador venía leyendo y la ignición cae sobre el área nueva, marcada
    /// recién este tick. Con 0 eso es lo de siempre; con 1 o más el aviso tiene que salir de un paso
    /// que se dispara una vez por ciclo, no uno por turno.
    /// </para>
    /// <para>
    /// <b>Todo lo que no sea el comportamiento de siempre es opt-in.</b> Este nodo lo monta cada
    /// jefe que prende piso, así que <see cref="RetireFullyReplaced"/>,
    /// <see cref="AnnounceTurns"/>, <see cref="ChannelId"/> y
    /// <see cref="FailWhenNothingToBurn"/> arrancan todos en el valor que reproduce exactamente lo
    /// que hacía antes. No es prolijidad: Odin no corre field initializers al deserializar, así que
    /// el default de cada campo nuevo es el que ya tienen todos los <c>ED_Boss_*.asset</c>
    /// serializados. Un default distinto de "como siempre" cambiaría en silencio a jefes que nadie
    /// tocó.
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

        [Tooltip("Canal de la marca a consumir. Vacío = la marca del propio jefe, como siempre. " +
                 "Tiene que coincidir con el ChannelId del AINode_TelegraphMark que la levanta.")]
        public string ChannelId;

        [Tooltip("Turnos que la marca se queda avisada antes de prender. 0 = como siempre: marca y " +
                 "prende en el mismo tick. 1 = el turno del aviso deja el overlay puesto y prende " +
                 "al turno siguiente.")]
        [MinValue(0)]
        public int AnnounceTurns;

        [Tooltip("Apaga las instancias PROPIAS de esta definición que el área nueva tapa por " +
                 "completo, para que el terreno compartido arranque con el reloj nuevo. Off = como " +
                 "siempre: la instancia vieja se queda con su reloj y el área nueva sólo prende lo " +
                 "que no ardía.")]
        public bool RetireFullyReplaced;

        [Tooltip("Devuelve Failed cuando la ignición no tenía NADA nuevo que prender, para que el " +
                 "Selector que envuelve al paso pueda hacer otra cosa. Off = como siempre: " +
                 "Succeeded. El aviso al log sale igual en los dos casos.")]
        public bool FailWhenNothingToBurn;

        /// <summary>
        /// Turnos que la marca lleva avisada sin prender. <c>[NonSerialized]</c>: vive en la copia
        /// runtime del árbol (<c>EnemyDataSO.CreateRuntimeAIRoot</c>) y no en el asset, así que una
        /// pelea nueva arranca sin aviso a medias. Mismo patrón que <see cref="AINode_Alternate"/> y
        /// <see cref="AINode_CajeroCoinVault"/>.
        /// </summary>
        [NonSerialized] private int _turnsAnnounced;

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

            // Se resuelve ANTES de consumir, con el resto de los servicios: sin él no hay con qué
            // plantar, y consumir primero dejaba el aviso cobrado, el overlay apagado y el piso sin
            // prender — la marca desaparecía sin dejar fuego.
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
                // el aviso tiene que sobrevivir el turno. Succeeded porque el paso hizo lo que le
                // toca este turno — un Failed cortaría la Sequence del jefe por avisar.
                return AIResult.Succeeded;
            }
            _turnsAnnounced = 0;

            if (!threat.TryConsume(source, out var area)) return AIResult.Succeeded;

            ClearOverlay(source);

            // El golpe de la ignicion, antes de plantar. La casilla cobra por pisarla y por
            // arrancar el turno adentro, pero no por prenderse debajo de quien ya estaba parado
            // ahi: sin este paso, prender el paño entero encima del jugador no le hace nada hasta
            // su proximo turno, y el momento mas grande de la pelea pasa sin acuse de recibo.
            ChargeOnIgnition(context, area);

            var tiles = area.Tiles;
            if (tiles == null || tiles.Count == 0) return AIResult.Succeeded;

            // Dos filtros. El primero es la sala: la forma telegrafiada pudo marcar casillas que el
            // grafo horneado no tiene, y plantar fuera de la grilla deja instancias que nadie puede
            // pisar ni ver expirar.
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
        /// <para>
        /// <b>El aviso es incondicional porque el silencio era el bug.</b> Este camino se come el
        /// turno de quema entero —ni fuego, ni movimiento, ni ataque— y hasta ahora salía por un
        /// <c>Succeeded</c> mudo, así que desde afuera un turno absorbido y un turno resuelto se
        /// veían idénticos. Cuando el paño ya arde entero (el Pleno prende toda la sala por varias
        /// rondas y las igniciones vienen cada dos turnos) son <b>dos beats seguidos</b> los que
        /// desaparecen sin una línea, y eso se lee como un jefe congelado.
        /// </para>
        /// <para>
        /// <b>El resultado, en cambio, es opt-in.</b> <c>Failed</c> es lo honesto y es lo que
        /// permite que un <c>Selector[paso, otra cosa, Wait]</c> haga algo en vez de perder el beat.
        /// Pero este nodo también se monta <b>desnudo</b> dentro de una Sequence —el setup del Pleno
        /// va en <c>Once(Sequence[fase, teleport, marca, ignición])</c>—, y ahí un Failed corta la
        /// Sequence y, peor, deja a <c>AINode_Once</c> sin latchear: el evento de cambio de fase se
        /// volvería a emitir cada turno. Por eso el default sigue siendo <c>Succeeded</c> y el
        /// Failed se prende sólo donde hay un Selector que lo absorba.
        /// </para>
        /// </remarks>
        private AIResult NothingToBurn(int markedCount, int inRoomCount)
        {
            // Dos causas distintas, dos mensajes: "la sala no tiene esas casillas" apunta a la forma
            // o al grafo, "ya ardía todo" apunta al ritmo de las igniciones. Un solo texto para las
            // dos mandaría a mirar el lugar equivocado.
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
        /// <para>
        /// <b>Por qué el skip solo no alcanza.</b> Saltear la casilla compartida es correcto mientras
        /// la instancia vieja siga cubriendo terreno propio. Cuando queda entera adentro del área
        /// nueva ya no aporta superficie: lo único que aporta es su reloj, que es el más viejo y por
        /// lo tanto el más corto. Con el skip a secas, la banda recién avisada hereda los turnos que
        /// le quedaban a la anterior y se apaga antes de arder lo que prometió. Ese es el caso normal
        /// de este jefe, no un borde: enciende turno por medio y la banda dura una ronda más que el
        /// intervalo, así que la de dos turnos atrás llega viva a la ignición siguiente, se come la
        /// nueva casi entera y expira en el wrap — el paño queda apagado y el turno de quema no
        /// muestra nada.
        /// </para>
        /// <para>
        /// <b>Retirar, no plantar encima.</b> Dos instancias sobre una casilla cobran <b>dos
        /// veces</b>: <c>ResolveStand</c> y <c>ResolveEntries</c> recorren todas las instancias que
        /// la contienen y disparan cada una, y <c>Place</c> no valida solapamiento (a diferencia de
        /// <c>CreateRuntime</c>). Sacar la vieja antes de plantar mantiene el invariante intacto —una
        /// casilla, un fuego— en vez de comprarse una ronda de doble cobro.
        /// </para>
        /// <para>
        /// <b>Sólo lo que queda adentro.</b> Una instancia que asoma afuera se conserva intacta, con
        /// su reloj: refrescar terreno que el área nueva no toca es lo que dejaría el corredor por el
        /// que el jefe huye encendido para siempre.
        /// </para>
        /// <para>
        /// <b>Por qué está detrás de <see cref="RetireFullyReplaced"/> y no siempre puesto.</b>
        /// Apagar fuego que el jugador ya tiene en pantalla es una decisión de la pelea del jefe que
        /// releva bandas, no una regla del nodo: cualquier otro que lo monte —o una sala autorada—
        /// vería desaparecer instancias que nadie pidió apagar. El flag deja el relevo donde se
        /// autoró y a los demás exactamente como estaban.
        /// </para>
        /// </remarks>
        private void RetireReplaced(ISpecialTileService special, Guid owner, List<GridCoord> area)
        {
            if (area.Count == 0) return;

            var covered = new HashSet<GridCoord>(area);

            // ActiveInstances viene materializado, así que Remove a mitad de la enumeración es seguro.
            foreach (var existing in special.ActiveInstances())
            {
                if (existing.Definition != Definition || existing.Coords == null) continue;

                // Sólo las suyas. Reemplazar una banda propia es contabilidad de este jefe; apagar
                // fuego que plantó la sala u otra entidad no es asunto de este paso, y el filtro de
                // solape de abajo igual respeta lo ajeno.
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
        /// <para>
        /// Dos áreas del mismo jefe se pisan seguido — la banda apunta al jugador, así que la
        /// siguiente suele caer encima de la anterior. Y una casilla cubierta por dos instancias
        /// cobra <b>dos veces</b> (ver <see cref="RetireReplaced"/>). Se evita plantando
        /// únicamente sobre lo que todavía no arde.
        /// </para>
        /// <para>
        /// <b>Sólo contra la misma definición.</b> Dos sustancias distintas conviviendo en una
        /// casilla es legítimo — hielo encima de veneno son dos efectos, no uno duplicado. Filtrar
        /// por cualquier casilla especial convertiría esto en una regla global sobre un servicio
        /// que usan también las salas autoradas.
        /// </para>
        /// <para>
        /// <b>Se pregunta instancia por instancia y no con <c>TryGetTileAt</c>.</b> Ése devuelve la
        /// PRIMERA instancia que cubre la casilla, sea de la definición que sea y en un orden de
        /// diccionario que nadie garantiza: una casilla que sí ardía contestaba "libre" cuando el
        /// hielo o el veneno de la sala se enumeraban antes, y ahí el fuego se replantaba encima y la
        /// casilla pasaba a cobrar doble — justo lo que este filtro existe para evitar.
        /// </para>
        /// <para>
        /// La instancia que sobrevive al retiro <b>conserva su propio reloj</b> en las casillas
        /// compartidas: una banda que vuelve a pasar por donde ya ardía no la renueva.
        /// </para>
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
        /// <para>
        /// El numero lo trae la marca, no este nodo: <see cref="AINode_TelegraphMark.Damage"/> ya
        /// viaja dentro de <see cref="ThreatenedArea"/>, asi que cada ignicion decide si pega o no
        /// desde el paso que la telegrafio. Una banda que se marco un turno antes puede dejarlo en
        /// 0 --el jugador tuvo su turno para salirse-- y una que marca y prende en el mismo tick
        /// puede cobrar, porque nadie tuvo la chance de moverse.
        /// </para>
        /// <para>
        /// <b>Subir <see cref="AnnounceTurns"/> mueve ese numero al otro lado de la cuenta.</b> Un
        /// aviso que se sostiene un turno le da al jugador exactamente la ventana que justificaba
        /// cobrar en el mismo tick, asi que el <c>Damage</c> de la marca deja de ser "no tuviste
        /// tiempo" y pasa a ser un golpe extra sobre el que se quedo adentro. Es una decision de
        /// autoria, no del nodo: quien suba AnnounceTurns tiene que revisar el Damage de la marca
        /// que consume.
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

        /// <remarks>
        /// La fuente y no <c>context.SelfGuid</c>: con un canal declarado, el overlay del aviso se
        /// pintó bajo el guid derivado, y limpiar el del jefe apagaría el telegraph de otro paso y
        /// dejaría este dibujado sobre el fuego que acaba de plantar.
        /// </remarks>
        private static void ClearOverlay(Guid source)
        {
            if (ServiceLocator.TryGetService<IThreatOverlayService>(out var overlay) && overlay != null)
                overlay.Clear(source);
        }
    }
}
