using System.Collections.Generic;
using Rollgeon.Combat.AI.Bosses.Tahur;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Combat.AI.Targeting;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Entities;
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
    /// <b>Ojo con BaseHP.</b> <c>EnemyDataSO.BaseHP</c> tiene <c>[Range(1, 200)]</c> y el Tahúr son
    /// 290: el valor se escribe bien por código, pero tocar el slider en el Inspector lo clampea a
    /// 200. Ensanchar ese Range es cambio de fundación — reportado, no aplicado acá.
    /// </para>
    /// </remarks>
    public static class TahurAssetBuilder
    {
        // -----------------------------------------------------------------
        // Identidad + stats (ficha v2)
        // -----------------------------------------------------------------

        public const string AssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Tahur.asset";
        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/GeneralDirector.prefab";

        public const string EntityId = "boss.tahur";
        public const string DisplayName = "El Tahúr";

        public const int BaseHP = 290;
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

        /// <summary>Umbral de HP del volteo de la carta (PIDE → LEE).</summary>
        public const float Phase2HpThreshold = 0.40f;

        // -----------------------------------------------------------------
        // Árbol
        // -----------------------------------------------------------------

        /// <summary>
        /// El turno del Tahúr, en orden: cobra el Castigo marcado, voltea la carta si toca,
        /// liquida y marca, poke si la ronda quedó limpia, canta, se acerca y pone la mesa.
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
                    // 1 · Cobra: detona el Castigo marcado la ronda pasada.
                    new AINode_ExecuteTelegraph(),

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
        // Data
        // -----------------------------------------------------------------

        /// <summary>
        /// Escribe identidad, stats y árbol en <paramref name="data"/>. Puro: sin
        /// <c>AssetDatabase</c>, así que corre en tests con un <c>EnemyDataSO</c> en memoria.
        /// </summary>
        /// <param name="visualPrefab">Pawn visual. Null = no toca el campo (el caller de assets
        /// lo resuelve; en tests no hace falta).</param>
        public static void PopulateEnemyData(EnemyDataSO data, GameObject visualPrefab = null)
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

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[TahurAssetBuilder] No se encontró el prefab placeholder en " +
                                 $"'{VisualPrefabPath}' — el asset queda sin VisualPrefab.");
            }

            PopulateEnemyData(data, prefab);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[TahurAssetBuilder] {(created ? "Creado" : "Actualizado")} '{AssetPath}' " +
                      $"— HP {BaseHP}, castigos {string.Join("/", PotDamageTable)}, poke {PokeDamage}. " +
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
