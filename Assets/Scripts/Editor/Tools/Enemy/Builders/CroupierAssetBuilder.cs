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
        public const int MaxHp = 300;
        public const int Attack = 24;
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
        /// "Arde 2 rondas" = 3 rondas de casilla. La duración tickea en el wrap de ronda y el fuego
        /// nace en el turno del jefe, o sea después del turno del jugador de esa ronda (CNF-006):
        /// la ronda en la que se enciende no le queda ningún arranque de turno del jugador por
        /// delante. Arrancar N turnos adentro pide autorar N + 1.
        /// </summary>
        public const int FireDurationRounds = 3;

        /// <summary>
        /// Duración de las bandas desde "Pleno y color": 4 de casilla = arde 3. Dos bandas pueden
        /// convivir, pero no se pisan — la nueva sólo enciende lo que no ardía (ver
        /// <c>AINode_IgniteArea.AlreadyBurning</c>).
        /// </summary>
        public const int FireDurationRoundsPhase2 = 4;

        /// <summary>
        /// Duracion del hazard de paño que este builder autora para La Bandida (sus reels lo
        /// consumen). El Croupier no lo usa.
        /// </summary>
        public const int HazardDurationForBandida = 3;

        // ======================================================================
        // Kiteo y fuego
        // ======================================================================

        /// <summary>Disparo de T1.</summary>
        public const int ShotDamage = 18;

        /// <summary>
        /// Alcance del disparo, en Manhattan. 24 cubre la diagonal entera de la sala 11x11: en la
        /// practica es "a cualquier distancia" sin escribir un centinela.
        /// </summary>
        public const int ShotRange = 24;

        /// <summary>
        /// Distancia Manhattan al jugador desde la que el jefe se toma la molestia de huir. A esta
        /// distancia o menos, los dos saltos del ciclo (T1 y T2) corren; más lejos, se quedan
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
        /// salta al centro de la sala, o se queda.
        /// </summary>
        /// <remarks>
        /// <para>
        /// El borde se lleva el peso más alto porque desaparecer <b>es</b> el personaje, no una
        /// preferencia de balance: tiene que seguir siendo el resultado que el jugador espera. El
        /// centro es el que tiene textura —lo alcanzás, pero desde el medio el cono no se recorta
        /// contra ninguna pared, así que amenaza sus 16 casillas enteras: es un canje y no un
        /// premio—. Quedarse es el premio gordo.
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
        public const float FleeWeightEdge = 50f;
        public const float FleeWeightCenter = 30f;
        public const float FleeWeightStay = 20f;

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
        /// Cual dado se traba. RotateBlock etiqueta el candado con el indice + 1, asi que 0
        /// muestra "1".
        /// </summary>
        public const int LockedDieIndex = 0;

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
        /// El Pleno y la banda de T1 pueden estar marcados en el mismo turno, y tanto
        /// <c>IThreatenedAreaService</c> como el overlay guardan un área por fuente: sin canal propio
        /// la segunda marca borra la primera. El paso que la consume tiene que declarar este mismo
        /// string.
        /// </remarks>
        public const string PlenoChannelId = "pleno";

        /// <summary>Rojo de brasa — se tiene que leer distinto del naranja del telegraph.</summary>
        public static readonly Color FireOverlayTint = new Color(0.85f, 0.10f, 0.05f, 0.60f);

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

            var boss = LoadOrCreate<EnemyDataSO>(BossAssetPath);
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
            Sprite portrait)
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
            data.BaseAttack = Attack;
            data.BaseSpeed = Speed;
            data.MaxEnergy = 3;
            data.BaseAttackRange = 1;

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

            data.AIRoot = BuildAIRoot(fire);
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
        /// son por fuente, así que detrás de T1 le pasaría el trapo al aviso de la banda que T1
        /// acaba de levantar.
        /// </para>
        /// <para>
        /// Cada paso que puede fallar va en <c>Selector[paso, Wait]</c> porque el Sequence raíz corta
        /// en el primer <c>Failed</c> y hay un paso después del ciclo que no se puede perder. En el
        /// path coroutine un <c>Running</c> se drena y se promueve a <c>Succeeded</c>, así que el
        /// blink de la fuga no corta el Sequence.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(SpecialTileDefinitionSO fire)
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
                        DurationRounds = FireDurationRoundsPhase2,
                        ChannelId = PlenoChannelId,
                        // 0 y no 1: el turno de aviso ya lo da el orden de los hijos. Con 1 el nodo
                        // sumaria SU turno de espera arriba del que ya da el orden y prenderia en
                        // N+2.
                        AnnounceTurns = 0,
                        // El pano entero tapa a cualquier banda vieja: sin esto ese terreno se queda
                        // con el reloj de la banda --el mas corto-- y se apaga en el wrap siguiente.
                        RetireFullyReplaced = true,
                    }),

                    // 2. Desde el 70% le queda un dado con candado. SIN AINode_Once: DiceBlockService
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
                            DirectedIndex = new AIConstantInt { Value = LockedDieIndex },
                            BlockVfxId = BossFeedbackIds.CroupierConfiscaVfx,
                            BlockFeelId = BossFeedbackIds.CroupierConfiscaFeel,
                        },
                        Else = new AINode_Wait(),
                    }),

                    // 3. Los dos tiempos: la accion normal del turno. Alternate avanza el indice en
                    //    cada tick pase lo que pase, asi que un beat que falla igual gasta su turno y
                    //    el ciclo no se desincroniza.
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
                                        // Los tres ids explicitos: vacios el nodo degrada a silencio
                                        // sin dar rojo, y el ataque sale sin gesto.
                                        AnimFeedbackId = BossFeedbackIds.CroupierRangeAnim,
                                        ImpactVfxFeedbackId = BossFeedbackIds.CroupierRangeImpactVfx,
                                        ImpactFeelFeedbackId = BossFeedbackIds.CroupierRangeImpactFeel,
                                    }),

                                    // Sortea la fuga SI el jugador esta cerca, y ANTES de marcar:
                                    // AINode_TelegraphMark ancla el cono en la casilla del jefe al
                                    // tickear y AINode_IgniteArea lo consume sin recalcularlo, asi
                                    // que marcando primero el fuego saldria de donde el jefe ya no
                                    // esta. Con el sorteo eso pesa MAS que antes: una de las tres
                                    // salidas lo deja en el centro de la sala, y desde ahi el cono
                                    // no se recorta contra ninguna pared.
                                    Guarded(FleeIfClose(FleeRoulette())),

                                    // Marca el cono: anclado en el, apuntando al jugador. Size es
                                    // el semi-ancho del APEX, asi que 0 arranca en una casilla y se
                                    // abre 1 por lado en cada paso.
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
                                        },
                                        Else = new AINode_IgniteArea
                                        {
                                            Definition = fire,
                                            DurationRounds = FireDurationRounds,
                                            RetireFullyReplaced = true,
                                        },
                                    }),

                                    // Sortea detras de prender (el fuego cae en las casillas
                                    // guardadas el turno anterior, asi que el orden no le cambia el
                                    // area) y con el mismo gate de cercania que T1: los dos tiempos
                                    // apuestan, no solo el de reparto. Instancia propia del sorteo
                                    // --ver FleeRoulette--, no la de T1.
                                    Guarded(FleeIfClose(FleeRoulette())),
                                },
                            },
                        },
                    }),

                    // 4. El armado de "Pleno y color", una sola vez al cruzar el 50%: se planta en el
                    //    CENTRO de la sala y marca TODO el pano menos el cuadrado que lo rodea.
                    //    Prende al turno siguiente, arriba (paso 1). Va ULTIMO, despues de la accion
                    //    normal del turno: eso es lo que le da al jugador el turno entero para llegar
                    //    al hueco. El centro pone el hueco a la misma distancia de las cuatro
                    //    esquinas.
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
                                    // de T1 marcada, y sin esto el jugador ve DOS areas prendidas a
                                    // la vez y al turno siguiente detonan las dos. Se descarta la
                                    // telegrafia, no el turno: el disparo de T1 ya se cobro.
                                    new AINode_CancelTelegraph(),
                                    new AINode_TelegraphMark
                                    {
                                        Shape = ThreatShape.AllExceptSquareAroundSelf,
                                        // Size es el radio del HUECO que se salva, no del area
                                        // amenazada: 1 = deja libre su 3x3 y prende el resto.
                                        Size = PlenoHoleRadius,
                                        // Canal propio: la banda de T1 puede estar marcada en este
                                        // mismo turno y el servicio guarda un area por fuente.
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
        /// borde, se planta en el centro de la sala, o se queda donde está.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Devuelve una instancia nueva por llamada, y eso es obligatorio.</b> Los dos tiempos del
        /// ciclo piden su propio sorteo: compartir el objeto los haría compartir nodo en el árbol, y
        /// un nodo con estado por instancia (o un consumidor que lo busque por identidad) empezaría a
        /// ver los dos tiempos como uno.
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
