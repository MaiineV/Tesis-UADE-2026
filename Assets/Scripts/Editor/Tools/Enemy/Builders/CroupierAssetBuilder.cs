using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Rooms;
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
    /// de AI inline y su prefab visual (arte del Sunken Grand retintado a carmesí de crupier). El fuego que
    /// usa <b>no</b> lo escribe: es la casilla especial de <see cref="CroupierFirePath"/>, autorada a
    /// mano. El único hazard que sí autora es el de La Bandida (ver <see cref="BandidaReelFireDamage"/>).
    /// </summary>
    /// <remarks>
    /// <see cref="BuildAIRoot"/>, <see cref="BuildWrapperSpec"/> y <see cref="PopulateEnemyData"/>
    /// son estáticos puros — se testean en memoria sin tocar el <c>AssetDatabase</c>.
    /// <see cref="BuildCroupier"/> es la capa que persiste, y es idempotente.
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
        /// Este builder no la escribe: el asset es autoría a mano, y sus números de daño no salen de
        /// ninguna constante de acá.
        /// </remarks>
        public const string CroupierFirePath = "Assets/Rollgeon/Tiles/Tile_Fire_Croupier.asset";

        /// <summary>Llama del paño, compartida con La Bandida.</summary>
        public const string FireVfxPrefabPath = "Assets/Prefabs/VFX/VFX_Fire.prefab";

        /// <summary>Mesh de fuego que trajo el arte; es un MeshRenderer con luces, no un sistema de partículas.</summary>
        private const string FireMeshPrefabPath = "Assets/Art/3D/Models/Items/Fire.prefab";

        /// <summary>Segundos que dura el fogonazo de pisar una casilla encendida.</summary>
        private const float FireBurstLifetime = 0.9f;

        /// <summary>
        /// Arte a vestir: <c>SunkedGrand_Animated</c>, compartido con el Tahúr del piso 3
        /// (<c>TahurAssetBuilder</c>), que lo separa por retinte. Es el wrapper del rig, no el FBX:
        /// saltearlo se lleva el <c>Animator</c> y el jefe entra a la pelea en T-pose.
        /// </summary>
        public const string ArtPrefabPath = "Assets/Prefabs/Enemies/SunkedGrand_Animated.prefab";

        /// <summary>Prefab de gameplay que sale del wrapper y va a <c>EnemyData.VisualPrefab</c>.</summary>
        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Croupier.prefab";

        /// <summary>Retrato de la cola de turnos. Ver <see cref="BossPortraitLibrary"/>.</summary>
        public const string PortraitTexturePath = BossPortraitLibrary.SheetPath;

        // ======================================================================
        // Ficha de diseño — todos los números del jefe, en un solo lugar
        // ======================================================================

        public const string EntityId = "boss.croupier";
        public const string DisplayName = "The Croupier";

        /// <summary>Su debilidad es el Poker (cuatro dados iguales).</summary>
        public const string WeaknessComboId = ComboId.Poker;

        /// <summary>
        /// Cuánto lo castiga el Poker. Multiplica el golpe entero ya resuelto (fórmula v3:
        /// <c>N = base_combo + ATQ + Σcaras + bonos</c>), no el base del combo.
        /// </summary>
        public const float WeaknessMultiplier = 1.3f;

        /// <summary>Vida del jefe de piso 1.</summary>
        public const int MaxHp = 250;
        public const int Speed = 5;
        public const int MinGoldDrop = 15;
        public const int MaxGoldDrop = 23;

        /// <summary>
        /// Daño del <see cref="HazardDefinitionSO"/> de paño que este builder autora en
        /// <see cref="FirePhase1Path"/>. <b>No es el fuego del Croupier.</b>
        /// </summary>
        /// <remarks>
        /// Ese asset lo consume <b>La Bandida</b> para sus reels reventados
        /// (<c>BandidaAssetBuilder.ReelFireHazardPath</c> apunta ahí), y este builder es el único que
        /// lo escribe — de ahí el nombre. El fuego del Croupier es la casilla especial de
        /// <see cref="CroupierFirePath"/>.
        /// </remarks>
        public const int BandidaReelFireDamage = 6;

        /// <summary>
        /// "Arde 3 rondas" = 4 rondas de casilla. La duración tickea en el wrap de ronda y el fuego
        /// nace en el turno del jefe, o sea después del turno del jugador de esa ronda (CNF-006):
        /// la ronda en la que se enciende no le queda ningún arranque de turno del jugador por
        /// delante. Arrancar N turnos adentro pide autorar N + 1.
        /// </summary>
        /// <remarks>
        /// El número lo fija el ciclo, no el gusto: el cono prende en uno de los <b>tres</b> tiempos,
        /// así que hay una ignición cada tres turnos y el fuego tiene que durar los tres. Con menos,
        /// el paño queda limpio entre cono y cono y deja de ser algo que haya que rodear; con más, las
        /// bandas se apilan y la sala se queda sin piso.
        /// </remarks>
        public const int FireDurationRounds = 4;

        /// <summary>
        /// Duración de las bandas desde "Pleno y color": 5 de casilla = arde 4. Exactamente una ronda
        /// más que <see cref="FireDurationRounds"/>, y eso es lo que hace que dos bandas convivan
        /// durante el relevo — el único escalón de dificultad del umbral. No se pisan: la nueva sólo
        /// enciende lo que no ardía (ver <c>AINode_IgniteArea.AlreadyBurning</c>).
        /// </summary>
        public const int FireDurationRoundsPhase2 = 5;

        /// <summary>
        /// Duración del paño que prende "Pleno y color": 2 de casilla = arde 1. Es un fogonazo, no
        /// terreno — prende todo salvo el 3x3 del jefe, así que si durara como una banda no habría
        /// dónde pararse. Va aparte de <see cref="FireDurationRoundsPhase2"/> porque el Pleno cruza
        /// el umbral y compartir la constante lo ataría a la duración de las bandas.
        /// </summary>
        public const int PlenoFireDurationRounds = 2;

        /// <summary>
        /// Duracion del hazard de paño que este builder autora para La Bandida (sus reels lo
        /// consumen). El Croupier no lo usa.
        /// </summary>
        public const int HazardDurationForBandida = 3;

        // ======================================================================
        // Kiteo y fuego
        // ======================================================================

        /// <summary>Disparo del tiempo de reparto.</summary>
        public const int ShotDamage = 18;

        /// <summary>
        /// Alcance del disparo, en Manhattan. 24 cubre la diagonal entera de la sala 11x11: en la
        /// practica es "a cualquier distancia" sin escribir un centinela.
        /// </summary>
        public const int ShotRange = 24;

        /// <summary>
        /// Distancia Manhattan al jugador desde la que el jefe se toma la molestia de huir. A esta
        /// distancia o menos, los tres saltos del ciclo corren; más lejos, se quedan
        /// plantados.
        /// </summary>
        /// <remarks>
        /// Más allá de este umbral huir no le compra nada: el disparo no tiene techo
        /// (<see cref="ShotRange"/>) y el cono se marca desde donde esté parado, así que tepearse
        /// igual sólo le gasta el turno y a veces lo hace aterrizar más cerca de lo que estaba. Sin
        /// tope de aterrizaje (ver los dos <c>AINode_TeleportAwayToEdge</c>) porque la garantía de
        /// que la pelea sea ganable ya no depende de dónde cae el salto: si huye lejos, el jugador
        /// camina hacia él y el jefe no vuelve a huir hasta estar de nuevo dentro de este radio.
        /// </remarks>
        public const int FleeTriggerRange = 5;

        /// <summary>
        /// Pesos del sorteo de la fuga: adentro del radio no huye siempre, apuesta. Se va al borde,
        /// se te viene encima, salta al centro de la sala, o se queda.
        /// </summary>
        /// <remarks>
        /// <para>
        /// El acercamiento se lleva el grueso: el kit del jefe es todo a distancia, así que cerrar
        /// la distancia es la cara que le devuelve el turno al jugador —y por eso la banda lo deja
        /// cerca y no pegado—. Desaparecer <b>es</b> el personaje, pero con el borde pesado la fuga
        /// es la reacción cantada y la pelea se va en caminar detrás de él; acá es la cara rara, la
        /// que castiga cuando sale. El centro es el que tiene textura —lo alcanzás, pero desde el
        /// medio el cono no se recorta contra ninguna pared, así que amenaza sus 16 casillas
        /// enteras: es un canje y no un premio—. Quedarse es el piso del sorteo.
        /// </para>
        /// <para>
        /// Son <b>pesos</b>, no porcentajes: <c>AINode_Random</c> normaliza contra la suma. Suman 100
        /// para que se lean como porcentajes, no porque el nodo lo pida.
        /// </para>
        /// <para>
        /// Ojo con el vocabulario: <c>BossFeedbackIds</c> usa "la ruleta" para el mecanismo retirado
        /// del número cantado que giraba un rodillo. Este sorteo no tiene nada que ver con eso.
        /// </para>
        /// </remarks>
        public const float FleeWeightEdge = 20f;
        public const float FleeWeightNear = 55f;
        public const float FleeWeightCenter = 15f;
        public const float FleeWeightStay = 10f;

        /// <summary>Banda del acercamiento, en Manhattan al jugador. Ver
        /// <see cref="AINode_TeleportNearTarget"/>: pegado sería regalarle un turno franco.</summary>
        public const int NearMinDistance = 2;
        public const int NearMaxDistance = 3;

        /// <summary>Semi-ancho del apex del cono: 0 = arranca en una sola casilla.</summary>
        public const int ConeApexHalfWidth = 0;

        /// <summary>
        /// Profundidad del cono, contada desde la casilla del jefe hacia el jugador.
        /// </summary>
        /// <remarks>
        /// El jefe marca desde el borde al que acaba de saltar, no desde el medio de la sala, y el
        /// cono se abre una casilla por lado en cada paso: el fondo no escala el area en linea sino
        /// al cuadrado. En 4 cubre 16 casillas, casi las mismas 18 que barria la banda de 3x6.
        /// </remarks>
        public const int ConeDepth = 4;

        /// <summary>Umbral del candado: desde aca le queda un dado menos, y no vuelve.</summary>
        public const float LockHpThreshold = 0.7f;

        /// <summary>
        /// Dados que traba el candado por turno. El cual lo sortea el nodo, no este builder: el
        /// candado sale sin etiqueta y eso es lo correcto para un sorteo, porque no hay ningun
        /// numero cantado al que atarlo.
        /// </summary>
        public const int LockedDiceCount = 1;

        /// <summary>Umbral de "Pleno y color".</summary>
        public const float PlenoHpThreshold = 0.5f;

        /// <summary>
        /// Radio del hueco que "Pleno y color" NO prende, centrado en el jefe. 1 = su 3x3. Ojo que
        /// en <c>AllExceptSquareAroundSelf</c> el Size es el hueco, no el area amenazada.
        /// </summary>
        public const int PlenoHoleRadius = 1;

        /// <summary>
        /// Lo que cobra "Pleno y color" en el momento de prender, a quien este parado afuera del
        /// hueco.
        /// </summary>
        public const int PlenoIgnitionDamage = 7;

        /// <summary>
        /// Canal de la marca de "Pleno y color" (<c>AINode_TelegraphMark.ChannelId</c>).
        /// </summary>
        /// <remarks>
        /// El Pleno y la banda del cono pueden estar marcados en el mismo turno, y tanto
        /// <c>IThreatenedAreaService</c> como el overlay guardan un área por fuente: sin canal propio
        /// la segunda marca borra la primera. El paso que la consume tiene que declarar este mismo
        /// string.
        /// </remarks>
        public const string PlenoChannelId = "pleno";

        /// <summary>Rojo de brasa — se tiene que leer distinto del naranja del telegraph.</summary>
        public static readonly Color FireOverlayTint = new Color(0.85f, 0.10f, 0.05f, 0.60f);

        // ======================================================================
        // Bombas — el primer tiempo del ciclo, y lo que ya está puesto al entrar a la sala
        // ======================================================================

        public const string BombDefinitionPath = "Assets/Rollgeon/Combat/RoomObjects/RO_Croupier_Bomba.asset";
        public const string BombDefinitionId = "roomobj.croupier.bomba";
        public const string BombFireTilePath = "Assets/Rollgeon/Tiles/Tile_Fire_CroupierBomba.asset";
        public const string BombFireTileId = "TILE_FIRE_CROUPIER_BOMBA";
        public const string BombArtPrefabPath = "Assets/Art/3D/Models/Items/Bomb.fbx";
        public const string BombVisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Obj_Bomba.prefab";

        /// <summary>
        /// Bombas por siembra. Se leen contra las <see cref="BombFuseTurns"/> acciones que la mecha le
        /// deja al jugador: romperlas todas es posible con energía de sobra, pero el turno normal
        /// obliga a elegir cuáles.
        /// </summary>
        public const int BombCount = 3;

        /// <summary>
        /// Turnos que la bomba está en pie, o sea acciones que el jugador tiene para romperla. Es
        /// <b>menos que el ciclo</b> a propósito, y por eso el estallido vive en un nodo aparte
        /// (<see cref="AINode_DetonateBombField"/>) tickeado todos los turnos: un nodo que corre una
        /// vez por ciclo sólo puede expresar un plazo de un ciclo.
        /// </summary>
        /// <remarks>
        /// Con la siembra abriendo el ciclo, 2 hace que el estallido caiga siempre en el tiempo de
        /// reparto — el tercero —, o sea lejos del turno en que arde el cono. El jugador aprende en
        /// qué turno le explota el paño, y nunca se le juntan los dos fuegos.
        /// </remarks>
        public const int BombFuseTurns = 2;

        /// <summary>
        /// La siembra rota <c>+</c> y <c>×</c>, y arranca por la cruz ortogonal.
        /// </summary>
        /// <remarks>
        /// Las dos formas cubren 5 casillas, así que el fuego que queda pesa lo mismo y el balance no
        /// se mueve. Lo que se mueve es la lectura: con la <c>+</c> el lugar donde pararse al lado de
        /// una bomba es la diagonal, y con la <c>×</c> es justo la que mata. Rotando, la esquiva del
        /// ciclo anterior no sirve para el siguiente y hay que volver a mirar el paño.
        /// </remarks>
        public const AINode_BombField.BlastShape BombShape = AINode_BombField.BlastShape.Alternating;

        /// <summary>
        /// Separación mínima, en Chebyshev, entre bombas y contra el propio jefe.
        /// </summary>
        /// <remarks>
        /// <b>3 y no 2</b>: a 2 dos cruces alineadas comparten la casilla del medio, y ahí las dos
        /// bombas se leen como una mancha en vez de como dos preguntas. A 3 no se tocan nunca — ni
        /// las ortogonales ni las aspas, que se solaparían recién a 2.
        /// El precio es cuántas entran — medido sobre las 103 caminables de la sala, a 3 entran
        /// <see cref="BombCount"/> en el 100% de las siembras, ocho en el 92% y diez sólo en el 5%.
        /// </remarks>
        public const int BombSpacing = 3;

        /// <summary>
        /// Vida de cada bomba. Contra el daño del piso 1 (13-27) el golpe flojo ya no alcanza: lo
        /// que la bomba cobra sigue siendo <b>la acción</b>, pero una tirada pobre la deja en pie.
        /// El dado de La Generala tiene 45 porque ahí sí se quiere fundir una barra.
        /// </summary>
        public const int BombHp = 18;

        /// <summary>
        /// Lo que cobra la casilla que deja una bomba que llegó al plazo: los 10 del paño más 5 por
        /// haberla dejado madurar.
        /// </summary>
        public const int BombFireDamage = 15;

        /// <summary>
        /// Lo que cobra el estallido en sí: <b>nada</b>, igual que el cono. Quien esté parado en la
        /// cruz cuando prende paga los <see cref="BombFireDamage"/> al arrancar su turno ahí, que es
        /// lo que le da el turno para salirse. Cobrar también al prender lo cobraría dos veces.
        /// </summary>
        public const int BombIgnitionDamage = 0;

        /// <summary>
        /// De acá sale el canal de amenaza de cada bomba (prefijo + su guid). Uno por bomba es lo que
        /// hace que romper una levante <b>su</b> cruz: el servicio guarda un área por fuente.
        /// </summary>
        public const string BombChannelPrefix = "croupier.bomb.";

        // ======================================================================
        // Ficha visual — paleta
        // ======================================================================

        // Los ocho materiales de SunkedGrand_Animated y qué visten (mismo mapa que usa el Tahúr,
        // que viste el mismo rig — ver TahurAssetBuilder.BuildRetints):
        //   Mat_LightBrown → levita, galera y moño. Es el área grande de la silueta.
        //   Mat_Brown      → paneles y solapas del cuerpo.
        //   Mat_Green      → cinta de la galera.
        //   Mat_Bone       → canto de las 12 cartas del abanico.
        //   Mat_Black      → dorso de las cartas y detalles oscuros.
        //   Mat_LightGreen → piel (cabeza y manos).
        //   Mat_White      → caras de las cartas y camisa.
        //   Mat_Particle_Red → partícula del rig, no superficie.
        //
        // El Tahúr viste este mismo rig, y el material que un jefe no retinta se lo queda
        // compartido: los vuelve gemelos en esa superficie.
        //
        // Todos los colores van explícitos y no por PaletteSlot: los labels guardados en
        // PA_MainPalette están desalineados respecto de la tabla de PaletteSlots, así que un slot no
        // dice qué color sale.

        /// <summary>Carmesí de terciopelo del paño — el color que el jefe le presta a la mesa.</summary>
        public static readonly Color WineLight = new Color(0.647f, 0.157f, 0.251f);
        public static readonly Color WineMid = new Color(0.404f, 0.055f, 0.129f);
        public static readonly Color WineShadow = new Color(0.212f, 0.024f, 0.075f);

        /// <summary>Smoking casi negro con tiro a borravino: mantiene la luminancia del gris original.</summary>
        public static readonly Color TuxLight = new Color(0.196f, 0.145f, 0.169f);
        public static readonly Color TuxMid = new Color(0.129f, 0.090f, 0.110f);
        public static readonly Color TuxShadow = new Color(0.063f, 0.043f, 0.055f);

        /// <summary>Latón: la cinta de la galera y el canto de los naipes tienen que saltar del vino.</summary>
        public static readonly Color BrassLight = new Color(0.980f, 0.855f, 0.529f);
        public static readonly Color BrassMid = new Color(0.831f, 0.635f, 0.196f);
        public static readonly Color BrassShadow = new Color(0.439f, 0.310f, 0.078f);

        /// <summary>
        /// Piel de cera. Existe sólo porque el rig la trae en <c>Mat_LightGreen</c>: sin retintarla el
        /// jefe hereda el gris verdoso de ahogado del Sunken Grand.
        /// </summary>
        public static readonly Color WaxLight = new Color32(0xEE, 0xE2, 0xCF, 0xFF);
        public static readonly Color WaxMid = new Color32(0xCF, 0xBB, 0x9E, 0xFF);
        public static readonly Color WaxShadow = new Color32(0x87, 0x71, 0x59, 0xFF);

        /// <summary>
        /// Barra de vida más baja que el default de 3: el arte mide ~1.81 con galera, y a 3 quedaba
        /// flotando sobre el sombrero.
        /// </summary>
        public static readonly Vector3 HealthBarOffset = new Vector3(0f, 2.4f, 0f);

        /// <summary>
        /// Tope del radio del capsule. Los bounds de este rig dan ~0.95, que se come casi todo el
        /// tile vecino: el jugador tiene que poder clickear esas cuatro casillas para salir del
        /// fuego.
        /// </summary>
        public const float ColliderRadiusCap = 0.5f;

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

            // Este hazard lo consume La Bandida para sus reels, no el Croupier.
            BuildFireDefinition(FirePhase1Path, HazardDurationForBandida, FirePhase1SourceId, flame);

            var visual = BuildVisualPrefab();
            var portrait = BossPortraitLibrary.Croupier();

            var bombFire = EnsureBombFireTile();
            var bomb = EnsureBombDefinition(BuildBombVisual());

            var boss = LoadOrCreate<EnemyDataSO>(BossAssetPath);
            var croupierFire = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(CroupierFirePath);
            if (croupierFire == null)
            {
                Debug.LogError($"[CroupierAssetBuilder] Falta {CroupierFirePath}. El jefe queda sin " +
                               "fuego: el nodo de ignicion falla y sus turnos de quema no hacen nada.");
            }

            PopulateEnemyData(boss, croupierFire, visual, portrait, bomb, bombFire);

            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CroupierAssetBuilder] Listo: '{BossAssetPath}' + '{VisualPrefabPath}' + sus " +
                      $"bombas ('{bomb.name}' × {BombCount}, {BombHp} HP, mecha {BombFuseTurns}, con " +
                      $"'{bombFire.name}' a {BombFireDamage}) + el hazard de paño de La Bandida " +
                      $"('{FirePhase1Path}').");
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
                    // Levita, galera y moño: el smoking.
                    { "Mat_LightBrown", MaterialRetint.FromColors(TuxLight, TuxMid, TuxShadow) },
                    // Paneles y solapas: el paño carmesí que el jefe le presta a la mesa.
                    { "Mat_Brown", MaterialRetint.FromColors(WineLight, WineMid, WineShadow) },
                    // Cinta de la galera y canto de los naipes: los dos vivos de latón.
                    { "Mat_Green", MaterialRetint.FromColors(BrassLight, BrassMid, BrassShadow) },
                    { "Mat_Bone", MaterialRetint.FromColors(BrassLight, BrassMid, BrassShadow) },
                    // Dorso de las cartas y detalles oscuros: el tono del traje.
                    { "Mat_Black", MaterialRetint.FromColors(TuxLight, TuxMid, TuxShadow) },
                    // Piel. Ver WaxLight: sin esto queda con la cara del Sunken Grand.
                    { "Mat_LightGreen", MaterialRetint.FromColors(WaxLight, WaxMid, WaxShadow) },
                },
                Props = new List<BossPropSpec>(),
            };
        }

        /// <summary>
        /// Construye el wrapper. Devuelve <c>null</c> (con warning ya logueado por el wrapper) si el
        /// arte no está: el jefe queda sin <c>VisualPrefab</c>, que es exactamente lo que hay que ver
        /// en consola en vez de un prefab a medias.
        /// </summary>
        private static GameObject BuildVisualPrefab()
        {
            var wrapper = BossVisualWrapperBuilder.BuildWrapper(BuildWrapperSpec());
            if (wrapper == null) return null;

            ClampColliderRadius(VisualPrefabPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
        }

        /// <summary>
        /// Segunda pasada sobre el wrapper ya guardado: recorta el radio del capsule a
        /// <see cref="ColliderRadiusCap"/>.
        /// </summary>
        /// <remarks>
        /// Pasada aparte porque el wrapper dimensiona el collider contra los bounds del arte.
        /// Reescribir sobre el mismo path conserva el GUID, así que la ficha que ya apunta al
        /// wrapper sobrevive.
        /// </remarks>
        private static void ClampColliderRadius(string prefabPath)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogWarning($"[CroupierAssetBuilder] No se pudo abrir '{prefabPath}' para " +
                                 "recortar el collider — queda envolviendo las casillas vecinas.");
                return;
            }

            try
            {
                var capsule = contents.GetComponent<CapsuleCollider>();
                if (capsule == null || capsule.radius <= ColliderRadiusCap) return;

                capsule.radius = ColliderRadiusCap;
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }


        // ======================================================================
        // Datos del jefe (puro — sin AssetDatabase)
        // ======================================================================

        /// <summary>
        /// Escribe la ficha completa del Croupier en <paramref name="data"/>, incluido su
        /// <see cref="EnemyDataSO.AIRoot"/>. No toca <c>AssetDatabase</c>: sirve igual para el asset
        /// real y para una instancia in-memory de test.
        /// </summary>
        public static void PopulateEnemyData(
            EnemyDataSO data,
            SpecialTileDefinitionSO fire,
            GameObject visualPrefab,
            Sprite portrait,
            RoomObjectDefinitionSO bombs = null,
            SpecialTileDefinitionSO bombFire = null)
        {
            if (data == null) return;

            data.EntityId = EntityId;
            data.DisplayName = DisplayName;
            // Una línea, sin números y sin prometer un resultado: el tooltip es un adelanto, no la
            // ficha, y la fuga es un sorteo — nombrar una sola de las tres salidas miente la mitad
            // de los turnos.
            data.Description =
                "Burns the ground in front of him and rolls for the exit when you crowd him.";

            data.WeaknessComboId = WeaknessComboId;
            data.WeaknessMultiplierOverride = WeaknessMultiplier;

            data.BaseHP = MaxHp;
            data.BaseSpeed = Speed;
            data.MaxEnergy = 3;

            // Espejo del disparo, no un número propio. El jefe es 100% a distancia: su árbol no
            // lleva ningún nodo de melee, así que este par no lo lee nadie en runtime —
            // TreeDrivenEnemyAI saltea el BasicEnemyAI que sería su único consumidor. Pero se lee
            // a mano, y con un 24 a alcance 1 heredado del diseño viejo el bloque de stats decía
            // que el jefe de piso 1 pega más de cerca que el de piso 2, que es lo contrario de la
            // verdad. Un 0 tampoco servía: leído al lado de otro jefe lo hace parecer inofensivo.
            data.BaseAttack = ShotDamage;
            data.BaseAttackRange = ShotRange;

            // Sin esto su propio fuego lo quema: ShouldAffect exige
            // OwnerBossImmune && IsBoss && que el dueño sea este guid.
            data.IsBoss = true;

            data.BaseHealStrength = 0;

            data.MinGoldDrop = MinGoldDrop;
            data.MaxGoldDrop = MaxGoldDrop;
            data.VisualPrefab = visualPrefab;

            data.Portrait = portrait;

            data.Behaviors = new List<BaseBehavior>();
            data.ExtraTiers = new List<EnemyTier>();

            data.AIRoot = BuildAIRoot(fire, bombs, bombFire);
        }

        /// <summary>
        /// Árbol del Croupier: la detonación de lo avisado, los dos gates de HP y el ciclo de dos
        /// tiempos.
        /// </summary>
        /// <remarks>
        /// <para>
        /// El turno de aviso lo da el <i>orden de los hijos</i>: el paso que prende está arriba del
        /// que marca, así que en el turno en que se marca pasa sin encontrar nada y recién lo
        /// encuentra al siguiente. Mover la ignición debajo del marcado deja las dos cosas en el
        /// mismo tick: el overlay se muestra y se limpia en el mismo frame y el jugador no ve nada.
        /// </para>
        /// <para>
        /// La ignición también va antes del <c>Alternate</c>: <c>Clear</c> y <c>Show</c> del overlay
        /// son por fuente, así que detrás del ciclo le pasaría el trapo al aviso que el tiempo de
        /// las bombas acaba de levantar.
        /// </para>
        /// <para>
        /// Cada paso que puede fallar va en <c>Selector[paso, Wait]</c> porque el Sequence raíz corta
        /// en el primer <c>Failed</c> y hay un paso después del ciclo que no se puede perder. En el
        /// path coroutine un <c>Running</c> se drena y se promueve a <c>Succeeded</c>, así que el
        /// blink de la fuga no corta el Sequence.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(
            SpecialTileDefinitionSO fire,
            RoomObjectDefinitionSO bombs = null,
            SpecialTileDefinitionSO bombFire = null)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // 1. Prende lo que "Pleno y color" marco el turno pasado. Suelto y sin gate de HP:
                    //    el aviso hay que cobrarlo al turno siguiente exista o no la condicion que lo
                    //    levanto, y sin nada marcado en el canal es un no-op (TryConsume sin nada
                    //    pendiente devuelve Succeeded). La duracion no se ramifica porque este canal
                    //    solo se marca al cruzar el 50%.
                    Guarded(new AINode_IgniteArea
                    {
                        Definition = fire,
                        DurationRounds = PlenoFireDurationRounds,
                        ChannelId = PlenoChannelId,
                        // 0 y no 1: el turno de aviso ya lo da el orden de los hijos. Con 1 el nodo
                        // sumaria SU turno de espera arriba del que ya da el orden y prenderia en
                        // N+2.
                        AnnounceTurns = 0,
                        WindupFeedbackId = BossFeedbackIds.CroupierMeleeAnim,
                        // OFF, al reves que las bandas: el Pleno es el reloj mas corto de los tres,
                        // asi que relevar lo que tapa le recortaria la banda que ya venia ardiendo a
                        // un solo turno. Apagado, lo que ardia sigue con su reloj y el Pleno prende
                        // el resto del pano — que es lo unico que tiene que durar un turno.
                        // AlreadyBurning ya evita el doble cobro por su cuenta.
                        RetireFullyReplaced = false,
                    }),

                    // 2. La mecha de las bombas. FUERA del Alternate y tickeado todos los turnos:
                    //    es lo unico que permite un plazo mas corto que el ciclo, porque un nodo que
                    //    corre una vez cada tres turnos solo puede expresar tres. Y ANTES del ciclo,
                    //    o en el turno de la siembra detonaria lo que ese mismo turno acaba de
                    //    plantar.
                    Guarded(new AINode_DetonateBombField
                    {
                        FireTile = bombFire,
                        FireDurationRounds = FireDurationRounds,
                        IgnitionDamage = BombIgnitionDamage,
                        ChannelPrefix = BombChannelPrefix,

                        // Los ids son los que autoro AINode_DetonateSungSectors --la ruleta que se
                        // retiro-- y siguen instalados en el FeedbackDB.
                        DetonationVfxId = BossFeedbackIds.CroupierImpactVfx,
                        DetonationFeelId = BossFeedbackIds.CroupierImpactFeel,
                    }),

                    // 3. Desde el 70% le queda un dado con candado. SIN AINode_Once: DiceBlockService
                    //    se limpia solo al cerrar cada turno del jugador, asi que "permanente" se
                    //    consigue re-emitiendolo todos los turnos; con Once duraria un turno. Y va
                    //    FUERA del Alternate por lo mismo: adentro se emitiria uno de cada dos.
                    Guarded(new AINode_If
                    {
                        Conditions = new List<BasePreCondition>
                        {
                            new PcOwnerHpBelow { Percent = LockHpThreshold },
                        },
                        Then = new AINode_RotateBlock
                        {
                            Target = AINode_RotateBlock.BlockTarget.Dice,
                            // Sin DirectedIndex: el nodo sortea con el Rng del contexto, y como se
                            // re-emite cada turno el dado trabado cambia turno a turno. Count = 1
                            // porque el candado es uno; con mas de uno deja de ser una molestia y
                            // pasa a decidir la tirada.
                            Count = LockedDiceCount,
                            BlockVfxId = BossFeedbackIds.CroupierConfiscaVfx,
                            BlockFeelId = BossFeedbackIds.CroupierConfiscaFeel,

                            // El candado se re-emite todos los turnos porque DiceBlockService se
                            // limpia solo; el cartel no. Sin esto el jugador ve el mismo aviso desde
                            // el 70% hasta el final de la pelea.
                            AnnounceOnce = true,
                        },
                        Else = new AINode_Wait(),
                    }),

                    // 4. Los tres tiempos: la accion normal del turno. Alternate avanza el indice en
                    //    cada tick pase lo que pase, asi que un beat que falla igual gasta su turno y
                    //    el ciclo no se desincroniza.
                    Guarded(new AINode_Alternate
                    {
                        Children = new List<AIDecisionNode>
                        {
                            // -- T1 "Bombas" ---------------------------------------------------
                            // Solo siembra y marca: la mecha la descuenta el nodo de arriba, todos
                            // los turnos. Sembrando aca y con BombFuseTurns en 2, el estallido cae
                            // siempre en el turno de reparto del ciclo siguiente.
                            new AINode_Sequence
                            {
                                Children = new List<AIDecisionNode>
                                {
                                    Guarded(new AINode_BombField
                                    {
                                        Definition = bombs,
                                        Count = BombCount,
                                        Shape = BombShape,
                                        Spacing = BombSpacing,
                                        FuseTurns = BombFuseTurns,
                                        IgnitionDamage = BombIgnitionDamage,
                                        ChannelPrefix = BombChannelPrefix,
                                        SowFeedbackId = BossFeedbackIds.CroupierRangeAnim,
                                    }),

                                    // Mismo gate de cercania que los otros dos tiempos: los tres
                                    // apuestan, si no este seria el turno gratis para acercarsele.
                                    Guarded(FleeIfClose(FleeRoulette())),

                                    // El aviso del cono, DESPUES de la fuga: AINode_TelegraphMark
                                    // ancla el cono en la casilla del jefe al tickear y
                                    // AINode_IgniteArea lo consume sin recalcularlo, asi que
                                    // marcando antes de huir el fuego saldria de donde el jefe ya no
                                    // esta. Size es el semi-ancho del APEX: 0 arranca en una casilla
                                    // y se abre 1 por lado en cada paso.
                                    Guarded(new AINode_TelegraphMark
                                    {
                                        Shape = ThreatShape.DirectionalCone,
                                        Size = ConeApexHalfWidth,
                                        Depth = ConeDepth,
                                        Damage = 0,
                                        Kind = AttackKind.Environmental,
                                    }),
                                },
                            },

                            // -- T2 "Quema" ----------------------------------------------------
                            new AINode_Sequence
                            {
                                Children = new List<AIDecisionNode>
                                {
                                    // Enciende lo que marco el turno pasado. La duracion la elige la
                                    // fase con un If porque DurationRounds es un int del nodo, no un
                                    // stat que se pueda modificar en runtime.
                                    Guarded(new AINode_If
                                    {
                                        Conditions = new List<BasePreCondition>
                                        {
                                            new PcOwnerHpBelow { Percent = PlenoHpThreshold },
                                        },
                                        // RetireFullyReplaced en los dos: cada banda nueva contiene a
                                        // la anterior, y sin esto el terreno compartido se queda con
                                        // el reloj de la vieja --el mas corto-- y la banda recien
                                        // avisada se apaga en el wrap siguiente sin haber ardido.
                                        Then = new AINode_IgniteArea
                                        {
                                            Definition = fire,
                                            DurationRounds = FireDurationRoundsPhase2,
                                            RetireFullyReplaced = true,
                                            WindupFeedbackId = BossFeedbackIds.CroupierMeleeAnim,
                                        },
                                        Else = new AINode_IgniteArea
                                        {
                                            Definition = fire,
                                            DurationRounds = FireDurationRounds,
                                            RetireFullyReplaced = true,
                                            WindupFeedbackId = BossFeedbackIds.CroupierMeleeAnim,
                                        },
                                    }),

                                    // Sortea detras de prender (el fuego cae en las casillas
                                    // guardadas el turno anterior, asi que el orden no le cambia el
                                    // area) y con el mismo gate de cercania que los otros dos: los
                                    // tres tiempos apuestan, no solo el de reparto. Instancia propia
                                    // del sorteo --ver FleeRoulette--, no la de los otros tiempos.
                                    Guarded(FleeIfClose(FleeRoulette())),
                                },
                            },

                            // -- T3 "Reparte" --------------------------------------------------
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
                                        // Los tres ids explicitos: vacios el nodo degrada a silencio
                                        // sin dar rojo, y el ataque sale sin gesto.
                                        AnimFeedbackId = BossFeedbackIds.CroupierRangeAnim,
                                        ImpactVfxFeedbackId = BossFeedbackIds.CroupierRangeImpactVfx,
                                        ImpactFeelFeedbackId = BossFeedbackIds.CroupierRangeImpactFeel,
                                    }),

                                    // Sortea la fuga SI el jugador esta cerca. Este tiempo no marca
                                    // nada, asi que la fuga va al final y ya.
                                    Guarded(FleeIfClose(FleeRoulette())),
                                },
                            },
                        },
                    }),

                    // 5. El armado de "Pleno y color", una sola vez al cruzar el 50%: se planta en el
                    //    CENTRO de la sala y marca TODO el pano menos el cuadrado que lo rodea.
                    //    Prende al turno siguiente, arriba (paso 1). Va ULTIMO, despues de la accion
                    //    normal del turno: eso es lo que le da al jugador el turno entero para llegar
                    //    al hueco. El centro pone el hueco a la misma distancia de las cuatro
                    //    esquinas.
                    //
                    //    Las DOS condiciones a la vez, y el Once adentro del If a proposito: parado en
                    //    el centro el Once no tickea, asi que no latchea y el ataque queda esperando a
                    //    que su propia fuga lo saque. El salto ES el ataque, y sin salto no hay
                    //    sorpresa que dar.
                    Guarded(new AINode_If
                    {
                        Conditions = new List<BasePreCondition>
                        {
                            new PcOwnerHpBelow { Percent = PlenoHpThreshold },
                            new PCComposite
                            {
                                Mode = CompositeMode.Not,
                                Children = new List<BasePreCondition> { new PcOwnerAtRoomCenter() },
                            },
                        },
                        Then = new AINode_Once
                        {
                            Child = new AINode_Sequence
                            {
                                Children = new List<AIDecisionNode>
                                {
                                    // PRIMERO, y desnudo (sin Guarded). Dos razones:
                                    // 1) La marca ancla en la casilla del jefe en el momento del
                                    //    tick, asi que el teleport tiene que estar hecho antes de
                                    //    marcar o el hueco cae donde estaba parado.
                                    // 2) Es el unico paso de aca que puede fallar (pide una casilla
                                    //    libre en el centro) y AINode_Once NO latchea con Failed:
                                    //    guardarlo con un Selector[paso, Wait] se tragaria el Failed
                                    //    y dejaria la fase 2 anunciada sin teleport.
                                    new AINode_TeleportToRoomCenter
                                    {
                                        // Explicito y no por el default: consume la accion de
                                        // movimiento para que ningun paso posterior lo saque del
                                        // centro.
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
                                    // Saca de la cola el aviso del ciclo antes de encolar este: el
                                    // Alternate ya corrio en este mismo turno y pudo dejar la banda
                                    // del cono marcada, y sin esto el jugador ve DOS areas prendidas
                                    // a la vez y al turno siguiente detonan las dos. Se descarta la
                                    // telegrafia, no el turno: el tiempo que corrio ya se cobro.
                                    new AINode_CancelTelegraph(),
                                    new AINode_TelegraphMark
                                    {
                                        Shape = ThreatShape.AllExceptSquareAroundSelf,
                                        // Size es el radio del HUECO que se salva, no del area
                                        // amenazada: 1 = deja libre su 3x3 y prende el resto.
                                        Size = PlenoHoleRadius,
                                        // Canal propio: la banda del cono puede estar marcada en
                                        // este mismo turno y el servicio guarda un area por fuente.
                                        ChannelId = PlenoChannelId,
                                        // Lo cobra AINode_IgniteArea al consumir la marca: el numero
                                        // vive en la marca, no en el nodo que prende, porque es de
                                        // ESTE aviso.
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

        /// <summary>
        /// Gatea la reacción de fuga por cercanía: sólo corre <paramref name="reaction"/> si el
        /// jugador está a <see cref="FleeTriggerRange"/> o menos (Manhattan). <c>Else = Wait</c> y
        /// no vacío: un <c>If</c> sin <c>Else</c> devuelve <c>Failed</c> cuando la condición no
        /// pasa, y ese <c>Failed</c> aborta el <c>Sequence</c> del tiempo entero.
        /// </summary>
        private static AINode_If FleeIfClose(AIDecisionNode reaction)
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcTargetInRange { Range = FleeTriggerRange, Metric = DistanceMetric.Manhattan },
                },
                Then = reaction,
                Else = new AINode_Wait(),
            };
        }

        /// <summary>
        /// El sorteo de la fuga: adentro del radio el jefe apuesta en vez de huir siempre. Se va al
        /// borde, se te viene encima, se planta en el centro de la sala, o se queda donde está.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Devuelve una instancia nueva por llamada, y eso es obligatorio.</b> Los tres tiempos del
        /// ciclo piden su propio sorteo: compartir el objeto los haría compartir nodo en el árbol, y
        /// un nodo con estado por instancia (o un consumidor que lo busque por identidad) empezaría a
        /// ver los tres tiempos como uno.
        /// </para>
        /// <para>
        /// <b>El orden de las opciones es contrato.</b> <c>AINode_Random</c> acumula pesos y devuelve
        /// la primera que pasa el corte, así que quedarse/irse dependen de en qué lugar está cada una
        /// — los tests que fuerzan un resultado por RNG asumen este orden exacto.
        /// </para>
        /// <para>
        /// El "se queda" es un <c>AINode_Wait</c> y <b>no</b> <c>null</c>: un <c>Option.Node</c> nulo
        /// devuelve <c>Failed</c>, y ese <c>Failed</c> se comería el resto del tiempo (el disparo ya
        /// cobrado no, pero sí el marcado del cono que viene después).
        /// </para>
        /// </remarks>
        private static AINode_Random FleeRoulette()
        {
            return new AINode_Random
            {
                Options = new List<AINode_Random.Option>
                {
                    new AINode_Random.Option
                    {
                        Weight = FleeWeightEdge,
                        // Sin tope de aterrizaje: cuando le sale huir, huye de verdad. Que la pelea
                        // sea ganable ya no depende de dónde cae, sino de que el sorteo a veces no
                        // salga borde.
                        Node = new AINode_TeleportAwayToEdge
                        {
                            MaxDistanceFromPlayer = 0,
                            ConsumeMoveAction = true,
                        },
                    },
                    new AINode_Random.Option
                    {
                        Weight = FleeWeightNear,
                        Node = new AINode_TeleportNearTarget
                        {
                            MinDistance = NearMinDistance,
                            MaxDistance = NearMaxDistance,
                            ConsumeMoveAction = true,
                        },
                    },
                    new AINode_Random.Option
                    {
                        Weight = FleeWeightCenter,
                        // El mismo nodo que usa el armado del Pleno, pero OTRA instancia: la del
                        // Pleno no puede quedar colgada de este gate de cercanía o dejaría de
                        // plantarse en el centro al cruzar el 50%.
                        Node = new AINode_TeleportToRoomCenter { ConsumeMoveAction = true },
                    },
                    new AINode_Random.Option
                    {
                        Weight = FleeWeightStay,
                        Node = new AINode_Wait(),
                    },
                },
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
                // Las dos mitades del fuego: sin la llama persistente, entre pisada y pisada el
                // sector encendido se ve igual que uno apagado.
                fire.PersistentVfxPrefab = flame;
                fire.TriggerVfxPrefab = flame;
                fire.TriggerVfxLifetime = FireBurstLifetime;
            }

            fire.Trigger = HazardTriggerMode.OnTurnEndInTile;
            // Explícito y no por default: el jefe enciende sus propios sectores y arde bajo sus pies
            // todos los turnos.
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
        /// Se clona en vez de referenciar <see cref="FireMeshPrefabPath"/> directo porque el hazard
        /// instancia y destruye el objeto por casilla, y cualquier ajuste de escala para la grilla se
        /// le volcaría al prefab del arte. No es un ParticleSystem sino un mesh con luces: no hay
        /// <c>startColor</c> que retintar, el color viene en <c>Mat_Fire</c>.
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
        // Bombas
        // ======================================================================

        /// <summary>
        /// Ficha del wrapper de la bomba. <b>Sin retintes</b>: el arte trae sus propios materiales y
        /// pisarlos generaría materiales nuevos para un objeto que ya se lee.
        /// </summary>
        /// <remarks>
        /// De acá sale la barra de vida, y es la misma que la de cualquier enemigo del juego: la
        /// arma <see cref="BossVisualWrapperBuilder"/> con el atlas de
        /// <see cref="BossVisualWrapperBuilder.HealthBarAtlasPath"/>. No hay UI propia de la bomba.
        /// </remarks>
        public static BossWrapperSpec BuildBombSpec(BossArtFitter.ArtFit fit)
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = BombArtPrefabPath,
                OutputPrefabPath = BombVisualPrefabPath,
                BossName = "BombaCroupier",
                MaterialsFolder = BossVisualWrapperBuilder.DefaultMaterialsRoot + "/Croupier",

                AddHealthBar = true,
                HealthBarOffset = fit.HealthBarOffset,

                // Box y no Capsule: es un objeto chico apoyado en el piso, y el capsule del default
                // se le come las casillas vecinas — que son justo las cuatro que la bomba amenaza.
                Collider = ColliderKind.Box,

                Props = new List<BossPropSpec>(),
            };
        }

        /// <summary>
        /// Alto de la bomba ya apoyada. Por encima del dado de La Generala (0.8): la bomba es lo que
        /// el jugador tiene que decidir romper turno a turno, y a menos de esto no se le ve ni la
        /// barra.
        /// </summary>
        public const float BombTargetHeight = 1f;

        /// <summary>
        /// Ancho máximo. Todavía por debajo de la casilla: la bomba llena su tile pero no derrama
        /// sobre las cuatro que amenaza, que son justo las que el jugador tiene que poder leer libres.
        /// </summary>
        public const float BombMaxWidth = 0.9f;

        /// <summary>
        /// Aire entre la bomba y su barra. El mismo que el del jefe: con la barra a tamaño de enemigo
        /// (ver <see cref="BuildBombVisual"/>) menos que esto la deja apoyada sobre el arte.
        /// </summary>
        private const float BombBarClearance = 0.6f;

        /// <summary>
        /// El fit es obligatorio y no cosmético: <c>Bomb.fbx</c> tiene el pivot en el centro del
        /// volumen, así que el wrapper solo lo deja del alto del arte original y con media bomba
        /// abajo del piso.
        /// </summary>
        /// <remarks>
        /// <b>Sin encoger la barra.</b> El dado de La Generala la lleva a 0.35 y ahí los números no se
        /// leen; la bomba usa la barra <b>tal cual la lleva cualquier enemigo del juego</b> (escala 1),
        /// que es de dónde sale <see cref="BombBarClearance"/>: a tamaño completo necesita el mismo
        /// aire que la de un jefe.
        /// </remarks>
        public static GameObject BuildBombVisual()
        {
            var fit = BossArtFitter.Measure(
                BombArtPrefabPath, BombTargetHeight, BombMaxWidth, BombBarClearance);

            var wrapper = BossVisualWrapperBuilder.BuildWrapper(BuildBombSpec(fit));
            if (wrapper == null) return null;

            BossArtFitter.Apply(BombVisualPrefabPath, fit);
            return AssetDatabase.LoadAssetAtPath<GameObject>(BombVisualPrefabPath);
        }

        public static RoomObjectDefinitionSO EnsureBombDefinition(GameObject visual)
        {
            var def = LoadOrCreate<RoomObjectDefinitionSO>(BombDefinitionPath);
            ConfigureBombDefinition(def, visual);

            EditorUtility.SetDirty(def);
            return def;
        }

        /// <summary>
        /// Escribe los números de la bomba sobre <paramref name="def"/>. Parte pura, separada del
        /// <c>Ensure</c> para que los tests del turno la armen en memoria sin tocar el
        /// <c>AssetDatabase</c>.
        /// </summary>
        public static void ConfigureBombDefinition(RoomObjectDefinitionSO def, GameObject visual = null)
        {
            if (def == null) return;

            def.Id = BombDefinitionId;
            def.DisplayName = "Bomba";
            def.Hp = BombHp;

            // Bloquea, como el dado de La Generala: la bomba ocupa su casilla y hay que rodearla.
            def.Blocks = true;
            def.HideFromTurnQueue = true;

            // 0 y no -1: la siembra entera se rehace en cada tick del campo de bombas, y esa
            // reposición inmediata es lo que rellena tanto lo que detonó como lo que el jugador
            // rompió a mano. Con -1 la ranura se retira y no vuelve a sembrarse nunca.
            def.RespawnDelayTurns = 0;

            // Nada de hazard al morir: romperla a mano NO deja fuego. El fuego es exclusivamente lo
            // que deja la que llegó al plazo, y eso lo prende el campo de bombas, no la muerte.
            def.OnDeathHazard = null;

            // Y no le dan armadura al jefe: las bombas son un reloj del jugador, no el blindaje que
            // sí son la mesa de La Generala y los reels de La Bandida.
            def.OwnerDamageReductionPerObject = 0f;

            if (visual != null) def.VisualPrefab = visual;
        }

        public static SpecialTileDefinitionSO EnsureBombFireTile()
        {
            var tile = LoadOrCreate<SpecialTileDefinitionSO>(BombFireTilePath);
            ConfigureBombFireTile(
                tile, AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(CroupierFirePath));

            EditorUtility.SetDirty(tile);
            return tile;
        }

        /// <summary>
        /// El fuego que deja una bomba: el mismo del paño con 5 más encima. Casilla aparte y no un
        /// número sobre <see cref="CroupierFirePath"/> porque las dos conviven en la misma sala — el
        /// cono sigue cobrando 10 en el mismo turno en que las bombas cobran 15.
        /// </summary>
        /// <param name="basefire">
        /// El fuego del paño, del que copia todo salvo el daño. Con <c>null</c> la casilla queda con
        /// los defaults y sin arte: los tests del turno sólo le miran los números.
        /// </param>
        public static void ConfigureBombFireTile(
            SpecialTileDefinitionSO tile, SpecialTileDefinitionSO basefire = null)
        {
            if (tile == null) return;

            tile.TileId = BombFireTileId;
            tile.DisplayName = "Fuego de Bomba";
            tile.TileType = SpecialTileType.FireTemp;

            tile.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            tile.Category = TileEffectCategory.Damage;
            tile.Affinity = TileAffinity.All;
            tile.DamageKind = AttackKind.Environmental;

            tile.EnterDamage = BombFireDamage;
            tile.TurnStartDamage = BombFireDamage;

            tile.DisarmOnTrigger = false;
            tile.RearmOnRoundWrap = false;

            // Se quema con lo suyo, igual que con el fuego del paño: es lo que le da sentido a que
            // sus reacomodos esquiven las casillas que hacen daño.
            tile.OwnerBossImmune = false;

            if (basefire == null) return;

            tile.DefaultDurationRounds = basefire.DefaultDurationRounds;
            tile.VisualPrefab = basefire.VisualPrefab;
            tile.VisualYOffset = basefire.VisualYOffset;
            tile.OverlayTint = basefire.OverlayTint;
            tile.TriggerVfxPrefab = basefire.TriggerVfxPrefab;
            tile.TriggerVfxLifetime = basefire.TriggerVfxLifetime;
            tile.TriggerVfxYOffset = basefire.TriggerVfxYOffset;
            tile.EditorIcon = basefire.EditorIcon;
            tile.EditorColor = basefire.EditorColor;
            tile.NameKey = basefire.NameKey;
            tile.DescriptionKey = basefire.DescriptionKey;
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
