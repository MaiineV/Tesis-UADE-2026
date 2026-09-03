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
        /// <summary>Menú que regenera estos assets. Lo lee el Editor de enemigos para avisar que el builder pisa el árbol.</summary>
        public const string MenuPath = "Tools/Rollgeon/Bosses/Build Cajero";

        // ---- Rutas -------------------------------------------------------

        public const string EnemyAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Cajero.asset";
        public const string ChipHazardPath = "Assets/Rollgeon/Combat/Hazards/HZ_Cashier_Chip.asset";

        /// <summary>Moneda que se ve en la casilla mientras la ficha está sin levantar.</summary>
        private const string CoinModelPath = "Assets/Art/3D/Models/Items/coin.fbx";

        /// <summary>Levanta la moneda del piso lo justo para que no z-fightee con el quad del overlay.</summary>
        private const float CoinYOffset = 0.12f;

        /// <summary>
        /// Arte del jefe: mech humanoide con tres cañones en el pecho, animado por
        /// <c>SteppedAnimation</c> sobre <c>AnimCon_Mecha</c>.
        /// </summary>
        /// <remarks>
        /// Es el mismo rig que viste la Bandida (<c>BandidaAssetBuilder.BossArtPrefabPath</c>).
        /// Comparten malla, no materiales: cada builder clona y retinta la suya en
        /// <c>Assets/Rollgeon/Enemies/Materials/&lt;Jefe&gt;</c>.
        /// </remarks>
        public const string ArtPrefabPath = "Assets/Prefabs/Enemies/MechaBoss_Animated.prefab";

        /// <summary>
        /// Los gestos de ataque del rig, los que tienen que publicar el frame del golpe.
        /// </summary>
        /// <remarks>
        /// El mandoble y el empujón heredan de <c>AINode_RangedShot</c>, que arranca su VFX y su feel
        /// de impacto con <c>StartMode: OnEvent</c>. Sin el evento en el clip esos steps se quedan
        /// esperando y el golpe entero —daño, tumbo y monedas— cae recién cuando el watchdog mata la
        /// secuencia, unos tres segundos después del gesto.
        /// </remarks>
        public static readonly string[] AttackClipPaths =
        {
            "Assets/Art/3D/Animations/Enemies/Mecha/Anim_Mecha_AttackMelee.anim",
            "Assets/Art/3D/Animations/Enemies/Mecha/Anim_Mecha_AttackRange.anim",
        };

        /// <summary>
        /// Arte de la Comisión: rig propio, no el del jefe. Compartir el mech hacía que el minion
        /// fuera el jefe en chico, y lo único que los separaba era la escala y el tinte.
        /// </summary>
        /// <remarks>
        /// Su animator declara un solo trigger, <c>Attack</c> — ver <c>ComisionBiteAnim</c> — y no
        /// tiene ciclo de caminata. No hace falta: el bicho vuela, y el lerp de <c>Walk</c> con el
        /// Idle corriendo es cómo se ve planear. Blink la teletransportaría.
        /// </remarks>
        public const string CritterArtPrefabPath = "Assets/Prefabs/Enemies/GeneralDirector_Animated.prefab";

        /// <summary>Wrapper de gameplay que arma <see cref="BossVisualWrapperBuilder"/>.</summary>
        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Cajero.prefab";

        /// <summary>Caja de fichas, parenteada al costado del jefe. Es lo que dice de un vistazo
        /// que la plata es su tema.</summary>
        public const string ChipsBoxPropPath = "Assets/Prefabs/Props/CajaFichasv01.prefab";

        public const string ChipsBoxPropName = "ChipsBox";

        /// <summary>
        /// Retrato del jefe: la cara de <c>MechaBoss_Animated</c>, el rig que viste. Ver
        /// <see cref="BossPortraitLibrary"/> y <see cref="EnsurePortrait"/>.
        /// </summary>
        public const string PortraitTexturePath = BossPortraitLibrary.CajeroPath;

        /// <summary>
        /// Placeholder viejo: una ficha que todavía lo apunte se actualiza al wrapper sin preguntar.
        /// </summary>
        public const string PlaceholderVisualPrefabPath = "Assets/Prefabs/Enemies/SecurityGuardBoss.prefab";

        // ---- Ficha (números del diseño; una sola fuente de verdad) --------

        public const string EntityId = "boss.cashier";
        public const string DisplayName = "El Cajero";

        /// <summary>
        /// Vida del jefe de piso 2. Pelados: no se cura con nada, así que lo que aguanta es
        /// exactamente esto.
        /// </summary>
        public const int BaseHP = 450;

        /// <summary>Mandoble.</summary>
        public const int BaseAttack = 20;

        public const int BaseSpeed = 4;
        public const int MaxEnergy = 3;
        public const int MinGoldDrop = 30;
        public const int MaxGoldDrop = 60;
        public const float WeaknessMultiplier = 1.5f;

        /// <summary>Alcance de sus dos ataques. Melee puro: no tiene nada a distancia.</summary>
        public const int MeleeRange = 1;

        /// <summary>
        /// Pasos por turno cuando persigue. Va como constante y no como lectura del stat porque
        /// <c>AIReadSelfStat</c> devuelve 0 sin <c>AttributesManager</c> (EditMode) y un MaxSteps
        /// de 0 deja al jefe clavado, sin caer a ningún default.
        /// </summary>
        public const int ChaseSteps = BaseSpeed;

        // ---- Los dos golpes ----------------------------------------------

        /// <summary>Mandoble: su <see cref="BaseAttack"/> y nada más.</summary>
        public const int HeavyDamage = BaseAttack;

        /// <summary>
        /// Empujón. Cada casilla de pinchos que cruce el tumbo suma <see cref="SpikeDamage"/>.
        /// </summary>
        public const int ShoveDamage = 14;

        /// <summary>Casillas del tumbo. Frena en seco contra una caja fuerte o contra la pared.</summary>
        public const int ShovePushTiles = 3;

        /// <summary>Fracción del oro del jugador que se lleva cada empujón.</summary>
        /// <remarks>
        /// Porcentaje y no monto fijo porque el empujón sale cada dos turnos: en una pelea de doce
        /// son seis cobros, y cualquier número fijo multiplicado por seis funde al que viene con
        /// poco y es ruido para el que viene cargado. El porcentaje decae solo, porque cada cobro se
        /// calcula sobre lo que quedó.
        /// </remarks>
        public const float ShoveTaxPercent = 0.10f;

        /// <summary>
        /// Piso del cobro: sin él un jugador sin oro sería inmune a media pelea —no le sacaría nada,
        /// no caerían monedas— justo cuando peor viene.
        /// </summary>
        public const int ShoveTaxMinimum = 10;

        /// <summary>
        /// Parte de lo cobrado que vuelve al piso repartida entre las monedas del tumbo. El resto se
        /// lo queda él y no vuelve nunca.
        /// </summary>
        public const float ShoveRefundPercent = 0.70f;

        // ---- Las monedas -------------------------------------------------

        /// <summary>
        /// Valor de una moneda de la <b>lluvia</b>, la que suelta la sala. Fijo y no un rango: son
        /// cuatro por tanda, y que cada una valga distinto no cambia ninguna decisión —sólo hace
        /// que la tanda valga un número que el jugador no puede leer del piso.
        /// </summary>
        /// <remarks>
        /// Las del empujón no salen de acá: valen <see cref="ShoveRefundPercent"/> de lo que te
        /// cobró, porque son tu plata.
        /// </remarks>
        public const int ChipValue = 5;

        /// <summary>Monedas que suelta la sala por tanda.</summary>
        public const int CoinsPerRain = 4;

        /// <summary>Rondas entre tandas de la sala.</summary>
        public const int CoinRainEveryNRounds = 3;

        /// <summary>Distancia Chebyshev mínima entre dos monedas de la misma tanda.</summary>
        public const int CoinRainMinSeparation = 2;

        /// <summary>
        /// Rondas que vive una moneda en el piso.
        /// </summary>
        /// <remarks>
        /// No es el <c>DurationRounds</c> del hazard: la moneda nace permanente y la mata
        /// <c>AINode_CajeroCoinVault</c>, el único que puede distinguir una levantada de una vencida
        /// (el servicio de hazards expira las dos igual). Es el vencimiento de cada moneda y no el de
        /// la tanda: el nodo se cobra <b>una por turno</b>.
        /// </remarks>
        public const int ChipDurationRounds = 2;

        /// <summary>
        /// Monedas que se le caen al jugador en cada empujón, repartidas al azar por la sala.
        /// </summary>
        /// <remarks>
        /// Por la sala y no sobre el tumbo: en el recorrido quedaban a un paso y recuperarlas no
        /// costaba el desvío que la mecánica cobra. Lejos, ir a buscarlas compite con pegarle.
        /// </remarks>
        public const int ChipCount = 2;

        /// <summary>Id estable del hazard-ficha: el servicio de hazards keyea por él. Hex válido —
        /// un SourceId que no parsea a Guid loguea error cada vez que se lee.</summary>
        public const string ChipHazardSourceId = "3c0a7d18-9f42-4a6b-9c3e-5b1ca5e70001";

        // ---- Los pinchos de la sala --------------------------------------

        /// <summary>
        /// Pinchos propios y no <c>Tile_Spikes</c>: tocar el genérico se lo cambiaría a todas las
        /// salas del juego.
        /// </summary>
        public const string SpikeTilePath = "Assets/Rollgeon/Tiles/Tile_Spikes_Cajero.asset";

        public const string SpikeTileId = "TILE_SPIKES_CAJERO";

        /// <summary>Daño al entrar, también empujado. Es el mismo para el jugador y para él.</summary>
        public const int SpikeDamage = 20;

        /// <summary>
        /// Costo virtual que hace que el pathing lea un pincho como <b>intransitable</b> y no como
        /// caro. Sin desarme son las diez, toda la pelea.
        /// </summary>
        /// <remarks>
        /// <c>AIPathPlanner.ComputeHazardPenalty</c> es <c>ceil(daño / HP × 10 × Caution)</c> y
        /// <c>ComputeTileCost</c> es <c>1 + penalty</c>: con <c>20 + 430 = 450</c> sobre 450 de vida
        /// la casilla cuesta 11, más que cualquier desvío posible en un movimiento de
        /// <see cref="ChaseSteps"/> pasos. <b>No es daño</b>: el filtro de supervivencia sólo mira
        /// los <see cref="SpikeDamage"/> reales, así que empujado se los come igual.
        /// <para>
        /// Va atado a <see cref="BaseHP"/>, no a un número suelto: la saturación es la suma dando la
        /// vida entera del jefe, así que <b>tocarle la vida obliga a recalcular esto</b> o el pincho
        /// vuelve a ser sólo caro y el jefe se camina sus propios pinchos.
        /// </para>
        /// <para>
        /// Con los pinchos permanentes esto es lo único que lo mantiene afuera, y no hay ventana en
        /// la que el campo se abra: por eso el layout los deja sueltos —ninguno tocando a otro— para
        /// que nunca formen barrera y siempre le quede ruta hasta el jugador.
        /// </para>
        /// </remarks>
        public const int SpikeAIVirtualDamage = 430;

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
        /// <c>RoomLayout.SpecialTilePlacements</c> de <c>Boss_Room_Cajero</c>. La regla que lo
        /// gobierna es que ninguno toca a otro, ni en diagonal.
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
        /// Entran como <c>BlockerPlanCells</c> del plano del Cajero en <c>BossRoomBuilder</c> y no
        /// como casillas especiales: lo suyo es <b>bloquear</b>, y una casilla especial no toca el
        /// grafo de navegación.
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
        public const string CritterAssetPath = "Assets/Rollgeon/Enemies/ED_Min_Comision.asset";

        /// <summary>
        /// Lo que el Cajero invoca. Ficha propia por balance (más débil que el ranged común),
        /// pero para el jugador se lee como un Enemigo a Distancia más: mismo nombre y mismos
        /// textos, sin identidad propia.
        /// </summary>
        /// <remarks>
        /// Su kit no se puede autorar sobre <c>ED_RangedEnemy</c> —es el asset compartido de todos
        /// los encuentros normales— así que vive en su propia ficha.
        /// </remarks>
        public const string ReinforcementAssetPath = CritterAssetPath;

        /// <summary>
        /// El ranged común, de donde la Comisión saca el aspecto: arte, retrato y descripción. Se
        /// copian en cada build en vez de duplicarse acá para que no puedan divergir — que se lea
        /// como un ranged más es la decisión de diseño, y dos fichas a mano ya habían derivado en
        /// dos bichos distintos.
        /// </summary>
        /// <remarks>
        /// <b>Fuente del aspecto y nada más.</b> El spawn apunta a <see cref="ReinforcementAssetPath"/>:
        /// invocar este asset le daría al jugador dos bichos de 50 de vida a mitad de pelea, y hay
        /// un test que lo prohíbe.
        /// </remarks>
        public const string SharedRangedAssetPath = "Assets/Rollgeon/Enemies/ED_RangedEnemy.asset";

        /// <summary>
        /// La presentación del disparo del ranged común, que la Comisión reusa entera. Los ids
        /// viven autorados en <c>ED_RangedEnemy</c> y en el <c>FeedbackDB</c>; se nombran acá para
        /// poder pedirlos desde el árbol.
        /// </summary>
        private const string SharedRangedShotAnim = "anim.enemy.ranged.attack";
        private const string SharedRangedShotImpactVfx = "vfx.enemy.ranged.impact";
        private const string SharedRangedShotImpactFeel = "feel.enemy.ranged.impact";

        public const string CritterVisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Min_Comision.prefab";

        public const string CritterEntityId = "minion.cajero_comision";
        public const string CritterDisplayName = "Enemigo a Distancia";

        /// <summary>Nombre corto para la carpeta y el prefijo de sus materiales clonados.</summary>
        public const string CritterName = "Comision";

        /// <summary>Dos, y una sola vez. Ver <see cref="BuildCritterGate"/>.</summary>
        public const int CritterCount = 2;

        /// <summary>Al cruzar la mitad de la vida suelta las dos Comisiones.</summary>
        public const float CritterHpThreshold = 0.5f;

        /// <summary>Vida de la Comisión.</summary>
        public const int CritterHp = 18;

        /// <summary>Su disparo.</summary>
        public const int CritterDamage = 8;

        /// <summary>Alcance del disparo.</summary>
        public const int CritterRange = 5;

        /// <summary>Va antes que el jefe (4) en la cola de turnos.</summary>
        public const int CritterSpeed = 5;

        /// <summary>Alcance de vuelo por turno.</summary>
        public const int CritterMoveSteps = 3;

        // ---- Vestuario de la Comisión ------------------------------------

        /// <summary>
        /// Escala del arte dentro de su wrapper: el rig alado mide ~2 de alto y a 0.45 queda en ~0.9.
        /// ApplyCritterFit reajusta el collider y la barra contra los bounds reales, así que esto es
        /// lo único que hay que tocar si el bicho queda chico o grande en la sala.
        /// </summary>
        public const float CritterArtScale = 0.45f;

        /// <summary>
        /// Altura a la que se levanta la BASE del arte sobre su casilla. Despegada porque el bicho
        /// tiene <c>IsFlying</c>: es la única pista en pantalla de por qué cruza los pinchos sin
        /// cobrar. Apoyada, la inmunidad se ve como un bug.
        /// </summary>
        /// <remarks>
        /// Se levanta el hijo <c>Art</c> del wrapper y no <c>EntityPawn.PawnYOffset</c>, que es un
        /// <c>const</c> privado compartido por héroe y enemigos: levantarlo de ahí levantaría a todo
        /// el bestiario. <c>ApplyCritterFit</c> recalcula collider y barra contra los bounds ya
        /// levantados, así que este número es lo único que hay que tocar.
        /// </remarks>
        public const float CritterHoverHeight = 0.35f;

        /// <summary>Aire entre la punta del bicho y su barra de vida.</summary>
        private const float CritterBarClearance = 0.35f;

        /// <summary>
        /// La barra está autorada en unidades de mundo para un jefe de 2 de alto: sobre un bicho de
        /// 0.9 tapa la entidad entera.
        /// </summary>
        private const float CritterBarScale = 0.4f;

        /// <summary>Nombre del hijo que envuelve el arte — el default de <see cref="BossWrapperSpec"/>.</summary>
        private const string ArtChildName = "Art";

        /// <summary>Nombre del hijo con la barra de vida world-space que arma el wrapper.</summary>
        private const string HealthBarChildName = "Canvas";

        // Los cinco slots del rig alado, nombrados por FUNCIÓN y no por material fuente porque el
        // retinte cruza colores: los tres amarillos son los discos que lleva encima, Mat_Black es la
        // masa del cuerpo y Mat_Bone son las alas.
        public const string CritterChipFaceMaterial = "Mat_Yellow";
        public const string CritterChipEdgeMaterial = "Mat_DarkYellow";
        public const string CritterChipShineMaterial = "Mat_LightYellow";
        public const string CritterBodyMaterial = "Mat_Black";
        public const string CritterAccentMaterial = "Mat_Bone";

        // Plata y no oro: los discos en plata leen "cambio chico" contra el oro fuerte del jefe.
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

        private static readonly MaterialRetint CritterAccentRetint = MaterialRetint.FromColors(
            new Color(0.88f, 0.89f, 0.92f),
            new Color(0.63f, 0.65f, 0.70f),
            new Color(0.31f, 0.33f, 0.38f));

        // ---- Vestuario ---------------------------------------------------

        // Materiales del arte (Assets/Art/3D/Materials). Los cinco que usa el mech, nombrados por
        // FUNCIÓN y no por material fuente porque el retinte cruza colores: Mat_Gold, que cubre la
        // carcasa, es lo que queda dorado, pero Mat_Gray —las placas— se va a verde fieltro.
        public const string ShellMaterial = "Mat_Gold";
        public const string TrimMaterial = "Mat_DarkGray";
        public const string HighlightMaterial = "Mat_White";
        public const string BodyMaterial = "Mat_Gray";
        public const string AccentMaterial = "Mat_Brown";

        /// <summary>
        /// Tope del radio del capsule. El mech está en T-pose y sus bounds dan ~1.5, que taparía las
        /// cuatro casillas vecinas — y el jugador tiene que poder clickearlas (las monedas del piso
        /// y los pinchos de la sala).
        /// </summary>
        public const float ColliderRadiusCap = 0.5f;

        // La caja va al costado derecho y algo atrás para no tapar la silueta, y a escala 0.65 para
        // que no se meta en la casilla vecina (en las salas va a 1 y ocupa un tile entero).
        public static readonly Vector3 ChipsBoxLocalPosition = new Vector3(0.45f, 0f, -0.2f);
        public static readonly Vector3 ChipsBoxLocalEuler = new Vector3(0f, -25f, 0f);
        public static readonly Vector3 ChipsBoxLocalScale = new Vector3(0.65f, 0.65f, 0.65f);

        // Oro de banca: la carcasa —la superficie más grande del mech— se lo queda entera, con canto
        // y brillo en su propio ramp por tono (si los tres compartieran colores el volumen saldría
        // plano). Las placas van a verde fieltro para cortar el bloque dorado.
        private static readonly MaterialRetint ShellRetint = MaterialRetint.FromColors(
            new Color(1.00f, 0.91f, 0.52f),
            new Color(0.97f, 0.76f, 0.24f),
            new Color(0.60f, 0.42f, 0.11f));

        private static readonly MaterialRetint TrimRetint = MaterialRetint.FromColors(
            new Color(0.86f, 0.64f, 0.22f),
            new Color(0.63f, 0.45f, 0.14f),
            new Color(0.33f, 0.22f, 0.07f));

        private static readonly MaterialRetint HighlightRetint = MaterialRetint.FromColors(
            new Color(1.00f, 0.98f, 0.80f),
            new Color(1.00f, 0.91f, 0.55f),
            new Color(0.85f, 0.66f, 0.24f));

        private static readonly MaterialRetint BodyRetint = MaterialRetint.FromColors(
            new Color(0.17f, 0.44f, 0.29f),
            new Color(0.09f, 0.28f, 0.19f),
            new Color(0.04f, 0.13f, 0.09f));

        private static readonly MaterialRetint AccentRetint = MaterialRetint.FromColors(
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
                // El jefe muestra vida en la BossBarView del HUD; una barra world-space
                // encima del pawn la duplicaría.
                AddHealthBar = false,
                Retints = new Dictionary<string, MaterialRetint>
                {
                    { ShellMaterial, ShellRetint },
                    { TrimMaterial, TrimRetint },
                    { HighlightMaterial, HighlightRetint },
                    { BodyMaterial, BodyRetint },
                    { AccentMaterial, AccentRetint },
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
        /// <item>La persecución.</item>
        /// <item>El ciclo de ataque: pegado a vos, <c>Alternate[mandoble, empujón]</c>.</item>
        /// <item>Las monedas de la sala, cada <see cref="CoinRainEveryNRounds"/> rondas.</item>
        /// <item>La caja: vence monedas y lo cura con lo que nadie levantó.</item>
        /// </list>
        /// Todo lo que puede devolver Failed va en <c>Selector[acción, Wait]</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// La <b>persecución va antes del golpe</b>, y ahí está toda la diferencia entre empujarte y
        /// caminar con vos, y empujarte y dejarte ir. Si arranca el turno pegado,
        /// <c>AINode_Move</c> devuelve Failed —ya está en la banda— y nada lo mueve después del
        /// tumbo, así que el empujón se lee. Si arranca lejos, cierra y pega en el mismo turno:
        /// <see cref="BuildChase"/> apunta a <see cref="MeleeRange"/>, que es exactamente el rango
        /// que pide el gate del ataque.
        /// </para>
        /// <para>
        /// Con el movimiento en el medio el turno no se trunca: <c>AINode_Move</c> devuelve Running
        /// al caminar, pero el <c>Selector</c> de <see cref="WrapFallible"/> sólo propaga Succeeded
        /// en su path coroutine, así que la caja y la lluvia siguen corriendo el turno que camina.
        /// </para>
        /// <para>
        /// La <b>caja va después del ataque y de la lluvia</b> porque descubre las monedas barriendo
        /// las instancias vivas: si fuera antes, cada moneda soltada este turno viviría una ronda de
        /// más.
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
                    WrapFallible(BuildChase()),
                    WrapFallible(BuildAttackGate(chip)),
                    WrapFallible(BuildCoinRain(chip)),
                    WrapFallible(BuildCoinVault(chip)),
                },
            };
        }

        /// <summary>
        /// El ciclo de ataque, con el gate de rango <b>por fuera</b> del <c>Alternate</c>.
        /// </summary>
        /// <remarks>
        /// <c>AINode_Alternate</c> avanza el índice ANTES de tickear y no lo devuelve si el hijo
        /// falla, así que con los golpes auto-gateados por rango cada turno que el jefe pasa
        /// caminando le quemaría un turno del ciclo. Con el <c>If</c> afuera el índice sólo avanza en
        /// los turnos en que pega.
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
        /// Alternate y no Random: la alternancia es estricta y el mandoble va primero (el índice
        /// arranca en 0). Cada rama va en su propio <c>Selector[…, Wait]</c> porque el Alternate
        /// propaga el resultado del hijo.
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
        /// Comisión: el daño de <c>EffDealDamage</c> es privado y un builder no puede autorarlo. Los
        /// tres ids son los del gesto melee.
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
        /// con el cobro del <see cref="ShoveTaxPercent"/> y <see cref="ChipCount"/> monedas de esa
        /// plata tiradas en el camino.
        /// </summary>
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
                TaxPercent = ShoveTaxPercent,
                TaxMinimum = ShoveTaxMinimum,
                RefundPercent = ShoveRefundPercent,

                // La misma separación que la lluvia: las dos fuentes reparten por la sala, y dos
                // monedas pegadas son un solo viaje en cualquiera de las dos.
                CoinMinSeparation = CoinRainMinSeparation,
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
                MinValue = ChipValue,
                MaxValue = ChipValue,
                MinSeparation = CoinRainMinSeparation,
            };

        /// <summary>El reloj de las monedas: la que nadie levanta se pierde, y no lo cura.</summary>
        public static AINode_CajeroCoinVault BuildCoinVault(HazardDefinitionSO chip) =>
            new AINode_CajeroCoinVault
            {
                Coin = chip,
                LifetimeRounds = ChipDurationRounds,
            };

        /// <summary>
        /// La persecución: <see cref="ChaseSteps"/> pasos hacia el jugador, esquivando los pinchos.
        /// </summary>
        /// <remarks>
        /// El esquive no está acá: sale del planner, que lee el costo de cada casilla especial. Lo
        /// que lo hace tratarlos como intransitables es <see cref="SpikeAIVirtualDamage"/>, y no se
        /// gastan: los diez siguen siendo muro para él toda la pelea.
        /// <para>
        /// <c>Retreat = false</c>: no kitea nunca. Si ya está pegado el nodo sale por Failed y el
        /// Selector lo absorbe.
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
        /// <see cref="AINode_Once"/> y no el auto-gateo del nodo: con el auto-gateo las repone para
        /// siempre. El gesto es el trigger <c>Attack</c> porque es el único no-idle que declara su
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

                    // Inerte bajo el Once: el nodo no vuelve a tickear después del primer Succeeded.
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

            // Fallback: lo que el jugador lee sale de boss.cashier.desc, que se resuelve por
            // EntityId. Copia literal de esa entrada para que las dos no puedan divergir.
            data.Description = "Te tira lejos y te saca oro, y parte de esa plata cae al piso. " +
                               "La que no levantás a tiempo se pierde.";

            data.BaseHP = BaseHP;
            data.BaseAttack = BaseAttack;
            data.BaseSpeed = BaseSpeed;
            data.MaxEnergy = MaxEnergy;
            data.BaseHealStrength = 0;
            data.BaseAttackRange = MeleeRange;

            // Explícito y no por default: con IsFlying en true los pinchos (GroundOnly) dejarían de
            // cobrarle, y "los esquiva caminando pero los come empujado" es la herramienta
            // defensiva que la sala le da al jugador.
            data.IsFlying = false;

            // "La mano que paga fijo, la de la casa": combo.full ⇒ el id canónico del full house.
            data.WeaknessComboId = ComboId.FullHouse;
            data.WeaknessMultiplierOverride = WeaknessMultiplier;

            data.MinGoldDrop = MinGoldDrop;
            data.MaxGoldDrop = MaxGoldDrop;

            if (visualPrefab != null) data.VisualPrefab = visualPrefab;
            if (portrait != null) data.Portrait = portrait;

            data.AIRoot = BuildAIRoot(chip, critter);
            data.AIDetachedNodes.Clear(); // el builder es fuente de verdad: nada suelto sobrevive
            data.Design = new EnemyDesignSheet
            {
                Archetype = EnemyArchetype.Melee,
                Pattern = AttackPatternKind.ContactAdjacent,
                Timing = AttackTiming.Instant,
                Notes = "Mandoble y empujón alternados a distancia 1; lluvia de monedas; caja fuerte; refuerzos (Comisión).",
            };
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
        public static void PopulateCritterData(EnemyDataSO data, EnemyDataSO look = null)
        {
            if (data == null) return;

            data.EntityId = CritterEntityId;
            data.DisplayName = CritterDisplayName;

            data.BaseHP = CritterHp;

            // Su daño real sale del nodo del árbol, no de este stat (el árbol autorado saltea el
            // BasicEnemyAI). Se escribe igual porque lo leen el tooltip y los
            // TargetSelector_ByAttribute: en 0 la marcarían como support.
            data.BaseAttack = CritterDamage;
            data.BaseSpeed = CritterSpeed;
            data.MaxEnergy = 1;
            data.BaseHealStrength = 0;
            data.BaseAttackRange = CritterRange;

            // Al revés que su jefe: los pinchos son GroundOnly y la Comisión los sobrevuela. La
            // misma guarda alimenta al planner (ISpecialTileAIQuery.TryGetTileFor), así que no los
            // cobra Y tampoco los rodea — es la diferencia de movilidad que tiene contra el Cajero,
            // que sí tiene que esquivarlos. Con 18 de vida un pinchazo de 14 la borraba de una.
            data.IsFlying = true;

            data.WeaknessComboId = string.Empty;
            data.WeaknessMultiplierOverride = 0f;

            data.MinGoldDrop = 0;
            data.MaxGoldDrop = 0;

            // Todo lo que el jugador ve sale del ranged común, sin copia intermedia: arte,
            // retrato y la frase del párrafo. Sin la ficha a mano queda lo que ya había — es un
            // build parcial, no una razón para dejarlo sin arte.
            if (look != null)
            {
                data.Description = look.Description;
                if (look.VisualPrefab != null) data.VisualPrefab = look.VisualPrefab;
                if (look.Portrait != null) data.Portrait = look.Portrait;
            }

            data.AIRoot = BuildCritterAIRoot();
            data.AIDetachedNodes.Clear(); // el builder es fuente de verdad: nada suelto sobrevive
            data.Design = new EnemyDesignSheet
            {
                Archetype = EnemyArchetype.Ranged,
                Pattern = AttackPatternKind.DiamondArea,
                Timing = AttackTiming.Instant,
                Notes = "Dispara si el jugador está a ≤5 (Manhattan) y mantiene distancia 5; si no, se acerca.",
            };
        }

        /// <summary>
        /// Árbol de la Comisión: si el jugador está a tiro dispara, se despega si lo tiene encima,
        /// y si no vuela hacia él. El mismo reparto que el ranged común.
        /// </summary>
        /// <remarks>
        /// Dispara primero y se mueve después: los nodos de movimiento devuelven Running cuando se
        /// mueven, y un Running corta el Sequence, así que con el orden invertido el turno en que
        /// entra en rango se comería el disparo. Por lo mismo el retroceso va antes del approach:
        /// el que corta el turno es el que resolvió que había que moverse.
        /// </remarks>
        public static AINode_Sequence BuildCritterAIRoot() => new AINode_Sequence
        {
            Children = new List<AIDecisionNode>
            {
                WrapFallible(BuildCritterBite()),
                WrapFallible(BuildCritterKeepDistance()),
                WrapFallible(BuildCritterApproach()),
            },
        };

        /// <summary>El disparo de la Comisión: el del ranged común, presentación incluida.</summary>
        /// <remarks>
        /// El nodo base y no <see cref="AINode_CashierRangedShot"/>: esa subclase existe sólo para
        /// defaultear a los feedbacks del jefe, y el minion disparaba con el gesto, el vfx y el
        /// golpe de cámara de su jefe — era la mitad de lo que lo delataba como cosa suya. Queda
        /// sin autor y así se deja: el tipo lo siguen nombrando los guards del árbol del Cajero.
        /// Los tres ids van explícitos porque el base no tiene fallback: en vacío el paso se
        /// saltea y el bicho cobra sin moverse.
        /// </remarks>
        public static AINode_RangedShot BuildCritterBite() => new AINode_RangedShot
        {
            Damage = CritterDamage,
            Range = CritterRange,
            Metric = DistanceMetric.Manhattan,
            Kind = AttackKind.BasicAttack,
            AnimFeedbackId = SharedRangedShotAnim,
            ImpactVfxFeedbackId = SharedRangedShotImpactVfx,
            ImpactFeelFeedbackId = SharedRangedShotImpactFeel,
        };

        /// <summary>
        /// Lo que la separa del jugador cuando se le acerca, con los valores del ranged común
        /// (3 pasos, distancia 5) — que son los mismos que su alcance y su vuelo por turno.
        /// </summary>
        public static AINode_KeepDistance BuildCritterKeepDistance() => new AINode_KeepDistance
        {
            MaxSteps = new AIConstantInt { Value = CritterMoveSteps },
            IdealDistance = new AIConstantInt { Value = CritterRange },
        };

        /// <remarks>
        /// <c>DesiredRange</c> = su propio alcance y no 1: es un tirador y pega lo mismo de lejos.
        /// Sin kite: si ya está a tiro el nodo sale por Failed y el Selector lo absorbe.
        /// </remarks>
        public static AINode_Move BuildCritterApproach() => new AINode_Move
        {
            MaxSteps = new AIConstantInt { Value = CritterMoveSteps },
            DesiredRange = new AIConstantInt { Value = CritterRange },
            Retreat = false,
        };

        // ---- MenuItem ----------------------------------------------------

        [MenuItem(MenuPath)]
        public static void BuildCajeroAsset()
        {
            var chip = EnsureChipHazard();
            var spikes = EnsureSpikeTile();
            var portrait = EnsurePortrait();

            // Va afuera del guard de churn del wrapper: los eventos viven en los clips, no en el
            // prefab, así que un wrapper que no hace falta reconstruir igual los necesita.
            BossVisualWrapperBuilder.EnsureAttackHitEvents(AttackClipPaths);

            var critter = LoadOrCreate<EnemyDataSO>(ReinforcementAssetPath);

            // Su wrapper propio ya no se reconstruye: el aspecto lo pone el ranged común, así que
            // rearmar PF_Min_Comision churnearía un prefab y sus materiales clonados que no
            // alimentan a nadie. El prefab queda en disco — material disponible, no deuda.
            var sharedRanged = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(SharedRangedAssetPath);
            if (sharedRanged == null)
            {
                Debug.LogWarning($"[CajeroAssetBuilder] No se encontró el ranged común en " +
                                 $"'{SharedRangedAssetPath}': la Comisión queda con el arte que " +
                                 "ya tenía y va a leerse como un bicho aparte.");
            }

            PopulateCritterData(critter, sharedRanged);
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
                      $"monedas: {CoinsPerRain} cada {CoinRainEveryNRounds} rondas de " +
                      $"{ChipValue}g + {ChipCount} por empujón con el " +
                      $"{ShoveRefundPercent:P0} del {ShoveTaxPercent:P0} cobrado " +
                      $"(mín. {ShoveTaxMinimum}), duran {ChipDurationRounds} rondas, " +
                      $"y se vencen de a una sin curarlo; " +
                      $"visual: {NameOf(visual)}, retrato: {NameOf(portrait)}) + {CritterCount} × " +
                      $"'{NameOf(critter)}' ({critter.BaseHP} HP, {critter.BaseAttack} a " +
                      $"≤{CritterRange}, con el aspecto de '{NameOf(sharedRanged)}') al " +
                      $"{CritterHpThreshold:P0}.");

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
        /// <param name="outputPath">Destino del wrapper. Default: <see cref="VisualPrefabPath"/>.</param>
        /// <param name="materialsFolder">
        /// Carpeta de los materiales clonados. <c>null</c> deja el default del wrapper builder.
        /// </param>
        public static GameObject EnsureVisualPrefab(
            string outputPath = VisualPrefabPath, string materialsFolder = null)
        {
            var wrapper = BossVisualWrapperBuilder.BuildWrapper(
                BuildWrapperSpec(outputPath, materialsFolder));
            if (wrapper == null)
            {
                Debug.LogWarning($"[CajeroAssetBuilder] No se pudo construir el wrapper visual en " +
                                 $"'{outputPath}' — se deja el VisualPrefab que ya tenga la ficha.");
                return null;
            }

            ClampColliderRadius(outputPath);
            return AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
        }

        /// <summary>
        /// Segunda pasada sobre el wrapper ya guardado: recorta el radio del capsule a
        /// <see cref="ColliderRadiusCap"/>.
        /// </summary>
        /// <remarks>
        /// Pasada aparte porque el wrapper dimensiona el collider contra los bounds del arte, y los
        /// del mech en T-pose dan ~1.5. Reescribir sobre el mismo path conserva el GUID, así que la
        /// ficha que ya apunta al wrapper sobrevive.
        /// </remarks>
        private static void ClampColliderRadius(string prefabPath)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogWarning($"[CajeroAssetBuilder] No se pudo abrir '{prefabPath}' para " +
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

        /// <summary>
        /// Ficha de armado del wrapper de la Comisión. Pura, por el mismo motivo que
        /// <see cref="BuildWrapperSpec"/>.
        /// </summary>
        public static BossWrapperSpec BuildCritterWrapperSpec(
            string outputPath = CritterVisualPrefabPath, string materialsFolder = null)
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = CritterArtPrefabPath,
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
                    { CritterChipShineMaterial, CritterChipShineRetint },
                    { CritterChipFaceMaterial, CritterChipFaceRetint },
                    { CritterChipEdgeMaterial, CritterChipEdgeRetint },
                    { CritterBodyMaterial, CritterBodyRetint },
                    { CritterAccentMaterial, CritterAccentRetint },
                },
            };
        }

        /// <summary>
        /// Construye (o reconstruye) el wrapper de la Comisión y lo devuelve, ya encogido y flotando.
        /// <c>null</c> + warning si el arte falta.
        /// </summary>
        /// <summary>Si el prefab ya construido anida el arte de <paramref name="artPath"/>.</summary>
        private static bool NestsArt(string prefabPath, string artPath)
        {
            var deps = AssetDatabase.GetDependencies(prefabPath, recursive: true);
            return System.Array.IndexOf(deps, artPath) >= 0;
        }

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
        /// Pasada aparte porque <see cref="BossVisualWrapperBuilder"/> fija el arte en identidad y
        /// dimensiona el collider asumiendo eso. Reescribir sobre el mismo path conserva el GUID, así
        /// que la ficha que ya apunta al wrapper sobrevive.
        /// </para>
        /// <para>
        /// El collider hay que reescribirlo sí o sí: dimensionado alrededor del arte a escala 1 y
        /// apoyado, después de encoger y subir queda envolviendo aire y el cursor pica el piso.
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

                // Se leen antes de medir, porque medir pisa las dos: son la referencia contra
                // la que se decide si hay algo que guardar.
                var hadScale = art.localScale;
                var hadPosition = art.localPosition;

                // Los bounds se miden con el arte en identidad, que es como lo dejó el wrapper.
                art.localScale = Vector3.one;
                art.localPosition = Vector3.zero;
                bool measured = TryMeasureRenderers(art, out var raw);

                art.localScale = Vector3.one * CritterArtScale;

                // El lift lleva la BASE del arte a CritterHoverHeight y no su pivot: dónde cae el
                // pivot es una convención del arte que este builder no puede asumir.
                float baseY = measured ? raw.min.y * CritterArtScale : 0f;
                art.localPosition = new Vector3(0f, CritterHoverHeight - baseY, 0f);

                // Vector3 == compara con epsilon, que es la tolerancia que quiere un valor
                // serializado en el prefab.
                bool changed = hadScale != art.localScale || hadPosition != art.localPosition;

                if (measured)
                {
                    var flying = new Bounds(
                        raw.center * CritterArtScale + new Vector3(0f, art.localPosition.y, 0f),
                        raw.size * CritterArtScale);

                    var box = contents.GetComponent<BoxCollider>();
                    if (box != null)
                    {
                        changed |= box.center != flying.center || box.size != flying.size;
                        box.center = flying.center;
                        box.size = flying.size;
                    }

                    var bar = contents.transform.Find(HealthBarChildName);
                    if (bar != null)
                    {
                        var barPosition = new Vector3(0f, flying.max.y + CritterBarClearance, 0f);
                        var barScale = Vector3.one * CritterBarScale;
                        changed |= bar.localPosition != barPosition || bar.localScale != barScale;
                        bar.localPosition = barPosition;
                        bar.localScale = barScale;
                    }
                }

                // Sin cambio no se reescribe: SaveAsPrefabAsset renumera fileIDs internos y
                // ensuciaría el diff del prefab en cada corrida del builder.
                if (changed) PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
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
        /// Retrato del jefe. Es un método y no una constante porque el sub-sprite hay que resolverlo
        /// contra el AssetDatabase, y <c>BuildContractCard</c> lo pide por separado.
        /// </summary>
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

            // 0 = no vence sola. AINode_CajeroCoinVault tiene que ser el ÚNICO que la mate: el
            // servicio de hazards expira igual una cobrada y una vencida, y desde afuera no se
            // pueden distinguir. Ver ChipDurationRounds para la vida real de la moneda.
            chip.DurationRounds = 0;

            chip.Shape = ThreatShape.Column; // Inerte: las monedas se activan con la overload de tiles.
            chip.Size = 1;
            chip.OverlayTint = new Color(1f, 0.84f, 0.25f, 0.55f); // oro
            chip.SourceId = ChipHazardSourceId;

            // Sin el prefab persistente, "hay una ficha acá" y "esta casilla está marcada" se ven
            // exactamente igual.
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
        /// Escribe la ficha de los pinchos sobre <paramref name="spikes"/>, sin el arte. Pura: no
        /// toca AssetDatabase ni marca dirty — mismo reparto que <see cref="PopulateEnemyData"/>.
        /// </summary>
        public static void PopulateSpikeTile(SpecialTileDefinitionSO spikes)
        {
            if (spikes == null) return;

            spikes.TileId = SpikeTileId;
            spikes.DisplayName = "Pinchos del Cajero";
            spikes.TileType = SpecialTileType.Spikes;

            // OnForcedMovementInto es lo que hace que el tumbo del empujón cobre las casillas que
            // cruza, y que el Empuje del jugador se los cobre a él.
            spikes.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
            spikes.Category = TileEffectCategory.Damage;
            spikes.Affinity = TileAffinity.GroundOnly;
            spikes.DamageKind = AttackKind.Environmental;
            spikes.EnterDamage = SpikeDamage;
            spikes.TurnStartDamage = 0;

            // Permanentes: son terreno de la sala, no algo que el jefe pone.
            spikes.DefaultDurationRounds = 0;

            // No se gastan: la casilla cobra cada vez que la cruzás, y el segundo empujón por el
            // mismo lugar sale igual que el primero. Con desarme, cada pincho disparado le abría al
            // jefe un pasillo por un turno —el pathing lee el bajado como suelo limpio— y el campo
            // dejaba de ser muro justo cuando el jugador ya había pagado por gastarlo.
            spikes.DisarmOnTrigger = false;
            spikes.RearmOnRoundWrap = false;

            spikes.AIVirtualEnterDamage = SpikeAIVirtualDamage;
            spikes.AIAnnouncesLethal = false;

            // Key propia y no la del pincho genérico: éste pega 20 contra los 12 del común, y el
            // tooltip no puede presentarlo como el mismo objeto.
            spikes.NameKey = "tile.spikes_cajero";
            spikes.DescriptionKey = "tile.spikes_cajero";
        }

        /// <summary>
        /// Crea (o actualiza) los pinchos de la sala del Cajero: cobran <see cref="SpikeDamage"/> al
        /// entrar —también empujado— y <b>no se gastan</b>: la misma casilla cobra todas las veces.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Este builder crea la definición, no las coloca.</b> Las casillas las escribe
        /// <c>BossRoomBuilder</c> en <c>RoomLayout.SpecialTilePlacements</c> de
        /// <c>Boss_Room_Cajero</c>, leyendo <see cref="SpikePlanCells"/>, y van por la lista de
        /// permanentes y no por un slot.
        /// </para>
        /// <para>
        /// Al venir de la sala el owner queda vacío, y eso es lo que hace que el jefe <b>no</b> sea
        /// inmune: <c>SpecialTileService.ShouldAffect</c> exime a un jefe sólo de las casillas cuyo
        /// owner es un jefe.
        /// </para>
        /// </remarks>
        public static SpecialTileDefinitionSO EnsureSpikeTile()
        {
            var spikes = LoadOrCreate<SpecialTileDefinitionSO>(SpikeTilePath);
            PopulateSpikeTile(spikes);

            // Mismo arte y mismo color de paleta que el pincho genérico: para el jugador es el mismo
            // objeto.
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
