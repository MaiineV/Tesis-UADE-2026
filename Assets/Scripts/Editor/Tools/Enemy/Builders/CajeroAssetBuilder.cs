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
    /// <b>El prefab visual es un placeholder.</b> Reusa el del Security Boss hasta que exista arte
    /// propio del Cajero; el builder sólo lo asigna si el asset todavía no tiene uno o si apunta al
    /// placeholder, para no pisar un prefab que un artista haya wireado a mano.
    /// </para>
    /// </remarks>
    public static class CajeroAssetBuilder
    {
        // ---- Rutas -------------------------------------------------------

        public const string EnemyAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Cajero.asset";
        public const string ChipHazardPath = "Assets/Rollgeon/Combat/Hazards/HZ_Cashier_Chip.asset";
        public const string PlaceholderVisualPrefabPath = "Assets/Prefabs/Enemies/SecurityGuardBoss.prefab";

        // ---- Ficha (números del diseño; una sola fuente de verdad) --------

        public const string EntityId = "boss.cashier";
        public const string DisplayName = "El Cajero";

        public const int BaseHP = 190;
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

        /// <summary>Id estable del hazard-ficha: el servicio de hazards keyea por él. Hex válido —
        /// un SourceId que no parsea a Guid loguea error cada vez que se lee.</summary>
        public const string ChipHazardSourceId = "3c0a7d18-9f42-4a6b-9c3e-5b1ca5e70001";

        // ---- Árbol -------------------------------------------------------

        /// <summary>
        /// Escalones de la columna, tal cual la ficha: &lt;100 ⇒ Size 1 / 14, 100-249 ⇒ Size 3 / 28,
        /// ≥250 ⇒ Size 3 / 35 (35 = techo de daño de piso 2, no subir).
        /// </summary>
        public static List<CashierGoldTier> BuildGoldTiers() => new List<CashierGoldTier>
        {
            new CashierGoldTier { MinGold = 0,   ColumnSize = 1, Damage = 14 },
            new CashierGoldTier { MinGold = 100, ColumnSize = 3, Damage = 28 },
            new CashierGoldTier { MinGold = 250, ColumnSize = 3, Damage = 35 },
        };

        /// <summary>
        /// Árbol del Cajero. Sequence raíz de 5 hijos:
        /// <list type="number">
        /// <item><c>ExecuteTelegraph</c> — detona la columna del turno pasado.</item>
        /// <item>Gate del arqueo (50% HP) → <c>Once → Sequence[Audit, ApplyStatModifier]</c>.</item>
        /// <item>La columna que engorda (<c>TelegraphMarkGoldScaled</c>).</item>
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
        /// Las fichas van <b>después</b> de la marca por contrato de la ficha: caen dentro de la
        /// columna de este turno, que se lee de <c>IThreatenedAreaService</c>.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(HazardDefinitionSO chip = null)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    new AINode_ExecuteTelegraph(),
                    WrapFallible(BuildAuditGate()),
                    WrapFallible(BuildColumn()),
                    WrapFallible(BuildChipDrop(chip)),
                    WrapFallible(BuildKeepDistance()),
                },
            };
        }

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

        public static AINode_TelegraphMarkGoldScaled BuildColumn() => new AINode_TelegraphMarkGoldScaled
        {
            Shape = ThreatShape.Column,
            Kind = AttackKind.BasicAttack,
            ApplyBribeStepDown = true,
            Tiers = BuildGoldTiers(),
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
        public static void PopulateEnemyData(
            EnemyDataSO data, GameObject visualPrefab = null, HazardDefinitionSO chip = null)
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

            data.AIRoot = BuildAIRoot(chip);
        }

        // ---- MenuItem ----------------------------------------------------

        [MenuItem("Tools/Rollgeon/Bosses/Build Cajero")]
        public static void BuildCajeroAsset()
        {
            var chip = EnsureChipHazard();
            var data = LoadOrCreate<EnemyDataSO>(EnemyAssetPath);

            var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderVisualPrefabPath);
            if (placeholder == null)
                Debug.LogWarning($"[CajeroAssetBuilder] No se encontró el prefab placeholder en " +
                                 $"'{PlaceholderVisualPrefabPath}' — el jefe queda sin VisualPrefab.");

            // Sólo se pisa el prefab si nadie lo cambió a mano (o si ya era el placeholder).
            bool keepAuthoredPrefab = data.VisualPrefab != null && data.VisualPrefab != placeholder;
            PopulateEnemyData(data, keepAuthoredPrefab ? data.VisualPrefab : placeholder, chip);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CajeroAssetBuilder] '{EnemyId(data)}' actualizado en '{EnemyAssetPath}' " +
                      $"(ficha: {BaseHP} HP, columna {ChipMinValue}-{ChipMaxValue}g, arqueo al " +
                      $"{AuditHpThreshold:P0}).");
            Selection.activeObject = data;
        }

        private static string EnemyId(EnemyDataSO data) => string.IsNullOrEmpty(data.EntityId) ? EntityId : data.EntityId;

        /// <summary>
        /// Crea (o actualiza) el hazard que representa una ficha en el piso: se dispara al pisarla,
        /// se consume, no hace daño y vive un turno.
        /// </summary>
        public static HazardDefinitionSO EnsureChipHazard()
        {
            var chip = LoadOrCreate<HazardDefinitionSO>(ChipHazardPath);

            chip.Trigger = HazardTriggerMode.OnEnter;
            chip.ConsumeOnTrigger = true;
            chip.Damage = 0;
            chip.Kind = AttackKind.Environmental;
            chip.DurationRounds = ChipDurationRounds;
            chip.Shape = ThreatShape.Column; // Inerte: las fichas se activan con la overload de tiles.
            chip.Size = 1;
            chip.OverlayTint = new Color(1f, 0.84f, 0.25f, 0.55f); // oro
            chip.SourceId = ChipHazardSourceId;

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
