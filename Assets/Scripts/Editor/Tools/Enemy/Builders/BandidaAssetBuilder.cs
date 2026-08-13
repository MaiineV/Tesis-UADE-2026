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

        /// <summary>Placeholder: el jefe todavía no tiene arte propio.</summary>
        public const string PlaceholderPrefabPath = "Assets/Prefabs/Enemies/SunkedGrand.prefab";

        public const string BossEntityId = "boss.one_armed";
        public const string ReelEntityId = "obj.reel";

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

        /// <summary>Brazo: 9 en el 3×3 alrededor del jefe. Un paso atrás lo esquiva entero.</summary>
        public const int ArmDamage = 9;
        public const int ArmSize = 1;
        public const int ArmRange = 1;

        /// <summary>Dos rondas de cuenta antes de marcar; el mark tarda un turno más.</summary>
        public const int CountdownStart = 2;

        public const int ReelCount = 3;
        public const int ReelHp = 3;
        public const int RespawnDelayPhase1 = 2;
        public const int RespawnDelayPhase2 = 1;

        public const float Phase2HpThreshold = 0.5f;
        public const int Phase2Index = 2;

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
        /// <item>ExecuteTelegraph — cobra la marca del turno anterior (jackpot o brazo).</item>
        /// <item>Gate de Fase 2 — HOLD del rodillo del medio + reposición a 1 turno.</item>
        /// <item>Fila de rodillos — arma, detecta rotos y repone.</item>
        /// <item>TickJackpot — baja el número gigante.</item>
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
        /// El <c>Selector</c> del pool es lo que garantiza que jackpot y brazo nunca resuelven el
        /// mismo turno: una amenaza por turno se lee mejor.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(EnemyDataSO reelData)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    new AINode_ExecuteTelegraph(),
                    IsolateFailure(BuildPhaseTwoGate()),
                    IsolateFailure(BuildReelRow(reelData)),
                    new AINode_TickJackpot(),
                    BuildActionPool(),
                },
            };
        }

        /// <summary>
        /// Gate de Fase 2 (50% HP): traba el rodillo del medio y baja la reposición a un turno.
        /// Ningún número de daño cambia — el jackpot sigue en 25 y el brazo en 9. Cambia la
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
        private static AINode_SpawnReels BuildReelRow(EnemyDataSO reelData)
        {
            return new AINode_SpawnReels
            {
                ReelData = reelData,
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
                            // Chebyshev: el rango del gate tiene que coincidir con el área 3×3 que
                            // marca el brazo, si no el jefe telegrafía en diagonal y no llega.
                            new PcTargetInRange { Range = ArmRange, Metric = DistanceMetric.Chebyshev },
                        },
                        Then = new AINode_TelegraphMark
                        {
                            Shape = ThreatShape.SquareAroundSelf,
                            Size = ArmSize,
                            Damage = ArmDamage,
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

        public static void PopulateEnemyData(EnemyDataSO boss, EnemyDataSO reelData, GameObject visualPrefab)
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

            boss.AIRoot = BuildAIRoot(reelData);
        }

        /// <summary>
        /// El rodillo: objeto de 3 de vida que no actúa. Su árbol es un <c>Wait</c> — está en la cola
        /// de turnos solo para que la limpieza de fin de combate lo levante junto con el resto.
        /// </summary>
        public static void PopulateReelData(EnemyDataSO reel, GameObject visualPrefab)
        {
            if (reel == null) return;

            reel.EntityId = ReelEntityId;
            reel.DisplayName = "Reel";
            reel.Description =
                "One of La Bandida's three reels. Three hit points, bolted in a row against the " +
                "wall: any hit breaks it and cancels the jackpot count.";

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

            reel.AIRoot = new AINode_Wait();
        }

        // ======================================================================
        // MenuItem (la única parte que toca el AssetDatabase)
        // ======================================================================

        [MenuItem("Tools/Rollgeon/Bosses/Build Bandida")]
        public static void BuildBandida()
        {
            var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPrefabPath);
            if (placeholder == null)
                Debug.LogWarning($"[BandidaAssetBuilder] No se encontró el prefab placeholder en " +
                                 $"'{PlaceholderPrefabPath}' — los assets quedan sin VisualPrefab.");

            var reel = LoadOrCreate(ReelAssetPath);
            PopulateReelData(reel, placeholder);
            EditorUtility.SetDirty(reel);

            var boss = LoadOrCreate(BossAssetPath);
            PopulateEnemyData(boss, reel, placeholder);
            EditorUtility.SetDirty(boss);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BandidaAssetBuilder] Listo: '{BossAssetPath}' + '{ReelAssetPath}'. " +
                      "Falta a mano: Portrait de los dos SOs, prefab propio del jefe, la UI del " +
                      "número gigante (TypedEvent<JackpotCountdownPayload>) y el alta del jefe en " +
                      "el BossFloorManagerSO del piso 1.");
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
