using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Combat.Cashier;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Arma el asset del jefe de piso 2, <b>El Cajero</b> (<c>boss.cashier</c>), desde su ficha de
    /// diseño: stats, debilidad, drop de oro y el árbol de AI completo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos capas.</b> <see cref="BuildAIRoot"/> y <see cref="PopulateEnemyData"/> son estáticas y
    /// puras (no tocan <c>AssetDatabase</c>), así los tests validan el wiring en memoria sin
    /// depender de que el asset exista ni de un reimport. El <see cref="MenuItem"/> es la única
    /// parte que escribe en disco, y es idempotente: re-ejecutarlo actualiza el asset existente en
    /// vez de duplicarlo (conserva su GUID y las referencias que ya lo apunten).
    /// </para>
    /// <para>
    /// <b>El prefab visual lo genera el builder.</b> <see cref="EnsureVisualPrefab"/> arma el wrapper
    /// de gameplay sobre el arte propio del jefe (ver <see cref="BossVisualWrapperBuilder"/>) y lo
    /// deja en <see cref="VisualPrefabPath"/>. El placeholder del Security Boss sigue nombrado sólo
    /// para poder migrar una ficha que todavía lo apunte; un prefab distinto de esos dos se considera
    /// autorado a mano y no se pisa (ver <see cref="ResolveVisualPrefab"/>).
    /// </para>
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

        /// <summary>Caja de fichas del mostrador, parenteada al costado del jefe.</summary>
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
        /// Piso 2: ~7 turnos con el golpe base del piso (mediana 24). Los hasta
        /// <see cref="AuditMaxHeal"/> que se cura en el arqueo son presupuesto aparte:
        /// suman turnos sin figurar acá.
        /// </summary>
        public const int BaseHP = 170;
        public const int BaseAttack = 30;
        public const int BaseSpeed = 4;
        public const int MaxEnergy = 3;
        public const int MinGoldDrop = 30;
        public const int MaxGoldDrop = 60;
        public const float WeaknessMultiplier = 1.5f;

        /// <summary>Umbral de HP del arqueo de caja (Fase 2).</summary>
        public const float AuditHpThreshold = 0.5f;

        public const float AuditTaxPercent = 0.4f;
        public const int AuditMaxHeal = 30;
        public const int ChipMultiplierAfterAudit = 2;

        public const int ChipMinValue = 6;
        public const int ChipMaxValue = 9;
        public const int ChipDurationRounds = 1;
        public const int ChipMinDistance = 2;
        public const int ChipMaxDistance = 3;

        public const int KeepDistanceIdeal = 4;
        public const int KeepDistanceMaxSteps = 3;

        // ---- La columna --------------------------------------------------

        /// <summary>
        /// Umbrales de oro de los tres escalones. Medidos contra el oro con el que se entra de
        /// verdad al piso 2 (~65-70), no contra un ideal: con los viejos 80/250 el jugador caía
        /// en el escalón pobre casi siempre y el jefe medía 0% de vida perdida en la mediana de
        /// 3000 peleas simuladas. Con 40/120, 65 de oro paga el escalón medio y el rico queda a
        /// una tanda de fichas de distancia.
        /// </summary>
        public const int MidTierMinGold = 40;

        /// <inheritdoc cref="MidTierMinGold" />
        public const int RichTierMinGold = 120;

        public const int PoorTierDamage = 14;
        public const int MidTierDamage = 28;

        /// <summary>Techo de daño por golpe del piso 2. No subir.</summary>
        public const int RichTierDamage = 35;

        // ---- El disparo --------------------------------------------------

        /// <summary>
        /// Daño del disparo a rango, el ataque de los turnos en que no marca columna. Es el
        /// primer ranged del juego y el que cierra la tenaza: la columna se esquiva con un paso,
        /// pero pegarle exige distancia 1 y distancia 1 está dentro de este alcance.
        /// </summary>
        public const int ShotDamage = 12;

        /// <summary>
        /// Alcance del disparo. Igual a <see cref="KeepDistanceIdeal"/> a propósito: si fuera
        /// menor, el jefe se replegaría fuera de su propio rango y perdería el turno de disparo.
        /// </summary>
        public const int ShotRange = KeepDistanceIdeal;

        // ---- El peaje ----------------------------------------------------

        /// <summary>
        /// Peaje del mostrador: lo que cuesta terminar el turno del lado de él. Es el precio de la
        /// abertura — sin peaje, elegir puerta no cuesta nada y el mostrador es decorado.
        /// </summary>
        /// <remarks>
        /// Subido de 10 a 20. Con 10 el peaje era barato de pagar: cruzar y quedarse costaba menos
        /// que un turno perdido replegándose, así que el mostrador dejaba de ser una decisión y
        /// pasaba a ser un impuesto que convenía comer. A 20 —dos tercios de un golpe suyo del
        /// escalón medio— quedarse del lado de él tiene que valer la pena de verdad.
        /// <para>
        /// Sigue por debajo del techo de daño por golpe del piso 2 (<see cref="RichTierDamage"/> =
        /// 35) a propósito: el peaje es el precio de una posición, no su ataque.
        /// </para>
        /// </remarks>
        public const int CounterTollDamage = 20;

        /// <summary>
        /// Fila del mostrador en coordenadas de la sala. Autorada acá porque el jefe no tiene forma
        /// de leer el terreno: los blockers son props con <c>TileMarker</c> horneados en el
        /// NavGraph, y un agujero en el grafo no dice "esto es un mostrador".
        /// </summary>
        /// <remarks>
        /// El valor sale del plano de <c>Boss_Room_Cajero</c> (<c>BossRoomBuilder.Plans</c> +
        /// <c>docs/setup/boss-rooms.md §3</c>): las nueve casillas del mostrador están en la fila
        /// <c>y = 3</c> del plano, que <c>PlanToRoom</c> manda a <c>Y = 0</c> de la sala. El jefe
        /// spawnea en <c>(0, 2)</c> ⇒ su lado es <c>Y &gt; 0</c> y el del jugador <c>Y &lt; 0</c>.
        /// Si el mostrador se mueve de fila hay que mover esto con él —
        /// <c>CajeroPhaseWiringTests</c> lo cruza contra el plano para que no se olvide.
        /// </remarks>
        public const int CounterRow = 0;

        /// <summary>Id estable del hazard-ficha: el servicio de hazards keyea por él. Hex válido —
        /// un SourceId que no parsea a Guid loguea error cada vez que se lee.</summary>
        public const string ChipHazardSourceId = "3c0a7d18-9f42-4a6b-9c3e-5b1ca5e70001";

        // ---- Las Comisiones ----------------------------------------------

        /// <summary>
        /// Ficha de la Comisión: el bicho volador que el Cajero suelta al cruzar el 50%.
        /// </summary>
        /// <remarks>
        /// Va en <c>ED_Min_</c> y no en <c>ED_Obj_</c> porque muerde. Los otros dos acompañantes
        /// autorados por un builder de jefe (<c>ED_Obj_DadoCasa</c>, <c>ED_Obj_Rodillo</c>) son
        /// terreno con vida: Attack 0, no actúan. Esta sí, y meterla en la misma familia haría que
        /// "obj." dejara de querer decir nada.
        /// </remarks>
        public const string CritterAssetPath = "Assets/Rollgeon/Enemies/ED_Min_Comision.asset";

        public const string CritterVisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Min_Comision.prefab";

        public const string CritterEntityId = "minion.cajero_comision";
        public const string CritterDisplayName = "Comisión";

        /// <summary>Nombre corto para la carpeta y el prefijo de sus materiales clonados.</summary>
        public const string CritterName = "Comision";

        /// <summary>Dos, y una sola vez. Ver <see cref="BuildCritterGate"/>.</summary>
        public const int CritterCount = 2;

        /// <summary>
        /// Mismo umbral que el arqueo: el 50% es <b>un</b> momento de la pelea, no dos. Cruzarlo te
        /// cobra el 40% del oro y te deja dos bichos encima en el mismo turno.
        /// </summary>
        public const float CritterHpThreshold = AuditHpThreshold;

        /// <summary>
        /// Muere de un golpe de la mediana del piso 2 (24) y sobrevive a uno flojo. Es la medida de
        /// lo que cuesta sacárselos de encima: un golpe cada uno, dos golpes que no fueron al jefe.
        /// </summary>
        public const int CritterHp = 18;

        /// <summary>
        /// Mordisco. Los dos juntos pegan 12 por turno — menos que el peaje, y a propósito: la
        /// Comisión es un impuesto por dejarlos vivos, no un segundo jefe. Suben el costo de
        /// quedarse quieto pegándole al Cajero justo cuando el arqueo lo acaba de curar.
        /// </summary>
        public const int CritterDamage = 6;

        /// <summary>Vuela: va antes que el jefe (4) en la cola, así el turno en que aparecen ya presionan.</summary>
        public const int CritterSpeed = 5;

        /// <summary>Alcance de vuelo por turno. Tres cubre media sala — perseguir es lo único que hace.</summary>
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

        // La caja va al costado derecho y algo atrás para no tapar la silueta ni el telegráfico de la
        // columna. Escala 0.65 (en las salas la caja va a 1 y ocupa un tile entero) para que no se
        // meta en la casilla vecina, que es donde el jugador tiene que poder leer la amenaza.
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
        /// Escalones de la columna, tal cual la ficha: &lt;40 ⇒ Size 1 / 14, 40-119 ⇒ Size 3 / 28,
        /// ≥120 ⇒ Size 3 / 35 (35 = techo de daño de piso 2, no subir).
        /// </summary>
        /// <remarks>
        /// El escalón no lo decide sólo el oro: el rastrillo de
        /// <c>ICashierLedgerService.DamageStepUp</c> le suma uno cada 3 rondas y el soborno se lo
        /// resta. La tabla es el piso desde el que arranca ese tira y afloja, no el daño final.
        /// </remarks>
        public static List<CashierGoldTier> BuildGoldTiers() => new List<CashierGoldTier>
        {
            new CashierGoldTier { MinGold = 0,              ColumnSize = 1, Damage = PoorTierDamage },
            new CashierGoldTier { MinGold = MidTierMinGold,  ColumnSize = 3, Damage = MidTierDamage },
            new CashierGoldTier { MinGold = RichTierMinGold, ColumnSize = 3, Damage = RichTierDamage },
        };

        /// <summary>
        /// Árbol del Cajero. Sequence raíz de 7 hijos:
        /// <list type="number">
        /// <item><c>ExecuteTelegraph</c> — detona la columna del turno pasado.</item>
        /// <item>Gate del arqueo (50% HP) → <c>Once → Sequence[Audit, ApplyStatModifier]</c>.</item>
        /// <item>Gate de las Comisiones (50% HP) → <c>Once → SpawnReinforcements ×2</c>.</item>
        /// <item>El peaje del mostrador, que cobra al cerrar el turno del jugador.</item>
        /// <item>El ciclo de ataque: <c>Alternate[columna, disparo]</c>.</item>
        /// <item>Fichas, dentro de la columna recién marcada.</item>
        /// <item><c>KeepDistance</c> al otro lado del mostrador.</item>
        /// </list>
        /// Todo lo que puede devolver Failed va en <c>Selector[acción, Wait]</c>.
        /// </summary>
        /// <remarks>
        /// <b>Desvío de la ficha:</b> el gate de fase está en el hijo 2 y no en el 4. La ficha lo
        /// dibuja último, pero el patrón obligado (y el bug que dejó quieto al Sunken Grand) pide
        /// las fases <b>antes</b> del ataque: en el path no-coroutine un Running del ataque aborta
        /// lo que venga después, y el arqueo no puede depender de eso. Efecto lateral asumido: el
        /// turno en que cruza el 50%, el arqueo se cobra antes de marcar, así que la columna de ese
        /// turno ya usa el oro reducido — un alivio coherente con "te cobró, pega menos".
        /// <para>
        /// Las fichas van <b>después</b> del ciclo de ataque por contrato de la ficha: caen dentro
        /// de la columna de este turno, que se leen de <c>IThreatenedAreaService</c>. En los turnos
        /// de disparo no hay columna y el nodo sale por Failed sin gastar el flag de daño.
        /// </para>
        /// <para>
        /// <b>El peaje también va antes del ataque</b>, y por el mismo motivo que el arqueo: es lo
        /// que arma el cobro del cierre de turno del jugador, y un Running del ataque lo dejaría
        /// sin armar justo en los turnos en que el jefe sí actuó.
        /// </para>
        /// <para>
        /// <b>Las Comisiones tienen su propio gate y su propio <c>Once</c></b> aunque compartan
        /// umbral con el arqueo. Colgarlas del Sequence de adentro del arqueo habría dejado los dos
        /// efectos atados a un solo latch, y <c>AINode_SpawnReinforcements</c> devuelve Failed
        /// cuando la sala no tiene tiles de borde libres: ese Failed cortaría el Sequence, el
        /// <c>Once</c> no latchearía (sólo latchea con Succeeded) y el turno siguiente el arqueo se
        /// cobraría de nuevo — 40% del oro y hasta 30 de cura, dos veces.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(
            HazardDefinitionSO chip = null, EnemyDataSO critter = null)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // La columna marcada la ronda pasada cobra ACÁ, y sin este id cobraba en
                    // silencio: el jefe se quedaba en idle mientras aparecía el número. Es su
                    // ataque principal — el que el jugador ve cinco veces por pelea.
                    new AINode_ExecuteTelegraph { WindupFeedbackId = BossFeedbackIds.CajeroShotAnim },
                    WrapFallible(BuildAuditGate()),
                    WrapFallible(BuildCritterGate(critter)),
                    WrapFallible(BuildCounterToll()),
                    WrapFallible(BuildAttackCycle()),
                    WrapFallible(BuildChipDrop(chip)),
                    WrapFallible(BuildKeepDistance()),
                },
            };
        }

        /// <summary>
        /// El ciclo de ataque: un turno marca columna, el siguiente dispara, y así siempre.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Alternate y no Random.</b> La alternancia tiene que ser estricta y legible: el
        /// jugador aprende que el turno después de la marca no hay marca nueva, y planta el
        /// movimiento en consecuencia. Un <c>AINode_Random</c> puede dar tres columnas seguidas
        /// por probabilidad y convierte la lectura en adivinanza.
        /// </para>
        /// <para>
        /// <b>La columna va primera.</b> El índice del <c>Alternate</c> arranca en 0 y es
        /// <c>[NonSerialized]</c> (copia fresca por combate), así que el primer turno del jefe
        /// siempre marca. Abrir disparando sería 12 de daño sin que el jugador haya visto todavía
        /// de qué se trata la pelea.
        /// </para>
        /// <para>
        /// Cada rama va envuelta en su propio <c>Selector[…, Wait]</c>: el Alternate propaga el
        /// resultado del hijo, y un Failed benigno del disparo (jugador fuera de rango) abortaría
        /// el turno entero antes de las fichas y del repliegue.
        /// </para>
        /// </remarks>
        public static AINode_Alternate BuildAttackCycle() => new AINode_Alternate
        {
            Children = new List<AIDecisionNode>
            {
                WrapFallible(BuildColumn()),
                WrapFallible(BuildRangedShot()),
            },
        };

        /// <summary>Gate del arqueo: dispara una sola vez al cruzar el 50% de HP.</summary>
        public static AINode_If BuildAuditGate() => new AINode_If
        {
            TargetSelector = new TargetSelector_Self(),
            Conditions = new List<BasePreCondition>
            {
                new PcOwnerHpBelow { Percent = AuditHpThreshold },
            },
            Then = new AINode_Once
            {
                Child = new AINode_Sequence
                {
                    Children = new List<AIDecisionNode>
                    {
                        new AINode_CashierAudit
                        {
                            TaxPercent = AuditTaxPercent,
                            MaxHeal = AuditMaxHeal,
                            ChipValueMultiplierAfterAudit = ChipMultiplierAfterAudit,
                        },
                        // Sin deltas de stat: el Cajero no pega más fuerte por la fase (su daño lo
                        // decide el oro). El nodo va sólo por el OnBossPhaseChanged del feedback.
                        new AINode_ApplyStatModifier
                        {
                            AttackDelta = 0,
                            SpeedDelta = 0,
                            PhaseIndex = 2,
                            EmitPhaseChangedEvent = true,
                        },
                    },
                },
            },
            Else = new AINode_Wait(),
        };

        /// <summary>
        /// Gate de las Comisiones: al cruzar el 50% suelta <see cref="CritterCount"/> bichos
        /// voladores, una sola vez en toda la pelea.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b><see cref="AINode_Once"/> y no el auto-gateo del nodo.</b> El
        /// <c>AINode_SpawnReinforcements</c> sabe reponer oleadas solo (así lo usa La Generala para
        /// su mesa de dados), y esa es exactamente la lectura que no queremos acá: el Cajero ya se
        /// cura hasta 30 en el arqueo del mismo umbral, y sumarle refuerzos infinitos convierte el
        /// tramo final en una pelea que no termina. Dos bichos, una vez, y el que los mata se los
        /// saca de encima para siempre.
        /// </para>
        /// <para>
        /// <b>El gesto de invocar es <see cref="BossFeedbackIds.CajeroMeleeAnim"/></b>, o sea el
        /// trigger <c>Attack</c> — el único no-idle que declara <c>AnimCon_GeneralDirector</c>. No es
        /// una animación de invocar y no la hay; la alternativa era dejarlo vacío y que dos bichos se
        /// materialicen mientras el jefe está quieto, que es el síntoma que documenta el propio
        /// <c>AINode_SpawnReinforcements</c> ("aparecían de la nada"). Un manotazo al aire en el
        /// frame en que aparecen los ata a él. Es el mismo criterio con el que La Generala repone la
        /// mesa usando su clip de <c>Heal</c>, y con el que este mismo id ya cubre el peaje y el
        /// arqueo del Cajero.
        /// </para>
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

        public static AINode_TelegraphMarkGoldScaled BuildColumn() => new AINode_TelegraphMarkGoldScaled
        {
            Shape = ThreatShape.Column,
            Kind = AttackKind.BasicAttack,
            ApplyBribeStepDown = true,
            ApplyRakeStepUp = true,
            Tiers = BuildGoldTiers(),
        };

        /// <summary>
        /// El peaje del mostrador. El nodo sólo arma <c>ICashierCounterTollService</c>: el cobro
        /// ocurre al cerrar el turno del jugador, fuera del turno del jefe.
        /// </summary>
        public static AINode_CashierCounterToll BuildCounterToll() => new AINode_CashierCounterToll
        {
            Damage = CounterTollDamage,
            CounterRow = CajeroAssetBuilder.CounterRow,
        };

        public static AINode_CashierRangedShot BuildRangedShot() => new AINode_CashierRangedShot
        {
            Damage = ShotDamage,
            Range = ShotRange,
            Metric = DistanceMetric.Manhattan,
            Kind = AttackKind.BasicAttack,
        };

        public static AINode_CashierDropChips BuildChipDrop(HazardDefinitionSO chip) =>
            new AINode_CashierDropChips
            {
                Chip = chip,
                Count = 1,
                MinValue = ChipMinValue,
                MaxValue = ChipMaxValue,
                MinDistanceFromPlayer = ChipMinDistance,
                MaxDistanceFromPlayer = ChipMaxDistance,
                RequireDamageTaken = true,
            };

        public static AINode_KeepDistance BuildKeepDistance() => new AINode_KeepDistance
        {
            MaxSteps = new AIConstantInt { Value = KeepDistanceMaxSteps },
            IdealDistance = new AIConstantInt { Value = KeepDistanceIdeal },
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
            data.Description =
                "Cortés, contable, imperturbable. Su daño lo decide el oro que llevás encima, " +
                "y cada golpe tuyo tira fichas al piso.";

            data.BaseHP = BaseHP;
            data.BaseAttack = BaseAttack;
            data.BaseSpeed = BaseSpeed;
            data.MaxEnergy = MaxEnergy;
            data.BaseHealStrength = 0;
            data.BaseAttackRange = 1;

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
                "Lo que el Cajero manda a cobrar cuando ya no le alcanza el mostrador. Vuela, " +
                "muerde poco y no se va sola.";

            data.BaseHP = CritterHp;

            // Su daño real sale del nodo del árbol, no de este stat (el árbol autorado saltea el
            // BasicEnemyAI). Se escribe igual y con el mismo número porque es lo que leen el
            // tooltip y los TargetSelector_ByAttribute: dejarlo en 0 la marcaría como support.
            data.BaseAttack = CritterDamage;
            data.BaseSpeed = CritterSpeed;
            data.MaxEnergy = 1;
            data.BaseHealStrength = 0;
            data.BaseAttackRange = 1;

            data.WeaknessComboId = string.Empty;
            data.WeaknessMultiplierOverride = 0f;

            // Cero oro, y no por tacañería: el daño de la columna del Cajero ESCALA con el oro que
            // el jugador lleva encima (ver BuildGoldTiers). Un bicho que paga al morir le subiría el
            // escalón al jefe — matarlos haría la pelea más difícil, que es exactamente al revés de
            // lo que el jugador va a leer.
            data.MinGoldDrop = 0;
            data.MaxGoldDrop = 0;

            if (visualPrefab != null) data.VisualPrefab = visualPrefab;
            if (portrait != null) data.Portrait = portrait;

            data.AIRoot = BuildCritterAIRoot();
        }

        /// <summary>
        /// Árbol de la Comisión: si está pegada muerde, y si no vuela hacia el jugador.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Muerde primero y se mueve después</b>, no al revés. <see cref="AINode_Move"/> devuelve
        /// Running cuando efectivamente se mueve, y un Running corta el Sequence: con el orden
        /// invertido, el turno en que llega al jugador se le comería el mordisco. Con este orden el
        /// bicho que ya está pegado muerde (y el Move sale por Failed benigno, "ya estoy en la
        /// banda"), y el que está lejos falla el mordisco y vuela.
        /// </para>
        /// <para>
        /// <b>El mordisco es <see cref="AINode_CashierRangedShot"/> con <c>Range = 1</c></b>, o sea
        /// el disparo del Cajero a quemarropa. No es un rodeo: el nodo es "tantos de daño directo a
        /// distancia ≤ Range, auto-gateado por rango", y con 1 eso <b>es</b> un melee. El camino
        /// alternativo —<c>AINode_Behavior → EnemyActionBehavior → EffDealDamage</c>— no sirve acá
        /// por lo que el propio nodo documenta: el daño base de <c>EffDealDamage</c> es un campo
        /// privado sin setter, así que un builder no puede autorarlo y el mordisco quedaría clavado
        /// en el default de 10. Y sin árbol propio el bicho caería al <c>BasicEnemyAI</c>, que pega
        /// al jugador <b>desde cualquier distancia</b>: un impuesto inesquivable en vez de un bicho.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildCritterAIRoot() => new AINode_Sequence
        {
            Children = new List<AIDecisionNode>
            {
                WrapFallible(BuildCritterBite()),
                WrapFallible(BuildCritterApproach()),
            },
        };

        public static AINode_CashierRangedShot BuildCritterBite() => new AINode_CashierRangedShot
        {
            Damage = CritterDamage,
            Range = 1,
            Metric = DistanceMetric.Manhattan,
            Kind = AttackKind.BasicAttack,
        };

        public static AINode_Move BuildCritterApproach() => new AINode_Move
        {
            MaxSteps = new AIConstantInt { Value = CritterMoveSteps },
            DesiredRange = new AIConstantInt { Value = 1 },

            // Sin kite: la Comisión no tiene nada que hacer lejos. Si ya está pegada, el nodo sale
            // por Failed y el Selector lo absorbe.
            Retreat = false,
        };

        // ---- MenuItem ----------------------------------------------------

        [MenuItem("Tools/Rollgeon/Bosses/Build Cajero")]
        public static void BuildCajeroAsset()
        {
            var chip = EnsureChipHazard();
            var portrait = EnsurePortrait();

            // La Comisión primero: la ficha del jefe la referencia desde su árbol, y un
            // AINode_SpawnReinforcements con EnemyToSpawn en null devuelve Failed siempre.
            var critter = LoadOrCreate<EnemyDataSO>(CritterAssetPath);
            PopulateCritterData(critter, EnsureCritterVisualPrefab(), portrait);
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
                      $"(ficha: {BaseHP} HP, escalones {MidTierMinGold}/{RichTierMinGold} de oro, " +
                      $"disparo {ShotDamage} a ≤{ShotRange}, fichas {ChipMinValue}-{ChipMaxValue}g, " +
                      $"peaje {CounterTollDamage} en la fila {CounterRow}, " +
                      $"arqueo al {AuditHpThreshold:P0}; visual: {NameOf(visual)}, " +
                      $"retrato: {NameOf(portrait)}) + {CritterCount} × '{CritterEntityId}' " +
                      $"({CritterHp} HP, mordisco {CritterDamage}) al {CritterHpThreshold:P0} en " +
                      $"'{CritterAssetPath}'.");
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
        /// Sin props: la caja de fichas es el mostrador del jefe, y colgársela a un bicho de 0.9 lo
        /// convertiría en un Cajero chiquito con la misma silueta.
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
        /// <para>
        /// <b>Un prefab autorado a mano manda.</b> Si la ficha apunta a algo que no es ni el wrapper
        /// de este builder ni el placeholder viejo, lo puso alguien a propósito y un rebuild no lo
        /// tiene que revertir.
        /// </para>
        /// <para>
        /// El placeholder <b>sí</b> se pisa: era el parche de "no hay arte todavía", no una decisión.
        /// </para>
        /// <para>
        /// Si el wrapper no se pudo construir se devuelve lo que ya había: un build fallido no deja al
        /// jefe sin cuerpo.
        /// </para>
        /// </remarks>
        public static GameObject ResolveVisualPrefab(
            GameObject current, GameObject wrapper, GameObject placeholder)
        {
            bool authored = current != null && current != wrapper && current != placeholder;
            if (authored) return current;

            return wrapper != null ? wrapper : current;
        }

        /// <summary>
        /// Crea (o actualiza) el hazard que representa una ficha en el piso: se dispara al pisarla,
        /// se consume, no hace daño y vive un turno.
        /// </summary>
        public static HazardDefinitionSO EnsureChipHazard()
        {
            var chip = LoadOrCreate<HazardDefinitionSO>(ChipHazardPath);

            chip.Trigger = HazardTriggerMode.OnEnter;
            // La ficha es un pickup del jugador: si la levantara un refuerzo al caminarle encima, se
            // consumiría la casilla y el jugador se quedaría sin nada que juntar.
            chip.Affects = HazardAffects.PlayerOnly;
            chip.ConsumeOnTrigger = true;
            chip.Damage = 0;
            chip.Kind = AttackKind.Environmental;
            chip.DurationRounds = ChipDurationRounds;
            chip.Shape = ThreatShape.Column; // Inerte: las fichas se activan con la overload de tiles.
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
