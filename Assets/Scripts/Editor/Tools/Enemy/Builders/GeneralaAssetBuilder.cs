using System.Collections.Generic;
using System.IO;
using Rollgeon.Combat.AI.Decisions;
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
    /// Builder idempotente de La Generala (jefe del piso 3) y de los cinco dados de su mesa.
    /// <see cref="BuildAIRoot"/> y <see cref="PopulateEnemyData"/> son estáticos puros — se testean
    /// en memoria sin tocar el <see cref="AssetDatabase"/>; el <c>[MenuItem]</c> es el único que
    /// escribe assets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>El jefe.</b> Cinco dados propios sobre la mesa (objetos de 4 HP). Cada turno tira los que
    /// le queden vivos, los corre por el mismo detector de combos que la mano del jugador, y el
    /// combo que sale <b>es</b> el ataque: la Escalera una franja, el Full dos áreas, el Póker un
    /// 5×5, la Generala casi toda la sala. Romperle un dado le borra una categoría.
    /// </para>
    /// <para>
    /// <b>El cubilete.</b> Los turnos impares baja el cubilete: 3×3 alrededor suyo, 12 de daño. Es
    /// el peaje de estar rompiéndole la mano, y va por un canal de telegraph secundario
    /// (<see cref="AINode_AuxTelegraph"/>) porque <c>IThreatenedAreaService</c> guarda una sola
    /// marca por fuente y la mano ya usa la principal.
    /// </para>
    /// </remarks>
    public static class GeneralaAssetBuilder
    {
        private const string LogPrefix = "[GeneralaAssetBuilder] ";

        private const string EnemiesFolder = "Assets/Rollgeon/Enemies";
        public const string BossAssetPath = EnemiesFolder + "/ED_Boss_Generala.asset";
        public const string DiceAssetPath = EnemiesFolder + "/ED_Obj_DadoCasa.asset";

        /// <summary>Placeholder hasta que exista el arte propio del jefe.</summary>
        private const string PlaceholderPrefabPath = "Assets/Prefabs/Enemies/GeneralDirector.prefab";

        public const string BossEntityId = "boss.la_generala";
        public const string DiceEntityId = "obj.dado_casa";

        /// <summary>Canal del telegraph secundario del cubilete. Compartido por Mark y Execute.</summary>
        public const string CupChannelId = "generala.cubilete";

        // ---- Números de la ficha ------------------------------------------------------

        public const int BossHp = 250;
        public const int BossAttack = 40;
        public const int DiceHp = 4;
        public const int HandSize = 5;

        /// <summary>Turnos del boss que tarda en reponer la mesa entera.</summary>
        public const int TableRefillTurns = 4;

        public const int BustDamage = 18;
        public const int PairDamage = 25;
        public const int LadderDamage = 45;
        public const int FullHouseDamage = 20;
        public const int PokerDamage = 45;

        /// <summary>
        /// Daño de la mano grande. <b>La ficha pide 65, pero el techo de daño por golpe del piso 3
        /// es 45</b> — se clampea acá y queda pendiente que diseño confirme cuál gana. Lo que hace
        /// grande a la mano igual se mantiene: ocho cuadrados de 3×3 y una ronda extra de aviso.
        /// </summary>
        public const int GeneralaDamage = 45;

        /// <summary>Daño del cubilete (peaje de la mesa, turnos impares).</summary>
        public const int CupTollDamage = 12;

        public const float Phase2HpThreshold = 0.5f;
        public const float WeaknessMultiplier = 1.5f;

        private const int MinGold = 60;
        private const int MaxGold = 80;

        // ======================================================================
        // MenuItem
        // ======================================================================

        [MenuItem("Tools/Rollgeon/Bosses/Build Generala")]
        public static void Run()
        {
            var placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPrefabPath);
            if (placeholder == null)
                Debug.LogWarning(LogPrefix + $"No se encontró el prefab placeholder en '{PlaceholderPrefabPath}' — " +
                                 "el VisualPrefab queda vacío y hay que asignarlo a mano.");

            var dice = LoadOrCreate<EnemyDataSO>(DiceAssetPath);
            PopulateDiceData(dice, placeholder);
            EditorUtility.SetDirty(dice);

            var boss = LoadOrCreate<EnemyDataSO>(BossAssetPath);
            PopulateEnemyData(boss, dice, placeholder);
            EditorUtility.SetDirty(boss);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(LogPrefix + $"Listo: '{BossAssetPath}' ({BossHp} HP) + '{DiceAssetPath}' " +
                      $"({HandSize} × {DiceHp} HP). Re-ejecutable sin duplicar nada.");
        }

        // ======================================================================
        // Data (puro — testeable sin assets)
        // ======================================================================

        /// <summary>
        /// Escribe identidad, stats, recompensa y árbol de La Generala sobre
        /// <paramref name="boss"/>. <paramref name="diceObject"/> es el <see cref="EnemyDataSO"/> de
        /// los dados de la mesa (puede ser null en tests que no miren el spawn).
        /// </summary>
        public static void PopulateEnemyData(EnemyDataSO boss, EnemyDataSO diceObject, GameObject visualPrefab)
        {
            if (boss == null) return;

            boss.EntityId = BossEntityId;
            boss.DisplayName = "La Generala";
            boss.Description =
                "The house playing your own game. Five dice of her own on the table, the same combo " +
                "sheet you use, and one hand per round. Her roll is public before it detonates: you " +
                "see the five numbers and you know what is coming. Break a die and you erase a " +
                "category — with four she cannot roll Generala, with three she loses Poker. Walking " +
                "up to the table is not free: on odd turns the dice cup comes down around her.";

            boss.BaseHP = BossHp;
            boss.BaseAttack = BossAttack;
            boss.BaseSpeed = 4;
            boss.MaxEnergy = 3;
            boss.BaseAttackRange = 1;
            boss.BaseHealStrength = 0;

            // Base: débil a la Generala. En Fase 2 AINode_AdoptWeakness la repunta al combo que el
            // jugador más viene usando, con el mismo multiplicador.
            boss.WeaknessComboId = Rollgeon.Combos.ComboId.Generala;
            boss.WeaknessMultiplierOverride = WeaknessMultiplier;

            boss.MinGoldDrop = MinGold;
            boss.MaxGoldDrop = MaxGold;

            if (visualPrefab != null) boss.VisualPrefab = visualPrefab;

            boss.AIRoot = BuildAIRoot(diceObject);
        }

        /// <summary>
        /// Escribe los dados de la mesa: objetos de <see cref="DiceHp"/> HP que no atacan ni se
        /// mueven. Existen para ser rotos — cada uno que cae le borra una categoría a la mano.
        /// </summary>
        public static void PopulateDiceData(EnemyDataSO dice, GameObject visualPrefab)
        {
            if (dice == null) return;

            dice.EntityId = DiceEntityId;
            dice.DisplayName = "Dado de la Casa";
            dice.Description =
                "One of the five dice the house rolls. Four health, no attack: it just sits on the " +
                "table being part of her hand until you walk over and break it.";

            dice.BaseHP = DiceHp;
            dice.BaseAttack = 0;
            dice.BaseSpeed = 1;
            dice.MaxEnergy = 0;
            dice.BaseAttackRange = 0;
            dice.BaseHealStrength = 0;

            dice.WeaknessComboId = string.Empty;
            dice.WeaknessMultiplierOverride = 0f;

            // Los dados no dropean oro: el premio de romperlos es la categoría que le borrás.
            dice.MinGoldDrop = 0;
            dice.MaxGoldDrop = 0;

            if (visualPrefab != null) dice.VisualPrefab = visualPrefab;

            // AIRoot explícito: sin árbol el spawn cae al BasicEnemyAI, que ataca siempre — un dado
            // que le pega al jugador rompe la lectura de "todo el daño entra por la mano".
            dice.AIRoot = new AINode_Wait();
        }

        // ======================================================================
        // Árbol
        // ======================================================================

        /// <summary>
        /// Árbol de decisión del jefe. Orden del turno: cobra los dos avisos pendientes, corre el
        /// gate de fase, repone la mesa, tira la mano, marca el área del combo y cierra con el
        /// cubilete si el turno es impar.
        /// </summary>
        public static AINode_Sequence BuildAIRoot(EnemyDataSO diceObject)
        {
            return new AINode_Sequence
            {
                Children = new List<AIDecisionNode>
                {
                    // 1. La mano de la ronda pasada explota con la forma del combo que le salió.
                    new AINode_ExecuteTelegraph(),

                    // 2. Y el cubilete de la ronda pasada cobra su peaje, por su propio canal.
                    new AINode_AuxTelegraph
                    {
                        Step = AINode_AuxTelegraph.TelegraphStep.Execute,
                        ChannelId = CupChannelId,
                    },

                    // 3. Fase 2 ANTES del ataque, para que el reroll aplique en el mismo turno en
                    //    que cruza el umbral. En Selector[gate, Wait] para que un fallo del setup
                    //    (sin ComboLog, sin registry) no le cancele el turno.
                    Isolate(BuildPhaseTwoGate()),

                    // 4. La mesa: cinco dados, reposición completa cada TableRefillTurns turnos.
                    //    Sin Once — el nodo se auto-gatea y necesita tickear para reponer.
                    Isolate(new AINode_SpawnReinforcements
                    {
                        EnemyToSpawn = diceObject,
                        Count = HandSize,
                        RespawnDelayTurns = TableRefillTurns,
                    }),

                    // 5. Tira los dados vivos y canta el combo (público un turno antes de detonar).
                    new AINode_RollHand
                    {
                        SizeSource = AINode_RollHand.HandSizeSource.AliveAllies,
                        MaxDice = HandSize,
                        DieFaces = 6,
                        SlowCombos = new List<string> { Rollgeon.Combos.ComboId.Generala },
                    },

                    // 6. La tabla combo → telegraph. Es data: cambiar cuánto pega una mano es
                    //    editar el TelegraphMark de su rama, no tocar código.
                    BuildHandTelegraphTable(),

                    // 7. El cubilete: turnos impares. PcRoundNumber sabe de múltiplos, no de
                    //    paridad, así que "impar" es el Else de If(múltiplo de 2) — mismo compás
                    //    que el lápiz del Anotador.
                    BuildCupTollGate(),
                },
            };
        }

        /// <summary>
        /// Selector con una rama por categoría, de la mano más alta a la más baja, y un
        /// <see cref="AINode_Wait"/> al final. Ese Wait es el que cubre el turno de la mano
        /// <i>cantada</i> (Generala recién tirada): ninguna rama matchea porque todas piden la mano
        /// armada, y el turno tiene que seguir igual.
        /// </summary>
        private static AINode_Selector BuildHandTelegraphTable()
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode>
                {
                    // Casi toda la sala salvo el anillo del borde: ocho cuadrados de 3×3 anclados
                    // en el 50% central, que es cómo ScatteredSquares reparte por construcción.
                    HandBranch(Rollgeon.Combos.ComboId.Generala, new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.ScatteredSquares,
                        Count = 8,
                        Size = 3,
                        Damage = GeneralaDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    HandBranch(Rollgeon.Combos.ComboId.Poker, new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.SquareAroundPlayer,
                        Size = 2, // radio 2 ⇒ 5×5 sobre el jugador
                        Damage = PokerDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    HandBranch(Rollgeon.Combos.ComboId.FullHouse, new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.ScatteredSquares,
                        Count = 2,
                        Size = 3,
                        Damage = FullHouseDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    HandBranch(Rollgeon.Combos.ComboId.Straight, new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.Row,
                        Size = 3,
                        Damage = LadderDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    HandBranch(Rollgeon.Combos.ComboId.Par, new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.Row,
                        Size = 1,
                        Damage = PairDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    // El bust: fallar del todo duele menos que un par.
                    BustBranch(new AINode_TelegraphMark
                    {
                        Shape = ThreatShape.Row,
                        Size = 1,
                        Damage = BustDamage,
                        Kind = AttackKind.BasicAttack,
                    }),

                    new AINode_Wait(),
                },
            };
        }

        /// <summary>
        /// <c>If(mano == comboId) → mark</c>, sin <c>Else</c>: el <see cref="AINode_If"/> devuelve
        /// Failed cuando la rama elegida es null, que es justo lo que hace avanzar al Selector a la
        /// categoría siguiente.
        /// </summary>
        private static AINode_If HandBranch(string comboId, AIDecisionNode mark)
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcBossHandCombo
                    {
                        Match = PcBossHandCombo.HandMatch.Combo,
                        ComboId = comboId,
                        RequireArmed = true,
                    },
                },
                Then = mark,
            };
        }

        private static AINode_If BustBranch(AIDecisionNode mark)
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcBossHandCombo
                    {
                        Match = PcBossHandCombo.HandMatch.NoCombo,
                        RequireArmed = true,
                    },
                },
                Then = mark,
            };
        }

        /// <summary>
        /// Turnos impares ⇒ cubilete. El <c>Then</c> (ronda par) es un Wait real, si no el If
        /// devolvería Failed y abortaría el Sequence del turno.
        /// </summary>
        private static AINode_If BuildCupTollGate()
        {
            return new AINode_If
            {
                Conditions = new List<BasePreCondition>
                {
                    new PcRoundNumber
                    {
                        Mode = PcRoundNumber.CompareMode.Multiple,
                        Value = 2,
                    },
                },
                Then = new AINode_Wait(),
                Else = Isolate(new AINode_AuxTelegraph
                {
                    Step = AINode_AuxTelegraph.TelegraphStep.Mark,
                    ChannelId = CupChannelId,
                    Shape = ThreatShape.SquareAroundSelf,
                    Size = 1, // radio 1 ⇒ 3×3 alrededor suyo
                    Damage = CupTollDamage,
                    Kind = AttackKind.BasicAttack,
                }),
            };
        }

        /// <summary>
        /// Fase 2 al 50%: le dan reroll, adopta como debilidad el combo que el jugador más usa, y
        /// emite el cambio de fase para el feedback. <see cref="AINode_Once"/> porque es setup, no
        /// un efecto por turno.
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
                            new AINode_SetHandReroll { RerollsPerRound = 1 },
                            new AINode_AdoptWeakness
                            {
                                LogWindow = 8,
                                MultiplierOverride = WeaknessMultiplier,
                            },
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
                // Sin Else: en Fase 1 el If devuelve Failed y lo absorbe el Selector de Isolate.
            };
        }

        /// <summary>
        /// Envuelve un nodo que puede devolver Failed en <c>Selector[node, Wait]</c>: sin esto un
        /// fallo suyo (sin tiles de borde libres, servicio ausente) le cancelaría al jefe el resto
        /// del turno — el ataque incluido.
        /// </summary>
        private static AINode_Selector Isolate(AIDecisionNode node)
        {
            return new AINode_Selector
            {
                Children = new List<AIDecisionNode> { node, new AINode_Wait() },
            };
        }

        // ======================================================================
        // AssetDatabase helpers
        // ======================================================================

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
