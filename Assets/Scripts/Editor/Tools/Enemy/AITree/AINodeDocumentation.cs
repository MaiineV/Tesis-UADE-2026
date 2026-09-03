using System;
using System.Collections.Generic;
using Rollgeon.Combat.AI.Bosses.Bandida;
using Rollgeon.Combat.AI.Bosses.Croupier;
using Rollgeon.Combat.AI.Decisions;

namespace Rollgeon.Editor.Tools.Enemy.AITree
{
    /// <summary>
    /// Catálogo de descripciones por tipo de nodo del AI tree, mostradas en el side panel
    /// del inspector. Cuando un nodo nuevo se agrega al runtime, sumar su entry acá para
    /// que el editor le muestre help al designer.
    /// </summary>
    public static class AINodeDocumentation
    {
        private static readonly Dictionary<Type, string> _descriptions = new Dictionary<Type, string>
        {
            [typeof(AINode_Sequence)] =
                "Secuencia (AND): ejecuta los Children en orden. Si alguno retorna Failed, " +
                "corta ahí y devuelve Failed sin tocar el resto. Si todos retornan Succeeded, " +
                "devuelve Succeeded.\n\n" +
                "Usalo cuando una serie de pasos tiene que pasar todos en orden " +
                "(ej: 'moverte → atacar → recuperar energía').",

            [typeof(AINode_Selector)] =
                "Selector (OR): prueba los Children en orden hasta que uno retorne Succeeded — " +
                "devuelve Succeeded sin tocar los demás. Si todos fallan, devuelve Failed.\n\n" +
                "Usalo para fallback chains: 'intentá X; si no podés, intentá Y; si no, hacé Z'.",

            [typeof(AINode_If)] =
                "If: ramifica según la lista AND-evaluada de Conditions (PreConditions). " +
                "Si todas pasan, ejecuta el branch Then; si alguna falla, ejecuta Else.\n\n" +
                "TargetSelector decide a quién apuntan las Conditions (default: Always Player). " +
                "Lista de Conditions vacía = AND-empty = pasa siempre (toma Then).",

            [typeof(AINode_While)] =
                "While: loop. Re-ejecuta el Body mientras todas las Conditions sean true, " +
                "hasta MaxIterations.\n\n" +
                "• Conditions vacías → permisivo (true) → MaxIterations es el único corte.\n" +
                "• Body retorna Failed → propaga Failed inmediatamente.\n" +
                "• Cap alcanzado → Failed + warning log (señal de bug — condition que nunca flips).",

            [typeof(AINode_Random)] =
                "Random (weighted): elige un hijo al azar entre los Options usando Weight " +
                "como probabilidad relativa (mayor weight = más probable). Devuelve el " +
                "resultado del child elegido.\n\n" +
                "RNG inyectado vía AIContext.Rng — los tests pueden seedearlo para determinismo.",

            [typeof(AINode_Alternate)] =
                "Alternate (round-robin): rota entre los Children en orden fijo, uno por " +
                "turno (0, 1, 2, ..., 0, 1, 2, ...). A diferencia de Random, garantiza que " +
                "nunca se repite el mismo hijo dos veces seguidas.\n\n" +
                "Usalo cuando necesitás que el enemigo alterne dos comportamientos (ej. " +
                "'ataque especial' / 'golpe normal') sin depender del azar. El índice vive " +
                "en la instancia runtime (se resetea a 0 en cada pelea nueva).",

            [typeof(AINode_Move)] =
                "Move Toward Target: mueve al enemy hasta MaxSteps casillas (AIIntReader) hacia " +
                "el target del TargetSelector (null = player), manteniendo DesiredRange casillas " +
                "de distancia Manhattan. Si DesiredRange es null usa el legacy StopAdjacent " +
                "(true → 1, false → 0).\n\n" +
                "Si Retreat es true y está más cerca que DesiredRange, retrocede (kite); si es " +
                "false, demasiado cerca = no se mueve. Devuelve Succeeded si se movió, Failed si " +
                "ya está en la banda o no hay tile mejor. Setea AIContext.PendingWait con la animación.",

            [typeof(AINode_KeepDistance)] =
                "Keep Distance (kiting): mueve al enemy hasta MaxSteps casillas para mantener " +
                "IdealDistance del player. Si ya está a la distancia ideal, no se mueve.\n\n" +
                "Usalo para enemigos ranged/casters que evitan el melee.",

            [typeof(AINode_Wait)] =
                "Wait: no-op. Siempre devuelve Succeeded sin hacer nada.\n\n" +
                "Usalo como placeholder durante prototipado, o como pad de turno cuando un " +
                "branch no debería actuar pero tampoco fallar.",

            [typeof(AINode_TelegraphMark)] =
                "Telegraph Mark: marca un área (sin dañar este turno) para que " +
                "ExecuteTelegraph la detone el turno siguiente del enemigo.\n\n" +
                "Shape=DirectionalBand: el área sale del propio enemigo (no del jugador) — " +
                "banda perpendicular de 2·Size+1 casillas, centrada en su coordenada, " +
                "extendida Depth casillas en la dirección cardinal dominante hacia el " +
                "jugador.\n\n" +
                "Shape=ScatteredSquares: Count cuadrados de Size×Size, anclados al azar " +
                "(AIContext.Rng) en el 50% central de la sala — ni el jugador ni el " +
                "enemigo son el centro, y nunca aparecen pegados a las paredes. Prioriza " +
                "que los cuadrados no se toquen ni se solapen entre sí (degrada el gap si " +
                "la sala no da lugar). Requiere una sala con bounds reales.\n\n" +
                "Shape=SquareAroundSelf: mismo cálculo que Square (2·Size+1), pero centrado " +
                "en la coordenada del propio enemigo en vez de la del jugador.\n\n" +
                "Las demás shapes (Square/Row/Column/HalfRoom) se centran en el jugador " +
                "como siempre.",

            [typeof(AINode_Behavior)] =
                "Behavior: ejecuta un EnemyActionBehavior — la unidad reusable de combate " +
                "(ataque, heal, buff, etc.).\n\n" +
                "El TargetSelector del Behavior resuelve a quién apuntan los Effects. Cada " +
                "Effect tiene su propia lista de PreConditions independiente — el behavior " +
                "ejecuta solo los Effects cuyas PreConditions pasan.",

            [typeof(AINode_ActivateRainHazard)] =
                "Activate Rain Hazard: activa RainHazardService (idempotente) — una amenaza " +
                "ambiental independiente del boss (fuente propia) que, una vez activa, marca " +
                "y detona zonas erráticas en su propio ciclo de rondas, en paralelo a lo que " +
                "esté haciendo el boss.\n\n" +
                "Pensado para envolver en If(PcOwnerHpBelow) → Once(...), igual que el " +
                "trigger de refuerzos — dispara una sola vez al cruzar el umbral de HP.",

            [typeof(AINode_SpawnReinforcements)] =
                "Spawn Reinforcements: spawnea Count copias de EnemyToSpawn en tiles del " +
                "borde de la sala (perímetro del bounding box, walkable y libres) y los " +
                "suma a la ronda de combate en curso.\n\n" +
                "Los refuerzos van al FINAL de la ronda actual — quien ya estaba en cola " +
                "(player/boss) termina su turno normal antes; de ahí en adelante rotan de " +
                "forma regular y estable, como cualquier otro participante.\n\n" +
                "Pensado para envolver en If(PcOwnerHpBelow) → Once(...) — dispara una sola " +
                "vez al cruzar el umbral de HP, igual que otros triggers de fase.",

            [typeof(AINode_SpinWheel)] =
                "Spin Wheel (Croupier): canta 1 número del 1 al 6 (2 en fase 2) y lo deja " +
                "flotando sobre el jefe. Ese número es dos cosas a la vez — el sector del " +
                "paño que va a caer el turno que viene y el dado de la bolsa que se " +
                "confisca — así que este nodo sólo lo elige: marcar y confiscar son los dos " +
                "nodos siguientes, que leen de la rueda.\n\n" +
                "Abre el windup, y mientras dura pasan dos cosas distintas. Pegarle al jefe " +
                "le cobra RetaliationDamage al atacante: cualquier golpe no letal que haya " +
                "hecho daño o comido escudo, sin importar el número ni si la rueda está " +
                "trucada. Y cerrar el turno parado DENTRO de un sector cantado corre ese " +
                "número un lugar — eso sí se apaga con la rueda trucada (fase 2).\n\n" +
                "AvoidRepeatingLastNumber saca el número del turno anterior del pool (no " +
                "re-sortea), así el paño se mueve todos los turnos sin sesgar el azar.",

            [typeof(AINode_MarkSungSectors)] =
                "Mark Sung Sectors (Croupier): marca como amenazado el sector de cada número " +
                "cantado, para que detone en el próximo turno del jefe. No hace daño este " +
                "turno.\n\n" +
                "Es el TelegraphMark de este jefe: existe aparte porque el área sale de un " +
                "número decidido en runtime (no de un Size autorado) y porque en fase 2 hay " +
                "DOS áreas simultáneas, marcadas bajo fuentes distintas para que se resuelvan " +
                "por separado — el jugador parado en la columna de costura, donde los dos " +
                "sectores se pisan, cobra los dos golpes (2 × SectorDamagePhase2).\n\n" +
                "El daño se congela al marcar: un sector marcado en fase 1 detona por " +
                "SectorDamage aunque el jefe cruce el umbral en el medio.",

            [typeof(AINode_DetonateSungSectors)] =
                "Detonate Sung Sectors (Croupier): detona los sectores cantados el turno " +
                "pasado y cierra el windup (pegarle ya no mueve la rueda hasta que vuelva a " +
                "cantar).\n\n" +
                "Va PRIMERO en el Sequence raíz, como ExecuteTelegraph, y siempre devuelve " +
                "Succeeded: 'no había nada marcado' (turno 1) o 'el jugador se fue del " +
                "sector' son resoluciones válidas, no fallos que deban cortarle el turno al " +
                "jefe.",

            [typeof(AINode_IgniteDetonatedSectors)] =
                "Ignite Detonated Sectors (Croupier): prende fuego el/los sector(es) que " +
                "detonaron en ESTE turno — 6 de daño a quien termine su turno adentro. Mata " +
                "la lectura de que el bloque recién explotado es el lugar más seguro del " +
                "paño.\n\n" +
                "Duración por fase = dos definiciones (Fire / FirePhase2), porque " +
                "HazardService toma DurationRounds del SO. OJO: pedí una ronda MÁS que lo que " +
                "dice la ficha — el fuego nace en el turno del jefe y el jugador juega " +
                "primero en cada ronda, así que DurationRounds=1 expira antes de poder " +
                "tickear. 'Un turno' = 2, 'dos turnos' = 3.\n\n" +
                "BlastConsumesFlame: si el jugador se comió la detonación, el fuego de ese " +
                "sector se saltea su primer tick (SkipNextTick) y el peor caso de la costura " +
                "sigue siendo 24 en vez de 30. Se arma sólo cuando el jugador estaba adentro: " +
                "el flag se consume recién con un tick que hubiera pegado, así que armarlo a " +
                "ciegas se tragaría el primer tick legítimo.",

            [typeof(AINode_SetWheelMode)] =
                "Set Wheel Mode (Croupier): el setup de 'Pleno y color'. Cambia cuántos " +
                "números canta por turno y truca la rueda (pegarle deja de correrla y de " +
                "cobrar Represalia: la fase abarata pegarle, lo que te saca es la palanca).\n\n" +
                "Va envuelto en If(PcOwnerHpBelow 0.5) → Once, al lado del ApplyStatModifier " +
                "que dispara el feedback de fase. El PhaseIndex que setea acá es el que leen " +
                "los nodos con valores por fase (daño de sector, definición de fuego), así " +
                "que hay un único lugar del árbol que decide 'estamos en fase 2'.",

            // --- La Bandida (piso 1) ----------------------------------------------------
            [typeof(AINode_SpawnReels)] =
                "Spawn Reels (La Bandida): mantiene la fila de rodillos. La arma alineada en " +
                "el primer turno —Count casillas consecutivas a un paso del jefe, del lado con " +
                "más tiles libres (Direction fija el lado a mano)—, detecta los rotos y los " +
                "repone en su MISMA ranura a los RespawnDelayTurns turnos del jefe.\n\n" +
                "Al devolver un rodillo rearma la cuenta del jackpot en CountdownOnRespawn: " +
                "reponer y resetear la cuenta son el mismo paso, si no el jackpot dispararía al " +
                "turno siguiente del respawn.\n\n" +
                "NO envolver en Once: se auto-gatea (arma la fila una sola vez) pero necesita " +
                "tickear cada turno para correr los relojes de reposición. Latcheado en Once, " +
                "ningún rodillo vuelve nunca.",

            [typeof(AINode_TickJackpot)] =
                "Tick Jackpot Countdown (La Bandida): baja un turno la cuenta regresiva del " +
                "jackpot (2 → 1 → 0) y la publica para el número gigante sobre la máquina. " +
                "No-op si la cuenta está cancelada.\n\n" +
                "Va suelto en el Sequence raíz, antes del pool de acción, y devuelve siempre " +
                "Succeeded. Quién marca el jackpot al llegar a 0 lo decide el pool vía la " +
                "PreCondition Jackpot Countdown — este nodo solo lleva la cuenta.",

            [typeof(AINode_ResetJackpotCountdown)] =
                "Reset Jackpot Countdown (La Bandida): rearma la cuenta en Value y la vuelve a " +
                "poner a contar.\n\n" +
                "Va inmediatamente después del TelegraphMark del jackpot, en el mismo Sequence: " +
                "la cuenta que dispara se rearma en el acto. Esa asimetría es de diseño — la " +
                "ronda muerta solo la cobra quien rompe un rodillo (la reposición). La pausa es " +
                "el premio de cancelar; tanquear el jackpot no la recibe.",

            [typeof(AINode_LockReel)] =
                "Lock Reel (La Bandida, Fase 2 · HOLD): traba una ranura de la fila. El rodillo " +
                "trabado deja de cancelar la cuenta y se repone con LockedHp de vida (pool " +
                "inagotable: el pipeline de daño no tiene canal de inmunidad), así que quedan " +
                "dos blancos válidos — los dos de la punta, los que están más lejos.\n\n" +
                "One-shot: va dentro del Once del gate de fase. Devuelve Failed si la fila " +
                "todavía no está armada, para que el Once no latchee en falso.",

            [typeof(AINode_SetReelRespawnDelay)] =
                "Set Reel Respawn Delay (La Bandida, Fase 2): pisa el delay de reposición de los " +
                "rodillos. Bajarlo a 1 hace que la cuenta arranque de nuevo cada ronda en vez de " +
                "cada dos.\n\n" +
                "Solo cambia frecuencia: ningún número de daño se mueve.",

            // --- El Cajero (piso 2) ---------------------------------------
            [typeof(AINode_TelegraphMarkGoldScaled)] =
                "Telegraph Mark Gold-Scaled: igual que Telegraph Mark, pero el ancho (Size) y " +
                "el daño salen de la tabla Tiers según el ORO del jugador — es la columna que " +
                "engorda del Cajero.\n\n" +
                "Tiers: cada escalón declara MinGold (umbral inclusive), ColumnSize y Damage. " +
                "Se elige el escalón más alto cuyo MinGold <= oro actual; el orden en que los " +
                "arrastres no importa (se rankean por MinGold).\n\n" +
                "ApplyBribeStepDown: si hay un soborno vigente (ICashierLedgerService), baja un " +
                "escalón el resultado. Sin economía registrada asume oro 0 (escalón más barato) " +
                "en vez de fallar — un jefe que no marca nada es peor que uno que pega flojo.",

            [typeof(AINode_CashierAudit)] =
                "Cashier Audit (arqueo de caja): guarda TaxPercent del oro del jugador en la caja " +
                "del jefe, lo cura por lo guardado con tope MaxHeal, y sube el valor de las fichas " +
                "a ChipValueMultiplierAfterAudit.\n\n" +
                "El oro NO se destruye: vuelve completo al jugador cuando el jefe muere (lo " +
                "devuelve CashierLedgerService al escuchar OnEntityDestroyed). Si el jugador muere " +
                "primero, se pierde.\n\n" +
                "Devuelve Succeeded incluso cobrando 0 (jugador sin oro) para no romper el " +
                "Once → Sequence[Audit, ApplyStatModifier] del gate de fase.",

            [typeof(AINode_CashierDropChips)] =
                "Cashier Drop Chips: suelta Count ficha(s) de MinValue-MaxValue de oro dentro del " +
                "área telegráfica pendiente del propio enemigo (la columna que marcó ESTE turno), " +
                "a MinDistanceFromPlayer-MaxDistanceFromPlayer casillas del jugador.\n\n" +
                "La ficha es un hazard: apuntá Chip a un HazardDefinitionSO con Trigger=OnEnter, " +
                "Damage=0, ConsumeOnTrigger=true y DurationRounds=1. Cobrarla la paga el " +
                "CashierLedgerService cuando el hazard se dispara.\n\n" +
                "Con RequireDamageTaken sólo suelta ficha en turnos en que el enemigo recibió " +
                "daño. Devuelve Failed cuando no hay nada que soltar (no le pegaron, no hay " +
                "columna marcada, no hay casilla válida) ⇒ va SIEMPRE en Selector[DropChips, Wait].",

            [typeof(AINode_IceTrail)] =
                "Ice Trail (Anotador, piso 2): congela las casillas que el boss ACABA de pisar " +
                "en su repliegue. Pisarlas no hace daño: stunea StunTurns turno(s) y derrite esa " +
                "casilla.\n\n" +
                "Va SIEMPRE inmediatamente después del nodo de repliegue (KeepDistance/Move) — " +
                "lee el path real que publicó IMovementService en ese movimiento, así que antes " +
                "del repliegue no tiene nada que congelar.\n\n" +
                "El Hazard tiene que ser una HazardDefinitionSO con Trigger=OnEnter, Damage=0, " +
                "ConsumeOnTrigger=true y DurationRounds=2 (una ronda entera del jugador: con 1 " +
                "la estela expira en el wrap de ronda, antes de que el jugador vuelva a moverse). " +
                "El stun NO lo aplica el hazard: lo aplica IceStunBinder escuchando " +
                "OnHazardTriggered, y solo para las instancias que este nodo publicó.\n\n" +
                "Sin repliegue este turno devuelve Succeeded (no-op transparente): un Failed acá " +
                "abortaría el Sequence del turno y el boss perdería su marca de fila.",

            [typeof(AINode_ShiftComboToNeighbor)] =
                "Shift Combo To Neighbor (Anotador, piso 2): corre el combo que el jugador MÁS " +
                "viene usando (ComboLog, ventana ComboLogWindow) al vecino de la hoja por daño " +
                "base — su Escalera pasa a pagar como Doble Par, o al revés si Direction=Up.\n\n" +
                "Efecto de inicio de turno: va como hijo del Sequence raíz, antes del pool, y no " +
                "consume la acción. Direction=RandomNeighbor sortea el vecino por corrimiento (hay " +
                "corrimientos aprovechables, no solo castigos).\n\n" +
                "Maneja la fase 2 internamente leyendo su propia vida (igual que PromulgateRule con " +
                "su intervalo): bajo Phase2HpThreshold pasa a ShiftsPerTurnPhase2 corrimientos y " +
                "deja de devolverlos — se acumulan hasta el final del combate. En fase 1 'dura 1 " +
                "turno' se implementa como ClearAll + volver a promulgar, porque " +
                "IContractModifierService no tiene expiración por modificador.\n\n" +
                "ImmuneComboIds saca combos del sorteo: combo.generala es la debilidad del jefe y " +
                "la única mano que no depende de la tabla.",

            // ---- La Generala (piso 3) — mano de dados propia -------------------------

            [typeof(AINode_RollHand)] =
                "Roll Hand: tira la mano de dados del propio boss y la corre por el MISMO " +
                "detector de combos que usa el jugador (ComboResolver + ComboCatalogSO). " +
                "Publica el resultado (caras + combo) en IBossDiceHandService; las ramas de " +
                "ataque lo leen con la precondición PcBossHandCombo.\n\n" +
                "SizeSource=AliveAllies tira tantos dados como aliados vivos tenga el boss — " +
                "sus dados SON sus aliados (objetos en el piso), así que romperle uno le borra " +
                "una categoría gratis: Generala pide 5 dados en la tirada, Póker 4. NO metas " +
                "otros enemigos en la arena o van a contar como dados. Fixed = siempre MaxDice.\n\n" +
                "SlowCombos: esos combos se publican 'cantados pero no armados' — ese turno " +
                "nadie marca y el siguiente el nodo los arma SIN re-tirar. Eso es la ronda " +
                "extra de aviso (2 rondas entre la tirada y el impacto).\n\n" +
                "Con rerolls habilitados (AINode_SetHandReroll) re-tira los dados que no " +
                "contribuyen al combo y se queda con la mejor mano por prioridad.",

            [typeof(AINode_SetHandReroll)] =
                "Set Hand Reroll: habilita (o saca) el reroll de la mano de dados del boss — " +
                "cuántas veces re-tira por tirada los dados que no le sirven.\n\n" +
                "Setup de Fase 2: metelo en If(PcOwnerHpBelow) → Once(...). El flag vive en el " +
                "servicio de la mano (run-scoped), así que aplicarlo una vez alcanza.",

            [typeof(AINode_AdoptWeakness)] =
                "Adopt Weakness: le reasigna la debilidad al propio boss al combo que el " +
                "jugador MÁS viene usando — lee IComboLogService y escribe IWeaknessRegistry.\n\n" +
                "Empates: gana el más reciente. El marcador de 'sin combo' del log se ignora. " +
                "Con el log vacío devuelve Succeeded y deja la debilidad como estaba (poné " +
                "FailWhenLogEmpty si preferís que falle).\n\n" +
                "Setup de Fase 2: If(PcOwnerHpBelow) → Once(...).",

            [typeof(AINode_AuxTelegraph)] =
                "Aux Telegraph: telegraph de canal SECUNDARIO. Misma semántica que " +
                "TelegraphMark + ExecuteTelegraph (marco en el turno N, cobro en el N+1), pero " +
                "bajo una fuente propia derivada de ChannelId, así que NO se pisa con el " +
                "telegraph principal del boss.\n\n" +
                "Existe porque IThreatenedAreaService guarda UN área pendiente por fuente y " +
                "Mark sobrescribe la anterior: un boss que amenaza dos áreas el mismo turno " +
                "perdería una.\n\n" +
                "Se cablea de a dos instancias con el MISMO ChannelId: una en Step=Execute " +
                "arriba del Sequence (al lado del ExecuteTelegraph principal y FUERA de todo " +
                "gate — el aviso hay que cobrarlo aunque este turno no se marque de nuevo) y " +
                "una en Step=Mark donde corresponda.\n\n" +
                "Shapes soportadas: las centradas (SquareAroundSelf, SquareAroundPlayer, Row, " +
                "Column, HalfRoom). DirectionalBand y ScatteredSquares no — usá el nodo principal.",

            // --- El Tahúr (piso 3). Tipos calificados a propósito: este archivo lo comparten
            // varias ramas de jefes y agregar usings arriba multiplica los conflictos de merge.
            [typeof(Rollgeon.Combat.AI.Bosses.Tahur.AINode_TahurSettleWager)] =
                "Tahúr — Settle Wager: liquida la ronda. Lee la mano que el jugador jugó " +
                "(ComboPlayedPayload), la mide contra el canto sobre la escalera del contrato " +
                "(ordenada por Priority, NO por daño base) y mueve el pozo.\n\n" +
                "• Exacto ⇒ 0 dmg y, si el jugador está en La Mesa, cobra 12 × fichas contra el " +
                "propio jefe. El cobro REEMPLAZA su ataque: esa ronda no marca Castigo.\n" +
                "• Codicia (mano mejor) ⇒ +2 fichas y el Castigo más ancho (Scattered 6×2).\n" +
                "• Fallo (mano peor, o ninguna) ⇒ +1 ficha y la forma dice cuánto faltó: " +
                "Column 1 → Row 1 → Column 3 → Scattered 4×2.\n" +
                "• Fase 2 con el canto invertido, armar el canto ⇒ te leyó: el peor resultado.\n\n" +
                "El daño sale de PotDamageTable por cantidad de fichas (26/32/38/42/45) y jamás " +
                "supera DamageCeiling (45 = techo por golpe del piso 3). Marca vía " +
                "IThreatenedAreaService, así que lo detona el ExecuteTelegraph estándar el turno " +
                "siguiente. Puede fallar (sin contrato, sin grilla) ⇒ envolver en " +
                "Selector[nodo, Wait].",

            [typeof(Rollgeon.Combat.AI.Bosses.Tahur.AINode_TahurCallHand)] =
                "Tahúr — Call Hand: canta un escalón de la escalera del contrato del jugador y lo " +
                "publica como el objetivo de la próxima ronda.\n\n" +
                "No inventa mecánicas: usa las dos reglas del Contrato que ya existen — " +
                "ForbidCombo (R03) sobre la mano cantada (armarla hace 0: cobrar cuesta el ataque, " +
                "no la vida) y MultiplyCombo ×2 (R01) sobre todo lo que esté por encima del " +
                "escalón a armar (la codicia paga doble en el golpe y doble en el pozo).\n\n" +
                "La válvula nunca canta dos escalones altos seguidos (HighRankThreshold) y " +
                "UseRotationMemory evita repetir hasta agotar el conjunto — rotativo se aprende " +
                "más rápido. En fase 2 (canto invertido) nunca canta el escalón 1: no habría " +
                "escalón debajo desde el que cobrar.",

            [typeof(Rollgeon.Combat.AI.Bosses.Tahur.AINode_TahurMarkTable)] =
                "Tahúr — Mark Table: pinta La Mesa, su 3×3, daño 0, en cian. Es el único lugar " +
                "desde donde se cobra el pozo.\n\n" +
                "Va DESPUÉS del movimiento: la mesa de esta ronda no está donde estaba la anterior, " +
                "así que hasta la ronda perfecta pide un paso. No usa IThreatenedAreaService (se " +
                "indexa por guid de fuente y pisaría al Castigo): las casillas viven en " +
                "ITahurWagerService y el overlay usa una key propia.",

            [typeof(Rollgeon.Combat.AI.Bosses.Tahur.AINode_TahurFlipCard)] =
                "Tahúr — Flip Card: setup de Fase 2 (se voltea la carta). El cartel pasa de PIDE a " +
                "LEE — la mano cantada es ahora la que NO hay que armar y se cobra el escalón " +
                "inmediatamente inferior —, entra el rastrillo (+1 ficha por ronda, sola) y cobrar " +
                "deja el pozo en 1, nunca en 0.\n\n" +
                "No cambia un solo número: cambia el puzzle. La primera liquidación después del " +
                "volteo es de gracia (el canto pendiente se armó con las reglas viejas). Envolver " +
                "en If(PcOwnerHpBelow 0.40) → Once(...).",

            [typeof(Rollgeon.Combat.AI.Bosses.Tahur.AINode_TahurPoke)] =
                "Tahúr — Poke: 12 de daño melee, solo en ronda limpia. Es el precio fijo de cobrar, " +
                "porque cobrar es estar en su cara.\n\n" +
                "Se auto-gatea con RequireCleanRound: el poke y el Castigo nunca resuelven la misma " +
                "ronda porque 12 + 45 rompe el techo de 45 por golpe del piso 3. El árbol lo gatea " +
                "además con PcTahurCleanRound + PcTargetInRange 1.",

            [typeof(Rollgeon.Combat.AI.Bosses.Generala.AINode_GeneralaFrostRing)] =
                "Generala — Escarcha: congela el ANILLO de casillas a Radius exactas (Chebyshev) de " +
                "ella. Cruzarlo cuesta StunTurns turno(s); quedarse adentro o afuera no cuesta nada, " +
                "y quien ya estaba parado en una casilla del anillo cuando se forma tampoco paga " +
                "(el hazard es OnEnter).\n\n" +
                "El hueco central es a propósito: es desde donde el jugador le rompe los cinco " +
                "dados. Con Radius=1 el anillo tapaba justo esas casillas y desarmarle la mesa " +
                "dejaba de ser posible.\n\n" +
                "Daño 0 en la definición del hazard: el techo del piso 3 ya lo llenan la mano " +
                "detonada (45) y el cubilete (18). El hielo cobra el TURNO — quien se congela come " +
                "la mano de la ronda siguiente sin poder esquivarla, y ese golpe ya está " +
                "presupuestado.\n\n" +
                "El Hazard tiene que ser una HazardDefinitionSO con Trigger=OnEnter, Damage=0, " +
                "ConsumeOnTrigger=true y DurationRounds=2 (= 'dura 1 turno': la duración se " +
                "descuenta en el wrap de ronda y el anillo nace con el turno del jugador de esa " +
                "ronda ya jugado). El stun lo aplica IceStunBinder, no el hazard, y nunca al dueño " +
                "del anillo — el reposicionamiento corre después y la hace cruzar su propio hielo.\n\n" +
                "Devuelve Failed sin hazard / sin IHazardService / sin anillo posible ⇒ va SIEMPRE " +
                "en Selector[Escarcha, Wait].",
            // --- Genéricos sin entrada hasta Fix#0048 ---------------------------------

            [typeof(AINode_Once)] =
                "Decorador: ejecuta su hijo una sola vez en toda la pelea. La primera vez que el hijo " +
                "devuelve Succeeded queda 'latcheado' y de ahí en más el nodo devuelve Succeeded sin " +
                "volver a ejecutarlo. Si el hijo falla NO latchea: se reintenta el próximo turno.\n\n" +
                "Uso típico: If(PcOwnerHpBelow) → Once(ApplyStatModifier) dispara el setup de fase una " +
                "única vez al cruzar el umbral, sin que el cambio se vuelva a aplicar (y stackee) cada turno.",

            [typeof(AINode_RangedShot)] =
                "Disparo a distancia genérico: Damage directo al jugador si está a Range o menos (según " +
                "Metric), sin área y sin telegraph. Se auto-gatea por rango: devuelve Failed si el jugador " +
                "está lejos, así que va en Selector[Shot, Wait] y un rewire que olvide la condición no " +
                "lo convierte en un ataque de alcance infinito.\n\n" +
                "Kind es el tipo de ataque (para inmunidades/feedback); los tres ids de feedback " +
                "(anim, VFX de impacto, Feel) son opcionales.",

            [typeof(AINode_ExecuteTelegraph)] =
                "Resuelve el 'ataque telegráfico, turno N+1': consume el área que el jefe marcó el turno " +
                "anterior con TelegraphMark. Si el jugador sigue en alguna casilla marcada recibe el daño " +
                "guardado; si se movió, el ataque falla sin daño. En ambos casos limpia el resaltado.\n\n" +
                "Va como PRIMER hijo del Sequence del jefe (se resuelve al inicio de su turno, antes de " +
                "elegir la acción nueva). Siempre devuelve Succeeded: 'no había nada pendiente' o 'esquivó' " +
                "no deben cortar el Sequence. WindupFeedbackId e ImpactEventKey son opcionales.",

            [typeof(AINode_CancelTelegraph)] =
                "Descarta el aviso pendiente de un canal (ChannelId) sin ejecutarlo: saca el área del " +
                "servicio de amenaza y apaga su overlay. Es lo que le permite a un jefe reemplazar lo que " +
                "tenía anunciado en vez de sumarle un segundo anuncio.\n\n" +
                "Cancela solo la telegrafía, no lo que el jefe ya hizo este turno. Que no haya nada " +
                "pendiente no es un fallo: siempre devuelve Succeeded.",

            [typeof(AINode_IgniteArea)] =
                "Convierte en casillas especiales (Definition) el área que este jefe ya telegrafió en " +
                "ChannelId: consume la marca pendiente y planta el hazard encima. Consume la marca en " +
                "vez de recalcular la forma para que el fuego caiga exactamente donde el jugador lo vio " +
                "anunciado.\n\n" +
                "DurationRounds = 0 usa la duración por defecto del SO (0 sería PERMANENTE en el servicio). " +
                "'Arde N rondas' se autora como N+1 porque el fuego nace en el turno del jefe y el jugador " +
                "tiene el primer turno de la ronda. AnnounceTurns repite el aviso; FailWhenNothingToBurn " +
                "decide si devolver Failed cuando no hay nada que prender.",

            [typeof(AINode_ActivateHazard)] =
                "Activa un hazard de sala (HazardDefinitionSO) vía IHazardService. Es idempotente: " +
                "tickearlo cada turno no lo duplica. Un jefe con varios hazards solo necesita otra " +
                "instancia del nodo apuntando a otro .asset.",

            [typeof(AINode_ApplyStatModifier)] =
                "Aplica un cambio PERMANENTE de stats al propio jefe (AttackDelta, SpeedDelta) y emite el " +
                "evento de cambio de fase (PhaseIndex) para que el feedback visual y el diálogo reaccionen. " +
                "Attack es inmediato; Speed reordena la cola de turnos recién en la próxima ronda.\n\n" +
                "El modificador no se revierte si el HP vuelve a subir: envolverlo en Once dentro de un " +
                "If(PcOwnerHpBelow) para que se aplique una sola vez.",

            [typeof(AINode_RotateBlock)] =
                "Recalcula el 'bloqueo rotativo' del jefe al cerrar su turno. Target = Dice: sortea Count " +
                "dados de la build del jugador y los bloquea. Target = Combo: prohíbe los últimos Count " +
                "combos del contrato, en ventana deslizante. DirectedIndex (opcional) fuerza qué índice " +
                "bloquear en vez de sortear.\n\n" +
                "Acá un id de feedback vacío significa silencio (al revés que en los nodos propios de un " +
                "jefe, donde vacío = id canónico). AnnounceOnce evita repetir el aviso cada turno.",

            [typeof(AINode_PromulgateRule)] =
                "'Promulgar Regla' del Director General: efecto de inicio de turno (primer hijo del " +
                "Sequence, no consume la acción). Cada IntervalPhase1 turnos (IntervalPhase2 por debajo " +
                "de Phase2HpThreshold) cancela las reglas previas y promulga RulesPerPromulgation reglas " +
                "al azar de EnabledRules. DoubleFactor / HalfFactor parametrizan las reglas de daño.\n\n" +
                "Un único nodo es un único contador: no dupliques el nodo en dos ramas o se desincronizan.",

            [typeof(AINode_TeleportToRoomCenter)] =
                "Reubica al que actúa en el centro de la sala (centro del bounding box de las casillas " +
                "caminables, el mismo que usan HalfRoom y GridPartition). Sin campos obligatorios: sirve " +
                "en cualquier arena. Va por teletransporte, no por caminata: no dispara los OnEnter de las " +
                "casillas especiales del camino. ConsumeMoveAction marca el movimiento como usado; " +
                "AvoidHarmfulTiles evita aterrizar sobre fuego/pinchos.",

            [typeof(AINode_TeleportNearTarget)] =
                "Reubica al que actúa en una casilla libre a una distancia del jugador entre MinDistance " +
                "y MaxDistance (banda, no mínimo: todas las casillas de la banda empatan y se sortea, así " +
                "el acercamiento no se aprende de memoria). Es la cara opuesta de TeleportAwayToEdge.\n\n" +
                "MinDistance = 1 es un regalo para un kit a distancia (turno franco sin caminar); el " +
                "default lo deja cerca pero a un paso. Teletransporte: no dispara OnEnter del camino.",

            [typeof(AINode_TeleportAwayToEdge)] =
                "Reubica al que actúa en una casilla libre que esté a la vez lejos del jugador (al menos " +
                "MinPlayerDistance, clampeado a MaxDistanceFromPlayer) y pegada al borde de la sala (a lo " +
                "sumo EdgeBandDepth). Con los clamps todas las casillas que cumplen empatan y se sortea; " +
                "sin ellos el jefe elegiría siempre la misma esquina.\n\n" +
                "Sin MaxDistanceFromPlayer el jefe siempre encuentra casilla más lejos de lo que el jugador " +
                "cubre en un turno y la pelea no se puede ganar. Teletransporte: no dispara OnEnter del camino.",

            // --- Sala / objetos (Rollgeon.Combat.Rooms) ------------------------------

            [typeof(Rollgeon.Combat.Rooms.AINode_SpawnRoomObjects)] =
                "Mantiene los objetos de sala de una definición (Definition): los coloca la primera vez " +
                "según Pattern / Side / MinSpacing (Count objetos), detecta los rotos, deja el hazard de " +
                "muerte en su casilla y los repone en la MISMA ranura pasado el delay de la definición " +
                "(ResolveSlotsEachSpawn re-sortea la casilla en cada reposición).\n\n" +
                "NO envolver en Once: necesita tickear cada turno para correr los relojes de reposición. " +
                "Devuelve Succeeded en los ticks de espera para no abortar el Sequence del jefe.",

            [typeof(Rollgeon.Combat.Rooms.AINode_BombField)] =
                "Siembra Count bombas por la sala (Shape / Spacing), cada una con su propia cruz avisada " +
                "y su mecha (FuseTurns). Solo siembra: el estallido lo hace DetonateBombField, que va " +
                "fuera del ciclo y descuenta la mecha todos los turnos. La cruz se guarda por bomba " +
                "(ChannelPrefix + guid), así romper una no toca las demás.\n\n" +
                "Definition tiene que traer RespawnDelayTurns = 0 para que la siembra reponga en la misma " +
                "pasada lo que detonó y lo que el jugador rompió.",

            [typeof(Rollgeon.Combat.Rooms.AINode_DetonateBombField)] =
                "La otra mitad de BombField: descuenta la mecha de las bombas del mismo ChannelPrefix y " +
                "prende la cruz (FireTile durante FireDurationRounds, IgnitionDamage) de las que llegaron " +
                "a cero. Va FUERA del Alternate y ANTES del ciclo de ataque: dentro correría una vez cada " +
                "N turnos y la mecha valdría siempre un ciclo entero; detrás detonaría lo que el mismo " +
                "turno acaba de sembrar.\n\n" +
                "Romper una bomba a mano no deja fuego: el fuego es el premio por dejarla madurar.",

            // --- El Cajero --------------------------------------------------------------

            [typeof(AINode_CajeroCoinRain)] =
                "'Lluvia de monedas': cada EveryNRounds rondas suelta Count monedas (Coin, valor entre " +
                "MinValue y MaxValue, separadas al menos MinSeparation casillas) repartidas por la sala. " +
                "Es el reloj de la pelea: juntarlas obliga al jugador a caminar con el jefe persiguiéndolo.\n\n" +
                "Las monedas nacen permanentes; las vence CoinVault. Su Failed ('todavía no toca tanda') " +
                "es benigno: va en Selector[CoinRain, Wait].",

            [typeof(AINode_CajeroCoinVault)] =
                "'La caja': le pone reloj (LifetimeRounds) a cada moneda del piso y, cuando una se vence " +
                "sin que nadie la levante, se la lleva. No cura al jefe: la plata simplemente se pierde. " +
                "Se lleva UNA moneda por turno, nunca la tanda entera: las cuatro de una lluvia se " +
                "pierden a lo largo de cuatro turnos.\n\n" +
                "Descubre las monedas por barrido de las instancias vivas, así que va después de los " +
                "nodos que las sueltan.",

            [typeof(AINode_CajeroShove)] =
                "El empujón del Cajero: hereda de RangedShot con Range = 1 (rango, daño, giro y " +
                "presentación ya vienen de ahí) y además manda al jugador PushTiles casillas en línea " +
                "recta hacia el lado opuesto. Le cobra TaxPercent del oro que lleve encima (nunca menos de " +
                "TaxMinimum) y deja RefundPercent de eso tirado en CoinCount monedas a lo largo del tumbo: " +
                "lo que cae al piso es plata del jugador. El tumbo frena contra paredes y blockers y cobra las " +
                "casillas atravesadas (pinchos incluidos).",

            [typeof(AINode_CashierCounterToll)] =
                "Arma el 'peaje del mostrador': la fila CounterRow cobra Damage al jugador cada " +
                "ChargesEveryNRounds rondas al cerrar su turno. El nodo solo ARMA (es idempotente y se " +
                "re-arma todos los turnos); el cobro lo hace el servicio fuera del árbol.\n\n" +
                "Va antes del ciclo de ataque: en el path sin coroutine un Running del ataque aborta " +
                "lo que venga después.",

            [typeof(AINode_CashierRangedShot)] =
                "Variante del RangedShot para El Cajero con la presentación canónica del jefe. Un id de " +
                "feedback vacío significa 'usá el canónico', no 'sin presentación' (Odin no corre field " +
                "initializers al deserializar el asset).",

            // --- El Anotador / La Bandida / La Generala / El Tahúr ------------------------

            [typeof(AINode_AnotadorPencil)] =
                "El lápiz del Anotador: golpe de Damage si el jugador está a Range o menos (Metric, " +
                "Kind). Va ANTES del repliegue en el Sequence: la distancia se mide al empezar el turno " +
                "del jefe y, después de alejarse, el lápiz no cobraría nunca.",

            [typeof(AINode_BandidaArm)] =
                "El brazo de La Bandida: golpe de Damage a Range (Metric, Kind) con su secuencia de " +
                "presentación (ArmFeedbackId, ImpactVfxId, ImpactFeelId, ImpactEventKey). Metric tiene " +
                "que ser la misma que la del PcTargetInRange que lo gatea, o una de las dos mitades miente " +
                "sobre las diagonales.",

            [typeof(AINode_BandidaReelToll)] =
                "El peaje de los rodillos: cada rodillo vivo le descuenta rolls al jugador, hasta Cap por " +
                "turno. Cap tiene que quedar muy por debajo del grant de rolls por turno o el jugador entra " +
                "en economía negativa. Siempre devuelve Succeeded: un Failed le cortaría al jefe el resto " +
                "del turno.",

            [typeof(Rollgeon.Combat.AI.Bosses.Generala.AINode_GeneralaCupSlam)] =
                "El cubiletazo de La Generala: golpe de Damage si el jugador está a Range o menos (Metric, " +
                "Kind), con AnimFeedbackIdOverride opcional. Con el jugador lejos devuelve Failed, así que " +
                "va envuelto en Selector[CupSlam, Wait]: suelto le cancelaría al jefe el resto del turno, " +
                "el telegraph de la mano incluido.",

            [typeof(Rollgeon.Combat.AI.Bosses.Tahur.AINode_TahurMarkBanca)] =
                "'La Banca' del Tahúr: cuando el jugador acumula ChipsThreshold fichas, marca alrededor de " +
                "la mesa (TableRadius) un castigo de Damage (tope DamageCeiling, Kind). Va ÚLTIMO en el " +
                "turno, después del movimiento y de poner la mesa, porque el hueco se ancla en el jefe. " +
                "Marca sobre el guid del jefe (el mismo canal que el Castigo, y lo sobrescribe), así que " +
                "nunca detonan juntos. Puede devolver Failed: va en Selector[Banca, Wait].",

        };

        /// <summary>
        /// Devuelve la descripción para el tipo de nodo, o <c>null</c> si no hay registrada.
        /// </summary>
        public static string Get(Type t)
        {
            if (t == null) return null;
            return _descriptions.TryGetValue(t, out var doc) ? doc : null;
        }
    }
}
