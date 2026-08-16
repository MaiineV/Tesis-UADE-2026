using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Bosses.Bandida;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Arma los assets de <b>La Bandida</b> (jefe de piso 1) y de su rodillo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos mitades a propósito.</b> <see cref="BuildAIRoot"/> y <see cref="PopulateEnemyData"/>
    /// son estáticas puras y no tocan el <c>AssetDatabase</c>: los tests de wiring arman el árbol en
    /// memoria y verifican orden de gates y números sin cargar un solo asset. El
    /// <see cref="MenuItem"/> es la única parte que escribe a disco.
    /// </para>
    /// <para>
    /// <b>Idempotente.</b> Correr el menú dos veces actualiza los mismos dos assets en lugar de
    /// duplicarlos — es la vía para re-aplicar un cambio de números sin re-autorar el árbol a mano.
    /// </para>
    /// </remarks>
    public static class BandidaAssetBuilder
    {
        // ======================================================================
        // Contrato de la ficha de diseño — todos los números viven acá.
        // ======================================================================

        public const string BossAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Bandida.asset";
        public const string ReelAssetPath = "Assets/Rollgeon/Enemies/ED_Obj_Rodillo.asset";

        /// <summary>
        /// El fuego que deja el rodillo roto: el <b>mismo</b> asset que el fuego de paño del
        /// Croupier (6 por terminar el turno adentro, 2 rondas, <c>OnTurnEndInTile</c>).
        /// </summary>
        /// <remarks>
        /// Reusar el asset y no clonar uno "de La Bandida" es deliberado: es la misma sustancia del
        /// mundo, y dos definiciones gemelas se desincronizan en el primer ajuste de balance. Lo
        /// construye <c>CroupierAssetBuilder</c>; si todavía no corrió, el menú avisa y el jefe queda
        /// sin fuego en vez de autorar un asset a espaldas del otro builder.
        /// </remarks>
        public const string ReelFireHazardPath = "Assets/Rollgeon/Combat/Hazards/HZ_Croupier_TableFire.asset";

        public const string BossEntityId = "boss.one_armed";
        public const string ReelEntityId = "obj.reel";

        /// <summary>
        /// Piso 1: ~7 turnos con el golpe base del piso (13-27, mediana 20). Va por encima del
        /// Croupier (120, ~6 turnos) porque su vida no es todo el presupuesto de la pelea: los
        /// tres rodillos de <see cref="ReelHp"/> aportan el resto, y como reaparecen, la palanca
        /// de duración real es <see cref="RespawnDelayPhase1"/> y no este número.
        /// </summary>
        /// <remarks>
        /// El número no cambió al corregirle el piso — estaba derivado contra una mediana de 24
        /// que era del piso 2, y contra la del piso 1 da los mismos ~7 turnos por casualidad
        /// aritmética (140/20 = 7). Lo que estaba mal era la cuenta escrita, no el balance.
        /// </remarks>
        public const int BossHp = 140;
        public const int BossAttack = 20;
        public const int BossSpeed = 4;
        public const int BossEnergy = 3;

        /// <summary>Piso 1, tipo 15-23 (mismo rango que ED_Boss_Sunken_Grand).</summary>
        public const int MinGold = 15;
        public const int MaxGold = 23;

        /// <summary>Debilidad: la mano que no alinea. La máquina paga por lo igual.</summary>
        public const string WeaknessComboId = "combo.ladder";
        public const float WeaknessMultiplier = 1.5f;

        /// <summary>Jackpot: 25 en 7×7 centrado en el jugador (Size 3 ⇒ 2·3+1).</summary>
        public const int JackpotDamage = 25;
        public const int JackpotSize = 3;

        /// <summary>
        /// Brazo: 12 de melee directo, sin marca y sin área, a quien haya cerrado el turno pegado a
        /// la máquina.
        /// </summary>
        /// <remarks>
        /// Era una marca de 3×3 sobre el jefe: avisaba un turno antes, se esquivaba con un paso y no
        /// entraba nunca. Directo, es el precio de desarmar de cerca — y como los rodillos viven en
        /// el anillo del jefe, romperlos es exactamente estar a su alcance.
        /// </remarks>
        public const int ArmDamage = 12;
        public const int ArmRange = 1;

        /// <summary>Dos rondas de cuenta antes de marcar; el mark tarda un turno más.</summary>
        public const int CountdownStart = 2;

        public const int ReelCount = 3;

        /// <summary>
        /// Vida del rodillo. La ficha pide 50-70: a 60, con el turno mediano del jugador en 42,
        /// romper uno cuesta casi un turno entero de daño.
        /// </summary>
        /// <remarks>
        /// Estuvo en 3, y a esa vida no había pelea: cualquier golpe partía cualquier rodillo, así
        /// que la decisión que define al jefe —cuál rompés y cuándo, sabiendo que el brazo cobra por
        /// estar cerca y que la casilla queda ardiendo— no existía. La cancelación del jackpot sigue
        /// siendo por daño y no por rotura (ver <c>IBandidaJackpotService</c>): con 60 de vida el
        /// caso normal es pegarle y que siga en pie.
        /// </remarks>
        public const int ReelHp = 60;

        public const int RespawnDelayPhase1 = 2;
        public const int RespawnDelayPhase2 = 1;

        public const float Phase2HpThreshold = 0.5f;
        public const int Phase2Index = 2;

        /// <summary>
        /// Techo de energía que la fila le cobra al jugador por turno. Dimensionado contra el kit
        /// del jugador, que no se toca: <c>EnergyMax</c> 4, <c>EnergyRegenBase</c> 2, y el reroll
        /// extra a 1 de energía. Cobrar 1 le come el reroll pago; cobrar 2 le empata el regen y le
        /// saca el margen sin dejarlo nunca en neto negativo, que sería un candado.
        /// </summary>
        public const int ReelTollCapPhase1 = 1;
        public const int ReelTollCapPhase2 = 2;

        // ======================================================================
        // Contrato visual — arte, retinte y retratos.
        // ======================================================================

        /// <summary>
        /// Arte del jefe. Mech humanoide con <b>tres cañones en el pecho</b> (<c>Cannon</c>,
        /// <c>Cannon_1</c>, <c>Cannon_2</c>): son los tres rodillos leídos como parte del cuerpo, que
        /// es exactamente lo que la ficha pide contar de un vistazo. Trae además el set de anims más
        /// completo del proyecto (<c>AnimCon_Mecha</c>: Idle/IdleVar/Walk/AttackMelee/AttackRange), y
        /// estaba huérfano — ningún otro enemigo lo referencia.
        /// </summary>
        public const string BossArtPrefabPath = "Assets/Prefabs/Enemies/MechaBoss_Animated.prefab";

        public const string BossVisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Bandida.prefab";

        /// <summary>Retrato del rig que viste (<c>MechaBoss_Animated</c>). Ver <see cref="BossPortraitLibrary"/>.</summary>
        public const string BossPortraitPath = BossPortraitLibrary.BandidaPath;

        /// <summary>
        /// Arte del rodillo: una tragamonedas real. Sin <c>Animator</c> ni rig — quieto es como el
        /// jugador distingue una pared que hay que romper de un enemigo que va a actuar.
        /// </summary>
        public const string ReelArtPrefabPath = "Assets/Prefabs/Props/slotv02.prefab";

        public const string ReelVisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Obj_Rodillo.prefab";

        /// <summary>Cerezas: el símbolo del rodillo, distinto del 7 del jackpot del jefe.</summary>
        public const string ReelPortraitPath = "Assets/Art/2D/Symbols/Sprites/Casino_0049.png";

        /// <summary>Los clones de material del retinte viven acá (uno por material fuente).</summary>
        public const string MaterialsFolder = BossVisualWrapperBuilder.DefaultMaterialsRoot + "/Bandida";

        /// <summary>
        /// Tope del radio del capsule del jefe, en tiles (<c>GridManager.TileSize</c> = 1).
        /// </summary>
        /// <remarks>
        /// El mech está en T-pose: los bounds del arte dan un radio de ~1.5 (manos y cañones) y
        /// <c>PawnPicker</c> resuelve el pick por collider, así que ese capsule taparía las casillas
        /// vecinas. Los rodillos se paran justo al lado del jefe y romperlos es <b>la</b> mecánica de
        /// la pelea: el collider del jefe no puede pasarse de su propia casilla.
        /// </remarks>
        public const float BossColliderRadius = 0.5f;

        /// <summary>Misma altura que el resto del roster (GeneralDirector, Healer, CardEnemy).</summary>
        public static readonly Vector3 BossHealthBarOffset = new Vector3(0f, 3f, 0f);

        /// <summary>
        /// La barra del rodillo va más abajo que la del jefe a propósito: con las cuatro a 3 de altura
        /// la fila queda una sopa de barras y no se lee cuál es la del jefe.
        /// </summary>
        public static readonly Vector3 ReelHealthBarOffset = new Vector3(0f, 2.2f, 0f);

        /// <summary>
        /// Corrección de altura del arte del rodillo. <c>slotv02</c> trae su malla en un hijo a
        /// <c>y = -0.5</c> (las salas la compensan colocando la instancia a <c>y = +1</c> sobre un
        /// GridOrigin a <c>0.5</c>). El wrapper fuerza el hijo Art a identidad, así que sin este
        /// lift la máquina queda medio tile hundida en el piso.
        /// </summary>
        public const float ReelArtYLift = 0.5f;

        // Paleta: gabinete rojo tragamonedas + herrajes dorados. Los nombres son por FUNCIÓN y no
        // por material fuente porque el retinte cruza colores (Mat_Gold pasa a ser el rojo del
        // gabinete: es el material que cubre torso, brazos y piernas, o sea la carcasa).
        public static readonly Color CabinetLight = new Color32(0xF2, 0x56, 0x4B, 0xFF);
        public static readonly Color CabinetMid = new Color32(0xC8, 0x1D, 0x2E, 0xFF);
        public static readonly Color CabinetShadow = new Color32(0x5E, 0x0A, 0x16, 0xFF);

        public static readonly Color TrimLight = new Color32(0xFF, 0xE0, 0x8A, 0xFF);
        public static readonly Color TrimMid = new Color32(0xE0, 0xA8, 0x25, 0xFF);
        public static readonly Color TrimShadow = new Color32(0x6B, 0x44, 0x10, 0xFF);

        public static readonly Color ReelFaceLight = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        public static readonly Color ReelFaceMid = new Color32(0xC9, 0xCE, 0xD9, 0xFF);
        public static readonly Color ReelFaceShadow = new Color32(0x5A, 0x62, 0x72, 0xFF);

        public static readonly Color AccentLight = new Color32(0x8C, 0x2A, 0x33, 0xFF);
        public static readonly Color AccentMid = new Color32(0x59, 0x15, 0x1F, 0xFF);
        public static readonly Color AccentShadow = new Color32(0x2A, 0x08, 0x10, 0xFF);

        // ======================================================================
        // Árbol (puro — testeable sin assets)
        // ======================================================================

        /// <summary>
        /// Árbol de La Bandida. Sin <c>Move</c> ni <c>KeepDistance</c>: está atornillada a la pared
        /// y no se mueve nunca.
        /// </summary>
        /// <remarks>
        /// <para>Orden del <c>Sequence</c> raíz:</para>
        /// <list type="number">
        /// <item>ExecuteTelegraph — cobra la marca del turno anterior (el jackpot: es la única
        /// amenaza telegrafiada que le queda al jefe).</item>
        /// <item>Gate de Fase 2 — HOLD del rodillo del medio + reposición a 1 turno.</item>
        /// <item>TickJackpot — baja el número gigante.</item>
        /// <item>Fila de rodillos — arma, detecta rotos y repone (rearmando la cuenta).</item>
        /// <item>Pool de acción — jackpot XOR brazo XOR nada.</item>
        /// </list>
        /// <para>
        /// El gate de fase va ANTES del pool (convención del proyecto, fijada por
        /// <c>SunkenGrandPhaseWiringTests</c>): en el path no-coroutine un <c>Running</c> del ataque
        /// abortaría la secuencia y la fase no tickearía. Los dos hijos que pueden devolver
        /// <c>Failed</c> (gate de fase y fila de rodillos) van envueltos en
        /// <c>Selector[nodo, Wait]</c> para que su fallo no le cancele el turno al jefe.
        /// </para>
        /// <para>
        /// <b>TickJackpot va antes de la fila</b> y no después: la reposición rearma la cuenta en 2,
        /// y con el tick posterior el jugador vería un 1 el turno en que el rodillo vuelve — se
        /// comería una de las dos rondas de aviso que compró rompiéndolo.
        /// </para>
        /// <para>
        /// El <c>Selector</c> del pool es lo que garantiza que el jefe no marque el jackpot y pegue
        /// con el brazo en el mismo turno: una decisión por turno se lee mejor. Lo que sí puede
        /// sumarse es el jackpot que <b>cobra</b> (marcado el turno anterior) más el brazo de este,
        /// y es a propósito: 25 + 12 es lo que cuesta quedarse pegado a la máquina ignorando la
        /// cuenta, y las dos mitades están cada una por debajo del techo por golpe del piso.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(EnemyDataSO reelData, HazardDefinitionSO reelFire = null)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    new AINode_ExecuteTelegraph(),
                    IsolateFailure(BuildPhaseTwoGate()),
                    new AINode_TickJackpot(),

                    // El peaje va DESPUÉS del tick y ANTES de la reposición: cobra por la fila que
                    // el jugador dejó en pie durante su turno, no por los rodillos que la máquina
                    // está por reponer en este mismo paso. Cobrar por un rodillo que todavía no
                    // existe sería un peaje que el jugador no pudo evitar.
                    IsolateFailure(BuildReelToll()),

                    IsolateFailure(BuildReelRow(reelData, reelFire)),
                    BuildActionPool(),
                },
            };
        }

        /// <summary>
        /// El peaje de la fila, con su techo por fase. Es una rama persistente y no un
        /// <c>AINode_Once</c>: el cobro pasa todos los turnos, lo que cambia con la fase es cuánto.
        /// </summary>
        /// <remarks>
        /// Fase 1 cobra 1 y fase 2 cobra 2, contra un regen de <c>EnergyRegenBase</c> = 2. El techo
        /// de fase 2 empata el regen a propósito: el jugador deja de acumular margen, pero nunca
        /// entra en energía neta negativa. Ver los remarks de <see cref="AINode_BandidaReelToll"/>.
        /// </remarks>
        private static AINode_If BuildReelToll()
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcOwnerHpBelow { Percent = Phase2HpThreshold },
                },
                Then = new AINode_BandidaReelToll { Cap = ReelTollCapPhase2 },
                Else = new AINode_BandidaReelToll { Cap = ReelTollCapPhase1 },
            };
        }

        /// <summary>
        /// Gate de Fase 2 (50% HP): traba el rodillo del medio y baja la reposición a un turno.
        /// Ningún número de daño cambia — el jackpot sigue en 25 y el brazo en 12. Cambia la
        /// frecuencia y la distancia.
        /// </summary>
        private static AINode_If BuildPhaseTwoGate()
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcOwnerHpBelow { Percent = Phase2HpThreshold },
                },
                Then = new AINode_Once
                {
                    Child = new AINode_Sequence
                    {
                        Children = new List<AIDecisionNode>
                        {
                            new AINode_LockReel { Side = ReelSide.Middle },
                            new AINode_SetReelRespawnDelay { Value = RespawnDelayPhase2 },
                            new AINode_ApplyStatModifier
                            {
                                AttackDelta = 0,
                                SpeedDelta = 0,
                                PhaseIndex = Phase2Index,
                                EmitPhaseChangedEvent = true,
                            },
                        },
                    },
                },
            };
        }

        /// <summary>
        /// La fila de rodillos. <b>Sin <c>Once</c></b>: el nodo se auto-gatea pero necesita tickear
        /// cada turno para correr los relojes de reposición.
        /// </summary>
        /// <remarks>
        /// <paramref name="reelFire"/> es opcional para que los tests de wiring armen el árbol sin
        /// cargar assets; el menú siempre lo pasa. Sin él el rodillo roto deja piso limpio y la
        /// casilla desde la que se desarma el siguiente sale gratis.
        /// </remarks>
        private static AINode_SpawnReels BuildReelRow(EnemyDataSO reelData, HazardDefinitionSO reelFire)
        {
            return new AINode_SpawnReels
            {
                ReelData = reelData,
                OnBreakHazard = reelFire,
                Count = ReelCount,
                RespawnDelayTurns = RespawnDelayPhase1,
                CountdownOnRespawn = CountdownStart,
                Direction = AINode_SpawnReels.RowDirection.Auto,
            };
        }

        /// <summary>
        /// Pool de acción: jackpot si la cuenta llegó a 0, brazo si el jugador está pegado, y si no,
        /// nada. El <c>Wait</c> final es obligatorio — sin él el Selector devuelve <c>Failed</c> y
        /// aborta el turno.
        /// </summary>
        /// <remarks>
        /// El brazo va gateado por <c>PcTargetInRange</c> aunque <c>AINode_BandidaArm</c> también
        /// mida la distancia: la condición queda declarada en el árbol —visible y editable desde el
        /// editor de árboles— en vez de escondida adentro del nodo. La medición del nodo es la red:
        /// gate y nodo tienen que compartir métrica, si no una de las dos mitades miente sobre las
        /// diagonales.
        /// </remarks>
        private static AINode_Selector BuildActionPool()
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode>
                {
                    new AINode_If
                    {
                        Conditions = new List<BasePreCondition>
                        {
                            new PcJackpotCountdown
                            {
                                Comparison = IntComparison.Equal,
                                Value = 0,
                                RequireCounting = true,
                            },
                        },
                        Then = new AINode_Sequence
                        {
                            Children = new List<AIDecisionNode>
                            {
                                new AINode_TelegraphMark
                                {
                                    Shape = ThreatShape.SquareAroundPlayer,
                                    Size = JackpotSize,
                                    Damage = JackpotDamage,
                                },
                                // Rearme en el acto: la ronda muerta la cobra solo quien rompe un
                                // rodillo. Tanquear el jackpot no compra pausa.
                                new AINode_ResetJackpotCountdown { Value = CountdownStart },
                            },
                        },
                    },
                    new AINode_If
                    {
                        Conditions = new List<BasePreCondition>
                        {
                            // Chebyshev en los dos lados: el brazo alcanza las diagonales, que es
                            // por donde se llega a los rodillos de las puntas.
                            new PcTargetInRange { Range = ArmRange, Metric = DistanceMetric.Chebyshev },
                        },
                        Then = new AINode_BandidaArm
                        {
                            Damage = ArmDamage,
                            Range = ArmRange,
                            Metric = DistanceMetric.Chebyshev,
                        },
                    },
                    new AINode_Wait(),
                },
            };
        }

        /// <summary>
        /// Envuelve en <c>Selector[nodo, Wait]</c> — el idiom del proyecto para que un hijo del
        /// <c>Sequence</c> raíz que puede devolver <c>Failed</c> no le cancele el turno al jefe.
        /// </summary>
        private static AINode_Selector IsolateFailure(AIDecisionNode node)
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode> { node, new AINode_Wait() },
            };
        }

        // ======================================================================
        // Populate (puro — testeable sin assets)
        // ======================================================================

        /// <remarks>
        /// <paramref name="visualPrefab"/>, <paramref name="portrait"/> y <paramref name="reelFire"/>
        /// son opcionales para que los tests de wiring puedan verificar números sin cargar assets; el
        /// menú siempre los pasa.
        /// </remarks>
        public static void PopulateEnemyData(EnemyDataSO boss, EnemyDataSO reelData,
            GameObject visualPrefab, Sprite portrait = null, HazardDefinitionSO reelFire = null)
        {
            if (boss == null) return;

            boss.EntityId = BossEntityId;
            boss.DisplayName = "La Bandida";
            boss.Description =
                "A three-reel slot machine bolted to the wall. It never chases you: it counts to " +
                "the jackpot. Break any reel to cancel the count.";

            boss.BaseHP = BossHp;
            boss.BaseAttack = BossAttack;
            boss.BaseSpeed = BossSpeed;
            boss.MaxEnergy = BossEnergy;
            boss.BaseHealStrength = 0;
            boss.BaseAttackRange = ArmRange;

            boss.WeaknessComboId = WeaknessComboId;
            boss.WeaknessMultiplierOverride = WeaknessMultiplier;

            boss.MinGoldDrop = MinGold;
            boss.MaxGoldDrop = MaxGold;

            if (visualPrefab != null) boss.VisualPrefab = visualPrefab;
            if (portrait != null) boss.Portrait = portrait;

            boss.AIRoot = BuildAIRoot(reelData, reelFire);
        }

        /// <summary>
        /// El rodillo: pared de <see cref="ReelHp"/> de vida que no actúa. Su árbol es un
        /// <c>Wait</c> — está en la cola de turnos solo para que la limpieza de fin de combate lo
        /// levante junto con el resto.
        /// </summary>
        public static void PopulateReelData(EnemyDataSO reel, GameObject visualPrefab,
            Sprite portrait = null)
        {
            if (reel == null) return;

            reel.EntityId = ReelEntityId;
            reel.DisplayName = "Reel";
            reel.Description =
                "One of La Bandida's three reels: a wall bolted in a row against hers. Any hit " +
                "cancels the jackpot count, but breaking one costs most of a turn — and the tile " +
                "it leaves behind burns.";

            reel.BaseHP = ReelHp;
            reel.BaseAttack = 0;
            reel.BaseSpeed = 1;
            reel.MaxEnergy = 1;
            reel.BaseHealStrength = 0;
            reel.BaseAttackRange = 1;

            reel.WeaknessComboId = string.Empty;
            reel.WeaknessMultiplierOverride = 0f;

            // Pregunta abierta de la ficha (¿romper un rodillo devuelve algo?): hasta que se
            // conteste, romperlo cuesta tempo y no paga oro.
            reel.MinGoldDrop = 0;
            reel.MaxGoldDrop = 0;

            if (visualPrefab != null) reel.VisualPrefab = visualPrefab;

            // El retrato del rodillo también alimenta la cola de turnos: AINode_SpawnReels registra
            // ReelData.Portrait en el IEntityPortraitResolver al reponer cada rodillo.
            if (portrait != null) reel.Portrait = portrait;

            reel.AIRoot = new AINode_Wait();
        }

        // ======================================================================
        // Specs de wrapper (puras — el test las arma y las redirige a una carpeta temporal)
        // ======================================================================

        /// <summary>
        /// Ficha del wrapper del jefe: el mech retintado a gabinete rojo con herrajes dorados.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Por qué colores directos y no <c>PaletteSlot</c></b>: los labels guardados en
        /// <c>PA_MainPalette.asset</c> están desalineados respecto de la tabla de
        /// <see cref="PaletteSlots"/>, así que pedir "slot Red" no garantiza rojo. Con
        /// <see cref="MaterialRetint.FromColors"/> el color queda escrito en el material y no depende
        /// de un asset que alguien editó a mano.
        /// </para>
        /// <para>
        /// <b>El cruce de nombres es a propósito.</b> El material que cubre torso, brazos y piernas
        /// del mech es <c>Mat_Gold</c> (10 slots): esa es la carcasa, y la carcasa de una tragamonedas
        /// es roja. El dorado se reserva para <c>Mat_DarkGray</c>, que son los herrajes/articulaciones
        /// más <c>Cannon_2</c>. <c>Mat_Gray</c> (sólo <c>Cannon_1</c>) se va a blanco cromo para que el
        /// cañón del medio lea como el vidrio del rodillo. <c>Mat_White</c> queda <b>sin retintar</b>:
        /// es el punto de luz del torso y sobre gabinete rojo hace de "777" iluminado.
        /// </para>
        /// </remarks>
        public static BossWrapperSpec BuildBossWrapperSpec(
            string outputPrefabPath = BossVisualPrefabPath,
            string materialsFolder = MaterialsFolder)
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = BossArtPrefabPath,
                OutputPrefabPath = outputPrefabPath,
                BossName = "Bandida",
                MaterialsFolder = materialsFolder,
                Collider = ColliderKind.Capsule,
                AddHealthBar = true,
                HealthBarOffset = BossHealthBarOffset,
                Retints = new Dictionary<string, MaterialRetint>
                {
                    { "Mat_Gold", MaterialRetint.FromColors(CabinetLight, CabinetMid, CabinetShadow) },
                    { "Mat_DarkGray", MaterialRetint.FromColors(TrimLight, TrimMid, TrimShadow) },
                    { "Mat_Gray", MaterialRetint.FromColors(ReelFaceLight, ReelFaceMid, ReelFaceShadow) },
                    { "Mat_Brown", MaterialRetint.FromColors(AccentLight, AccentMid, AccentShadow) },
                },
            };
        }

        /// <summary>
        /// Ficha del wrapper del rodillo. <b>Sin retinte</b>: el prop ya es una tragamonedas autorada
        /// con ocho materiales por submalla y desde el YAML no hay forma de saber cuál es el gabinete
        /// y cuál la palanca — retintar a ciegas repinta la pieza equivocada. Queda como pendiente de
        /// una pasada con el editor abierto.
        /// </summary>
        /// <remarks>
        /// Collider <see cref="ColliderKind.Box"/> y no capsule: la máquina es una caja, y el pick de
        /// un blanco al que hay que meterle varios turnos de daño tiene que cubrir la silueta entera.
        /// </remarks>
        public static BossWrapperSpec BuildReelWrapperSpec(
            string outputPrefabPath = ReelVisualPrefabPath,
            string materialsFolder = MaterialsFolder)
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = ReelArtPrefabPath,
                OutputPrefabPath = outputPrefabPath,
                BossName = "Rodillo",
                MaterialsFolder = materialsFolder,
                Collider = ColliderKind.Box,
                AddHealthBar = true,
                HealthBarOffset = ReelHealthBarOffset,
            };
        }

        // ======================================================================
        // Wrappers (AssetDatabase)
        // ======================================================================

        /// <summary>
        /// Construye <see cref="BossVisualPrefabPath"/> y lo devuelve. Idempotente: el wrapper se
        /// reescribe sobre el mismo path (GUID estable) y el ajuste de collider se re-aplica con
        /// valores absolutos, así que dos corridas dan el mismo prefab.
        /// </summary>
        public static GameObject BuildBossVisual(
            string outputPrefabPath = BossVisualPrefabPath,
            string materialsFolder = MaterialsFolder)
        {
            var spec = BuildBossWrapperSpec(outputPrefabPath, materialsFolder);
            if (BossVisualWrapperBuilder.BuildWrapper(spec) == null) return null;

            EditPrefab(outputPrefabPath, root =>
            {
                var capsule = root.GetComponent<CapsuleCollider>();
                if (capsule == null) return;
                if (capsule.radius > BossColliderRadius) capsule.radius = BossColliderRadius;
            });

            return AssetDatabase.LoadAssetAtPath<GameObject>(outputPrefabPath);
        }

        /// <summary>Construye <see cref="ReelVisualPrefabPath"/> y lo devuelve.</summary>
        public static GameObject BuildReelVisual(
            string outputPrefabPath = ReelVisualPrefabPath,
            string materialsFolder = MaterialsFolder)
        {
            var spec = BuildReelWrapperSpec(outputPrefabPath, materialsFolder);
            if (BossVisualWrapperBuilder.BuildWrapper(spec) == null) return null;

            EditPrefab(outputPrefabPath, root =>
            {
                var art = root.transform.Find("Art");
                if (art != null) art.localPosition = new Vector3(0f, ReelArtYLift, 0f);

                // El box lo dimensionó el wrapper con el arte todavía en el origen: hay que subirlo
                // lo mismo que el arte o el pick queda medio tile por debajo de la máquina.
                var box = root.GetComponent<BoxCollider>();
                if (box != null) box.center += new Vector3(0f, ReelArtYLift, 0f);
            });

            return AssetDatabase.LoadAssetAtPath<GameObject>(outputPrefabPath);
        }

        // ======================================================================
        // MenuItem (la única parte que toca el AssetDatabase)
        // ======================================================================

        [MenuItem("Tools/Rollgeon/Bosses/Build Bandida")]
        public static void BuildBandida()
        {
            var bossVisual = BuildBossVisual();
            var reelVisual = BuildReelVisual();

            // El rodillo se queda con el símbolo de cerezas: es un objeto de la sala, no un
            // personaje, y darle cara de jefe lo haría leer como un segundo enemigo con turno
            // propio. Los símbolos entran al repo como textura Default, de ahí el EnsureSpriteImport.
            var bossPortrait = BossPortraitLibrary.Bandida();
            var reelPortrait = SpriteImportUtility.EnsureSpriteImport(ReelPortraitPath);

            var reelFire = LoadReelFire();

            var reel = LoadOrCreate(ReelAssetPath);
            PopulateReelData(reel, reelVisual, reelPortrait);
            EditorUtility.SetDirty(reel);

            var boss = LoadOrCreate(BossAssetPath);
            PopulateEnemyData(boss, reel, bossVisual, bossPortrait, reelFire);
            EditorUtility.SetDirty(boss);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BandidaAssetBuilder] Listo: '{BossAssetPath}' + '{ReelAssetPath}' con " +
                      $"'{BossVisualPrefabPath}' + '{ReelVisualPrefabPath}'. " +
                      "Falta a mano: la UI del número gigante " +
                      "(TypedEvent<JackpotCountdownPayload>) y el alta del jefe en el " +
                      "BossFloorManagerSO de su piso.");
        }

        /// <summary>
        /// El fuego del rodillo roto, tomado del asset del Croupier. Devuelve <c>null</c> con un
        /// aviso si ese builder todavía no corrió: preferimos un jefe sin fuego —y un log que dice
        /// exactamente qué correr— antes que autorar acá una copia del hazard de otro jefe.
        /// </summary>
        private static HazardDefinitionSO LoadReelFire()
        {
            var fire = AssetDatabase.LoadAssetAtPath<HazardDefinitionSO>(ReelFireHazardPath);
            if (fire != null) return fire;

            Debug.LogWarning($"[BandidaAssetBuilder] No está '{ReelFireHazardPath}': el rodillo roto " +
                             "va a dejar piso limpio. Corré Tools/Rollgeon/Bosses/Build Croupier y " +
                             "volvé a correr este menú.");
            return null;
        }

        /// <summary>
        /// Abre el prefab guardado, le aplica <paramref name="edit"/> y lo vuelve a guardar sobre el
        /// mismo path (GUID estable).
        /// </summary>
        /// <remarks>
        /// Existe porque <see cref="BossVisualWrapperBuilder"/> es fundación compartida por los seis
        /// jefes: los ajustes que sólo valen para La Bandida (el capsule que no puede tapar la fila de
        /// rodillos, el lift del arte del prop) se hacen acá y no agregándole campos al spec común.
        /// </remarks>
        private static void EditPrefab(string prefabPath, System.Action<GameObject> edit)
        {
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            if (contents == null)
            {
                Debug.LogWarning($"[BandidaAssetBuilder] No se pudo abrir '{prefabPath}' para el " +
                                 $"post-proceso — queda con los valores del wrapper genérico.");
                return;
            }

            try
            {
                edit(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static EnemyDataSO LoadOrCreate(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path));
            var created = ScriptableObject.CreateInstance<EnemyDataSO>();
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
