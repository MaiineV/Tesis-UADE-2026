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

        /// <summary>Retrato del jefe: la mano recibiendo monedas del pack de símbolos.</summary>
        public const string PortraitTexturePath = "Assets/Art/2D/Symbols/Sprites/Casino_0070.png";

        /// <summary>
        /// Prefab que usaba el jefe mientras no tenía arte propio. Se sigue conociendo para poder
        /// migrarlo: una ficha que todavía lo apunte se actualiza al wrapper sin preguntar.
        /// </summary>
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
        /// <remarks>
        /// <paramref name="visualPrefab"/> y <paramref name="portrait"/> se asignan sólo si no son
        /// null: los tests (y cualquier caller que sólo quiera refrescar los números) llaman sin
        /// ellos, y nulearlos dejaría al jefe sin cuerpo y sin cara en la cola de turnos.
        /// </remarks>
        public static void PopulateEnemyData(
            EnemyDataSO data,
            GameObject visualPrefab = null,
            HazardDefinitionSO chip = null,
            Sprite portrait = null)
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

            data.AIRoot = BuildAIRoot(chip);
        }

        // ---- MenuItem ----------------------------------------------------

        [MenuItem("Tools/Rollgeon/Bosses/Build Cajero")]
        public static void BuildCajeroAsset()
        {
            var chip = EnsureChipHazard();
            var data = LoadOrCreate<EnemyDataSO>(EnemyAssetPath);

            var wrapper = EnsureVisualPrefab();
            var portrait = EnsurePortrait();
            var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderVisualPrefabPath);

            var visual = ResolveVisualPrefab(data.VisualPrefab, wrapper, placeholder);
            PopulateEnemyData(data, visual, chip, portrait);

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CajeroAssetBuilder] '{EnemyId(data)}' actualizado en '{EnemyAssetPath}' " +
                      $"(ficha: {BaseHP} HP, columna {ChipMinValue}-{ChipMaxValue}g, arqueo al " +
                      $"{AuditHpThreshold:P0}; visual: {NameOf(visual)}, retrato: {NameOf(portrait)}).");
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
        /// Retrato del jefe, forzando el import a Sprite: el pack de símbolos entra al repo como
        /// textura Default y un campo <c>Sprite</c> no puede referenciarla.
        /// </summary>
        public static Sprite EnsurePortrait()
        {
            var portrait = SpriteImportUtility.EnsureSpriteImport(PortraitTexturePath);
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
