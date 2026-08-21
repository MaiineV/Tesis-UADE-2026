using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Feedback;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Tiles;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Arma por código el jefe de piso 1 <b>El Croupier</b>: su <see cref="EnemyDataSO"/> con el árbol
    /// de AI inline y su prefab visual (arte del Healer retintado a carmesí de crupier). El fuego que
    /// usa <b>no</b> lo escribe: es la casilla especial de <see cref="CroupierFirePath"/>, autorada a
    /// mano. El único hazard que sí autora es el de La Bandida (ver <see cref="BandidaReelFireDamage"/>).
    /// </summary>
    /// <remarks>
    /// <see cref="BuildAIRoot"/>, <see cref="BuildWrapperSpec"/> y <see cref="PopulateEnemyData"/>
    /// son estáticos puros — se testean en memoria sin tocar el <c>AssetDatabase</c>.
    /// <see cref="BuildCroupier"/> es la capa que persiste, y es idempotente. El arte es el del
    /// Healer retintado: su lectura la lleva el paño, no la silueta, y su
    /// <c>LocomotionStyle.Blink</c> le queda bien — el crupier no camina, reaparece.
    /// </remarks>
    public static class CroupierAssetBuilder
    {
        // ======================================================================
        // Rutas
        // ======================================================================

        public const string BossAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Croupier.asset";
        public const string FirePhase1Path = "Assets/Rollgeon/Combat/Hazards/HZ_Croupier_TableFire.asset";
        public const string FirePhase2Path = "Assets/Rollgeon/Combat/Hazards/HZ_Croupier_TableFire_Phase2.asset";

        /// <summary>
        /// La casilla de fuego del jefe —y el único fuego que este jefe usa—. Propia y no
        /// <c>Tile_FireTemp</c>: sus números de daño y duración se tunean para esta pelea, y tocar la
        /// genérica se los cambiaría al resto del juego.
        /// </summary>
        /// <remarks>
        /// <b>Este builder no la escribe: es autoría a mano.</b> Los dos números del fuego
        /// (<c>EnterDamage</c> y <c>TurnStartDamage</c>, hoy 7 y 7) viven en el asset y en ningún otro
        /// lado, así que subirle el fuego al Croupier es editar ese archivo. No existe ninguna
        /// constante acá que los mueva —y la que se parece, <see cref="BandidaReelFireDamage"/>, es
        /// de otro jefe.
        /// </remarks>
        public const string CroupierFirePath = "Assets/Rollgeon/Tiles/Tile_Fire_Croupier.asset";

        /// <summary>
        /// Llama del paño. Vive acá y no en el builder de la Bandida porque el fuego es del Croupier
        /// y ella lo reusa — un solo asset, un solo lugar donde cambiarlo.
        /// </summary>
        public const string FireVfxPrefabPath = "Assets/Prefabs/VFX/VFX_Fire.prefab";

        /// <summary>Mesh de fuego que trajo el arte; es un MeshRenderer con luces, no un sistema de partículas.</summary>
        private const string FireMeshPrefabPath = "Assets/Art/3D/Models/Items/Fire.prefab";

        /// <summary>Segundos que dura el fogonazo de pisar una casilla encendida.</summary>
        private const float FireBurstLifetime = 0.9f;

        /// <summary>Arte a vestir: el Healer ya viene con copa, moño, capa y bastón.</summary>
        public const string ArtPrefabPath = "Assets/Prefabs/Enemies/Healer_Animated.prefab";

        /// <summary>Prefab de gameplay que sale del wrapper y va a <c>EnemyData.VisualPrefab</c>.</summary>
        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Croupier.prefab";

        // La ruleta (prop, hijo que giraba, label del numero cantado, su fuente y su encuadre) se
        // fue con la mecanica: el jefe no canta numeros, BuildWrapperSpec no monta props y nada le
        // cuelga CroupierWheelSpinVisual. Las constantes que la describian se borraron en vez de
        // dejarse: leian como el cableado vivo de un prop que el prefab no tiene.

        /// <summary>Retrato del rig que viste (<c>Healer_Animated</c>). Ver <see cref="BossPortraitLibrary"/>.</summary>
        public const string PortraitTexturePath = BossPortraitLibrary.SheetPath;

        // ======================================================================
        // Ficha de diseño — todos los números del jefe, en un solo lugar
        // ======================================================================

        public const string EntityId = "boss.croupier";
        public const string DisplayName = "The Croupier";

        /// <summary>
        /// Su debilidad es el Poker (cuatro dados iguales), no el Par.
        /// </summary>
        /// <remarks>
        /// El Par salía 9 de cada 10 tiradas (90,7% en la primera), así que la debilidad era un piso
        /// que se cobraba todos los turnos. El Poker pide cuatro caras iguales y sale ~2 de cada 10
        /// tirando el pozo entero: la debilidad deja de ser un piso y pasa a ser un pico. Eso le baja
        /// el daño por turno al jugador incluso con <see cref="WeaknessMultiplier"/> más alto —el bono
        /// pasa de aplicarse siempre a aplicarse a veces—, y es lo que hace que la palanca del
        /// multiplicador tenga que compensar en la otra dirección.
        /// </remarks>
        public const string WeaknessComboId = ComboId.Poker;

        /// <summary>
        /// Cuánto lo castiga el Poker. <b>No es una perilla de dificultad: es si la debilidad existe
        /// o no.</b> La debilidad multiplica el golpe entero ya resuelto (fórmula v3:
        /// <c>N = base_combo + ATQ + Σcaras + bonos</c>), no el base del combo.
        /// </summary>
        /// <remarks>
        /// <para>
        /// El ×2 es una consecuencia de haber movido la debilidad, no un ajuste aparte. El Par salía
        /// 9 de cada 10 primeras tiradas: la debilidad era un <b>piso</b> que se cobraba todos los
        /// turnos, así que ahí un ×1.5 ya alcanzaba para que se notara. El Poker sale ~2 de cada 10
        /// gastando el pozo entero, o sea que el bono pasa a cobrarse una vez cada varios turnos: con
        /// el mismo ×1.5 la debilidad casi desaparece del daño de la pelea y vuelve a ser una línea de
        /// la ficha que la jugada óptima ignora.
        /// </para>
        /// <para>
        /// A ×2 es un premio que vale ir a buscar: el Poker tiene 55 de base, así que sale a 110 y se
        /// siente como el turno que da vuelta la pelea — que es exactamente lo que tiene que sentirse
        /// una mano que aparece dos veces cada diez tiradas. Si el jefe queda demasiado blando la
        /// palanca es <see cref="MaxHp"/>, no esto.
        /// </para>
        /// </remarks>
        public const float WeaknessMultiplier = 2.0f;

        /// <summary>
        /// Jefe de piso 1, con el golpe base del piso (13-27, mediana 20).
        /// </summary>
        /// <remarks>
        /// Los 200 valen el doble de lo que parecen porque <b>sólo se lo puede golpear en uno de cada
        /// dos turnos</b>: en T1 huye a <see cref="FleeIdealDistance"/> y el único beat en que se
        /// queda quieto es T2 (ver el <c>Alternate</c> de <see cref="BuildAIRoot"/>). Contado en turnos
        /// de jugador —que es la unidad en la que se siente la vida de un jefe— cuesta lo mismo que el
        /// doble de HP en un jefe que se dejara pegar siempre. Los 170 previos compensaban una
        /// debilidad en el Par que se cobraba casi todos los turnos; movida al Poker
        /// (<see cref="WeaknessComboId"/>) ese descuento dejó de tener motivo.
        /// </remarks>
        public const int MaxHp = 200;
        public const int Attack = 24;
        public const int Speed = 5;
        public const int MinGoldDrop = 15;
        public const int MaxGoldDrop = 23;

        // La rueda cantada (sectores, represalia de mesa) se retiró de esta pelea: el árbol no monta
        // AINode_SpinWheel / AINode_MarkSungSectors / AINode_DetonateSungSectors ni crea el
        // CroupierWheelService, así que no queda nada acá que la tunee. Las constantes que había
        // (SectorDamage, SectorDamagePhase2, RetaliationDamage) se borraron en vez de dejarse
        // "documentando la intención": leían como perillas vivas y no movían nada, y la corrida
        // pasada se gastó en re-tunearlas. Los números de la mecánica siguen existiendo en los
        // defaults de esos nodos, que es donde volverían a aplicar si la rueda vuelve.

        /// <summary>
        /// Daño del <see cref="HazardDefinitionSO"/> de paño que este builder autora en
        /// <see cref="FirePhase1Path"/>. <b>No es el fuego del Croupier.</b>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Ese asset lo consume <b>La Bandida</b> para sus reels reventados
        /// (<c>BandidaAssetBuilder.ReelFireHazardPath</c> apunta ahí), y este builder es el único que
        /// lo escribe — de ahí el nombre. El Croupier no lo usa: su fuego es la casilla especial de
        /// <see cref="CroupierFirePath"/>, autorada a mano.
        /// </para>
        /// <para>
        /// <b>Nunca rutear el fuego del Croupier por acá.</b> Ya pasó una vez: subir este número para
        /// subirle el fuego al Croupier no le toca una casilla al Croupier y le sube el daño a otro
        /// jefe que nadie pidió cambiar. Si hay que mover el fuego del Croupier, se edita el
        /// <c>.asset</c> de la casilla.
        /// </para>
        /// </remarks>
        public const int BandidaReelFireDamage = 6;

        /// <summary>
        /// "Arde 2 rondas" = 3 rondas de casilla. La duración tickea en el wrap de ronda y el fuego
        /// nace en el turno del jefe, o sea después del turno del jugador de esa ronda (CNF-006):
        /// la ronda en la que se enciende no le queda ningún arranque de turno del jugador por
        /// delante. Arrancar N turnos adentro pide autorar N + 1.
        /// </summary>
        /// <remarks>
        /// <b>Esto es igual al intervalo entre igniciones, y es a propósito.</b> El jefe prende uno
        /// de cada dos tiempos, así que una banda se apaga justo cuando se enciende la siguiente:
        /// nunca conviven dos y el paño vuelve a estar limpio cada vez. El fuego pasa a ser una
        /// amenaza que se esquiva, no un piso que se achica ronda a ronda. Lo segundo empezaba
        /// legible y terminaba en una sala sin lugar donde plantarse.
        /// </remarks>
        public const int FireDurationRounds = 3;

        /// <summary>
        /// Desde "Pleno y color" las bandas duran una ronda más: 4 de casilla = arde 3. Recién acá
        /// el fuego supera el intervalo entre igniciones, así que cuando nace una banda la anterior
        /// todavía está prendida y hay más piso encendido a la vez. No se pisan — la nueva sólo
        /// enciende lo que no ardía (ver <c>AINode_IgniteArea.AlreadyBurning</c>) — así que lo que
        /// crece es la superficie, no el daño por casilla.
        /// </summary>
        public const int FireDurationRoundsPhase2 = 4;

        /// <summary>
        /// Duracion del hazard de paño que este builder deja autorado para La Bandida (sus reels lo
        /// consumen). El Croupier ya no lo usa: bajarlo o subirlo no le cambia nada a el, pero si a
        /// ella. Era el "arde 2 rondas" original.
        /// </summary>
        public const int HazardDurationForBandida = 3;

        // ======================================================================
        // Rediseno: kiter de dos tiempos
        // ======================================================================

        /// <summary>
        /// Disparo de T1. Chico a proposito: el jefe no gana por sus golpes directos sino por el
        /// piso que va quemando, y un tiro grande a rango infinito volveria la persecucion injusta.
        /// </summary>
        public const int ShotDamage = 12;

        /// <summary>
        /// Alcance del disparo, en Manhattan. 20 cubre la diagonal entera de la sala 11x11 (max 20),
        /// asi que en la practica es "a cualquier distancia" sin escribir un centinela.
        /// </summary>
        public const int ShotRange = 20;

        /// <summary>Casillas que retrocede por turno de fuga.</summary>
        public const int FleeSteps = 2;

        /// <summary>
        /// Mientras el jugador este a menos de esto, huye. Alto a proposito: KeepDistance no hace
        /// nada en cuanto ya esta a la distancia ideal, y un ideal chico lo dejaria plantado a media
        /// sala esperando. Con 8 huye practicamente siempre, que es lo que define al personaje.
        /// </summary>
        public const int FleeIdealDistance = 8;

        /// <summary>Semi-ancho de la banda de fuego: 1 = 3 casillas de ancho.</summary>
        public const int BandHalfWidth = 1;

        /// <summary>
        /// Profundidad de la banda. 11 = el largo de la sala, asi que la banda siempre llega a la
        /// pared y no deja un pedazo del pasillo sin quemar por donde rodearla de una.
        /// </summary>
        public const int BandDepth = 11;

        /// <summary>Umbral del candado: desde aca le queda un dado menos, y no vuelve.</summary>
        public const float LockHpThreshold = 0.7f;

        /// <summary>
        /// Cual dado se traba. Fijo y no sorteado: el candado se tiene que leer como "me saco ESE",
        /// y RotateBlock etiqueta el candado con el indice + 1, asi que 0 muestra "1".
        /// </summary>
        public const int LockedDieIndex = 0;

        /// <summary>Umbral de "Pleno y color".</summary>
        public const float PlenoHpThreshold = 0.5f;

        /// <summary>
        /// Radio del hueco que "Pleno y color" NO prende, centrado en el jefe. 1 = su 3x3. Ojo que
        /// en <c>AllExceptSquareAroundSelf</c> el Size es el hueco, no el area amenazada.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 1 es lo que dice la ficha de diseño en las tres veces que describe el golpe, y lo que
        /// hace que 1 alcance es el <b>turno de aviso</b>: la marca se levanta en el turno N y la
        /// ignicion es en N+1 (ver <see cref="BuildAIRoot"/>), asi que el jugador tiene un pozo de
        /// dados entero para llegar al hueco. El 2 de la corrida pasada se justificaba con la
        /// distancia desde una esquina, pero medida contra un solo turno de reaccion que ya no es el
        /// que la pelea da.
        /// </para>
        /// <para>
        /// La cuenta, en la sala 11x11 y con el hueco en el centro: desde la esquina el borde del 3x3
        /// queda a 8 pasos y el del 5x5 a 6, y el jugador se mueve 5 por accion de movimiento -- o
        /// sea que los dos cuestan <b>dos</b> acciones desde la esquina, 2 de las 6 tiradas del pozo.
        /// Lo que el 5x5 cambiaba no era el peor caso sino el promedio: bajaba de ~20% de la sala a
        /// ~3% las casillas desde las que hay que gastar la segunda accion. Con un turno entero para
        /// cruzar, esa segunda accion es el precio del golpe, no un impuesto: si el hueco se alcanza
        /// desde cualquier parte con una sola tirada, "correr al centro" deja de ser una decision.
        /// </para>
        /// </remarks>
        public const int PlenoHoleRadius = 1;

        /// <summary>
        /// Lo que cobra "Pleno y color" en el momento de prender, a quien este parado afuera del
        /// hueco. Bajo a proposito: el golpe no es el punto, el punto es el paño encendido que queda
        /// despues.
        /// </summary>
        /// <remarks>
        /// Cobra 7 y no 0 como la banda <b>aunque las dos avisen un turno antes</b>, y la diferencia
        /// es cuanto cuesta obedecer el aviso: salirse de una banda de <see cref="BandHalfWidth"/>
        /// de semi-ancho es un paso al costado, mientras que del Pleno solo se salva una casilla de
        /// la sala --el hueco alrededor del jefe, en el centro-- y llegar ahi cuesta hasta dos
        /// acciones de movimiento. El 7 es lo que paga el que decide no gastarlas.
        /// </remarks>
        public const int PlenoIgnitionDamage = 7;

        /// <summary>
        /// Canal de la marca de "Pleno y color" (<c>AINode_TelegraphMark.ChannelId</c>).
        /// </summary>
        /// <remarks>
        /// El Pleno y la banda de T1 <b>conviven</b>: los dos se marcan en el turno N y prenden en el
        /// N+1, así que el jefe tiene dos avisos abiertos a la vez. <c>IThreatenedAreaService</c>
        /// guarda un área por fuente y <c>Mark</c> sobrescribe, y el overlay pinta un área por fuente,
        /// así que sin un canal propio la segunda marca del turno le borra la primera en los dos
        /// lados: se perdería el aviso o el fuego. El canal es lo que las separa —el paso que la
        /// consume tiene que declarar este mismo string—.
        /// </remarks>
        public const string PlenoChannelId = "pleno";

        /// <summary>Umbral de "Pleno y color".</summary>
        // Borradas por el mismo motivo que las de la rueda: nadie las leia y leian como perillas.
        //   Phase2HpThreshold    duplicaba PlenoHpThreshold con el mismo 0.5f -- dos umbrales
        //                        identicos y uno solo cableado es la peor version del problema:
        //                        el que quiera mover la fase 2 tiene la mitad de chances de editar
        //                        el que no hace nada.
        //   Phase2NumbersPerTurn de la rueda cantada, retirada.
        //   DesiredRange         el kiteo lo definen FleeSteps y FleeIdealDistance; este numero
        //                        colgaba de la Represalia, que tampoco existe en esta pelea.
        //   MoveSteps            idem: el unico movimiento del arbol es el KeepDistance de T1.

        /// <summary>Rojo de brasa — se tiene que leer distinto del naranja del telegraph.</summary>
        public static readonly Color FireOverlayTint = new Color(0.85f, 0.10f, 0.05f, 0.60f);

        // ======================================================================
        // Ficha visual — paleta y transform del prop, en un solo lugar
        // ======================================================================

        // Los tres materiales que dominan la silueta del Healer y qué visten:
        //   Mat_Red      → capa, moño, banda del sombrero y mango del bastón.
        //   Mat_DarkGray → copa del sombrero y traje.
        //   Mat_Gold     → vivos del traje y cabeza del bastón.
        // Mat_White (camisa, guantes, esclerótica), Mat_Bone (piel) y Mat_Black (pupila) quedan
        // sin clonar a propósito: los guantes blancos son la mitad de la lectura "crupier", y
        // retintar el blanco también le cambiaría el ojo.
        //
        // Todos los colores van explícitos y no por PaletteSlot: los labels guardados en
        // PA_MainPalette están desalineados respecto de la tabla de PaletteSlots (Mat_Red hoy
        // apunta al slot 7, que en la tabla es "Green"), así que un slot no dice qué color sale.

        /// <summary>Carmesí de terciopelo del paño — el color que el jefe le presta a la mesa.</summary>
        public static readonly Color WineLight = new Color(0.647f, 0.157f, 0.251f);
        public static readonly Color WineMid = new Color(0.404f, 0.055f, 0.129f);
        public static readonly Color WineShadow = new Color(0.212f, 0.024f, 0.075f);

        /// <summary>Smoking casi negro con tiro a borravino: mantiene la luminancia del gris original.</summary>
        public static readonly Color TuxLight = new Color(0.196f, 0.145f, 0.169f);
        public static readonly Color TuxMid = new Color(0.129f, 0.090f, 0.110f);
        public static readonly Color TuxShadow = new Color(0.063f, 0.043f, 0.055f);

        /// <summary>Latón más brillante que el Mat_Gold de fábrica: los vivos tienen que saltar del vino.</summary>
        public static readonly Color BrassLight = new Color(0.980f, 0.855f, 0.529f);
        public static readonly Color BrassMid = new Color(0.831f, 0.635f, 0.196f);
        public static readonly Color BrassShadow = new Color(0.439f, 0.310f, 0.078f);

        /// <summary>
        /// Barra de vida más baja que el default de 3: el arte del Healer mide ~1.95 con bastón, y a 3
        /// la barra quedaba flotando con un hueco de una unidad sobre el sombrero.
        /// </summary>
        public static readonly Vector3 HealthBarOffset = new Vector3(0f, 2.4f, 0f);

        // Ids fijos y escritos a mano (no Guid.NewGuid): el builder es idempotente, y un id nuevo por
        // corrida haría que el asset cambie en cada build y que un fuego ya activo quede huérfano.
        private const string FirePhase1SourceId = "c0a17e11-0001-4c00-b001-6f75706965f1";
        private const string FirePhase2SourceId = "c0a17e11-0002-4c00-b002-6f75706965f2";

        // ======================================================================
        // Menú
        // ======================================================================

        [MenuItem("Tools/Rollgeon/Bosses/Build Croupier")]
        public static void BuildCroupier()
        {
            var flame = BuildFireVfx();

            // El Croupier ya no usa este hazard --su fuego es una casilla especial-- pero se sigue
            // autorando: HZ_Croupier_TableFire lo consume La Bandida para sus reels, y dejar de
            // escribirlo la dejaria con la definicion vieja sin que nadie lo note. La de fase 2 se
            // va porque no la referenciaba nadie mas.
            BuildFireDefinition(FirePhase1Path, HazardDurationForBandida, FirePhase1SourceId, flame);

            var visual = BuildVisualPrefab();
            var portrait = BossPortraitLibrary.Croupier();

            var boss = LoadOrCreate<EnemyDataSO>(BossAssetPath);
            // El fuego del jefe pasa a ser una casilla especial: trae visual propio, VFX al
            // dispararse, tooltip localizado, y el planner de la IA la esquiva sola.
            var croupierFire = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(CroupierFirePath);
            if (croupierFire == null)
            {
                Debug.LogError($"[CroupierAssetBuilder] Falta {CroupierFirePath}. El jefe queda sin " +
                               "fuego: el nodo de ignicion falla y sus turnos de quema no hacen nada.");
            }

            PopulateEnemyData(boss, croupierFire, visual, portrait);

            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CroupierAssetBuilder] Listo: '{BossAssetPath}' + '{VisualPrefabPath}' + el " +
                      $"hazard de paño de La Bandida ('{FirePhase1Path}').");
            Selection.activeObject = boss;
        }

        // ======================================================================
        // Prefab visual
        // ======================================================================

        /// <summary>
        /// Ficha de armado del wrapper. Pura (no toca <c>AssetDatabase</c>) para que los tests puedan
        /// afirmar arte y retintes sin construir el prefab.
        /// </summary>
        public static BossWrapperSpec BuildWrapperSpec()
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = ArtPrefabPath,
                OutputPrefabPath = VisualPrefabPath,
                EntityId = EntityId,
                BossName = "Croupier",
                HealthBarOffset = HealthBarOffset,
                Retints = new Dictionary<string, MaterialRetint>
                {
                    { "Mat_Red", MaterialRetint.FromColors(WineLight, WineMid, WineShadow) },
                    { "Mat_DarkGray", MaterialRetint.FromColors(TuxLight, TuxMid, TuxShadow) },
                    { "Mat_Gold", MaterialRetint.FromColors(BrassLight, BrassMid, BrassShadow) },
                },
                // Sin props. La ruleta que colgaba a su izquierda ya no representa nada: el jefe
                // no canta numeros. Una rueda que gira sin significar nada se lee como si importara,
                // y encima tapaba la vista de la mitad de la sala desde ese lado.
                Props = new List<BossPropSpec>(),
            };
        }

        /// <summary>
        /// Construye el wrapper. Devuelve <c>null</c> (con warning ya logueado por el wrapper) si el
        /// arte no está: el jefe queda sin <c>VisualPrefab</c>, que es exactamente lo que hay que ver
        /// en consola en vez de un prefab a medias.
        /// </summary>
        /// <remarks>
        /// Es sólo el wrapper: al root ya no se le cuelga <c>CroupierWheelSpinVisual</c> ni el label
        /// del número cantado, porque sin ruleta no hay nada que girar ni número que mostrar. Queda
        /// como método propio y no inline en <see cref="BuildCroupier"/> para que el punto donde se
        /// vestiría el prefab siga siendo uno.
        /// </remarks>
        private static GameObject BuildVisualPrefab()
        {
            return BossVisualWrapperBuilder.BuildWrapper(BuildWrapperSpec());
        }

        // ======================================================================
        // Datos del jefe (puro — sin AssetDatabase)
        // ======================================================================

        /// <summary>
        /// Texto de hover del jefe: qué hace y con qué números, en tres oraciones. Nada de tono ni de
        /// consejos — es un tooltip que se lee de reojo en medio de un turno, y la versión larga
        /// anterior se saltaba entera.
        /// </summary>
        /// <remarks>
        /// Todo número sale de una constante de la ficha o de <paramref name="fire"/> —los del fuego
        /// viven en el asset de la casilla— y ninguno está escrito a mano: es el único texto de la
        /// pelea que el jugador puede leer sin morir primero, así que no puede quedar desfasado de
        /// los números que la pelea usa de verdad. La duración se muestra con una ronda menos que la
        /// autorada: la ronda en la que se enciende no tiene cierre de turno del jugador por delante
        /// (CNF-006), así que la autorada se juega como una menos.
        /// </remarks>
        public static string BuildDescription(SpecialTileDefinitionSO fire)
        {
            int bandWidth = BandHalfWidth * 2 + 1;
            int holeSide = PlenoHoleRadius * 2 + 1;
            int lockPercent = Mathf.RoundToInt(LockHpThreshold * 100f);
            int plenoPercent = Mathf.RoundToInt(PlenoHpThreshold * 100f);

            var sb = new System.Text.StringBuilder();
            sb.Append("Retreats every turn and shoots for ").Append(ShotDamage)
              .Append(" at any range. Every other turn he lights a ").Append(bandWidth)
              .Append("-tile lane of fire");

            if (fire != null)
            {
                sb.Append(" — ").Append(fire.EnterDamage).Append(" to cross, ")
                  .Append(fire.TurnStartDamage).Append(" to start a turn on it");
            }

            sb.Append(", burning ").Append(Mathf.Max(1, FireDurationRounds - 1)).Append(" rounds (")
              .Append(Mathf.Max(1, FireDurationRoundsPhase2 - 1)).Append(" under ").Append(plenoPercent)
              .Append("%). At ").Append(lockPercent).Append("% he padlocks one die; at ")
              .Append(plenoPercent).Append("% he warps to the centre and marks the whole table ")
              .Append("except the ").Append(holeSide).Append("×").Append(holeSide)
              .Append(" square around him — it burns on his next turn for ")
              .Append(PlenoIgnitionDamage).Append(".");

            return sb.ToString();
        }

        /// <summary>
        /// Escribe la ficha completa del Croupier en <paramref name="data"/>, incluido su
        /// <see cref="EnemyDataSO.AIRoot"/>. No toca <c>AssetDatabase</c>: sirve igual para el asset
        /// real y para una instancia in-memory de test.
        /// </summary>
        public static void PopulateEnemyData(
            EnemyDataSO data,
            SpecialTileDefinitionSO fire,
            GameObject visualPrefab,
            Sprite portrait)
        {
            if (data == null) return;

            data.EntityId = EntityId;
            data.DisplayName = DisplayName;
            data.Description = BuildDescription(fire);

            data.WeaknessComboId = WeaknessComboId;
            data.WeaknessMultiplierOverride = WeaknessMultiplier;

            data.BaseHP = MaxHp;
            data.BaseAttack = Attack;
            data.BaseSpeed = Speed;
            data.MaxEnergy = 3;
            data.BaseAttackRange = 1;

            // Sin esto su propio fuego lo quema: ShouldAffect exige
            // OwnerBossImmune && IsBoss && el dueño sea este guid, y ningún builder venía
            // escribiendo IsBoss, así que el jefe contaba como enemigo común.
            data.IsBoss = true;

            // No cura ni tiene behaviors de curación: dejar 0 evita autorar un número que miente.
            data.BaseHealStrength = 0;

            data.MinGoldDrop = MinGoldDrop;
            data.MaxGoldDrop = MaxGoldDrop;
            data.VisualPrefab = visualPrefab;

            // Sin retrato la cola de turnos y la barra de jefe caen a su visual default: no rompe, pero
            // el jefe se ve como cualquier otro enemigo del piso.
            data.Portrait = portrait;

            // Sin behaviors: el Croupier no tiene melee ni rango. Su único daño directo es la
            // Represalia, y esa entra por el hook de daño de la rueda, no por el árbol.
            data.Behaviors = new List<BaseBehavior>();
            data.ExtraTiers = new List<EnemyTier>();

            data.AIRoot = BuildAIRoot(fire);
        }

        /// <summary>
        /// Árbol del Croupier: la detonación de lo avisado, los dos gates de HP y el ciclo de dos
        /// tiempos.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Detonar va primero y armar va último, y esa es la separación de un turno.</b> "Pleno y
        /// color" se marca en el turno N y prende en el N+1 (ver <see cref="PlenoChannelId"/>), y lo
        /// que garantiza el turno de aviso es el <i>orden de los hijos</i>: el paso que prende está
        /// arriba del que marca, así que en el turno en que se marca ya pasó sin encontrar nada, y
        /// recién lo encuentra al turno siguiente. Mover la ignición debajo del marcado deja las dos
        /// cosas en el mismo tick — el overlay se muestra y se limpia en el mismo frame y el jugador
        /// no ve nada, que es el bug que esto arregla.
        /// </para>
        /// <para>
        /// <b>Detonar también tiene que ir antes del Alternate</b>, por el overlay: <c>Clear</c> y
        /// <c>Show</c> del overlay son por fuente, y la ignición limpia la de su canal. Con la
        /// ignición del Pleno detrás de T1 le pasaría el trapo al aviso de la banda que T1 acaba de
        /// levantar en el mismo turno.
        /// </para>
        /// <para>
        /// Cada paso que puede fallar va en <c>Selector[paso, Wait]</c>: el Sequence raíz corta en el
        /// primer <c>Failed</c>, y ahora hay un paso <b>después</b> del ciclo que no se puede perder,
        /// así que el ciclo también va envuelto. En el path coroutine —el del juego— un
        /// <c>Running</c> se drena y se promueve a <c>Succeeded</c>, así que el blink de la fuga no
        /// corta el Sequence y el armado del Pleno se alcanza igual.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(SpecialTileDefinitionSO fire)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // 1. Prende lo que "Pleno y color" marco el turno pasado. Va suelto y sin gate de
                    //    HP a proposito: el aviso hay que cobrarlo al turno siguiente exista o no la
                    //    condicion que lo levanto, y mientras nadie marco el canal esto es un no-op
                    //    (TryConsume sin nada pendiente devuelve Succeeded).
                    //    Duracion de fase 2 fija y sin ramificar: este canal solo se marca al cruzar
                    //    el 50%, asi que cuando hay algo que prender ya esta en fase 2.
                    Guarded(new AINode_IgniteArea
                    {
                        Definition = fire,
                        DurationRounds = FireDurationRoundsPhase2,
                        ChannelId = PlenoChannelId,
                        // 0 y no 1 --explicito porque es contraintuitivo--: el turno de aviso ya lo
                        // da el orden de los hijos. Este paso corre ARRIBA del que marca, asi que en
                        // el turno N pasa sin encontrar nada y la marca queda pendiente con su
                        // overlay puesto todo el turno del jugador; recien la encuentra en el N+1.
                        // Con 1 el nodo sumaria SU turno de espera arriba del que ya da el orden y
                        // prenderia en N+2.
                        AnnounceTurns = 0,
                        // El pano entero tapa por completo a cualquier banda vieja: sin esto ese
                        // terreno se queda con el reloj de la banda --el mas corto-- y el momento
                        // mas grande de la pelea se apaga en el wrap siguiente.
                        RetireFullyReplaced = true,
                    }),

                    // 2. Desde el 70% le queda un dado con candado. SIN AINode_Once: RotateBlock
                    //    hace Clear() antes de bloquear en cada tick y DiceBlockService se limpia
                    //    solo al cerrar cada turno del jugador, asi que "permanente" se consigue
                    //    re-emitiendolo todos los turnos. Con Once el candado duraria un turno.
                    //    Y va FUERA del Alternate por lo mismo: adentro solo se emitiria uno de
                    //    cada dos turnos y el dado parpadearia.
                    Guarded(new AINode_If
                    {
                        Conditions = new List<BasePreCondition>
                        {
                            new PcOwnerHpBelow { Percent = LockHpThreshold },
                        },
                        Then = new AINode_RotateBlock
                        {
                            Target = AINode_RotateBlock.BlockTarget.Dice,
                            // Indice fijo y no la ruleta: el candado tiene que caer siempre en el
                            // mismo dado para que se lea como "me saco ese", no como un sorteo.
                            DirectedIndex = new AIConstantInt { Value = LockedDieIndex },
                            BlockVfxId = BossFeedbackIds.CroupierConfiscaVfx,
                            BlockFeelId = BossFeedbackIds.CroupierConfiscaFeel,
                        },
                        Else = new AINode_Wait(),
                    }),

                    // 3. Los dos tiempos: la accion normal del turno. Alternate avanza el indice en
                    //    cada tick pase lo que pase, asi que un beat que falla igual gasta su turno
                    //    -- que es lo que queremos: el ciclo no se desincroniza nunca y el jugador
                    //    puede contar los turnos.
                    //    Envuelto en Guarded aunque el Alternate ya aisle sus beats: ahora hay un
                    //    paso DESPUES del ciclo (el armado del Pleno) y un Failed que se escape de
                    //    aca le cortaria el Sequence raiz.
                    Guarded(new AINode_Alternate
                    {
                        Children = new List<AIDecisionNode>
                        {
                            // -- T1 "Reparte" --------------------------------------------------
                            new AINode_Sequence
                            {
                                Children = new List<AIDecisionNode>
                                {
                                    // Dispara primero: si huyera antes, el tiro saldria desde la
                                    // casilla nueva y el jugador veria el fogonazo salir de donde
                                    // el jefe ya no esta.
                                    Guarded(new AINode_RangedShot
                                    {
                                        Damage = ShotDamage,
                                        Range = ShotRange,
                                        Kind = AttackKind.BasicAttack,
                                    }),

                                    // Huye. KeepDistance y no Move{Retreat}: Move busca una banda
                                    // ABSOLUTA y devuelve Failed en cuanto ya esta a esa distancia,
                                    // asi que a distancia 2 se quedaba clavado. Esto se aleja
                                    // mientras el jugador este dentro de FleeIdealDistance, y contra
                                    // la pared se desliza al costado en vez de fallar.
                                    Guarded(new AINode_KeepDistance
                                    {
                                        MaxSteps = new AIConstantInt { Value = FleeSteps },
                                        IdealDistance = new AIConstantInt { Value = FleeIdealDistance },
                                    }),

                                    // Marca la banda: anclada en el, apuntando al jugador.
                                    // Size es el SEMI-ancho, asi que 1 = 3 casillas de ancho.
                                    Guarded(new AINode_TelegraphMark
                                    {
                                        Shape = ThreatShape.DirectionalBand,
                                        Size = BandHalfWidth,
                                        Depth = BandDepth,
                                        Damage = 0,
                                        Kind = AttackKind.Environmental,
                                    }),
                                },
                            },

                            // -- T2 "Quema" ----------------------------------------------------
                            // No se mueve ni dispara: es el unico turno en que se queda quieto, y
                            // eso es lo que lo hace matable. Enciende lo que marco el turno pasado.
                            // La duracion la elige la fase. Se ramifica con un If en vez de
                            // subirla con ApplyStatModifier porque DurationRounds es un int del
                            // nodo, no un stat del jefe: no hay nada que modificar en runtime.
                            Guarded(new AINode_If
                            {
                                Conditions = new List<BasePreCondition>
                                {
                                    new PcOwnerHpBelow { Percent = PlenoHpThreshold },
                                },
                                // RetireFullyReplaced en los dos: el jefe huye sobre el mismo eje y
                                // la banda le sale de atras con la profundidad de la sala, asi que
                                // cada banda nueva contiene a la anterior. Sin esto el terreno
                                // compartido se queda con el reloj de la banda vieja --el mas
                                // corto-- y la banda recien avisada se apaga en el wrap siguiente
                                // sin haber ardido: el turno de quema no muestra nada. Es el caso
                                // normal de este jefe, una vez por ciclo, no un borde.
                                Then = new AINode_IgniteArea
                                {
                                    Definition = fire,
                                    DurationRounds = FireDurationRoundsPhase2,
                                    RetireFullyReplaced = true,
                                },
                                Else = new AINode_IgniteArea
                                {
                                    Definition = fire,
                                    DurationRounds = FireDurationRounds,
                                    RetireFullyReplaced = true,
                                },
                            }),
                        },
                    }),

                    // 4. El armado de "Pleno y color", una sola vez al cruzar el 50%: se planta en el
                    //    CENTRO de la sala y marca TODO el pano menos el cuadrado que lo rodea.
                    //    Prende al turno siguiente, arriba (paso 1).
                    //    VA ULTIMO, despues de la accion normal del turno, y eso es diseno: el turno
                    //    del aviso no es un turno perdido para el --dispara o prende su banda igual-- y
                    //    solo despues se corre al centro y marca. Ademas es lo que le da al jugador el
                    //    turno entero para cruzar la sala, que es lo que hace que el hueco pueda ser
                    //    chico (ver PlenoHoleRadius).
                    //    El centro no es decorativo: es lo que pone el hueco a la misma distancia de
                    //    las cuatro esquinas. Antes el hueco caia donde el jefe hubiera terminado de
                    //    huir --contra una pared--, asi que el efecto salia distinto cada pelea y a
                    //    veces no habia nada que recorrer.
                    Guarded(new AINode_If
                    {
                        Conditions = new List<BasePreCondition>
                        {
                            new PcOwnerHpBelow { Percent = PlenoHpThreshold },
                        },
                        Then = new AINode_Once
                        {
                            Child = new AINode_Sequence
                            {
                                Children = new List<AIDecisionNode>
                                {
                                    // PRIMERO, y desnudo (sin Guarded). Dos razones distintas:
                                    // 1) La marca ancla en la casilla del jefe en el momento del
                                    //    tick, asi que el teleport tiene que estar hecho antes de
                                    //    marcar o el hueco vuelve a caer donde estaba parado.
                                    // 2) Es el unico paso de aca que puede fallar de verdad (pide
                                    //    una casilla libre en el centro), y AINode_Once NO latchea
                                    //    con Failed. Ponerlo antes del anuncio de fase es lo que
                                    //    evita que un teleport fallado deje la fase 2 anunciada y la
                                    //    re-anuncie al turno siguiente. Guardarlo con un
                                    //    Selector[paso, Wait] romperia las dos cosas: se tragaria el
                                    //    Failed y el resto correria igual.
                                    new AINode_TeleportToRoomCenter
                                    {
                                        // Explicito y no por el default del nodo: en este mismo
                                        // turno el Alternate ya corrio y puede haber caido en T1,
                                        // cuyo KeepDistance ya marco su accion de movimiento. Lo que
                                        // esto evita es lo inverso --que un movimiento posterior lo
                                        // saque del centro-- y dejarlo explicito es lo que hace que
                                        // el orden de los pasos deje de ser load-bearing.
                                        ConsumeMoveAction = true,
                                    },
                                    // No sube el dano: solo anuncia (feedback + dialogo de fase).
                                    // Detras del teleport porque el evento no se puede "des-emitir".
                                    new AINode_ApplyStatModifier
                                    {
                                        AttackDelta = 0,
                                        SpeedDelta = 0,
                                        PhaseIndex = 2,
                                        EmitPhaseChangedEvent = true,
                                    },
                                    new AINode_TelegraphMark
                                    {
                                        Shape = ThreatShape.AllExceptSquareAroundSelf,
                                        // Size es el radio del HUECO que se salva, no del area
                                        // amenazada: 1 = deja libre su 3x3 y prende el resto.
                                        Size = PlenoHoleRadius,
                                        // Canal propio: la banda de T1 puede estar marcada en este
                                        // mismo turno y el servicio guarda un area por fuente.
                                        ChannelId = PlenoChannelId,
                                        // Lo cobra AINode_IgniteArea al consumir la marca. El numero
                                        // vive en la marca y no en el nodo que prende porque es de
                                        // ESTE aviso: la banda usa el mismo nodo con 0 porque
                                        // salirse de ella cuesta un paso al costado.
                                        Damage = PlenoIgnitionDamage,
                                        Kind = AttackKind.Environmental,
                                    },
                                },
                            },
                        },
                        Else = new AINode_Wait(),
                    }),
                },
            };
        }

        /// <summary>
        /// Envuelve un paso del turno en <c>Selector[paso, Wait]</c> — el idiom de aislamiento de
        /// fallos que ya usa el árbol del Sunken Grand.
        /// </summary>
        private static AINode_Selector Guarded(AIDecisionNode step)
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode> { step, new AINode_Wait() },
            };
        }

        // ======================================================================
        // Hazards
        // ======================================================================

        /// <summary>
        /// Crea/actualiza el <see cref="HazardDefinitionSO"/> de fuego de paño en
        /// <paramref name="path"/>. La duración va en el asset y no en un campo del nodo porque
        /// <see cref="IHazardService"/> la toma de la definición al activar: cambiarla desde el nodo
        /// pediría tocar el servicio, que es fundación compartida.
        /// </summary>
        /// <remarks>
        /// Hoy se llama una sola vez, para el hazard que consume La Bandida
        /// (<see cref="FirePhase1Path"/>): el Croupier ya no usa hazards y el de fase 2
        /// (<see cref="FirePhase2Path"/>) no lo referencia nadie, así que dejó de escribirse.
        /// </remarks>
        /// <param name="flame">
        /// Llama persistente y burst de pisada. <c>null</c> deja el fuego como estaba —sólo el quad
        /// naranja—: el visual no es parte del contrato del hazard, así que un builder corrido sin el
        /// prefab construido no rompe la pelea.
        /// </param>
        public static HazardDefinitionSO BuildFireDefinition(
            string path, int durationRounds, string sourceId, GameObject flame = null)
        {
            var fire = LoadOrCreate<HazardDefinitionSO>(path);

            if (flame != null)
            {
                // Las dos mitades del fuego: la llama dice "esta casilla está ardiendo" mientras dura,
                // el burst dice "y te acaba de cobrar". Sin la primera, entre pisada y pisada el
                // sector encendido se ve igual que uno apagado.
                fire.PersistentVfxPrefab = flame;
                fire.TriggerVfxPrefab = flame;
                fire.TriggerVfxLifetime = FireBurstLifetime;
            }

            fire.Trigger = HazardTriggerMode.OnTurnEndInTile;
            // Explícito y no por default: el Croupier enciende sus propios sectores y su fila es
            // costura de dos bloques, así que arde bajo sus pies todos los turnos. Es el jefe al que
            // un rebuild silencioso le costaría la vida.
            fire.Affects = HazardAffects.PlayerOnly;
            // El único consumidor de este asset es La Bandida (ver BandidaReelFireDamage): el número
            // que se escribe acá es el de ELLA, no el del fuego del Croupier.
            fire.Damage = BandidaReelFireDamage;
            fire.Kind = AttackKind.Environmental;
            fire.DurationRounds = durationRounds;
            fire.ConsumeOnTrigger = false; // El fuego quema el bloque entero, no una casilla y se apaga.
            fire.OverlayTint = FireOverlayTint;
            fire.SourceId = sourceId;

            // El área real la pasa el nodo de ignición (el sector que detonó). Shape queda declarativa:
            // el overload con tiles la ignora, pero deja dicho en el asset de qué forma es el fuego.
            fire.Shape = ThreatShape.RoomSector;
            fire.Size = 1;
            fire.CycleRounds = 1;

            EditorUtility.SetDirty(fire);
            return fire;
        }

        /// <summary>
        /// Deja <see cref="FireVfxPrefabPath"/> listo clonando el mesh de fuego del arte.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Se clona en vez de referenciar <see cref="FireMeshPrefabPath"/> directo porque el hazard
        /// instancia y destruye el objeto por casilla: apuntar al prefab del arte lo ataría a un uso
        /// que no es el suyo, y cualquier ajuste de escala para la grilla se le volcaría encima.
        /// </para>
        /// <para>
        /// A diferencia del burst de hielo, esto <b>no</b> es un ParticleSystem: es un mesh con luces.
        /// Por eso no se retinta el <c>startColor</c> de nada — el color ya viene en <c>Mat_Fire</c>.
        /// </para>
        /// </remarks>
        public static GameObject BuildFireVfx()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FireVfxPrefabPath) == null)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(FireMeshPrefabPath) == null)
                {
                    Debug.LogWarning($"[CroupierAssetBuilder] No está el mesh de fuego en " +
                                     $"'{FireMeshPrefabPath}' — el paño queda ardiendo sin llama.");
                    return null;
                }

                EnsureFolder(Path.GetDirectoryName(FireVfxPrefabPath));
                if (!AssetDatabase.CopyAsset(FireMeshPrefabPath, FireVfxPrefabPath))
                {
                    Debug.LogError($"[CroupierAssetBuilder] Falló el clon de " +
                                   $"'{FireMeshPrefabPath}' a '{FireVfxPrefabPath}'.");
                    return null;
                }
                AssetDatabase.ImportAsset(FireVfxPrefabPath);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(FireVfxPrefabPath);
        }

        // ======================================================================
        // Helpers de AssetDatabase
        // ======================================================================

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path));
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;

            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
