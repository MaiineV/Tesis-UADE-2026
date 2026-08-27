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
    /// Autorea <c>ED_Boss_Tahur.asset</c> — El Tahúr, jefe del piso 3.
    /// </summary>
    /// <remarks>
    /// Ojo con BaseHP: el <c>[Range]</c> de <c>EnemyDataSO.BaseHP</c> tiene que llegar a 1000 por
    /// este jefe. Con un tope menor el valor se escribe bien por código, pero el Inspector lo
    /// clampea al primer roce del slider.
    /// </remarks>
    public static class TahurAssetBuilder
    {
        /// <summary>Menú que regenera estos assets. Lo lee el Editor de enemigos para avisar que el builder pisa el árbol.</summary>
        public const string MenuPath = "Tools/Rollgeon/Bosses/Build Tahur";

        // -----------------------------------------------------------------
        // Identidad + stats
        // -----------------------------------------------------------------

        public const string AssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Tahur.asset";

        /// <summary>
        /// Arte del jefe, <b>compartido con el Sunken Grand del piso 1</b>: lo único que los separa
        /// es <see cref="BuildRetints"/>.
        /// </summary>
        public const string ArtPrefabPath = "Assets/Prefabs/Enemies/SunkedGrand_Animated.prefab";

        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/Bosses/PF_Boss_Tahur.prefab";

        /// <summary>Retrato del rig que viste (<c>SunkedGrand_Animated</c>). Ver <see cref="BossPortraitLibrary"/>.</summary>
        public const string PortraitTexturePath = BossPortraitLibrary.SheetPath;

        public const string BossName = "Tahur";
        public const string MaterialsFolder = "Assets/Rollgeon/Enemies/Materials/Tahur";

        /// <summary>El arte mide ~1,81: con el default 3 de la utility la barra flota despegada.</summary>
        public const float HealthBarHeight = 2.4f;

        public const string EntityId = "boss.tahur";
        public const string DisplayName = "El Tahúr";

        /// <summary>Vida del jefe de piso 3.</summary>
        public const int BaseHP = 240;
        public const int BaseAttack = 40;
        public const int BaseSpeed = 4;
        public const int MaxEnergy = 3;
        public const int MinGoldDrop = 60;
        public const int MaxGoldDrop = 80;

        // -----------------------------------------------------------------
        // Números del pozo y del turno
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
        /// El rastrillo: fichas que el pozo sube solo por ronda, <b>desde la fase 1</b>.
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
        /// El turno del Tahúr, en orden: cobra el Castigo marcado, voltea la carta si toca, liquida y
        /// marca, poke, canta, se acerca, pone la mesa y —con el pozo lleno— barre con La Banca.
        /// </summary>
        /// <remarks>
        /// Todo lo que puede fallar va en <c>Selector[nodo, Wait]</c>: un <c>Failed</c> aborta el
        /// Sequence y le come al jefe el resto del turno. Los gates de fase van ANTES de la acción
        /// para que tickeen en el path coroutine y en el que no.
        /// </remarks>
        public static AINode_Sequence BuildAIRoot()
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // 1 · Cobra el Castigo marcado la ronda pasada. Cae sobre tiles lejos suyo, así
                    //     que el gesto es el de rango.
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

                    // 6 · Se acerca — nunca kitea.
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

                    // 8 · La Banca, con el pozo lleno. Va DESPUÉS de la mesa: el hueco de la marca y
                    //     el paño cian tienen que ser el mismo 3×3, y el paño se pinta recién cuando
                    //     el jefe terminó de moverse.
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
        /// La Banca: con el pozo lleno, daño en toda la sala menos La Mesa. El hueco usa el mismo
        /// <see cref="TableSize"/> que pinta el paño cian, o el jugador lee una zona segura falsa.
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
        // Colores directos y no PaletteSlot: los slots de PA_MainPalette están desalineados respecto
        // de sus nombres. Nada de naranja ni cian: son los de sus propios telegraphs.

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

        /// <summary>Sangre de toro donde el arte pone negro: dorso de las cartas y copa de la galera.</summary>
        private static readonly Color OxbloodLight = new Color32(0x4A, 0x19, 0x21, 0xFF);
        private static readonly Color OxbloodMid = new Color32(0x2D, 0x0E, 0x14, 0xFF);
        private static readonly Color OxbloodShadow = new Color32(0x13, 0x05, 0x08, 0xFF);

        /// <summary>Marfil viejo — caras de las cartas y camisa: mazo marcado, no naipe de fábrica.</summary>
        private static readonly Color IvoryLight = new Color32(0xF6, 0xEF, 0xDB, 0xFF);
        private static readonly Color IvoryMid = new Color32(0xE0, 0xD4, 0xB4, 0xFF);
        private static readonly Color IvoryShadow = new Color32(0x96, 0x89, 0x6F, 0xFF);

        /// <summary>Piel cerosa: el tell que la separa del gris verdoso del Sunken Grand.</summary>
        private static readonly Color SallowLight = new Color32(0xE0, 0xC8, 0xA3, 0xFF);
        private static readonly Color SallowMid = new Color32(0xBE, 0x9F, 0x79, 0xFF);
        private static readonly Color SallowShadow = new Color32(0x79, 0x5C, 0x42, 0xFF);

        /// <summary>
        /// Cubre <b>los siete</b> materiales de <c>SunkedGrand_Animated</c>: el que quede afuera se
        /// comparte con el jefe del piso 1 y los vuelve gemelos en esa superficie.
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

        /// <summary>Ficha del wrapper de gameplay. Pura: no toca el <c>AssetDatabase</c>.</summary>
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

        /// <summary>Construye <c>PF_Boss_Tahur.prefab</c>. <c>null</c> si el arte no está.</summary>
        public static GameObject BuildVisualPrefab()
            => BossVisualWrapperBuilder.BuildWrapper(BuildWrapperSpec());

        /// <summary>Reenvío que <c>TahurVisualWiringTests</c> llama por nombre.</summary>
        public static GameObject EnsureAnimationFeedbackBridge(string prefabPath)
            => BossVisualWrapperBuilder.EnsureAnimationFeedbackBridge(prefabPath);

        // -----------------------------------------------------------------
        // Data
        // -----------------------------------------------------------------

        /// <summary>Escribe identidad, stats y árbol. Puro: sin <c>AssetDatabase</c>.</summary>
        /// <param name="visualPrefab">Null = no toca el campo.</param>
        /// <param name="portrait">Null = no toca el campo, así un arte sin importar no lo borra.</param>
        public static void PopulateEnemyData(
            EnemyDataSO data, GameObject visualPrefab = null, Sprite portrait = null)
        {
            if (data == null) return;

            data.EntityId = EntityId;
            data.DisplayName = DisplayName;
            data.Description = "Calls your hand before you play it. Match it exactly; going over costs.";

            data.BaseHP = BaseHP;
            data.BaseAttack = BaseAttack;
            data.BaseSpeed = BaseSpeed;
            data.MaxEnergy = MaxEnergy;
            data.BaseAttackRange = 1;
            data.BaseHealStrength = 5;

            data.MinGoldDrop = MinGoldDrop;
            data.MaxGoldDrop = MaxGoldDrop;

            // Sin debilidad: un ×1,5 encima de su ×2 de codicia apilaría dos multiplicadores.
            data.WeaknessComboId = string.Empty;
            data.WeaknessMultiplierOverride = 0f;

            if (visualPrefab != null) data.VisualPrefab = visualPrefab;
            if (portrait != null) data.Portrait = portrait;

            data.AIRoot = BuildAIRoot();
            data.AIDetachedNodes.Clear(); // el builder es fuente de verdad: nada suelto sobrevive
        }

        // -----------------------------------------------------------------
        // Menú
        // -----------------------------------------------------------------

        [MenuItem(MenuPath)]
        public static void BuildTahurAsset()
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetPath);
            bool created = data == null;

            if (created)
            {
                data = ScriptableObject.CreateInstance<EnemyDataSO>();
                AssetDatabase.CreateAsset(data, AssetPath);
            }

            // Antes de poblar, para asignarlo en la misma pasada. Con el arte ausente
            // PopulateEnemyData ignora el null y el asset conserva el prefab que ya tenía.
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
                      $"visual '{(prefab != null ? VisualPrefabPath : "sin cambios")}'.");

            Selection.activeObject = data;
            EditorGUIUtility.PingObject(data);
        }

        // -----------------------------------------------------------------

        /// <summary><c>Selector[nodo, Wait]</c>: su <c>Failed</c> no aborta el turno entero.</summary>
        private static AINode_Selector Isolate(AIDecisionNode node)
            => new AINode_Selector
            {
                Children = new List<AIDecisionNode> { node, new AINode_Wait() },
            };
    }
}
