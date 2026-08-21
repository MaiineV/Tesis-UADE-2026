using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Tiles;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Arma el asset del jefe de piso 2, <b>El Cajero</b> (<c>boss.cashier</c>), desde su ficha de
    /// diseño: stats, debilidad, drop de oro y el árbol de AI completo.
    /// </summary>
    /// <remarks>
    /// <see cref="BuildAIRoot"/> y <see cref="PopulateEnemyData"/> son estáticas y puras — se testean
    /// en memoria sin tocar el <c>AssetDatabase</c>. El <see cref="MenuItem"/> es el único que
    /// escribe, y es idempotente. Un prefab visual distinto de los dos conocidos se considera
    /// autorado a mano y no se pisa (ver <see cref="ResolveVisualPrefab"/>).
    /// </remarks>
    public static class CajeroAssetBuilder
    {
        // ---- Rutas -------------------------------------------------------

        public const string EnemyAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Cajero.asset";
        public const string ChipHazardPath = "Assets/Rollgeon/Combat/Hazards/HZ_Cashier_Chip.asset";

        /// <summary>Moneda que se ve en la casilla mientras la ficha está sin levantar.</summary>
        private const string CoinModelPath = "Assets/Art/3D/Models/Items/coin.fbx";

        /// <summary>Levanta la moneda del piso lo justo para que no z-fightee con el quad del overlay.</summary>
        private const float CoinYOffset = 0.12f;

        /// <summary>
        /// Arte del jefe: figura alada con seis discos de fichas modelados en el propio mesh
        /// (<c>Coin_Chips_1..6</c>) más las alas, animada por <c>SteppedAnimation</c> a 8 FPS sobre
        /// <c>AnimCon_GeneralDirector</c> (Idle / Attack).
        /// </summary>
        public const string ArtPrefabPath = "Assets/Prefabs/Enemies/GeneralDirector_Animated.prefab";

        /// <summary>Wrapper de gameplay que arma <see cref="BossVisualWrapperBuilder"/>.</summary>
        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Cajero.prefab";

        /// <summary>Caja de fichas, parenteada al costado del jefe. Es lo que dice de un vistazo
        /// que la plata es su tema.</summary>
        public const string ChipsBoxPropPath = "Assets/Prefabs/Props/CajaFichasv01.prefab";

        public const string ChipsBoxPropName = "ChipsBox";

        /// <summary>Retrato del rig que viste (<c>GeneralDirector_Animated</c>). Ver <see cref="BossPortraitLibrary"/>.</summary>
        public const string PortraitTexturePath = BossPortraitLibrary.SheetPath;

        /// <summary>
        /// Prefab que usaba el jefe mientras no tenía arte propio. Se sigue conociendo para poder
        /// migrarlo: una ficha que todavía lo apunte se actualiza al wrapper sin preguntar.
        /// </summary>
        public const string PlaceholderVisualPrefabPath = "Assets/Prefabs/Enemies/SecurityGuardBoss.prefab";

        // ---- Ficha (números del diseño; una sola fuente de verdad) --------

        public const string EntityId = "boss.cashier";
        public const string DisplayName = "El Cajero";

        /// <summary>
        /// Piso 2. Aguanta la pelea larga que pide la ficha: el jugador tiene que poder elegir
        /// varias veces entre pegarle y juntar monedas, y con 170 la elección no llegaba a
        /// aparecer. Lo que se cura con monedas vencidas es presupuesto aparte
        /// (<see cref="MaxHealPerFight"/>): suma turnos sin figurar acá.
        /// </summary>
        public const int BaseHP = 350;

        /// <summary>Mandoble. Baja de 30: el techo de daño por turno ahora lo pone el tumbo.</summary>
        public const int BaseAttack = 14;

        public const int BaseSpeed = 4;
        public const int MaxEnergy = 3;
        public const int MinGoldDrop = 30;
        public const int MaxGoldDrop = 60;
        public const float WeaknessMultiplier = 1.5f;

        /// <summary>Alcance de sus dos ataques. Melee puro: no tiene nada a distancia.</summary>
        public const int MeleeRange = 1;

        /// <summary>
        /// Pasos por turno cuando persigue. Atado a <see cref="BaseSpeed"/> en el mismo archivo a
        /// propósito: la ficha dice "camina 4" y ese 4 es su velocidad, no un segundo número que
        /// pueda separarse. Va como constante y no como lectura del stat porque
        /// <c>AIReadSelfStat</c> devuelve 0 sin <c>AttributesManager</c> (EditMode) y un MaxSteps
        /// de 0 deja al jefe clavado, sin caer a ningún default.
        /// </summary>
        public const int ChaseSteps = BaseSpeed;

        // ---- Los dos golpes ----------------------------------------------

        /// <summary>
        /// Mandoble: su <see cref="BaseAttack"/> y nada más. Es su piso de daño — el turno que no
        /// se puede evitar de ninguna manera estando a su alcance.
        /// </summary>
        public const int HeavyDamage = BaseAttack;

        /// <summary>
        /// Empujón. Pega menos que el mandoble porque lo que cobra de verdad es el tumbo: cada
        /// casilla de pinchos que cruce suma <see cref="SpikeDamage"/>.
        /// </summary>
        public const int ShoveDamage = 10;

        /// <summary>Casillas del tumbo. Frena en seco contra una caja fuerte o contra la pared.</summary>
        public const int ShovePushTiles = 3;

        // ---- Las monedas -------------------------------------------------

        public const int ChipMinValue = 6;
        public const int ChipMaxValue = 9;

        /// <summary>Monedas que suelta la sala por tanda.</summary>
        public const int CoinsPerRain = 4;

        /// <summary>Rondas entre tandas de la sala.</summary>
        public const int CoinRainEveryNRounds = 3;

        /// <summary>
        /// Distancia Chebyshev mínima entre dos monedas de la misma tanda. "Repartidas por la
        /// sala" es media mecánica: cada moneda tiene que ser un punto al que ir, y cuatro pegadas
        /// serían un solo viaje.
        /// </summary>
        public const int CoinRainMinSeparation = 2;

        /// <summary>HP que le devuelve al jefe cada moneda que el jugador deja vencer.</summary>
        public const int HealPerExpiredCoin = 12;

        /// <summary>
        /// Techo de curación en toda la pelea. Es lo que hace que juntar monedas sea la jugada
        /// ganadora en vez de una carrera imposible: alcanzado el techo las monedas vencidas
        /// siguen desapareciendo, pero ya no lo curan.
        /// </summary>
        public const int MaxHealPerFight = 60;

        /// <summary>
        /// Rondas que vive una moneda en el piso.
        /// </summary>
        /// <remarks>
        /// Ya no es el <c>DurationRounds</c> del hazard: el reloj lo lleva
        /// <c>AINode_CajeroCoinVault</c>, el único que puede distinguir una moneda levantada de una
        /// vencida (el servicio de hazards expira las dos igual). La moneda nace permanente y ese
        /// nodo la mata — ver <see cref="EnsureChipHazard"/>.
        /// <para>
        /// Es el vencimiento de cada moneda, no el de la tanda: el nodo se cobra <b>una por turno</b>,
        /// así que las cuatro que nacen juntas salen del piso en cuatro turnos y no en uno.
        /// </para>
        /// </remarks>
        public const int ChipDurationRounds = 3;

        /// <summary>Monedas que se le caen al jugador en cada empujón, repartidas por el tumbo.</summary>
        public const int ChipCount = 2;

        /// <summary>Id estable del hazard-ficha: el servicio de hazards keyea por él. Hex válido —
        /// un SourceId que no parsea a Guid loguea error cada vez que se lee.</summary>
        public const string ChipHazardSourceId = "3c0a7d18-9f42-4a6b-9c3e-5b1ca5e70001";

        // ---- Los pinchos de la sala --------------------------------------

        /// <summary>
        /// Pinchos propios y no <c>Tile_Spikes</c>: el genérico pega 12 y no encarece la ruta de la
        /// IA, y tocarlo se lo cambiaría a todas las salas del juego. Mismo criterio que
        /// <c>Tile_Fire_Croupier</c>.
        /// </summary>
        public const string SpikeTilePath = "Assets/Rollgeon/Tiles/Tile_Spikes_Cajero.asset";

        public const string SpikeTileId = "TILE_SPIKES_CAJERO";

        /// <summary>Daño al entrar, también empujado. Es el mismo para el jugador y para él.</summary>
        public const int SpikeDamage = 14;

        /// <summary>
        /// Costo virtual que hace que el pathing lea un pincho armado como <b>intransitable</b> y no
        /// como caro.
        /// </summary>
        /// <remarks>
        /// <c>AIPathPlanner.ComputeHazardPenalty</c> es <c>ceil(daño / HP × 10 × Caution)</c> y
        /// <c>ComputeTileCost</c> es <c>1 + penalty</c>. Con los 14 reales sobre 350 de vida el
        /// penalty da 1 y la casilla cuesta 2: rodea si el desvío es de un paso y se la come si es
        /// de dos. Sumando esto, <c>14 + 336 = 350</c> sobre 350 da penalty 10 y la casilla cuesta
        /// 11 — más que cualquier desvío posible dentro de un movimiento de
        /// <see cref="ChaseSteps"/> pasos. <b>No es daño</b>: el filtro de supervivencia sólo mira
        /// los 14 reales, así que la mitad "empujado se los come igual" no se toca.
        /// </remarks>
        public const int SpikeAIVirtualDamage = 336;

        /// <summary>Pinchos de la sala, sueltos y en casillas exactas. Ver <see cref="SpikePlanCells"/>.</summary>
        public const int SpikeCount = 10;

        /// <summary>
        /// Layout de los pinchos en coordenadas del plano de <c>BossRoomBuilder</c> (11 × 11,
        /// origen arriba-izquierda, <c>y</c> hacia abajo; la sala real sale de
        /// <c>BossRoomBuilder.PlanToRoom</c>).
        /// </summary>
        /// <remarks>
        /// <b>Fuente única del layout</b>, no una copia: el plano del Cajero en
        /// <c>BossRoomBuilder.Plans</c> lee este array y lo escribe en
        /// <c>RoomLayout.SpecialTilePlacements</c> de <c>Boss_Room_Cajero</c>. Vive acá y no allá
        /// porque la regla que lo gobierna —<b>ninguno toca a otro, ni en diagonal</b>— es de la
        /// ficha del jefe, y es lo que los tests de esta ficha verifican.
        /// <para>
        /// La definición de la casilla es <see cref="SpikeTilePath"/>: este builder la crea, el de
        /// salas la coloca. Ninguno de los dos duplica lo del otro.
        /// </para>
        /// </remarks>
        public static readonly Vector2Int[] SpikePlanCells =
        {
            new Vector2Int(2, 1), new Vector2Int(6, 2), new Vector2Int(9, 2),
            new Vector2Int(2, 4), new Vector2Int(6, 4), new Vector2Int(5, 6),
            new Vector2Int(9, 7), new Vector2Int(1, 8), new Vector2Int(5, 9),
            new Vector2Int(9, 9),
        };

        /// <summary>
        /// Las seis cajas fuertes: lo único que bloquea, y lo único que frena un empujón en seco.
        /// Mismas coordenadas de plano que <see cref="SpikePlanCells"/>.
        /// </summary>
        /// <remarks>
        /// Contra los costados para que el centro quede abierto: la pelea pasa en el medio porque es
        /// donde hay lugar para que te tire. Entran como <c>BlockerPlanCells</c> del plano del Cajero
        /// en <c>BossRoomBuilder</c> y no como casillas especiales: lo suyo es <b>bloquear</b>, y una
        /// casilla especial no toca el grafo de navegación.
        /// </remarks>
        public static readonly Vector2Int[] SafeBoxPlanCells =
        {
            new Vector2Int(1, 1), new Vector2Int(8, 1),
            new Vector2Int(2, 7), new Vector2Int(7, 7),
            new Vector2Int(1, 9), new Vector2Int(8, 9),
        };

        // ---- Las Comisiones ----------------------------------------------

        /// <summary>
        /// Ficha de la Comisión: el tirador volador que el Cajero suelta al cruzar el 50%.
        /// </summary>
        /// <remarks>
        /// Va en <c>ED_Min_</c> y no en <c>ED_Obj_</c> porque pega. Los otros dos acompañantes
        /// autorados por un builder de jefe (<c>ED_Obj_DadoCasa</c>, <c>ED_Obj_Rodillo</c>) son
        /// terreno con vida: Attack 0, no actúan. Esta sí, y meterla en la misma familia haría que
        /// "obj." dejara de querer decir nada.
        /// </remarks>
        public const string CritterAssetPath = "Assets/Rollgeon/Enemies/ED_Min_Comision.asset";

        /// <summary>
        /// Lo que el Cajero invoca: su propia Comisión, no el ranged común del juego.
        /// </summary>
        /// <remarks>
        /// El ranged común trae 50 de vida y 10 de daño, y a la altura del 50% dos de ésos son otro
        /// jefe. La ficha pide 18 y 6: molestan, se limpian si te ocupás, y no te obligan a dejar de
        /// pelear con él. Ese kit no se puede autorar sobre <c>ED_RangedEnemy</c> —es el asset
        /// compartido de todos los encuentros normales— así que vive en su propia ficha.
        /// </remarks>
        public const string ReinforcementAssetPath = CritterAssetPath;

        public const string CritterVisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Min_Comision.prefab";

        public const string CritterEntityId = "minion.cajero_comision";
        public const string CritterDisplayName = "Comisión";

        /// <summary>Nombre corto para la carpeta y el prefijo de sus materiales clonados.</summary>
        public const string CritterName = "Comision";

        /// <summary>Dos, y una sola vez. Ver <see cref="BuildCritterGate"/>.</summary>
        public const int CritterCount = 2;

        /// <summary>
        /// Único umbral y único evento de la pelea: al cruzar la mitad de la vida suelta las dos
        /// Comisiones y nada más cambia — ni sus números, ni sus ataques, ni el ritmo de las monedas.
        /// </summary>
        public const float CritterHpThreshold = 0.5f;

        /// <summary>
        /// Muere de un golpe cualquiera y sobrevive a uno flojo. Es la medida de lo que cuesta
        /// sacárselos de encima: un golpe cada uno, dos golpes que no fueron al jefe.
        /// </summary>
        public const int CritterHp = 18;

        /// <summary>
        /// Su disparo. Los dos juntos pegan 12 por turno, menos que el mandoble del jefe: son el
        /// precio de huir, no una segunda amenaza principal.
        /// </summary>
        public const int CritterDamage = 6;

        /// <summary>
        /// Alcance del disparo.
        /// </summary>
        /// <remarks>
        /// <b>La ficha no da alcance</b>: da 18 de vida y 6 de daño y nada más. Así que el número no
        /// se elige, se hereda del bicho del que la Comisión es una variante — el ranged común del
        /// juego, <c>Assets/Rollgeon/Enemies/ED_RangedEnemy.asset</c>, que gatea su disparo con
        /// <c>PcTargetInRange { Range = 5, Metric = Manhattan }</c> y se acerca con
        /// <c>AINode_Move { DesiredRange = 5 }</c>. Lo que la ficha sí decide —vida y daño— es lo
        /// único que se aparta de ese asset.
        /// </remarks>
        public const int CritterRange = 5;

        /// <summary>Vuela: va antes que el jefe (4) en la cola, así el turno en que aparecen ya presionan.</summary>
        public const int CritterSpeed = 5;

        /// <summary>
        /// Alcance de vuelo por turno. Tres cubre media sala, y con el disparo a
        /// <see cref="CritterRange"/> alcanza para tapar el único agujero del jefe: el jugador
        /// camina 5 y él 4, así que sin ellas se le escapa indefinidamente juntando monedas.
        /// </summary>
        public const int CritterMoveSteps = 3;

        // ---- Vestuario de la Comisión ------------------------------------

        /// <summary>
        /// Escala del arte dentro de su wrapper. El rig mide ~2 de alto (es el del jefe); a 0.45
        /// queda en ~0.9, que es "bicho" al lado de un Cajero de cuerpo entero y sigue leyéndose en
        /// qué casilla está.
        /// </summary>
        public const float CritterArtScale = 0.45f;

        /// <summary>
        /// Altura a la que flota el arte sobre su casilla. <b>Es el único recurso que hay</b>: la
        /// única elevación de pawn del proyecto (<c>EntityPawn.PawnYOffset</c>) es un <c>const</c>
        /// privado de 0.1 compartido por héroe y enemigos, así que levantarlo de ahí levantaría a
        /// todo el bestiario. Se levanta el hijo <c>Art</c> del wrapper, que es lo que ya hacen
        /// <c>GeneralaAssetBuilder.ApplyArtFit</c> y el lift del rodillo de la Bandida.
        /// <para>
        /// 0.7 ≈ tres cuartos de su propio alto: suficiente para que se vea aire abajo desde la
        /// cámara iso sin que quede fuera del encuadre de su casilla.
        /// </para>
        /// </summary>
        public const float CritterHoverHeight = 0.7f;

        /// <summary>Aire entre la punta del bicho y su barra de vida.</summary>
        private const float CritterBarClearance = 0.35f;

        /// <summary>
        /// La barra está autorada en unidades de mundo para un jefe de 2 de alto: sobre un bicho de
        /// 0.9 tapa la entidad entera. Mismo encogimiento que el dado de la Generala.
        /// </summary>
        private const float CritterBarScale = 0.4f;

        /// <summary>Nombre del hijo que envuelve el arte — el default de <see cref="BossWrapperSpec"/>.</summary>
        private const string ArtChildName = "Art";

        /// <summary>Nombre del hijo con la barra de vida world-space que arma el wrapper.</summary>
        private const string HealthBarChildName = "Canvas";

        // Plata y no oro. La Comisión viste el MISMO rig que el jefe (es el único alado del
        // proyecto), así que sin un corte de color fuerte el jugador ve tres Cajeros de tamaños
        // distintos. Los discos en plata dicen "cambio chico" de un vistazo y el cuerpo se va a un
        // verde más apagado que el del jefe: misma especie, rango menor.
        private static readonly MaterialRetint CritterChipShineRetint = MaterialRetint.FromColors(
            new Color(0.97f, 0.98f, 1.00f),
            new Color(0.82f, 0.85f, 0.90f),
            new Color(0.55f, 0.58f, 0.64f));

        private static readonly MaterialRetint CritterChipFaceRetint = MaterialRetint.FromColors(
            new Color(0.88f, 0.90f, 0.94f),
            new Color(0.70f, 0.73f, 0.79f),
            new Color(0.40f, 0.43f, 0.49f));

        private static readonly MaterialRetint CritterChipEdgeRetint = MaterialRetint.FromColors(
            new Color(0.68f, 0.71f, 0.77f),
            new Color(0.48f, 0.51f, 0.57f),
            new Color(0.24f, 0.26f, 0.31f));

        private static readonly MaterialRetint CritterBodyRetint = MaterialRetint.FromColors(
            new Color(0.13f, 0.32f, 0.22f),
            new Color(0.07f, 0.20f, 0.14f),
            new Color(0.03f, 0.09f, 0.07f));

        private static readonly MaterialRetint CritterWingRetint = MaterialRetint.FromColors(
            new Color(0.88f, 0.89f, 0.92f),
            new Color(0.63f, 0.65f, 0.70f),
            new Color(0.31f, 0.33f, 0.38f));

        // ---- Vestuario ---------------------------------------------------

        // Materiales del arte (Assets/Art/3D/Materials). Los seis discos comparten los tres amarillos
        // como cara / canto / brillo, el cuerpo skinneado usa Mat_Black y las alas Mat_Bone.
        public const string ChipFaceMaterial = "Mat_Yellow";
        public const string ChipEdgeMaterial = "Mat_DarkYellow";
        public const string ChipShineMaterial = "Mat_LightYellow";
        public const string BodyMaterial = "Mat_Black";
        public const string WingMaterial = "Mat_Bone";

        /// <summary>
        /// Altura de la barra de vida. Misma que <c>GeneralDirector.prefab</c>, que anida este mismo
        /// personaje: cambiarla la mete dentro de las alas.
        /// </summary>
        public static readonly Vector3 HealthBarOffset = new Vector3(0f, 3f, 0f);

        // La caja va al costado derecho y algo atrás para no tapar la silueta. Escala 0.65 (en las
        // salas la caja va a 1 y ocupa un tile entero) para que no se meta en la casilla vecina: con
        // un jefe melee, las cuatro casillas de al lado son justo las que el jugador tiene que leer.
        public static readonly Vector3 ChipsBoxLocalPosition = new Vector3(0.45f, 0f, -0.2f);
        public static readonly Vector3 ChipsBoxLocalEuler = new Vector3(0f, -25f, 0f);
        public static readonly Vector3 ChipsBoxLocalScale = new Vector3(0.65f, 0.65f, 0.65f);

        // Oro de banca. El Cajero escala con el oro que llevás encima, así que lo que tiene que leerse
        // a primera vista es la pila de fichas: los discos van a oro fuerte con su propio ramp por tono
        // (si los tres materiales compartieran colores, el disco saldría plano), el cuerpo a verde
        // fieltro de mesa para que no compita, y las alas a latón viejo para enmarcar sin brillar más
        // que las fichas.
        private static readonly MaterialRetint ChipShineRetint = MaterialRetint.FromColors(
            new Color(1.00f, 0.98f, 0.80f),
            new Color(1.00f, 0.91f, 0.55f),
            new Color(0.85f, 0.66f, 0.24f));

        private static readonly MaterialRetint ChipFaceRetint = MaterialRetint.FromColors(
            new Color(1.00f, 0.91f, 0.52f),
            new Color(0.97f, 0.76f, 0.24f),
            new Color(0.60f, 0.42f, 0.11f));

        private static readonly MaterialRetint ChipEdgeRetint = MaterialRetint.FromColors(
            new Color(0.86f, 0.64f, 0.22f),
            new Color(0.63f, 0.45f, 0.14f),
            new Color(0.33f, 0.22f, 0.07f));

        private static readonly MaterialRetint BodyRetint = MaterialRetint.FromColors(
            new Color(0.17f, 0.44f, 0.29f),
            new Color(0.09f, 0.28f, 0.19f),
            new Color(0.04f, 0.13f, 0.09f));

        private static readonly MaterialRetint WingRetint = MaterialRetint.FromColors(
            new Color(0.82f, 0.70f, 0.42f),
            new Color(0.58f, 0.46f, 0.25f),
            new Color(0.29f, 0.22f, 0.12f));

        /// <summary>
        /// Ficha de armado del wrapper visual. Pura (no toca <c>AssetDatabase</c>) para que los tests
        /// puedan afirmar el vestuario y redirigir la salida a una carpeta temporal.
        /// </summary>
        /// <param name="outputPath">Destino del wrapper. Default: <see cref="VisualPrefabPath"/>.</param>
        /// <param name="materialsFolder">
        /// Carpeta de los materiales clonados. <c>null</c> deja el default del wrapper builder
        /// (<c>Assets/Rollgeon/Enemies/Materials/Cajero</c>).
        /// </param>
        public static BossWrapperSpec BuildWrapperSpec(
            string outputPath = VisualPrefabPath, string materialsFolder = null)
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = ArtPrefabPath,
                OutputPrefabPath = outputPath,
                EntityId = EntityId,
                BossName = "Cajero",
                MaterialsFolder = materialsFolder,
                HealthBarOffset = HealthBarOffset,
                Retints = new Dictionary<string, MaterialRetint>
                {
                    { ChipShineMaterial, ChipShineRetint },
                    { ChipFaceMaterial, ChipFaceRetint },
                    { ChipEdgeMaterial, ChipEdgeRetint },
                    { BodyMaterial, BodyRetint },
                    { WingMaterial, WingRetint },
                },
                Props = new List<BossPropSpec>
                {
                    new BossPropSpec
                    {
                        PrefabPath = ChipsBoxPropPath,
                        Name = ChipsBoxPropName,
                        LocalPosition = ChipsBoxLocalPosition,
                        LocalEuler = ChipsBoxLocalEuler,
                        LocalScale = ChipsBoxLocalScale,
                    },
                },
            };
        }

        // ---- Árbol -------------------------------------------------------

        /// <summary>
        /// Árbol del Cajero. Sequence raíz de 5 hijos:
        /// <list type="number">
        /// <item>Gate de las Comisiones (50% HP) → <c>Once → SpawnReinforcements ×2</c>.</item>
        /// <item>El ciclo de ataque: pegado a vos, <c>Alternate[mandoble, empujón]</c>.</item>
        /// <item>Las monedas de la sala, cada <see cref="CoinRainEveryNRounds"/> rondas.</item>
        /// <item>La caja: vence monedas y lo cura con lo que nadie levantó.</item>
        /// <item>La persecución.</item>
        /// </list>
        /// Todo lo que puede devolver Failed va en <c>Selector[acción, Wait]</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// El orden es la mitad del diseño. La <b>caja va después del ataque y de la lluvia</b>
        /// porque descubre las monedas barriendo las instancias vivas: si fuera antes, cada moneda
        /// soltada este turno viviría una ronda de más. Y la <b>persecución va última</b> porque
        /// <c>AINode_Move</c> devuelve Running al moverse y en el path no-coroutine un Running corta
        /// el Sequence — con el movimiento en el medio, las monedas dejarían de vencerse justo en
        /// los turnos en que camina.
        /// </para>
        /// <para>
        /// El gate de fase va primero, como en todos los jefes: es lo único que no puede perderse un
        /// turno.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(
            HazardDefinitionSO chip = null, EnemyDataSO critter = null)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    WrapFallible(BuildCritterGate(critter)),
                    WrapFallible(BuildAttackGate(chip)),
                    WrapFallible(BuildCoinRain(chip)),
                    WrapFallible(BuildCoinVault(chip)),
                    WrapFallible(BuildChase()),
                },
            };
        }

        /// <summary>
        /// El ciclo de ataque, con el gate de rango <b>por fuera</b> del <c>Alternate</c>.
        /// </summary>
        /// <remarks>
        /// El gate no es decoración: <c>AINode_Alternate</c> avanza el índice ANTES de tickear y no
        /// lo devuelve si el hijo falla, así que con los dos golpes auto-gateados por rango cada
        /// turno que el jefe pasa caminando le quemaría un turno del ciclo — y la ficha promete que
        /// "el jugador siempre sabe cuál viene". Con el <c>If</c> afuera, el índice sólo avanza en
        /// los turnos en que de verdad pega, y la alternancia que se ve es estricta.
        /// </remarks>
        public static AINode_If BuildAttackGate(HazardDefinitionSO chip = null) => new AINode_If
        {
            TargetSelector = new TargetSelector_AlwaysPlayer(),
            Conditions = new List<BasePreCondition>
            {
                new PcTargetInRange { Range = MeleeRange, Metric = DistanceMetric.Manhattan },
            },
            Then = BuildAttackCycle(chip),
            Else = new AINode_Wait(),
        };

        /// <summary>
        /// Mandoble, empujón, mandoble, empujón.
        /// </summary>
        /// <remarks>
        /// Alternate y no Random: la alternancia tiene que ser estricta para que el jugador pueda
        /// plantar el movimiento — el turno del empujón es el único que se puede preparar, eligiendo
        /// desde qué casilla atacarlo. El mandoble va primero —el índice arranca en 0— para que la
        /// pelea abra con el golpe que no se puede hacer nada para evitar. Cada rama va en su propio
        /// <c>Selector[…, Wait]</c> porque el Alternate propaga el resultado del hijo.
        /// </remarks>
        public static AINode_Alternate BuildAttackCycle(HazardDefinitionSO chip = null) =>
            new AINode_Alternate
            {
                Children = new List<AIDecisionNode>
                {
                    WrapFallible(BuildHeavyBlow()),
                    WrapFallible(BuildShove(chip)),
                },
            };

        /// <summary>
        /// El mandoble: <see cref="HeavyDamage"/> lisos a distancia 1. No mueve, no saca nada.
        /// </summary>
        /// <remarks>
        /// Usa el nodo de disparo con <c>Range = 1</c>, el mismo idiom que el mordisco de la
        /// Comisión: el daño de <c>EffDealDamage</c> es privado y un builder no puede autorarlo.
        /// Los tres ids son los del gesto melee, no los del disparo — el disparo del diseño viejo ya
        /// no existe.
        /// </remarks>
        public static AINode_RangedShot BuildHeavyBlow() => new AINode_RangedShot
        {
            Damage = HeavyDamage,
            Range = MeleeRange,
            Metric = DistanceMetric.Manhattan,
            Kind = AttackKind.BasicAttack,
            AnimFeedbackId = BossFeedbackIds.CajeroMeleeAnim,
            ImpactVfxFeedbackId = BossFeedbackIds.CajeroImpactVfx,
            ImpactFeelFeedbackId = BossFeedbackIds.CajeroImpactFeel,
        };

        /// <summary>
        /// El empujón: <see cref="ShoveDamage"/> y <see cref="ShovePushTiles"/> casillas de tumbo,
        /// con <see cref="ChipCount"/> monedas tiradas en el camino.
        /// </summary>
        /// <remarks>
        /// Sin telegráfico, igual que el mandoble: los avisos son para áreas, ataques donde el
        /// peligro no está donde está el bicho. Éste es contacto — se lo ve pegado y ya se sabe lo
        /// que viene.
        /// </remarks>
        public static AINode_CajeroShove BuildShove(HazardDefinitionSO chip = null) =>
            new AINode_CajeroShove
            {
                Damage = ShoveDamage,
                Range = MeleeRange,
                Metric = DistanceMetric.Manhattan,
                Kind = AttackKind.BasicAttack,
                PushTiles = ShovePushTiles,
                Coin = chip,
                CoinCount = ChipCount,
                CoinMinValue = ChipMinValue,
                CoinMaxValue = ChipMaxValue,
                AnimFeedbackId = BossFeedbackIds.CajeroMeleeAnim,
                ImpactVfxFeedbackId = BossFeedbackIds.CajeroImpactVfx,
                ImpactFeelFeedbackId = BossFeedbackIds.CajeroImpactFeel,
            };

        /// <summary>Las monedas que suelta la sala, no él.</summary>
        public static AINode_CajeroCoinRain BuildCoinRain(HazardDefinitionSO chip) =>
            new AINode_CajeroCoinRain
            {
                Coin = chip,
                Count = CoinsPerRain,
                EveryNRounds = CoinRainEveryNRounds,
                MinValue = ChipMinValue,
                MaxValue = ChipMaxValue,
                MinSeparation = CoinRainMinSeparation,
            };

        /// <summary>El reloj de las monedas y el techo de curación de la pelea.</summary>
        public static AINode_CajeroCoinVault BuildCoinVault(HazardDefinitionSO chip) =>
            new AINode_CajeroCoinVault
            {
                Coin = chip,
                LifetimeRounds = ChipDurationRounds,
                HealPerCoin = HealPerExpiredCoin,
                MaxHealPerFight = MaxHealPerFight,
            };

        /// <summary>
        /// La persecución: <see cref="ChaseSteps"/> pasos hacia el jugador, esquivando los pinchos
        /// armados.
        /// </summary>
        /// <remarks>
        /// El esquive no está acá: sale del planner, que lee el costo de cada casilla especial. Lo
        /// que lo hace tratar un pincho armado como intransitable es
        /// <see cref="SpikeAIVirtualDamage"/> en la definición de la casilla — un pincho ya
        /// disparado queda desarmado hasta el cierre de ronda y el planner lo pisa sin problema, que
        /// es justo la mitad interesante de la regla.
        /// <para>
        /// <c>Retreat = false</c>: no kitea nunca. Es melee puro y lejos no tiene nada que hacer; si
        /// ya está pegado, el nodo sale por Failed y el Selector lo absorbe.
        /// </para>
        /// </remarks>
        public static AINode_Move BuildChase() => new AINode_Move
        {
            MaxSteps = new AIConstantInt { Value = ChaseSteps },
            DesiredRange = new AIConstantInt { Value = MeleeRange },
            Retreat = false,
        };

        /// <summary>
        /// Gate de las Comisiones: al cruzar el 50% suelta <see cref="CritterCount"/> tiradores
        /// voladores, una sola vez en toda la pelea.
        /// </summary>
        /// <remarks>
        /// <see cref="AINode_Once"/> y no el auto-gateo del nodo: reponerlas para siempre haría una
        /// pelea que no termina justo cuando el jefe ya se está curando con las monedas que se
        /// vencen. El gesto es el trigger <c>Attack</c> porque es el único no-idle que declara su
        /// animator — sin él los bichos se materializan con el jefe quieto.
        /// </remarks>
        public static AINode_If BuildCritterGate(EnemyDataSO critter = null) => new AINode_If
        {
            TargetSelector = new TargetSelector_Self(),
            Conditions = new List<BasePreCondition>
            {
                new PcOwnerHpBelow { Percent = CritterHpThreshold },
            },
            Then = new AINode_Once
            {
                Child = new AINode_SpawnReinforcements
                {
                    EnemyToSpawn = critter,
                    Count = CritterCount,

                    // Inerte bajo el Once: el nodo no vuelve a tickear después del primer
                    // Succeeded. Va en 0 igual, para que no diga algo que no pasa.
                    RespawnDelayTurns = 0,
                    SpawnFeedbackId = BossFeedbackIds.CajeroMeleeAnim,
                },
            },
            Else = new AINode_Wait(),
        };

        /// <summary>
        /// Aísla un hijo que puede devolver Failed: <c>Selector[hijo, Wait]</c>. Sin esto, un Failed
        /// benigno (ya estoy lejos, no le pegaron, área vacía) le aborta el turno entero al jefe.
        /// </summary>
        public static AINode_Selector WrapFallible(AIDecisionNode child) => new AINode_Selector
        {
            Children = new List<AIDecisionNode> { child, new AINode_Wait() },
        };

        // ---- EnemyDataSO -------------------------------------------------

        /// <summary>
        /// Escribe la ficha completa sobre <paramref name="data"/> (stats, debilidad, drop, árbol).
        /// Puro: no toca AssetDatabase, no marca dirty — el caller decide.
        /// </summary>
        /// <remarks>
        /// <paramref name="visualPrefab"/> y <paramref name="portrait"/> se asignan sólo si no son
        /// null: los tests (y cualquier caller que sólo quiera refrescar los números) llaman sin
        /// ellos, y nulearlos dejaría al jefe sin cuerpo y sin cara en la cola de turnos.
        /// </remarks>
        public static void PopulateEnemyData(
            EnemyDataSO data,
            GameObject visualPrefab = null,
            HazardDefinitionSO chip = null,
            Sprite portrait = null,
            EnemyDataSO critter = null)
        {
            if (data == null) return;

            data.EntityId = EntityId;
            data.DisplayName = DisplayName;
            // Interpolado y no escrito: el tooltip es lo único que el jugador lee sobre el jefe, y
            // un número a mano acá se queda viejo el día que se tunea la constante.
            data.Description =
                "Cortés, contable, imperturbable. No te mata: te sobrevive con tu plata. Te " +
                $"persigue, te agarra y te tira {ShovePushTiles} casillas, y lo que se te caiga y " +
                "no levantes se lo lleva él.";

            data.BaseHP = BaseHP;
            data.BaseAttack = BaseAttack;
            data.BaseSpeed = BaseSpeed;
            data.MaxEnergy = MaxEnergy;
            data.BaseHealStrength = 0;
            data.BaseAttackRange = MeleeRange;

            // Explícito y no por default: el arte tiene alas, y con IsFlying en true los pinchos
            // (GroundOnly) dejarían de cobrarle. "Los esquiva caminando pero los come empujado" es
            // la única herramienta defensiva real que la sala le da al jugador — sin esto, un tick
            // en el Inspector la borra sin que nada se ponga rojo.
            data.IsFlying = false;

            // "La mano que paga fijo, la de la casa": combo.full ⇒ el id canónico del full house.
            data.WeaknessComboId = ComboId.FullHouse;
            data.WeaknessMultiplierOverride = WeaknessMultiplier;

            data.MinGoldDrop = MinGoldDrop;
            data.MaxGoldDrop = MaxGoldDrop;

            if (visualPrefab != null) data.VisualPrefab = visualPrefab;
            if (portrait != null) data.Portrait = portrait;

            data.AIRoot = BuildAIRoot(chip, critter);
        }

        // ---- La Comisión (ficha propia) -----------------------------------

        /// <summary>
        /// Escribe la ficha de la Comisión sobre <paramref name="data"/>. Puro: no toca
        /// AssetDatabase, no marca dirty.
        /// </summary>
        /// <remarks>
        /// Mismos cuidados que <see cref="PopulateEnemyData"/>: visual y retrato sólo se asignan si
        /// no son null, así un rebuild que sólo refresca números no deja al bicho sin cuerpo.
        /// </remarks>
        public static void PopulateCritterData(
            EnemyDataSO data, GameObject visualPrefab = null, Sprite portrait = null)
        {
            if (data == null) return;

            data.EntityId = CritterEntityId;
            data.DisplayName = CritterDisplayName;
            data.Description =
                "Lo que el Cajero manda a cobrar cuando se le empieza a escapar. Vuela, tira de " +
                "lejos y hace que huir tenga precio.";

            data.BaseHP = CritterHp;

            // Su daño real sale del nodo del árbol, no de este stat (el árbol autorado saltea el
            // BasicEnemyAI). Se escribe igual y con el mismo número porque es lo que leen el
            // tooltip y los TargetSelector_ByAttribute: dejarlo en 0 la marcaría como support.
            data.BaseAttack = CritterDamage;
            data.BaseSpeed = CritterSpeed;
            data.MaxEnergy = 1;
            data.BaseHealStrength = 0;
            data.BaseAttackRange = CritterRange;

            data.WeaknessComboId = string.Empty;
            data.WeaknessMultiplierOverride = 0f;

            // Cero oro: la única plata de esta pelea son las monedas del piso, y son un reloj, no
            // un botín. Un refuerzo que paga al morir le daría al jugador una fuente de oro que la
            // sala no controla, justo en la mecánica donde el oro es la unidad de medida.
            data.MinGoldDrop = 0;
            data.MaxGoldDrop = 0;

            if (visualPrefab != null) data.VisualPrefab = visualPrefab;
            if (portrait != null) data.Portrait = portrait;

            data.AIRoot = BuildCritterAIRoot();
        }

        /// <summary>
        /// Árbol de la Comisión: si el jugador está a tiro dispara, y si no vuela hacia él.
        /// </summary>
        /// <remarks>
        /// Dispara primero y se mueve después: <see cref="AINode_Move"/> devuelve Running cuando se
        /// mueve, y un Running corta el Sequence, así que con el orden invertido el turno en que
        /// entra en rango se comería el disparo.
        /// </remarks>
        public static AINode_Sequence BuildCritterAIRoot() => new AINode_Sequence
        {
            Children = new List<AIDecisionNode>
            {
                WrapFallible(BuildCritterBite()),
                WrapFallible(BuildCritterApproach()),
            },
        };

        /// <summary>
        /// El disparo de la Comisión. Se sigue llamando <c>Bite</c> por compatibilidad de callers:
        /// lo que cambió con el rediseño es el alcance, no quién lo hace.
        /// </summary>
        public static AINode_CashierRangedShot BuildCritterBite() => new AINode_CashierRangedShot
        {
            Damage = CritterDamage,
            Range = CritterRange,
            Metric = DistanceMetric.Manhattan,
            Kind = AttackKind.BasicAttack,
        };

        /// <remarks>
        /// <c>DesiredRange</c> = su propio alcance y no 1: es un tirador, y caminar hasta el
        /// contacto la pondría al lado del jugador —donde muere de un golpe cualquiera— para pegar
        /// exactamente lo mismo que pega desde lejos. Sin kite: si ya está a tiro el nodo sale por
        /// Failed y el Selector lo absorbe.
        /// </remarks>
        public static AINode_Move BuildCritterApproach() => new AINode_Move
        {
            MaxSteps = new AIConstantInt { Value = CritterMoveSteps },
            DesiredRange = new AIConstantInt { Value = CritterRange },
            Retreat = false,
        };

        // ---- MenuItem ----------------------------------------------------

        [MenuItem("Tools/Rollgeon/Bosses/Build Cajero")]
        public static void BuildCajeroAsset()
        {
            var chip = EnsureChipHazard();
            var spikes = EnsureSpikeTile();
            var portrait = EnsurePortrait();

            // El refuerzo es SU Comisión, no el ranged común: 18/6 no se puede autorar sobre
            // ED_RangedEnemy sin pisarle los stats a todos los encuentros normales del juego.
            var critter = LoadOrCreate<EnemyDataSO>(ReinforcementAssetPath);

            // Load antes que Ensure: el wrapper de la Comisión ya existe y reconstruirlo en cada
            // rebuild de números le churnea el prefab (y los materiales clonados) sin cambiar nada.
            // Sólo se arma si falta.
            var critterWrapper = AssetDatabase.LoadAssetAtPath<GameObject>(CritterVisualPrefabPath);
            if (critterWrapper == null) critterWrapper = EnsureCritterVisualPrefab();

            PopulateCritterData(critter, critterWrapper, portrait);
            EditorUtility.SetDirty(critter);

            var data = LoadOrCreate<EnemyDataSO>(EnemyAssetPath);

            var wrapper = EnsureVisualPrefab();
            var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderVisualPrefabPath);

            var visual = ResolveVisualPrefab(data.VisualPrefab, wrapper, placeholder);
            PopulateEnemyData(data, visual, chip, portrait, critter);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CajeroAssetBuilder] '{EnemyId(data)}' actualizado en '{EnemyAssetPath}' " +
                      $"(ficha: {BaseHP} HP, mandoble {HeavyDamage} y empujón {ShoveDamage} + " +
                      $"{ShovePushTiles} casillas, alcance {MeleeRange}, camina {ChaseSteps}; " +
                      $"monedas: {CoinsPerRain} cada {CoinRainEveryNRounds} rondas + {ChipCount} por " +
                      $"empujón, {ChipMinValue}-{ChipMaxValue}g, duran {ChipDurationRounds} rondas, " +
                      $"se vencen de a una y cada una cura {HealPerExpiredCoin} con techo " +
                      $"{MaxHealPerFight}; " +
                      $"visual: {NameOf(visual)}, retrato: {NameOf(portrait)}) + {CritterCount} × " +
                      $"'{NameOf(critter)}' ({critter.BaseHP} HP, {critter.BaseAttack} a " +
                      $"≤{CritterRange}) al {CritterHpThreshold:P0}.");

            // Este builder crea la definición; las casillas las coloca el builder de salas leyendo
            // SpikePlanCells. El log lo dice porque son dos menús distintos: cambiar el daño acá no
            // reescribe la sala, y mover una casilla en SpikePlanCells no sirve hasta rebuildearla.
            Debug.Log($"[CajeroAssetBuilder] Pinchos de la sala en '{SpikeTilePath}' " +
                      $"({NameOf(spikes)}: {SpikeDamage} al entrar, +{SpikeAIVirtualDamage} de costo " +
                      $"IA). Las {SpikeCount} casillas las coloca " +
                      "'Rollgeon → Bosses → Build Boss Room → Cajero'.");

            Selection.activeObject = data;
        }

        private static string EnemyId(EnemyDataSO data) => string.IsNullOrEmpty(data.EntityId) ? EntityId : data.EntityId;

        private static string NameOf(UnityEngine.Object asset) => asset == null ? "—" : asset.name;

        // ---- Visual ------------------------------------------------------

        /// <summary>
        /// Construye (o reconstruye) el wrapper de gameplay del Cajero en
        /// <see cref="VisualPrefabPath"/> y lo devuelve. <c>null</c> + warning si el arte falta.
        /// </summary>
        /// <remarks>
        /// Idempotente por delegación: <see cref="BossVisualWrapperBuilder"/> reescribe el prefab sobre
        /// el mismo path preservando el GUID, así que la referencia de la ficha sobrevive al rebuild.
        /// </remarks>
        public static GameObject EnsureVisualPrefab()
        {
            var wrapper = BossVisualWrapperBuilder.BuildWrapper(BuildWrapperSpec());
            if (wrapper == null)
            {
                Debug.LogWarning($"[CajeroAssetBuilder] No se pudo construir el wrapper visual en " +
                                 $"'{VisualPrefabPath}' — se deja el VisualPrefab que ya tenga la ficha.");
            }
            return wrapper;
        }

        /// <summary>
        /// Ficha de armado del wrapper de la Comisión. Pura, por el mismo motivo que
        /// <see cref="BuildWrapperSpec"/>.
        /// </summary>
        /// <remarks>
        /// <b>Viste el mismo arte que el jefe</b> (<see cref="ArtPrefabPath"/>) porque
        /// <c>GeneralDirector_Animated</c> es <b>el único rig alado del proyecto</b> — no hay ningún
        /// otro modelo con alas, ni siquiera un murciélago o una moneda flotante. Lo que las separa
        /// es el tamaño (<see cref="CritterArtScale"/>), la altura de vuelo
        /// (<see cref="CritterHoverHeight"/>) y la paleta de plata.
        /// <para>
        /// Sin props: la caja de fichas es del jefe, y colgársela a un bicho de 0.9 lo convertiría en
        /// un Cajero chiquito con la misma silueta.
        /// </para>
        /// </remarks>
        public static BossWrapperSpec BuildCritterWrapperSpec(
            string outputPath = CritterVisualPrefabPath, string materialsFolder = null)
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = ArtPrefabPath,
                OutputPrefabPath = outputPath,
                EntityId = CritterEntityId,
                BossName = CritterName,
                MaterialsFolder = materialsFolder,

                // Box y no el Capsule default: ApplyCritterFit reescribe el collider contra los
                // bounds del arte ya encogido y flotando, y un Box es lo que se puede recentrar con
                // center/size sin recalcular radios ni ejes.
                Collider = ColliderKind.Box,

                // La barra final la reposiciona ApplyCritterFit contra los bounds reales; esto es
                // sólo el valor con el que nace el wrapper.
                HealthBarOffset = new Vector3(0f, CritterHoverHeight + CritterBarClearance, 0f),

                Retints = new Dictionary<string, MaterialRetint>
                {
                    { ChipShineMaterial, CritterChipShineRetint },
                    { ChipFaceMaterial, CritterChipFaceRetint },
                    { ChipEdgeMaterial, CritterChipEdgeRetint },
                    { BodyMaterial, CritterBodyRetint },
                    { WingMaterial, CritterWingRetint },
                },
            };
        }

        /// <summary>
        /// Construye (o reconstruye) el wrapper de la Comisión y lo devuelve, ya encogido y flotando.
        /// <c>null</c> + warning si el arte falta.
        /// </summary>
        public static GameObject EnsureCritterVisualPrefab()
        {
            var wrapper = BossVisualWrapperBuilder.BuildWrapper(BuildCritterWrapperSpec());
            if (wrapper == null)
            {
                Debug.LogWarning($"[CajeroAssetBuilder] No se pudo construir el wrapper de la " +
                                 $"Comisión en '{CritterVisualPrefabPath}' — se deja el VisualPrefab " +
                                 "que ya tenga la ficha.");
                return null;
            }

            ApplyCritterFit(CritterVisualPrefabPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(CritterVisualPrefabPath);
        }

        /// <summary>
        /// Segunda pasada sobre el wrapper ya guardado: encoge el arte, lo despega del piso y
        /// reacomoda collider y barra alrededor de donde quedó.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Es una pasada aparte y no un campo del spec por lo mismo que en
        /// <c>GeneralaAssetBuilder.ApplyArtFit</c>: <see cref="BossVisualWrapperBuilder"/> fija el
        /// arte en identidad a propósito y dimensiona el collider asumiendo eso. Reescribir sobre el
        /// mismo path conserva el GUID, así que la ficha que ya apunta al wrapper sobrevive.
        /// </para>
        /// <para>
        /// El collider hay que reescribirlo sí o sí: el wrapper lo dimensionó alrededor del arte a
        /// escala 1 y apoyado, y después de encogerlo a 0.45 y subirlo 0.7 quedaría envolviendo aire
        /// — el cursor picaría el piso y no al bicho.
        /// </para>
        /// </remarks>
        private static void ApplyCritterFit(string prefabPath)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogWarning($"[CajeroAssetBuilder] No se pudo abrir '{prefabPath}' para dejar " +
                                 "la Comisión flotando.");
                return;
            }

            try
            {
                var art = contents.transform.Find(ArtChildName);
                if (art == null)
                {
                    Debug.LogWarning($"[CajeroAssetBuilder] '{prefabPath}' no tiene hijo " +
                                     $"'{ArtChildName}' — la Comisión queda del tamaño del jefe y " +
                                     "apoyada en el piso.");
                    return;
                }

                // Los bounds se miden con el arte en identidad, que es como lo dejó el wrapper.
                art.localScale = Vector3.one;
                art.localPosition = Vector3.zero;
                bool measured = TryMeasureRenderers(art, out var raw);

                art.localScale = Vector3.one * CritterArtScale;

                // El lift lleva la BASE del arte a CritterHoverHeight, no su pivot: el rig está
                // autorado con el pivot en los pies, pero eso es una convención del arte y no algo
                // que este builder pueda asumir de un prefab que alguien reexporte.
                float baseY = measured ? raw.min.y * CritterArtScale : 0f;
                art.localPosition = new Vector3(0f, CritterHoverHeight - baseY, 0f);

                if (measured)
                {
                    var flying = new Bounds(
                        raw.center * CritterArtScale + new Vector3(0f, art.localPosition.y, 0f),
                        raw.size * CritterArtScale);

                    var box = contents.GetComponent<BoxCollider>();
                    if (box != null)
                    {
                        box.center = flying.center;
                        box.size = flying.size;
                    }

                    var bar = contents.transform.Find(HealthBarChildName);
                    if (bar != null)
                    {
                        bar.localPosition = new Vector3(0f, flying.max.y + CritterBarClearance, 0f);
                        bar.localScale = Vector3.one * CritterBarScale;
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// Bounds locales de los Mesh/SkinnedMesh renderers colgados de <paramref name="art"/>, con
        /// el transform en identidad. <c>false</c> si el arte no reporta volumen usable.
        /// </summary>
        private static bool TryMeasureRenderers(Transform art, out Bounds bounds)
        {
            bounds = default;
            bool any = false;

            foreach (var renderer in art.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;

                if (any) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; any = true; }
            }

            return any && bounds.size.y > Mathf.Epsilon;
        }

        /// <summary>
        /// Retrato del jefe. Sigue siendo un método y no una constante porque el sub-sprite hay que
        /// resolverlo contra el AssetDatabase, y <c>BuildContractCard</c> lo pide por separado.
        /// </summary>
        /// <remarks>
        /// La Comisión comparte este mismo retrato. <c>BossPortraitLibrary</c> tiene la regla
        /// explícita —"el retrato sigue al rig, no al nombre"— y las dos visten
        /// <c>GeneralDirector_Animated</c>: mostrar otra cara en la cola de turnos sería mentir sobre
        /// lo que el jugador tiene enfrente. El día que la Comisión tenga arte propio, se le hace su
        /// entrada en la library y se corta acá.
        /// </remarks>
        public static Sprite EnsurePortrait()
        {
            var portrait = BossPortraitLibrary.Cajero();
            if (portrait == null)
            {
                Debug.LogWarning($"[CajeroAssetBuilder] No se resolvió el retrato en " +
                                 $"'{PortraitTexturePath}' — la cola de turnos cae a su visual default.");
            }
            return portrait;
        }

        /// <summary>
        /// Decide qué prefab visual queda en la ficha. Puro y sin AssetDatabase para poder testear la
        /// regla, que es la que evita pisar trabajo ajeno.
        /// </summary>
        /// <remarks>
        /// Un prefab que no es ni el wrapper de este builder ni el placeholder viejo lo puso alguien
        /// a propósito y un rebuild no lo revierte. El placeholder sí se pisa. Y si el wrapper no se
        /// pudo construir se devuelve lo que había: un build fallido no deja al jefe sin cuerpo.
        /// </remarks>
        public static GameObject ResolveVisualPrefab(
            GameObject current, GameObject wrapper, GameObject placeholder)
        {
            bool authored = current != null && current != wrapper && current != placeholder;
            if (authored) return current;

            return wrapper != null ? wrapper : current;
        }

        /// <summary>
        /// Crea (o actualiza) el hazard que representa una moneda en el piso: se dispara al pisarla,
        /// se consume, no hace daño y no vence sola.
        /// </summary>
        public static HazardDefinitionSO EnsureChipHazard()
        {
            var chip = LoadOrCreate<HazardDefinitionSO>(ChipHazardPath);

            chip.Trigger = HazardTriggerMode.OnEnter;
            // La moneda es un pickup del jugador: si la levantara un refuerzo al caminarle encima,
            // se consumiría la casilla y el jugador se quedaría sin nada que juntar.
            chip.Affects = HazardAffects.PlayerOnly;
            chip.ConsumeOnTrigger = true;
            chip.Damage = 0;
            chip.Kind = AttackKind.Environmental;

            // 0 = no vence sola. El reloj lo lleva AINode_CajeroCoinVault, y tiene que ser el ÚNICO
            // que la mate: el servicio de hazards expira igual una moneda cobrada y una vencida, así
            // que si venciera sola nadie podría saber cuál de las dos pasó — y sólo la vencida cura
            // al jefe. Ver ChipDurationRounds para la vida real de la moneda.
            chip.DurationRounds = 0;

            chip.Shape = ThreatShape.Column; // Inerte: las monedas se activan con la overload de tiles.
            chip.Size = 1;
            chip.OverlayTint = new Color(1f, 0.84f, 0.25f, 0.55f); // oro
            chip.SourceId = ChipHazardSourceId;

            // La ficha es un pickup, no una amenaza: lo que tiene que verse es la moneda esperando en
            // el piso, y el quad dorado solo la acompaña. Sin el prefab persistente, "hay una ficha
            // acá" y "esta casilla está marcada" se ven exactamente igual.
            var coin = AssetDatabase.LoadAssetAtPath<GameObject>(CoinModelPath);
            if (coin == null)
            {
                Debug.LogWarning($"[CajeroAssetBuilder] No está la moneda en '{CoinModelPath}' — " +
                                 "las fichas quedan como quad dorado y nada más.");
            }
            else
            {
                chip.PersistentVfxPrefab = coin;
                chip.PersistentVfxYOffset = CoinYOffset;
            }

            EditorUtility.SetDirty(chip);
            return chip;
        }

        /// <summary>
        /// Crea (o actualiza) los pinchos de la sala del Cajero: cobran <see cref="SpikeDamage"/> al
        /// entrar —también empujado—, se bajan al dispararse y se rearman al cerrar la ronda.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Propios y no <c>Tile_Spikes</c>: el genérico pega 12 y no encarece la ruta de la IA, y
        /// subírselos ahí se lo cambiaría a todas las salas del juego. Mismo criterio que
        /// <c>Tile_Fire_Croupier</c>.
        /// </para>
        /// <para>
        /// <b>Este builder crea la definición, no las coloca.</b> Las diez casillas las escribe
        /// <c>BossRoomBuilder</c> en <c>RoomLayout.SpecialTilePlacements</c> de
        /// <c>Boss_Room_Cajero</c>, leyendo <see cref="SpikePlanCells"/>. Van por la lista de
        /// permanentes y no por un slot: la posición exacta es la autoría, no algo que se rolee.
        /// </para>
        /// <para>
        /// Al venir de la sala el owner queda vacío, y eso es lo que hace que el jefe <b>no</b> sea
        /// inmune: <c>SpecialTileService.ShouldAffect</c> exime a un jefe sólo de las casillas cuyo
        /// owner es un jefe. Es la mitad "empujado se los come igual" de la regla.
        /// </para>
        /// </remarks>
        public static SpecialTileDefinitionSO EnsureSpikeTile()
        {
            var spikes = LoadOrCreate<SpecialTileDefinitionSO>(SpikeTilePath);

            spikes.TileId = SpikeTileId;
            spikes.DisplayName = "Pinchos de la Caja";
            spikes.TileType = SpecialTileType.Spikes;

            // OnForcedMovementInto es la mitad del diseño: es lo que hace que el tumbo del empujón
            // cobre las casillas que cruza, y que el Empuje del jugador se los cobre a él.
            spikes.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
            spikes.Category = TileEffectCategory.Damage;
            spikes.Affinity = TileAffinity.GroundOnly;
            spikes.DamageKind = AttackKind.Environmental;
            spikes.EnterDamage = SpikeDamage;
            spikes.TurnStartDamage = 0;

            // Permanentes: son terreno de la sala, no algo que el jefe pone.
            spikes.DefaultDurationRounds = 0;

            // "Armado sí, bajado no": un pincho disparado queda bajado hasta el cierre de ronda, y el
            // pathing lo lee. Cada pincho que el jugador gasta le abre un pasillo al jefe.
            spikes.DisarmOnTrigger = true;
            spikes.RearmOnRoundWrap = true;

            spikes.AIVirtualEnterDamage = SpikeAIVirtualDamage;
            spikes.AIAnnouncesLethal = false;

            spikes.NameKey = "tile.spikes";
            spikes.DescriptionKey = "tile.spikes";

            // Mismo arte y mismo color de paleta que el pincho genérico: para el jugador es el mismo
            // objeto, y darle un look propio enseñaría una diferencia que no existe.
            var generic = AssetDatabase.LoadAssetAtPath<SpecialTileDefinitionSO>(
                "Assets/Rollgeon/Tiles/Tile_Spikes.asset");
            if (generic == null)
            {
                Debug.LogWarning("[CajeroAssetBuilder] No está 'Tile_Spikes.asset' — los pinchos del " +
                                 "Cajero quedan sin visual y se van a ver como el overlay pelado.");
            }
            else
            {
                spikes.VisualPrefab = generic.VisualPrefab;
                spikes.VisualYOffset = generic.VisualYOffset;
                spikes.OverlayTint = generic.OverlayTint;
                spikes.TriggerVfxPrefab = generic.TriggerVfxPrefab;
                spikes.TriggerVfxLifetime = generic.TriggerVfxLifetime;
                spikes.TriggerVfxYOffset = generic.TriggerVfxYOffset;
                spikes.EditorIcon = generic.EditorIcon;
                spikes.EditorColor = generic.EditorColor;
            }

            EditorUtility.SetDirty(spikes);
            return spikes;
        }

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

            var parts = folder.Split('/');
            var current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
