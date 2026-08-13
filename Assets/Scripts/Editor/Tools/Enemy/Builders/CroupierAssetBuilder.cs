using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Bosses.Croupier;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.PreConditions;
using Rollgeon.PreConditions.Concretes;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Builders
{
    /// <summary>
    /// Arma por código el jefe de piso 1 <b>El Croupier</b>: su <see cref="EnemyDataSO"/> con el árbol
    /// de AI inline y las dos definiciones de fuego de paño.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Dos mitades a propósito.</b> <see cref="BuildAIRoot"/> y
    /// <see cref="PopulateEnemyData"/> son estáticos puros y no tocan <c>AssetDatabase</c>: los tests
    /// de wiring arman el árbol en memoria y le afirman orden de gates, fallbacks y números sin
    /// depender de que el <c>.asset</c> esté generado ni de que Unity lo haya reimportado. El
    /// <see cref="BuildCroupier"/> del menú es la capa que persiste, y es idempotente: correrlo dos
    /// veces deja exactamente el mismo asset.
    /// </para>
    /// <para>
    /// El <c>VisualPrefab</c> es un <b>placeholder</b> (el pawn del Sunken Grand) hasta que exista arte
    /// propio: el jefe no se mueve nunca, así que sólo necesita algo que se pare en el pasillo.
    /// </para>
    /// </remarks>
    public static class CroupierAssetBuilder
    {
        // ======================================================================
        // Rutas
        // ======================================================================

        public const string BossAssetPath = "Assets/Rollgeon/Enemies/ED_Boss_Croupier.asset";
        public const string FirePhase1Path = "Assets/Rollgeon/Combat/Hazards/HZ_Croupier_TableFire.asset";
        public const string FirePhase2Path = "Assets/Rollgeon/Combat/Hazards/HZ_Croupier_TableFire_Phase2.asset";
        public const string PlaceholderVisualPrefabPath = "Assets/Prefabs/Enemies/SunkedGrand.prefab";

        // ======================================================================
        // Ficha de diseño — todos los números del jefe, en un solo lugar
        // ======================================================================

        public const string EntityId = "boss.croupier";
        public const string DisplayName = "The Croupier";
        public const string WeaknessComboId = "combo.pair";
        public const float WeaknessMultiplier = 1.5f;

        public const int MaxHp = 140;
        public const int Attack = 20;
        public const int Speed = 5;
        public const int MinGoldDrop = 15;
        public const int MaxGoldDrop = 23;

        /// <summary>Daño del sector en fase 1: 20% de la vida del jugador.</summary>
        public const int SectorDamage = 20;

        /// <summary>Daño de cada sector en fase 2 — 24 para quien esté en la columna de costura.</summary>
        public const int SectorDamagePhase2 = 12;

        /// <summary>Represalia de mesa: el precio de correr la rueda en un número impar.</summary>
        public const int RetaliationDamage = 8;

        /// <summary>Fuego de paño: lo que cuesta terminar el turno en el sector que acaba de caer.</summary>
        public const int FireDamage = 6;

        /// <summary>
        /// "Dura 1 turno" = 2 rondas de hazard. El fuego nace en el turno del jefe y el jugador tiene
        /// el primer turno de cada ronda (CNF-006), así que la ronda en la que se enciende ya no tiene
        /// cierres de turno del jugador por delante: con 1 expiraría sin tickear nunca. Ver los remarks
        /// de <see cref="AINode_IgniteDetonatedSectors"/>.
        /// </summary>
        public const int FireDurationRounds = 2;

        /// <summary>"Dura 2 turnos" en fase 2 — mismo corrimiento de +1 ronda que en fase 1.</summary>
        public const int FireDurationRoundsPhase2 = 3;

        /// <summary>Umbral de "Pleno y color".</summary>
        public const float Phase2HpThreshold = 0.5f;

        public const int Phase2NumbersPerTurn = 2;

        /// <summary>Rojo de brasa — se tiene que leer distinto del naranja del telegraph.</summary>
        public static readonly Color FireOverlayTint = new Color(0.85f, 0.10f, 0.05f, 0.60f);

        // Ids fijos y escritos a mano (no Guid.NewGuid): el builder es idempotente, y un id nuevo por
        // corrida haría que el asset cambie en cada build y que un fuego ya activo quede huérfano.
        private const string FirePhase1SourceId = "c0a17e11-0001-4c00-b001-6f75706965f1";
        private const string FirePhase2SourceId = "c0a17e11-0002-4c00-b002-6f75706965f2";

        // ======================================================================
        // Menú
        // ======================================================================

        [MenuItem("Tools/Rollgeon/Bosses/Build Croupier")]
        public static void BuildCroupier()
        {
            var fire = BuildFireDefinition(FirePhase1Path, FireDurationRounds, FirePhase1SourceId);
            var firePhase2 = BuildFireDefinition(FirePhase2Path, FireDurationRoundsPhase2, FirePhase2SourceId);

            var boss = LoadOrCreate<EnemyDataSO>(BossAssetPath);
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderVisualPrefabPath);
            if (visual == null)
            {
                Debug.LogWarning($"[CroupierAssetBuilder] No se encontró el prefab placeholder en " +
                                 $"'{PlaceholderVisualPrefabPath}' — el jefe queda sin VisualPrefab.");
            }

            PopulateEnemyData(boss, fire, firePhase2, visual);

            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CroupierAssetBuilder] Listo: '{BossAssetPath}' + fuego de paño (fase 1 y 2).");
            Selection.activeObject = boss;
        }

        // ======================================================================
        // Datos del jefe (puro — sin AssetDatabase)
        // ======================================================================

        /// <summary>
        /// Escribe la ficha completa del Croupier en <paramref name="data"/>, incluido su
        /// <see cref="EnemyDataSO.AIRoot"/>. No toca <c>AssetDatabase</c>: sirve igual para el asset
        /// real y para una instancia in-memory de test.
        /// </summary>
        public static void PopulateEnemyData(
            EnemyDataSO data, HazardDefinitionSO fire, HazardDefinitionSO firePhase2, GameObject visualPrefab)
        {
            if (data == null) return;

            data.EntityId = EntityId;
            data.DisplayName = DisplayName;
            data.Description =
                "\"Place your bets.\" He calls one number per turn, and that number is two things at " +
                "once: the block of the table that falls next turn and the die he confiscates from " +
                "your bag. Hitting him while the number hangs in the air spins the wheel one step " +
                "further — and on odd numbers the house charges you 8 for the privilege. He never " +
                "leaves the middle row.";

            data.WeaknessComboId = WeaknessComboId;
            data.WeaknessMultiplierOverride = WeaknessMultiplier;

            data.BaseHP = MaxHp;
            data.BaseAttack = Attack;
            data.BaseSpeed = Speed;
            data.MaxEnergy = 3;
            data.BaseAttackRange = 1;

            // No cura ni tiene behaviors de curación: dejar 0 evita autorar un número que miente.
            data.BaseHealStrength = 0;

            data.MinGoldDrop = MinGoldDrop;
            data.MaxGoldDrop = MaxGoldDrop;
            data.VisualPrefab = visualPrefab;

            // Sin behaviors: el Croupier no tiene melee ni rango. Su único daño directo es la
            // Represalia, y esa entra por el hook de daño de la rueda, no por el árbol.
            data.Behaviors = new List<BaseBehavior>();
            data.ExtraTiers = new List<EnemyTier>();

            data.AIRoot = BuildAIRoot(fire, firePhase2);
        }

        /// <summary>
        /// Árbol del Croupier. Sequence raíz de seis pasos, sin un solo nodo que lo mueva de casilla.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Orden.</b> Detonar va primero (resuelve lo cantado el turno pasado, como
        /// <c>ExecuteTelegraph</c>). El gate de fase va <b>antes</b> del marcado, que es el "ataque" de
        /// este jefe: en el path no-coroutine un <c>Running</c> aborta el Sequence, y una fase ubicada
        /// después del ataque no tickearía nunca en tests ni en simulación.
        /// </para>
        /// <para>
        /// <b>Cada paso que puede fallar va en <c>Selector[paso, Wait]</c>.</b> El Sequence corta en el
        /// primer <c>Failed</c>: sin el fallback, una sala sin bounds (marcado) o un servicio no
        /// registrado (confiscación, fuego) le cancelaría al jefe todo lo que viene después en el
        /// turno.
        /// </para>
        /// </remarks>
        public static AINode_Sequence BuildAIRoot(HazardDefinitionSO fire, HazardDefinitionSO firePhase2)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // 1. Detona lo que cantó el turno pasado. Siempre Succeeded — "nada marcado"
                    //    (turno 1) o "el jugador se fue del sector" no son fallos.
                    new AINode_DetonateSungSectors(),

                    // 2. Pleno y color, una sola vez al cruzar el 50%.
                    Guarded(new AINode_If
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
                                    // La fase no sube el daño: sólo anuncia (feedback + diálogo).
                                    new AINode_ApplyStatModifier
                                    {
                                        AttackDelta = 0,
                                        SpeedDelta = 0,
                                        PhaseIndex = 2,
                                        EmitPhaseChangedEvent = true,
                                    },
                                    new AINode_SetWheelMode
                                    {
                                        NumbersPerTurn = Phase2NumbersPerTurn,
                                        Rigged = true,
                                        PhaseIndex = 2,
                                    },
                                },
                            },
                        },
                        Else = new AINode_Wait(),
                    }),

                    // 3. Canta el número (o los dos) y abre el windup.
                    Guarded(new AINode_SpinWheel { RetaliationDamage = RetaliationDamage }),

                    // 4. Confisca el dado de ese mismo número — el sector y el dado son el mismo dato.
                    Guarded(new AINode_RotateBlock
                    {
                        Target = AINode_RotateBlock.BlockTarget.Dice,
                        Count = 1,
                        DirectedIndex = new AIReadCroupierWheelNumber { Slot = 0 },
                    }),

                    // 5. Marca el/los sector(es) cantados: el "ataque" telegrafiado del jefe.
                    Guarded(new AINode_MarkSungSectors
                    {
                        SectorDamage = SectorDamage,
                        SectorDamagePhase2 = SectorDamagePhase2,
                        Kind = AttackKind.BasicAttack,
                    }),

                    // 6. El sector que detonó este turno queda en llamas.
                    Guarded(new AINode_IgniteDetonatedSectors
                    {
                        Fire = fire,
                        FirePhase2 = firePhase2,
                        BlastConsumesFlame = true,
                    }),
                },
            };
        }

        /// <summary>
        /// Envuelve un paso del turno en <c>Selector[paso, Wait]</c> — el idiom de aislamiento de
        /// fallos que ya usa el árbol del Sunken Grand.
        /// </summary>
        private static AINode_Selector Guarded(AIDecisionNode step)
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode> { step, new AINode_Wait() },
            };
        }

        // ======================================================================
        // Hazards
        // ======================================================================

        /// <summary>
        /// Crea/actualiza una definición de fuego de paño. Dos assets (uno por fase) y no un campo del
        /// nodo porque <see cref="IHazardService"/> toma la duración de la definición al activar:
        /// cambiarla desde el nodo pediría tocar el servicio, que es fundación compartida.
        /// </summary>
        public static HazardDefinitionSO BuildFireDefinition(string path, int durationRounds, string sourceId)
        {
            var fire = LoadOrCreate<HazardDefinitionSO>(path);

            fire.Trigger = HazardTriggerMode.OnTurnEndInTile;
            fire.Damage = FireDamage;
            fire.Kind = AttackKind.Environmental;
            fire.DurationRounds = durationRounds;
            fire.ConsumeOnTrigger = false; // El fuego quema el bloque entero, no una casilla y se apaga.
            fire.OverlayTint = FireOverlayTint;
            fire.SourceId = sourceId;

            // El área real la pasa el nodo de ignición (el sector que detonó). Shape queda declarativa:
            // el overload con tiles la ignora, pero deja dicho en el asset de qué forma es el fuego.
            fire.Shape = ThreatShape.RoomSector;
            fire.Size = 1;
            fire.CycleRounds = 1;

            EditorUtility.SetDirty(fire);
            return fire;
        }

        // ======================================================================
        // Helpers de AssetDatabase
        // ======================================================================

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

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
