using System.Collections.Generic;
using Rollgeon.Combat.AI.Bosses.Tahur;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Entities;
using Rollgeon.Feedback;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Autorea <c>ED_Boss_Tahur.asset</c> — El Tahúr, jefe del piso 3. Números de la ficha de
    /// diseño v2 (calibrada por simulación el 12/08).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos capas a propósito.</b> <see cref="BuildAIRoot"/> y <see cref="PopulateEnemyData"/>
    /// son estáticos puros: no tocan <c>AssetDatabase</c>, así que los tests EditMode validan el
    /// wiring en memoria sin depender de que el asset exista ni de un merge que lo pise.
    /// <see cref="BuildTahurAsset"/> es la capa de assets, idempotente: crea el <c>.asset</c> si
    /// falta y lo actualiza si ya está.
    /// </para>
    /// <para>
    /// <b>Ojo con BaseHP.</b> El Tahúr son 650 — el HP más alto del juego. El <c>[Range]</c> de
    /// <c>EnemyDataSO.BaseHP</c> se ensanchó a 1000 justamente por él: con el tope viejo el valor se
    /// escribía bien por código pero el Inspector lo clampeaba al primer roce del slider.
    /// </para>
    /// <para>
    /// <b>Tres capas desde el vestido visual.</b> A las dos de arriba se suma
    /// <see cref="BuildVisualPrefab"/>, que arma <c>PF_Boss_Tahur.prefab</c> sobre el arte del
    /// Sunken Grand vía <see cref="BossVisualWrapperBuilder"/>. <see cref="BuildWrapperSpec"/> y
    /// <see cref="BuildRetints"/> quedan públicos y puros para que los tests validen la paleta sin
    /// escribir el prefab real.
    /// </para>
    /// </remarks>
    public static class TahurAssetBuilder
    {
        // -----------------------------------------------------------------
        // Identidad + stats (ficha v2)
        // -----------------------------------------------------------------

        public const string AssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Tahur.asset";

        /// <summary>
        /// Arte del jefe. <b>Compartido con el Sunken Grand del piso 1</b>, que sigue vivo en el pool
        /// con su propio wrapper (<c>SunkedGrand.prefab</c>) sobre este mismo prefab: el humanoide con
        /// abanico de 12 cartas es el tramposo de cartas que pide la ficha del Tahúr, y lo único que
        /// los separa es el retinte de <see cref="BuildRetints"/>.
        /// </summary>
        public const string ArtPrefabPath = "Assets/Prefabs/Enemies/SunkedGrand_Animated.prefab";

        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Tahur.prefab";

        /// <summary>Retrato del rig que viste (<c>SunkedGrand_Animated</c>). Ver <see cref="BossPortraitLibrary"/>.</summary>
        public const string PortraitTexturePath = BossPortraitLibrary.SheetPath;

        public const string BossName = "Tahur";
        public const string MaterialsFolder = "Assets/Rollgeon/Enemies/Materials/Tahur";

        /// <summary>
        /// Altura de la barra de vida. El arte mide ~1,81 de alto (el collider a mano de
        /// <c>SunkedGrand.prefab</c> lo confirma), así que el 3 del default de la utility —
        /// dimensionado para el GeneralDirector, más alto — dejaría la barra flotando despegada.
        /// </summary>
        public const float HealthBarHeight = 2.4f;

        public const string EntityId = "boss.tahur";
        public const string DisplayName = "El Tahúr";

        /// <summary>
        /// Piso 3: ~8 turnos con el golpe base del piso (mediana 30) — el pozo entero se
        /// juega en esa ventana. Mismo presupuesto que la Generala: es el otro jefe del
        /// piso, no uno más largo.
        /// </summary>
        public const int BaseHP = 240;
        public const int BaseAttack = 40;
        public const int BaseSpeed = 4;
        public const int MaxEnergy = 3;
        public const int MinGoldDrop = 60;
        public const int MaxGoldDrop = 80;

        // -----------------------------------------------------------------
        // Números del pozo y del turno (ficha v2)
        // -----------------------------------------------------------------

        /// <summary>Castigo por cantidad de fichas. La última entrada es el techo del piso 3.</summary>
        public static readonly int[] PotDamageTable = { 26, 32, 38, 42, 45 };

        /// <summary>Techo duro de daño por golpe del piso 3.</summary>
        public const int DamageCeiling = 45;

        public const int PayoutPerChip = 12;
        public const int MaxChips = 5;
        public const int PokeDamage = 12;
        public const int TableSize = 1;
        public const int MoveSteps = 3;
        public const int DesiredRange = 1;

        /// <summary>
        /// El rastrillo: fichas que el pozo sube por ronda, solo, <b>desde la fase 1</b>. Es lo que
        /// le pone reloj a no jugar — sin él el Castigo se quedaba en 26 mientras el jugador
        /// esquivara, y renunciar al pozo era una postura estable.
        /// </summary>
        public const int RakeChipsPerRound = 1;

        /// <summary>Daño de La Banca — el techo del piso 3, igual que el Castigo con el pozo lleno.</summary>
        public const int BancaDamage = DamageCeiling;

        /// <summary>Fichas con las que La Banca barre la mesa: el pozo lleno.</summary>
        public const int BancaChipsThreshold = MaxChips;

        /// <summary>Umbral de HP del volteo de la carta (PIDE → LEE).</summary>
        public const float Phase2HpThreshold = 0.40f;

        // -----------------------------------------------------------------
        // Árbol
        // -----------------------------------------------------------------

        /// <summary>
        /// El turno del Tahúr, en orden: cobra el Castigo marcado, voltea la carta si toca,
        /// liquida y marca, poke si la ronda quedó limpia, canta, se acerca, pone la mesa y —con el
        /// pozo lleno— barre la sala con La Banca.
        /// </summary>
        /// <remarks>
        /// Todo lo que puede fallar va en <c>Selector[nodo, Wait]</c>: en el path no-coroutine un
        /// <c>Failed</c> aborta el Sequence y le come al jefe el resto del turno (el patrón
        /// obligatorio de fases, ver <c>SunkenGrandPhaseWiringTests</c>). Y los gates de fase van
        /// ANTES de la acción para que tickeen en los dos paths.
        /// </remarks>
        public static AINode_Sequence BuildAIRoot()
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // 1 · Cobra: detona el Castigo marcado la ronda pasada. El Castigo cae sobre
                    //     tiles lejos suyo, así que el gesto es el de rango.
                    new AINode_ExecuteTelegraph { WindupFeedbackId = BossFeedbackIds.TahurRangeAnim },

                    // 2 · Se voltea la carta (40% de HP) — one-shot, antes de liquidar.
                    Isolate(new AINode_If
                    {
                        TargetSelector = new TargetSelector_AlwaysPlayer(),
                        Conditions = new List<BasePreCondition>
                        {
                            new PcOwnerHpBelow { Percent = Phase2HpThreshold },
                        },
                        Then = new AINode_Once { Child = BuildFlipCard() },
                    }),

                    // 3 · Liquida y mueve el pozo. Marca el Castigo, salvo que cobre.
                    Isolate(BuildSettleWager()),

                    // 4 · Poke, exclusivo de la rama de marcar.
                    Isolate(new AINode_If
                    {
                        TargetSelector = new TargetSelector_AlwaysPlayer(),
                        Conditions = new List<BasePreCondition>
                        {
                            new PcTahurCleanRound(),
                            new PcTargetInRange { Range = 1 },
                        },
                        Then = BuildPoke(),
                    }),

                    // 5 · Canta el escalón de la próxima ronda (R03 + R01).
                    Isolate(BuildCallHand()),

                    // 6 · Se acerca — nunca kitea: el que acorta es él.
                    Isolate(new AINode_Move
                    {
                        MaxSteps = new AIConstantInt { Value = MoveSteps },
                        TargetSelector = new TargetSelector_AlwaysPlayer(),
                        DesiredRange = new AIConstantInt { Value = DesiredRange },
                        Retreat = false,
                        StopAdjacent = true,
                    }),

                    // 7 · Pone la mesa en su posición final.
                    Isolate(BuildMarkTable()),

                    // 8 · La Banca, con el pozo lleno. Va DESPUÉS de la mesa: el hueco de la marca
                    //     y el paño cian tienen que ser el mismo 3×3, y el paño se pinta recién
                    //     cuando el jefe terminó de moverse.
                    Isolate(BuildBanca()),
                },
            };
        }

        /// <summary>Nodo de liquidación con la tabla del pozo y las formas del Castigo de la ficha.</summary>
        public static AINode_TahurSettleWager BuildSettleWager()
        {
            return new AINode_TahurSettleWager
            {
                PotDamageTable = new List<int>(PotDamageTable),
                DamageCeiling = DamageCeiling,
                PayoutPerChip = PayoutPerChip,
                MaxChips = MaxChips,
                RakeChipsPerRound = RakeChipsPerRound,
                MissChipGain = 1,
                GreedChipGain = 2,
                ReadChipGain = 2,
                MissShapes = new List<TahurPunishmentShape>
                {
                    new TahurPunishmentShape { Shape = ThreatShape.Column, Size = 1 },
                    new TahurPunishmentShape { Shape = ThreatShape.Row, Size = 1 },
                    new TahurPunishmentShape { Shape = ThreatShape.Column, Size = 3 },
                    new TahurPunishmentShape { Shape = ThreatShape.ScatteredSquares, Size = 2, Count = 4 },
                },
                GreedShape = new TahurPunishmentShape
                {
                    Shape = ThreatShape.ScatteredSquares, Size = 2, Count = 6,
                },
                PunishmentKind = AttackKind.BasicAttack,
                PayoutKind = AttackKind.ScriptedAbility,
            };
        }

        /// <summary>Canto: escalones 1-6, nunca dos altos seguidos, rotativo con memoria.</summary>
        public static AINode_TahurCallHand BuildCallHand()
        {
            return new AINode_TahurCallHand
            {
                MinRank = 1,
                MaxRank = 6,
                HighRankThreshold = 5,
                AvoidConsecutiveHighCalls = true,
                UseRotationMemory = true,
                ForbidCalledHand = true,
                GreedMultiplier = 2f,
                ClearPreviousRules = true,
            };
        }

        public static AINode_TahurMarkTable BuildMarkTable()
            => new AINode_TahurMarkTable { Size = TableSize, Tint = new Color(0f, 0.85f, 1f, 1f) };

        /// <summary>
        /// La Banca: con el pozo lleno, 45 en toda la sala menos La Mesa. El radio del hueco es el
        /// mismo <see cref="TableSize"/> que pinta el paño cian — si se separan, el jugador lee una
        /// zona segura que no lo es.
        /// </summary>
        public static AINode_TahurMarkBanca BuildBanca()
            => new AINode_TahurMarkBanca
            {
                ChipsThreshold = BancaChipsThreshold,
                Damage = BancaDamage,
                DamageCeiling = DamageCeiling,
                TableRadius = TableSize,
                Kind = AttackKind.BasicAttack,
            };

        public static AINode_TahurPoke BuildPoke()
            => new AINode_TahurPoke { Damage = PokeDamage, Range = 1, RequireCleanRound = true };

        public static AINode_TahurFlipCard BuildFlipCard()
            => new AINode_TahurFlipCard
            {
                RakeChipsPerRound = 1,
                ChipsFloorAfterFlip = 1,
                GraceOnFirstSettle = true,
            };

        // -----------------------------------------------------------------
        // Paleta — "la banca": fieltro de mesa, dorado y sangre de toro
        // -----------------------------------------------------------------
        //
        // Por qué colores directos y no PaletteSlot: los Mat_* del arte apuntan a slots de
        // PA_MainPalette que están desalineados respecto de sus nombres (Mat_LightGreen → slot 3,
        // que es "Gray, LightGreen, Cyan…" y renderea gris verdoso). Con FromColors el color que se
        // escribe es el color que se ve, y el Tahúr no depende de que nadie reordene la paleta.
        //
        // Por qué NO se retinta hacia naranja ni cian: la Mesa se pinta en cian y el Castigo en
        // naranja. Un jefe que comparta esos tonos con sus propios telegraphs es ilegible por
        // construcción (ver Table_IsAThreeByThreeAroundSelf_InItsOwnColour).

        /// <summary>Fieltro de la mesa — levita, galera y moño. Es el área grande de la silueta.</summary>
        private static readonly Color FeltLight = new Color32(0x3C, 0x8A, 0x63, 0xFF);
        private static readonly Color FeltMid = new Color32(0x1E, 0x5A, 0x3C, 0xFF);
        private static readonly Color FeltShadow = new Color32(0x0C, 0x2B, 0x1E, 0xFF);

        /// <summary>Fieltro gastado — paneles y solapas, un escalón más abajo que la levita.</summary>
        private static readonly Color FeltDeepLight = new Color32(0x27, 0x6B, 0x48, 0xFF);
        private static readonly Color FeltDeepMid = new Color32(0x14, 0x44, 0x2D, 0xFF);
        private static readonly Color FeltDeepShadow = new Color32(0x07, 0x1C, 0x13, 0xFF);

        /// <summary>Dorado de la banca — la cinta de la galera.</summary>
        private static readonly Color GoldLight = new Color32(0xED, 0xC4, 0x5A, 0xFF);
        private static readonly Color GoldMid = new Color32(0xBD, 0x8B, 0x2B, 0xFF);
        private static readonly Color GoldShadow = new Color32(0x66, 0x45, 0x14, 0xFF);

        /// <summary>Canto dorado de las cartas: un punto más claro para que el abanico corte la levita.</summary>
        private static readonly Color GiltLight = new Color32(0xF9, 0xDC, 0x8A, 0xFF);
        private static readonly Color GiltMid = new Color32(0xD4, 0xA4, 0x3C, 0xFF);
        private static readonly Color GiltShadow = new Color32(0x7A, 0x56, 0x19, 0xFF);

        /// <summary>
        /// Sangre de toro donde el arte pone negro (dorso de las cartas, copa de la galera). Casi
        /// negro a propósito: la silueta sigue leyéndose negra y el carmesí sólo aparece de cerca.
        /// </summary>
        private static readonly Color OxbloodLight = new Color32(0x4A, 0x19, 0x21, 0xFF);
        private static readonly Color OxbloodMid = new Color32(0x2D, 0x0E, 0x14, 0xFF);
        private static readonly Color OxbloodShadow = new Color32(0x13, 0x05, 0x08, 0xFF);

        /// <summary>Marfil viejo — caras de las cartas y camisa: mazo marcado, no naipe de fábrica.</summary>
        private static readonly Color IvoryLight = new Color32(0xF6, 0xEF, 0xDB, 0xFF);
        private static readonly Color IvoryMid = new Color32(0xE0, 0xD4, 0xB4, 0xFF);
        private static readonly Color IvoryShadow = new Color32(0x96, 0x89, 0x6F, 0xFF);

        /// <summary>
        /// Piel cerosa de cara y manos. El Sunken Grand del piso 1 la muestra gris verdosa (de
        /// ahogado); acá es amarillenta de tipo que vive bajo la lámpara. Es el tell de cerca más
        /// rápido para no confundir los dos jefes.
        /// </summary>
        private static readonly Color SallowLight = new Color32(0xE0, 0xC8, 0xA3, 0xFF);
        private static readonly Color SallowMid = new Color32(0xBE, 0x9F, 0x79, 0xFF);
        private static readonly Color SallowShadow = new Color32(0x79, 0x5C, 0x42, 0xFF);

        /// <summary>
        /// Retinte por material del arte. Cubre <b>los siete</b> materiales que usa
        /// <c>SunkedGrand_Animated</c>: cualquiera que quede afuera se comparte con el jefe del piso 1
        /// y los vuelve gemelos en esa superficie.
        /// </summary>
        public static Dictionary<string, MaterialRetint> BuildRetints()
        {
            return new Dictionary<string, MaterialRetint>
            {
                // Levita + galera + moño (Body, Hat, Bow_Tie).
                { "Mat_LightBrown", MaterialRetint.FromColors(FeltLight, FeltMid, FeltShadow) },
                // Paneles del cuerpo.
                { "Mat_Brown", MaterialRetint.FromColors(FeltDeepLight, FeltDeepMid, FeltDeepShadow) },
                // Cinta de la galera.
                { "Mat_Green", MaterialRetint.FromColors(GoldLight, GoldMid, GoldShadow) },
                // Canto de las 12 cartas del abanico.
                { "Mat_Bone", MaterialRetint.FromColors(GiltLight, GiltMid, GiltShadow) },
                // Dorso de las cartas y detalles oscuros.
                { "Mat_Black", MaterialRetint.FromColors(OxbloodLight, OxbloodMid, OxbloodShadow) },
                // Caras de las cartas y camisa.
                { "Mat_White", MaterialRetint.FromColors(IvoryLight, IvoryMid, IvoryShadow) },
                // Piel (Head, Hands).
                { "Mat_LightGreen", MaterialRetint.FromColors(SallowLight, SallowMid, SallowShadow) },
            };
        }

        // -----------------------------------------------------------------
        // Visual
        // -----------------------------------------------------------------

        /// <summary>
        /// Ficha del wrapper de gameplay del Tahúr. Pura: no toca el <c>AssetDatabase</c>.
        /// </summary>
        /// <remarks>
        /// <b>Sin props.</b> La pila de fichas (<c>Assets/Prefabs/Props/Fichasv01.prefab</c>) se
        /// evaluó como su pozo y se descartó por dos razones: el piso 3 ya tiene seis pilas idénticas
        /// desparramadas en cada sala (la del jefe incluida), así que no aportaría silueta; y el pozo
        /// es un contador de 0 a 5 que cambia todas las rondas — una pila fija que además se desliza
        /// por el piso cuando el jefe camina miente sobre el estado. El pozo pide un visual que lea la
        /// cuenta (VFX/telegraph), no set dressing parenteado al pawn.
        /// </remarks>
        public static BossWrapperSpec BuildWrapperSpec()
        {
            return new BossWrapperSpec
            {
                ArtPrefabPath = ArtPrefabPath,
                OutputPrefabPath = VisualPrefabPath,
                EntityId = EntityId,
                BossName = BossName,
                MaterialsFolder = MaterialsFolder,
                HealthBarOffset = new Vector3(0f, HealthBarHeight, 0f),
                Retints = BuildRetints(),
            };
        }

        /// <summary>
        /// Construye (o reconstruye) <c>PF_Boss_Tahur.prefab</c>. <c>null</c> si el arte no está.
        /// </summary>
        /// <remarks>
        /// El puente de Animation Events lo pone <see cref="BossVisualWrapperBuilder.BuildWrapper"/>:
        /// <c>Anim_SunkedGrand_Attack_Melee</c> y <c>Anim_SunkedGrand_Attack_Range</c> llaman
        /// <c>PushFeedbackEvent</c>, pero eso dejó de ser un caso del Tahúr — los seis jefes lo
        /// necesitan y por eso vive en la utility compartida.
        /// </remarks>
        public static GameObject BuildVisualPrefab()
            => BossVisualWrapperBuilder.BuildWrapper(BuildWrapperSpec());

        /// <summary>
        /// Alias de <see cref="BossVisualWrapperBuilder.EnsureAnimationFeedbackBridge"/>, que es donde
        /// vive ahora la lógica.
        /// </summary>
        /// <remarks>
        /// Queda como reenvío y no se borra porque <c>TahurVisualWiringTests</c> entra por acá: era el
        /// único builder con el puente propio, y su fixture lo llama por nombre.
        /// </remarks>
        public static GameObject EnsureAnimationFeedbackBridge(string prefabPath)
            => BossVisualWrapperBuilder.EnsureAnimationFeedbackBridge(prefabPath);

        // -----------------------------------------------------------------
        // Data
        // -----------------------------------------------------------------

        /// <summary>
        /// Escribe identidad, stats y árbol en <paramref name="data"/>. Puro: sin
        /// <c>AssetDatabase</c>, así que corre en tests con un <c>EnemyDataSO</c> en memoria.
        /// </summary>
        /// <param name="visualPrefab">Pawn visual. Null = no toca el campo (el caller de assets
        /// lo resuelve; en tests no hace falta).</param>
        /// <param name="portrait">Retrato de la cola de turnos / barra de jefe. Null = no toca el
        /// campo: si el arte todavía no está importado como Sprite, es mejor dejar el retrato que ya
        /// tenía el asset que borrarlo.</param>
        public static void PopulateEnemyData(
            EnemyDataSO data, GameObject visualPrefab = null, Sprite portrait = null)
        {
            if (data == null) return;

            data.EntityId = EntityId;
            data.DisplayName = DisplayName;
            data.Description =
                "The bank calls your hand before you play it. Building it exactly pays out; " +
                "overshooting is greed — and the pot keeps count of greed.";

            data.BaseHP = BaseHP;
            data.BaseAttack = BaseAttack;
            data.BaseSpeed = BaseSpeed;
            data.MaxEnergy = MaxEnergy;
            data.BaseAttackRange = 1;
            data.BaseHealStrength = 5;

            data.MinGoldDrop = MinGoldDrop;
            data.MaxGoldDrop = MaxGoldDrop;

            // Sin debilidad, a propósito: el build que maximiza siempre es el peor posible contra
            // él, y un ×1,5 encima de su ×2 de codicia apilaría dos multiplicadores.
            data.WeaknessComboId = string.Empty;
            data.WeaknessMultiplierOverride = 0f;

            if (visualPrefab != null) data.VisualPrefab = visualPrefab;
            if (portrait != null) data.Portrait = portrait;

            data.AIRoot = BuildAIRoot();
        }

        // -----------------------------------------------------------------
        // Menú
        // -----------------------------------------------------------------

        [MenuItem("Tools/Rollgeon/Bosses/Build Tahur")]
        public static void BuildTahurAsset()
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetPath);
            bool created = data == null;

            if (created)
            {
                data = ScriptableObject.CreateInstance<EnemyDataSO>();
                AssetDatabase.CreateAsset(data, AssetPath);
            }

            // El visual se construye antes de poblar para poder asignarlo en la misma pasada. Si el
            // arte no está, PopulateEnemyData ignora el null y el asset conserva el prefab que ya
            // tenía en vez de quedarse sin pawn.
            var prefab = BuildVisualPrefab();
            if (prefab == null)
            {
                Debug.LogWarning($"[TahurAssetBuilder] No se pudo construir '{VisualPrefabPath}' " +
                                 $"desde '{ArtPrefabPath}' — el asset conserva su VisualPrefab actual.");
            }

            var portrait = BossPortraitLibrary.Tahur();
            if (portrait == null)
            {
                Debug.LogWarning($"[TahurAssetBuilder] '{PortraitTexturePath}' no resolvió a Sprite " +
                                 $"— el asset conserva su Portrait actual.");
            }

            PopulateEnemyData(data, prefab, portrait);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TahurAssetBuilder] {(created ? "Creado" : "Actualizado")} '{AssetPath}' " +
                      $"— HP {BaseHP}, castigos {string.Join("/", PotDamageTable)}, poke {PokeDamage}, " +
                      $"rastrillo +{RakeChipsPerRound}/ronda desde fase 1, " +
                      $"Banca {BancaDamage} con el pozo en {BancaChipsThreshold}, " +
                      $"visual '{(prefab != null ? VisualPrefabPath : "sin cambios")}'. " +
                      "Falta sumarlo al EnemyCatalog / pool de jefes del piso 3 (wiring de data, a mano).");

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
        }

        // -----------------------------------------------------------------

        /// <summary>
        /// Envuelve un nodo que puede fallar en <c>Selector[nodo, Wait]</c> para que su
        /// <c>Failed</c> no aborte el turno entero.
        /// </summary>
        private static AINode_Selector Isolate(AIDecisionNode node)
            => new AINode_Selector
            {
                Children = new List<AIDecisionNode> { node, new AINode_Wait() },
            };
    }
}
