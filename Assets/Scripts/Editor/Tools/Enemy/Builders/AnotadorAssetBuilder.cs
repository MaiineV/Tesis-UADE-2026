using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Combat.AI.Readers;
using Rollgeon.Entities;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Arma El Anotador (jefe de piso 2, el del hielo): su <see cref="EnemyDataSO"/> con el árbol AI
    /// inline y el <see cref="HazardDefinitionSO"/> de la estela helada.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos capas a propósito.</b> <see cref="BuildAIRoot"/> / <see cref="PopulateEnemyData"/> /
    /// <see cref="ConfigureIceHazard"/> son estáticas y puras: no tocan
    /// <see cref="AssetDatabase"/>, así que los tests EditMode validan el wiring y los números en
    /// memoria sin depender de que el <c>.asset</c> exista ni de un import. El
    /// <see cref="MenuItem"/> es la única parte que escribe a disco, y es idempotente: si los assets
    /// ya están, los repopula en vez de duplicarlos (los GUID de los <c>.asset</c> se preservan, así
    /// que las referencias desde catálogos/escenas no se rompen).
    /// </para>
    /// <para>
    /// <b>Ficha de diseño.</b> HP 190 · Attack 30 · fila Row 1 = 30 · lápiz SquareAroundSelf 1 = 12
    /// en rondas impares · KeepDistance ideal 4 · estela de 1-3 casillas con stun 1 · fase 2 al 35%
    /// (2 corrimientos por turno, permanentes, y columna 32 alternada con la fila). Techo de daño de
    /// piso 2 = 35 por golpe: el 32 de la columna es el máximo que sale de acá.
    /// </para>
    /// <para>
    /// <b>LIMITACIÓN CONOCIDA — la fila y el lápiz comparten source.</b>
    /// <see cref="IThreatenedAreaService"/> guarda <b>un</b> área pendiente por source guid, y los
    /// dos <see cref="AINode_TelegraphMark"/> del árbol marcan con <c>context.SelfGuid</c>. En las
    /// rondas impares (fila + lápiz el mismo turno) la segunda marca sobrescribe a la primera, así
    /// que detona el lápiz (12) y no la fila (30) — la ficha pide 42 en ese turno (30 + 12). El árbol
    /// se deja EXACTAMENTE como lo pide la ficha, con el orden que pide (lápiz después del
    /// repliegue), y la colisión se reporta: arreglarla es tocar fundaciones (un sub-source por marca
    /// en TelegraphMark/ExecuteTelegraph, o N áreas pendientes por source), y eso no es de este
    /// worktree.
    /// </para>
    /// </remarks>
    public static class AnotadorAssetBuilder
    {
        // ======================================================================
        // Identidad y rutas
        // ======================================================================

        public const string EnemyAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Anotador.asset";
        public const string IceHazardAssetPath = "Assets/Rollgeon/Combat/Hazards/IceTrailHazardDefinition.asset";

        /// <summary>Placeholder de arte: el jefe todavía no tiene prefab propio.</summary>
        public const string VisualPrefabPath = "Assets/Prefabs/Enemies/SecurityGuardBoss.prefab";

        public const string EntityId = "boss.scorekeeper";
        public const string DisplayName = "El Anotador";

        /// <summary>Debilidad: la única mano que no depende de la tabla.</summary>
        public const string WeaknessComboId = "combo.generala";
        public const float WeaknessMultiplier = 1.5f;

        /// <summary>
        /// SourceId fijo (no <c>Guid.NewGuid()</c>) para que reconstruir el asset no le cambie la
        /// identidad al hazard. Las instancias de área dinámica no lo usan, pero un source id
        /// inestable rompería cualquier estado keyed por source si algún día se activa por ciclo.
        /// </summary>
        public const string IceHazardSourceId = "b7d4f2a6-3c81-4e59-9a02-5f6d8c1e7b43";

        // ======================================================================
        // Números de la ficha
        // ======================================================================

        public const int BaseHp = 190;
        public const int BaseAttack = 30;
        public const int MinGoldDrop = 30;
        public const int MaxGoldDrop = 60;

        public const int RowDamage = 30;
        public const int ColumnDamage = 32;
        public const int PencilDamage = 12;
        public const int MarkSize = 1;

        /// <summary>Distancia que el repliegue intenta mantener. Solo se mueve si lo tienen a 3 o menos.</summary>
        public const int IdealDistance = 4;

        /// <summary>Pasos del repliegue ⇒ tope natural de casillas de la estela (1-3).</summary>
        public const int RetreatSteps = 3;

        public const int MaxTrailTiles = 3;
        public const int TrailStunTurns = 1;

        /// <summary>
        /// Rondas de vida de la estela. <b>2, no 1</b>: la duración se descuenta en el wrap de ronda
        /// (<c>OnTurnQueueBuilt</c>) y el jugador tiene forzado el primer turno de cada ronda
        /// (CNF-006). Con 1, la estela que el boss deja en la ronda N muere en el arranque de la N+1,
        /// <i>antes</i> de que el jugador vuelva a moverse: nunca podría pisarla. Con 2 vive
        /// exactamente un turno del jugador, que es lo que la ficha llama "dura 1 turno".
        /// </summary>
        public const int TrailDurationRounds = 2;

        public const float Phase2HpThreshold = 0.35f;
        public const int ShiftsPerTurnPhase1 = 1;
        public const int ShiftsPerTurnPhase2 = 2;

        /// <summary>Paridad de ronda del lápiz y de la columna: impares lápiz/fila, pares columna.</summary>
        public const int ParityDivisor = 2;

        /// <summary>Celeste: la estela no puede leerse como el naranja del telegraph.</summary>
        public static readonly Color IceOverlayTint = new Color(0.35f, 0.8f, 1f, 0.55f);

        // ======================================================================
        // Capa pura — testeable sin assets
        // ======================================================================

        /// <summary>
        /// Árbol del turno. Sequence raíz de 7 hijos, en el orden de la ficha:
        /// <c>detona → tacha → se acomoda → estela → fila/columna → lápiz → fase 2</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Todo hijo que pueda devolver <c>Failed</c> va dentro de un <c>Selector[…, Wait]</c>: el
        /// Sequence aborta el turno al primer Failed y este boss tiene UN ataque (la marca de fila).
        /// El caso más peligroso es <see cref="AINode_KeepDistance"/>, que devuelve <c>Failed</c> en
        /// el caso benigno "ya estoy a distancia ideal" — la mayoría de los turnos de esta pelea,
        /// porque solo se mueve si lo tienen a 3 casillas o menos. Es el mismo Failed que dejó quieto
        /// al Sunken Grand.
        /// </para>
        /// <para>
        /// El <c>Selector</c> del hijo 5 es lo que garantiza <b>una sola</b> marca grande por turno:
        /// fila (30) + columna (32) el mismo turno son 62 sobre 100 de vida y rompen el techo del
        /// piso. Si la columna entra, el Selector corta y la fila no se marca.
        /// </para>
        /// <para>
        /// El lápiz va <b>después</b> del repliegue para que el anillo quede alrededor de la casilla
        /// final del boss; marcado antes, telegrafía dónde ya no está. No lleva gate de rango: el
        /// anillo ES la adyacencia.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(HazardDefinitionSO iceHazard)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // 1. Detona la marca del turno pasado.
                    new AINode_ExecuteTelegraph(),

                    // 2. Tacha: corre el combo más jugado al vecino de la hoja.
                    BuildShiftNode(),

                    // 3. Se acomoda: si lo tienen a 3 o menos, se repliega a 4.
                    Fallback(new AINode_KeepDistance
                    {
                        MaxSteps = new AIConstantInt { Value = RetreatSteps },
                        IdealDistance = new AIConstantInt { Value = IdealDistance },
                    }),

                    // 4. Congela lo que acaba de caminar.
                    Fallback(new AINode_IceTrail
                    {
                        Hazard = iceHazard,
                        MaxTiles = MaxTrailTiles,
                        StunTurns = TrailStunTurns,
                        ReplacePreviousTrail = true,
                    }),

                    // 5. Marca: columna solo en fase 2 y ronda par; si no, fila.
                    new AINode_Selector
                    {
                        Children = new List<AIDecisionNode>
                        {
                            new AINode_If
                            {
                                Conditions = new List<BasePreCondition>
                                {
                                    new PcOwnerHpBelow { Percent = Phase2HpThreshold },
                                    EvenRound(),
                                },
                                Then = BuildMark(ThreatShape.Column, ColumnDamage),
                            },
                            BuildMark(ThreatShape.Row, RowDamage),
                        },
                    },

                    // 6. El lápiz, solo en rondas impares.
                    Fallback(new AINode_If
                    {
                        Conditions = new List<BasePreCondition> { OddRound() },
                        Then = BuildMark(ThreatShape.SquareAroundSelf, PencilDamage),
                    }),

                    // 7. Fase 2 ("muestra la manga"): feedback + diálogo, una sola vez.
                    Fallback(new AINode_If
                    {
                        Conditions = new List<BasePreCondition>
                        {
                            new PcOwnerHpBelow { Percent = Phase2HpThreshold },
                        },
                        Then = new AINode_Once
                        {
                            Child = new AINode_ApplyStatModifier
                            {
                                AttackDelta = 0,
                                SpeedDelta = 0,
                                PhaseIndex = 2,
                                EmitPhaseChangedEvent = true,
                            },
                        },
                    }),
                },
            };
        }

        /// <summary>
        /// La "tacha". Los corrimientos de fase 2 (cantidad + permanencia) son campos de este mismo
        /// nodo en vez de acciones sueltas bajo el gate de HP: un único nodo es un único lugar donde
        /// vive ese estado, igual que <see cref="AINode_PromulgateRule"/> resuelve su intervalo de
        /// fase leyendo su propia vida.
        /// </summary>
        public static AINode_ShiftComboToNeighbor BuildShiftNode()
        {
            return new AINode_ShiftComboToNeighbor
            {
                // La ficha deja la dirección como pregunta abierta ("¿arriba, abajo, o al azar?").
                // RandomNeighbor es lo único consistente con sus dos mitades: "nunca a tu favor" y
                // "hay corrimientos que te mejoran — es el único jefe que se puede aprovechar".
                Direction = AINode_ShiftComboToNeighbor.ShiftDirection.RandomNeighbor,
                ComboLogWindow = 5,
                ShiftsPerTurnPhase1 = ShiftsPerTurnPhase1,
                ShiftsPerTurnPhase2 = ShiftsPerTurnPhase2,
                Phase2HpThreshold = Phase2HpThreshold,
                RevertPreviousShifts = true,
                Phase2ShiftsArePermanent = true,
                ImmuneComboIds = new List<string> { WeaknessComboId },
            };
        }

        /// <summary>Stats + identidad + árbol. No toca <see cref="AssetDatabase"/>.</summary>
        public static void PopulateEnemyData(EnemyDataSO data, HazardDefinitionSO iceHazard, GameObject visualPrefab)
        {
            if (data == null) return;

            data.EntityId = EntityId;
            data.DisplayName = DisplayName;
            data.Description = "El que lleva la planilla. No juega contra vos: te corrige el puntaje " +
                               "mientras tirás, y nunca a tu favor.";

            data.WeaknessComboId = WeaknessComboId;
            data.WeaknessMultiplierOverride = WeaknessMultiplier;

            data.BaseHP = BaseHp;
            data.BaseAttack = BaseAttack;
            data.BaseSpeed = 4;
            data.MaxEnergy = 3;
            data.BaseHealStrength = 0;
            data.BaseAttackRange = 1;

            data.MinGoldDrop = MinGoldDrop;
            data.MaxGoldDrop = MaxGoldDrop;

            if (visualPrefab != null) data.VisualPrefab = visualPrefab;

            data.AIRoot = BuildAIRoot(iceHazard);
        }

        /// <summary>
        /// Configura la definición del hielo. Área dinámica: <see cref="HazardDefinitionSO.Shape"/>
        /// se ignora (las casillas las pasa el nodo), el daño es 0 —la estela cobra en turnos, no en
        /// HP— y la casilla pisada se derrite, que es lo que impide encadenar stuns.
        /// </summary>
        public static void ConfigureIceHazard(HazardDefinitionSO definition)
        {
            if (definition == null) return;

            definition.Trigger = HazardTriggerMode.OnEnter;
            definition.Damage = 0;
            definition.Kind = AttackKind.Environmental;
            definition.ConsumeOnTrigger = true;
            definition.DurationRounds = TrailDurationRounds;
            definition.OverlayTint = IceOverlayTint;
            definition.SourceId = IceHazardSourceId;
        }

        // ======================================================================
        // Menú — la única capa que escribe a disco
        // ======================================================================

        [MenuItem("Tools/Rollgeon/Bosses/Build Anotador")]
        public static void BuildAnotador()
        {
            var ice = LoadOrCreate<HazardDefinitionSO>(IceHazardAssetPath);
            ConfigureIceHazard(ice);
            EditorUtility.SetDirty(ice);

            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            if (visualPrefab == null)
            {
                Debug.LogWarning($"[AnotadorAssetBuilder] No se encontró el prefab placeholder en " +
                                 $"'{VisualPrefabPath}' — el boss queda sin VisualPrefab.");
            }

            var boss = LoadOrCreate<EnemyDataSO>(EnemyAssetPath);
            PopulateEnemyData(boss, ice, visualPrefab);
            EditorUtility.SetDirty(boss);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[AnotadorAssetBuilder] Listo: '{EnemyAssetPath}' + '{IceHazardAssetPath}'. " +
                      "Falta a mano: sumarlo al EnemyCatalog / BossFloorManager del piso 2.");
        }

        /// <summary>
        /// Carga el asset o lo crea vacío. Reusar el existente (en vez de borrar y recrear) preserva
        /// su GUID, así que las referencias desde catálogos, prefabs y escenas sobreviven al rebuild.
        /// </summary>
        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                AssetDatabase.Refresh();
            }

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        // ======================================================================
        // Helpers de árbol
        // ======================================================================

        /// <summary>
        /// <c>Selector[node, Wait]</c> — el idiom de "intentá esto; si falla, el turno sigue".
        /// </summary>
        private static AINode_Selector Fallback(AIDecisionNode node)
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode> { node, new AINode_Wait() },
            };
        }

        private static AINode_TelegraphMark BuildMark(ThreatShape shape, int damage)
        {
            return new AINode_TelegraphMark
            {
                Shape = shape,
                Size = MarkSize,
                Damage = damage,
                Kind = AttackKind.BasicAttack,
            };
        }

        private static PcRoundNumber EvenRound()
        {
            return new PcRoundNumber
            {
                Mode = PcRoundNumber.CompareMode.Multiple,
                Value = ParityDivisor,
            };
        }

        /// <summary>
        /// "Ronda impar" = NOT(múltiplo de 2). <see cref="PcRoundNumber"/> no tiene negación propia,
        /// así que se envuelve en un <see cref="PCComposite"/> en modo <c>Not</c> — el concrete que
        /// existe justo para esto.
        /// </summary>
        private static PCComposite OddRound()
        {
            return new PCComposite
            {
                Mode = CompositeMode.Not,
                Children = new List<BasePreCondition> { EvenRound() },
            };
        }
    }
}
