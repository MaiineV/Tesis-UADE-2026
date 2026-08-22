using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Bosses.Bandida;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Threat;
using Rollgeon.Entities;
using Rollgeon.Feedback;
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
    /// <see cref="BuildAIRoot"/> y <see cref="PopulateEnemyData"/> son puras y se testean sin cargar
    /// assets; el <see cref="MenuItem"/> es lo único que escribe a disco, y es idempotente.
    /// </remarks>
    public static class BandidaAssetBuilder
    {
        // ======================================================================
        // Contrato de la ficha de diseño — todos los números viven acá.
        // ======================================================================

        public const string BossAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Bandida.asset";
        public const string ReelAssetPath = "Assets/Rollgeon/Enemies/ED_Obj_Rodillo.asset";

        /// <summary>
        /// El fuego del rodillo roto: el <b>mismo</b> asset que el fuego de paño del Croupier. Lo
        /// construye <c>CroupierAssetBuilder</c>; si no corrió, el menú avisa y el jefe queda sin
        /// fuego — un gemelo propio se desincronizaría en el primer ajuste de balance.
        /// </summary>
        public const string ReelFireHazardPath = "Assets/Rollgeon/Combat/Hazards/HZ_Croupier_TableFire.asset";

        public const string BossEntityId = "boss.one_armed";
        public const string ReelEntityId = "obj.reel";

        /// <summary>Vida del jefe de piso 1.</summary>
        public const int BossHp = 120;
        public const int BossAttack = 20;
        public const int BossSpeed = 4;
        public const int BossEnergy = 3;

        /// <summary>Drop de oro de piso 1.</summary>
        public const int MinGold = 15;
        public const int MaxGold = 23;

        /// <summary>Debilidad: la mano que no alinea. La máquina paga por lo igual.</summary>
        public const string WeaknessComboId = "combo.ladder";
        public const float WeaknessMultiplier = 1.5f;

        /// <summary>Jackpot: 25 en 7×7 centrado en el jugador (Size 3 ⇒ 2·3+1).</summary>
        public const int JackpotDamage = 25;
        public const int JackpotSize = 3;

        /// <summary>
        /// Brazo: melee directo, sin marca ni área, a quien cerró el turno pegado a la máquina.
        /// </summary>
        public const int ArmDamage = 12;
        public const int ArmRange = 1;

        /// <summary>Dos rondas de cuenta antes de marcar; el mark tarda un turno más.</summary>
        public const int CountdownStart = 2;

        public const int ReelCount = 3;

        /// <summary>
        /// Vida del rodillo. La cancelación del jackpot es por daño y no por rotura (ver
        /// <c>IBandidaJackpotService</c>).
        /// </summary>
        public const int ReelHp = 60;

        public const int RespawnDelayPhase1 = 2;
        public const int RespawnDelayPhase2 = 1;

        public const float Phase2HpThreshold = 0.5f;
        public const int Phase2Index = 2;

        /// <summary>Desde qué vida del jefe la fila cobra peaje.</summary>
        public const float ReelTollHpThreshold = 0.7f;

        /// <summary>Techo de rolls que la fila drena del pool por turno.</summary>
        public const int ReelTollCapPhase1 = 1;
        public const int ReelTollCapPhase2 = 2;

        // ======================================================================
        // Contrato visual — arte, retinte y retratos.
        // ======================================================================

        /// <summary>
        /// Mech humanoide con tres cañones en el pecho: los tres rodillos leídos como parte del
        /// cuerpo. Animado por <c>AnimCon_Mecha</c>.
        /// </summary>
        public const string BossArtPrefabPath = "Assets/Prefabs/Enemies/MechaBoss_Animated.prefab";

        public const string BossVisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Bandida.prefab";

        /// <summary>Retrato del rig que viste (<c>MechaBoss_Animated</c>). Ver <see cref="BossPortraitLibrary"/>.</summary>
        public const string BossPortraitPath = BossPortraitLibrary.BandidaPath;

        /// <summary>
        /// Arte del rodillo. Sin <c>Animator</c> ni rig: quieto es como el jugador distingue una
        /// pared de un enemigo que va a actuar.
        /// </summary>
        public const string ReelArtPrefabPath = "Assets/Prefabs/Props/slotv02.prefab";

        public const string ReelVisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Obj_Rodillo.prefab";

        /// <summary>Cerezas: el símbolo del rodillo, distinto del 7 del jackpot del jefe.</summary>
        public const string ReelPortraitPath = "Assets/Art/2D/Symbols/Sprites/Casino_0049.png";

        /// <summary>Los clones de material del retinte viven acá (uno por material fuente).</summary>
        public const string MaterialsFolder = BossVisualWrapperBuilder.DefaultMaterialsRoot + "/Bandida";

        /// <summary>
        /// Tope del radio del capsule, en tiles. El mech está en T-pose y sus bounds dan ~1.5, que
        /// taparía las casillas vecinas — y romper los rodillos pegados a él es <b>la</b> mecánica
        /// de la pelea, así que su collider no puede pasarse de su propia casilla.
        /// </summary>
        public const float BossColliderRadius = 0.5f;

        /// <summary>Misma altura que el resto del roster (GeneralDirector, Healer, CardEnemy).</summary>
        public static readonly Vector3 BossHealthBarOffset = new Vector3(0f, 3f, 0f);

        /// <summary>
        /// Más abajo que la del jefe: con las cuatro barras a la misma altura no se lee cuál es cuál.
        /// </summary>
        public static readonly Vector3 ReelHealthBarOffset = new Vector3(0f, 2.2f, 0f);

        /// <summary>
        /// <c>slotv02</c> trae su malla en un hijo a <c>y = -0.5</c> y el wrapper fuerza el hijo Art
        /// a identidad: sin este lift la máquina queda medio tile hundida en el piso.
        /// </summary>
        public const float ReelArtYLift = 0.5f;

        // Paleta: gabinete rojo + herrajes dorados. Nombres por FUNCIÓN y no por material fuente
        // porque el retinte cruza colores (Mat_Gold, que cubre la carcasa, pasa a ser el rojo).
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

        /// <summary>Sin <c>Move</c> ni <c>KeepDistance</c>: está atornillada a la pared.</summary>
        /// <remarks>
        /// Orden del <c>Sequence</c> raíz: telegraph → gate de fase → tick del jackpot → peaje →
        /// fila de rodillos → pool de acción. El gate de fase va antes del pool porque un
        /// <c>Running</c> del ataque abortaría la secuencia; los hijos que pueden devolver
        /// <c>Failed</c> van en <c>Selector[nodo, Wait]</c>. TickJackpot va antes de la fila: la
        /// reposición rearma la cuenta, y tickear después le comería al jugador una de las rondas de
        /// aviso que compró rompiendo el rodillo.
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(EnemyDataSO reelData, HazardDefinitionSO reelFire = null)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // La marca de la ronda pasada cobra acá, con el gesto de rango: cae sobre tiles
                    // lejos de ella, no sobre quien tenga pegado.
                    new AINode_ExecuteTelegraph { WindupFeedbackId = BossFeedbackIds.BandidaRangeAnim },
                    IsolateFailure(BuildPhaseTwoGate()),
                    new AINode_TickJackpot(),

                    // DESPUÉS del tick y ANTES de la reposición: cobra por la fila que el jugador
                    // dejó en pie, no por rodillos que todavía no existen y no pudo evitar.
                    IsolateFailure(BuildReelToll()),

                    IsolateFailure(BuildReelRow(reelData, reelFire)),
                    BuildActionPool(),
                },
            };
        }

        /// <summary>
        /// El peaje de la fila: dos gates anidados, uno que lo enciende y otro que lo endurece.
        /// Persistentes y no <c>AINode_Once</c> — el cobro pasa todos los turnos.
        /// </summary>
        private static AINode_If BuildReelToll()
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcOwnerHpBelow { Percent = ReelTollHpThreshold },
                },
                Then = new AINode_If
                {
                    Conditions = new List<BasePreCondition>
                    {
                        new PcOwnerHpBelow { Percent = Phase2HpThreshold },
                    },
                    Then = new AINode_BandidaReelToll { Cap = ReelTollCapPhase2 },
                    Else = new AINode_BandidaReelToll { Cap = ReelTollCapPhase1 },
                },
                Else = new AINode_Wait(),
            };
        }

        /// <summary>
        /// Fase 2 (50% HP): traba el rodillo del medio y baja la reposición a un turno. Ningún
        /// número de daño cambia — sólo la frecuencia y la distancia.
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
        /// La fila. <b>Sin <c>Once</c></b>: el nodo se auto-gatea pero necesita tickear cada turno
        /// para correr los relojes de reposición.
        /// </summary>
        /// <remarks>Sin <paramref name="reelFire"/> el rodillo roto deja piso limpio.</remarks>
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
        /// Jackpot si la cuenta llegó a 0, brazo si el jugador está pegado, y si no, nada. El
        /// <c>Wait</c> final es obligatorio: sin él el Selector devuelve <c>Failed</c> y aborta.
        /// </summary>
        /// <remarks>
        /// El gate <c>PcTargetInRange</c> y la medición interna de <c>AINode_BandidaArm</c> tienen que
        /// compartir métrica, si no una de las dos mitades miente sobre las diagonales.
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
                                // Rearme en el acto: tanquear el jackpot no compra pausa.
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

        /// <summary><c>Selector[nodo, Wait]</c>: su <c>Failed</c> no le cancela el turno al jefe.</summary>
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

        /// <remarks>Los assets son opcionales para que los tests corran sin cargarlos.</remarks>
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
        /// El rodillo: pared que no actúa. Está en la cola de turnos sólo para que la limpieza de
        /// fin de combate lo levante con el resto.
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

            reel.MinGoldDrop = 0;
            reel.MaxGoldDrop = 0;

            if (visualPrefab != null) reel.VisualPrefab = visualPrefab;

            // También alimenta la cola de turnos: AINode_SpawnReels registra ReelData.Portrait en el
            // IEntityPortraitResolver al reponer cada rodillo.
            if (portrait != null) reel.Portrait = portrait;

            reel.AIRoot = new AINode_Wait();
        }

        // ======================================================================
        // Specs de wrapper (puras — el test las arma y las redirige a una carpeta temporal)
        // ======================================================================

        /// <summary>Ficha del wrapper: el mech a gabinete rojo con herrajes dorados.</summary>
        /// <remarks>
        /// Colores directos y no <c>PaletteSlot</c>: los labels de <c>PA_MainPalette.asset</c> están
        /// desalineados y pedir "slot Red" no garantiza rojo. El cruce de nombres es a propósito —
        /// <c>Mat_Gold</c> cubre la carcasa, que va roja.
        /// </remarks>
        public static BossWrapperSpec BuildBossWrapperSpec(
            string outputPrefabPath = BossVisualPrefabPath,
            string materialsFolder = MaterialsFolder)
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = BossArtPrefabPath,
                OutputPrefabPath = outputPrefabPath,
                EntityId = BossEntityId,
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

        /// <summary>Ficha del wrapper del rodillo, sin retinte.</summary>
        /// <remarks>Box y no capsule: la máquina es una caja y el pick cubre la silueta entera.</remarks>
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
        /// Construye <see cref="BossVisualPrefabPath"/>. Idempotente: mismo path (GUID estable) y el
        /// ajuste de collider se re-aplica con valores absolutos.
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

            // Los símbolos entran al repo como textura Default, de ahí el EnsureSpriteImport.
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
                      $"jackpot {JackpotDamage} y {ReelCount} rodillos de {ReelHp} HP.");
        }

        /// <summary>El fuego del Croupier. <c>null</c> con aviso si ese builder no corrió.</summary>
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
        /// Abre el prefab, le aplica <paramref name="edit"/> y lo reescribe sobre el mismo path. Los
        /// ajustes que sólo valen para La Bandida van acá y no como campos del spec compartido.
        /// </summary>
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
