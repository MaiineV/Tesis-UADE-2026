using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Bosses.Generala;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.BossHand;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Editor.Tools.Enemy.Builders;
using Rollgeon.Entities;
using Rollgeon.Grid;
using Rollgeon.PreConditions.Concretes;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Tests del árbol de La Generala construido <b>en memoria</b> por
    /// <see cref="GeneralaAssetBuilder"/> — sin tocar el <see cref="UnityEditor.AssetDatabase"/>, así
    /// el wiring se valida aunque el <c>[MenuItem]</c> todavía no se haya corrido en el proyecto.
    /// </summary>
    [TestFixture]
    public class GeneralaAssetBuilderTests
    {
        private EnemyDataSO _dice;
        private AINode_Sequence _root;

        [SetUp]
        public void SetUp()
        {
            _dice = ScriptableObject.CreateInstance<EnemyDataSO>();
            _root = GeneralaAssetBuilder.BuildAIRoot(_dice);
        }

        [TearDown]
        public void TearDown()
        {
            if (_dice != null) UnityEngine.Object.DestroyImmediate(_dice);
        }

        // ======================================================================
        // Orden del turno
        // ======================================================================

        [Test]
        public void Root_OpensTheTurnByDetonatingTheHand_TheOnlyThingSheLeavesPending()
        {
            // Assert — la mano de la ronda pasada es lo único que hay para cobrar al abrir el
            // turno: desde que el cubilete es melee directo no queda un segundo aviso en cola.
            Assert.IsInstanceOf<AINode_ExecuteTelegraph>(_root.Children[0],
                "El primer hijo tiene que detonar la mano de la ronda pasada.");

            Assert.AreEqual(1, Descendants(_root).OfType<AINode_ExecuteTelegraph>().Count(),
                "Un solo Execute: un segundo aviso pendiente serían dos golpes por una tirada.");
        }

        [Test]
        public void Root_TicksThePhaseGate_BeforeRollingTheHand()
        {
            // Arrange
            int phaseIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_SetHandReroll));
            int rollIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_RollHand));

            // Assert — si el gate quedara después, el reroll de Fase 2 recién aplicaría un turno tarde.
            Assert.Greater(phaseIdx, -1, "No se encontró el gate de Fase 2.");
            Assert.Greater(rollIdx, phaseIdx, "El gate de fase tiene que ir antes de la tirada.");
        }

        [Test]
        public void Root_RefillsTheTable_BeforeRollingTheHand()
        {
            // Arrange — la mano se arma con los dados vivos, así que la mesa se repone antes.
            int spawnIdx = _root.Children.FindIndex(c =>
                Descendants(c).Any(n => n is AINode_SpawnReinforcements));
            int rollIdx = _root.Children.FindIndex(c => Descendants(c).Any(n => n is AINode_RollHand));

            // Assert
            Assert.Greater(spawnIdx, -1, "No se encontró el spawn de la mesa.");
            Assert.Greater(rollIdx, spawnIdx);
        }

        // ======================================================================
        // La mesa
        // ======================================================================

        [Test]
        public void Table_SpawnsFiveDice_AndRefillsEveryFourTurns()
        {
            // Act
            var spawn = Descendants(_root).OfType<AINode_SpawnReinforcements>().FirstOrDefault();

            // Assert
            Assert.IsNotNull(spawn);
            Assert.AreSame(_dice, spawn.EnemyToSpawn, "La mesa tiene que spawnear los dados de la casa.");
            Assert.AreEqual(GeneralaAssetBuilder.HandSize, spawn.Count);
            Assert.AreEqual(GeneralaAssetBuilder.TableRefillTurns, spawn.RespawnDelayTurns);
        }

        [Test]
        public void Table_IsNotWrappedInOnce_SoTheHandComesBack()
        {
            // Arrange — AINode_SpawnReinforcements se auto-gatea y necesita tickear cada turno;
            // envuelto en Once quedaría latcheado y la mesa nunca se repondría.
            var owner = _root.Children.FirstOrDefault(c =>
                Descendants(c).Any(n => n is AINode_SpawnReinforcements));

            // Assert
            Assert.IsNotNull(owner);
            Assert.IsFalse(Descendants(owner).Any(n => n is AINode_Once),
                "El spawn de la mesa no puede ir dentro de un Once — rompe la reposición.");
        }

        [Test]
        public void RiskyNodes_AreIsolatedInSelectorsWithAWaitFallback()
        {
            // Arrange — un Failed suelto en el Sequence raíz le cancela al jefe el resto del turno.
            var risky = _root.Children
                .OfType<AINode_Selector>()
                .Where(s => Descendants(s).Any(n =>
                    n is AINode_SpawnReinforcements
                    || n is AINode_SetHandReroll
                    || n is AINode_GeneralaCupSlam))
                .ToList();

            // Assert
            Assert.AreEqual(3, risky.Count,
                "La mesa, el setup de fase y el cubilete tienen que ir cada uno en su Selector de " +
                "aislamiento — el cubilete devuelve Failed con el jugador lejos, que es lo normal.");
            foreach (var selector in risky)
            {
                Assert.IsTrue(selector.Children.Any(c => c is AINode_Wait),
                    "El Selector de aislamiento necesita un Wait de fallback, si no aborta igual.");
            }
        }

        // ======================================================================
        // La tabla combo → telegraph
        // ======================================================================

        [Test]
        public void HandTable_MapsEveryCategoryToTheSpecdShapeAndDamage()
        {
            // Act + Assert — la ficha, mano por mano.
            AssertHandBranch(Rollgeon.Combos.ComboId.Generala, ThreatShape.ScatteredSquares,
                GeneralaAssetBuilder.GeneralaDamage, size: 3, count: 8);
            AssertHandBranch(Rollgeon.Combos.ComboId.Poker, ThreatShape.SquareAroundPlayer,
                GeneralaAssetBuilder.PokerDamage, size: 2);
            AssertHandBranch(Rollgeon.Combos.ComboId.FullHouse, ThreatShape.ScatteredSquares,
                GeneralaAssetBuilder.FullHouseDamage, size: 3, count: 2);
            AssertHandBranch(Rollgeon.Combos.ComboId.Straight, ThreatShape.Row,
                GeneralaAssetBuilder.LadderDamage, size: 3);
            AssertHandBranch(Rollgeon.Combos.ComboId.Par, ThreatShape.Row,
                GeneralaAssetBuilder.PairDamage, size: 1);
        }

        [Test]
        public void HandTable_HasABustBranch_ThatHurtsLessThanAPair()
        {
            // Act
            var bust = HandBranches()
                .FirstOrDefault(b => b.pc.Match == PcBossHandCombo.HandMatch.NoCombo);

            // Assert
            Assert.IsNotNull(bust.mark, "Falta la rama de bust: fallar del todo también pega.");
            Assert.AreEqual(GeneralaAssetBuilder.BustDamage, bust.mark.Damage);
            Assert.Less(GeneralaAssetBuilder.BustDamage, GeneralaAssetBuilder.PairDamage,
                "El bust tiene que doler menos que un Par.");
        }

        [Test]
        public void HandTable_EveryBranchRequiresAnArmedHand()
        {
            // Assert — sin esto, la Generala recién cantada marcaría el mismo turno y se perdería
            // la ronda extra de aviso.
            foreach (var branch in HandBranches())
                Assert.IsTrue(branch.pc.RequireArmed,
                    $"La rama '{branch.pc.ConditionName}' marca sin exigir mano armada.");
        }

        [Test]
        public void HandTable_EndsInAWait_SoTheCalledHandTurnDoesNotAbortTheSequence()
        {
            // Arrange
            var table = FindHandTable();

            // Assert
            Assert.IsInstanceOf<AINode_Wait>(table.Children.Last(),
                "El turno en que la mano solo se canta no matchea ninguna rama: hace falta el Wait.");
        }

        // ======================================================================
        // El cubilete
        // ======================================================================

        [Test]
        public void CupSlam_HitsForEighteen_AtOneTileInManhattan()
        {
            // Act
            var cup = FindCupSlam();

            // Assert — el alcance es el mismo con el que el jugador le pega a ella (Base Attack:
            // Range 1, Manhattan), así que la regla se lee de una: si le llegás, te llega.
            Assert.AreEqual(18, GeneralaAssetBuilder.CupSlamDamage,
                "La ficha del piso 3 pide melee 18 — el número vive en el builder, no en el nodo.");
            Assert.AreEqual(GeneralaAssetBuilder.CupSlamDamage, cup.Damage);
            Assert.AreEqual(1, cup.Range, "Range 1: solo cobra a quien esté pegado.");
            Assert.AreEqual(DistanceMetric.Manhattan, cup.Metric,
                "Chebyshev le sumaría las diagonales, desde donde el jugador no puede atacarla.");
            Assert.AreEqual(AttackKind.BasicAttack, cup.Kind);
        }

        [Test]
        public void CupSlam_FallsOnEveryRoll_WithoutARoundParityGate()
        {
            // Arrange
            var branch = FindCupBranch();

            // Assert — mientras fue un área avisada en rondas impares había una ronda franca para
            // romperle dados gratis. Ahora el único gate es la distancia, y esa la elige el jugador.
            Assert.AreEqual(1, Descendants(_root).OfType<AINode_GeneralaCupSlam>().Count(),
                "Un solo cubilete en el árbol: dos nodos serían dos golpes por tirada.");
            Assert.IsFalse(Descendants(branch).OfType<PcRoundNumber>().Any(),
                "Nada del cubilete puede colgar del número de ronda.");
            Assert.IsFalse(Descendants(branch).OfType<AINode_If>().Any(),
                "Ni de una condición: el cubilete se auto-gatea por distancia adentro del nodo.");
        }

        [Test]
        public void CupSlam_AnnouncesNothing_TheOnlyWarningIsTheDistance()
        {
            // Arrange
            var branch = FindCupBranch();

            // Assert — un aviso acá significaría que el cubilete volvió a ser un área que se marca
            // un turno y se cobra al siguiente, o sea dos golpes por una sola tirada.
            Assert.IsFalse(Descendants(branch).OfType<AINode_AuxTelegraph>().Any(),
                "El cubilete no ocupa canal de aviso: cobra en el acto.");
            Assert.IsFalse(Descendants(branch).OfType<AINode_TelegraphMark>().Any(),
                "Ni marca área — el único aviso es la distancia, que el jugador controla entera.");
        }

        // ======================================================================
        // Techo de daño
        // ======================================================================

        [Test]
        public void Damage_NeverExceedsTheFloorThreeCeiling()
        {
            // Arrange — techo de daño por golpe del piso 3.
            const int floorThreeCeiling = 45;

            // Act + Assert
            foreach (var mark in Descendants(_root).OfType<AINode_TelegraphMark>())
                Assert.LessOrEqual(mark.Damage, floorThreeCeiling,
                    $"Un TelegraphMark ({mark.Shape}) pega {mark.Damage}, sobre el techo del piso 3.");

            // El cubilete no se avisa, así que su daño es el que más caro sale sostener: entra sí
            // o sí por estar parado al lado.
            foreach (var cup in Descendants(_root).OfType<AINode_GeneralaCupSlam>())
                Assert.LessOrEqual(cup.Damage, floorThreeCeiling,
                    $"El cubilete pega {cup.Damage}, sobre el techo del piso 3.");
        }

        // ======================================================================
        // Fase 2
        // ======================================================================

        [Test]
        public void PhaseTwo_AtFiftyPercent_GivesRerollAndAdoptsTheWeakness_Once()
        {
            // Act
            var gate = Descendants(_root)
                .OfType<AINode_If>()
                .FirstOrDefault(g => g.Conditions != null && g.Conditions.OfType<PcOwnerHpBelow>()
                    .Any(p => Mathf.Abs(p.Percent - GeneralaAssetBuilder.Phase2HpThreshold) < 0.0001f));

            // Assert
            Assert.IsNotNull(gate, "No hay gate de HP al 50%.");
            Assert.IsInstanceOf<AINode_Once>(gate.Then, "El setup de fase corre una sola vez.");

            var reroll = Descendants(gate.Then).OfType<AINode_SetHandReroll>().FirstOrDefault();
            Assert.IsNotNull(reroll, "Fase 2 tiene que darle reroll.");
            Assert.AreEqual(1, reroll.RerollsPerRound, "Un reroll por tirada, como el del jugador.");

            var adopt = Descendants(gate.Then).OfType<AINode_AdoptWeakness>().FirstOrDefault();
            Assert.IsNotNull(adopt, "Fase 2 tiene que copiarle la debilidad al jugador.");
            Assert.AreEqual(GeneralaAssetBuilder.WeaknessMultiplier, adopt.MultiplierOverride, 0.0001f);

            var phase = Descendants(gate.Then).OfType<AINode_ApplyStatModifier>().FirstOrDefault();
            Assert.IsNotNull(phase);
            Assert.AreEqual(2, phase.PhaseIndex);
            Assert.IsTrue(phase.EmitPhaseChangedEvent, "El feedback de Fase 2 se engancha a este evento.");
        }

        // ======================================================================
        // Data del SO
        // ======================================================================

        [Test]
        public void PopulateEnemyData_WritesTheSpecdStatsAndIdentity()
        {
            // Arrange
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                // Act
                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, null);

                // Assert
                Assert.AreEqual(GeneralaAssetBuilder.BossEntityId, boss.EntityId);
                Assert.AreEqual("La Generala", boss.DisplayName);
                Assert.AreEqual(GeneralaAssetBuilder.BossHp, boss.BaseHP);
                Assert.AreEqual(GeneralaAssetBuilder.BossAttack, boss.BaseAttack);
                Assert.AreEqual(Rollgeon.Combos.ComboId.Generala, boss.WeaknessComboId);
                Assert.AreEqual(GeneralaAssetBuilder.WeaknessMultiplier, boss.WeaknessMultiplierOverride, 0.0001f);
                Assert.AreEqual(60, boss.MinGoldDrop, "Oro de jefe de piso 3.");
                Assert.AreEqual(80, boss.MaxGoldDrop);
                Assert.IsInstanceOf<AINode_Sequence>(boss.AIRoot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boss);
            }
        }

        [Test]
        public void PopulateDiceData_MakesObjectsThatDoNotAttack_WithTheSpecdHp()
        {
            // Arrange
            var dice = ScriptableObject.CreateInstance<EnemyDataSO>();
            try
            {
                // Act
                GeneralaAssetBuilder.PopulateDiceData(dice, null);

                // Assert
                Assert.AreEqual(GeneralaAssetBuilder.DiceEntityId, dice.EntityId);
                Assert.AreEqual(GeneralaAssetBuilder.DiceHp, dice.BaseHP);
                Assert.That(GeneralaAssetBuilder.DiceHp, Is.InRange(40, 50),
                    "La ficha pide dados de 40-50 HP: menos y la mesa se desarma de cualquier roce.");
                Assert.AreEqual(0, dice.BaseAttack, "Los dados no pegan: todo el daño entra por la mano.");
                Assert.AreEqual(0, dice.MaxGoldDrop, "Romper un dado paga en categorías, no en oro.");
                Assert.IsInstanceOf<AINode_Wait>(dice.AIRoot,
                    "Sin AIRoot el spawn cae al BasicEnemyAI y el dado atacaría.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dice);
            }
        }

        [Test]
        public void PopulateEnemyData_AssignsTheVisualPrefabAndThePortrait()
        {
            // Arrange
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            var visual = new GameObject("PF_Boss_Generala_Probe");
            var portrait = MakeSprite();
            try
            {
                // Act
                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, visual, portrait);

                // Assert — sin VisualPrefab, EntityVisualService loguea error y no spawnea nada.
                Assert.AreSame(visual, boss.VisualPrefab);
                Assert.AreSame(portrait, boss.Portrait,
                    "El retrato alimenta la cola de turnos y la BossBar por el mismo campo.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boss);
                UnityEngine.Object.DestroyImmediate(visual);
                DestroySprite(portrait);
            }
        }

        [Test]
        public void PopulateData_KeepsTheExistingVisual_WhenNothingIsPassed()
        {
            // Arrange — el builder es re-ejecutable: una corrida sin arte no puede borrar el wiring.
            var boss = ScriptableObject.CreateInstance<EnemyDataSO>();
            var visual = new GameObject("PF_Boss_Generala_Probe");
            var portrait = MakeSprite();
            try
            {
                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, visual, portrait);

                // Act
                GeneralaAssetBuilder.PopulateEnemyData(boss, _dice, null, null);

                // Assert
                Assert.AreSame(visual, boss.VisualPrefab);
                Assert.AreSame(portrait, boss.Portrait);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boss);
                UnityEngine.Object.DestroyImmediate(visual);
                DestroySprite(portrait);
            }
        }

        [Test]
        public void PopulateDiceData_AssignsItsOwnVisualPrefabAndPortrait()
        {
            // Arrange
            var dice = ScriptableObject.CreateInstance<EnemyDataSO>();
            var visual = new GameObject("PF_Obj_DadoCasa_Probe");
            var portrait = MakeSprite();
            try
            {
                // Act
                GeneralaAssetBuilder.PopulateDiceData(dice, visual, portrait);

                // Assert — el dado tiene visual propio: con el del jefe no se leería como dado.
                Assert.AreSame(visual, dice.VisualPrefab);
                Assert.AreSame(portrait, dice.Portrait);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dice);
                UnityEngine.Object.DestroyImmediate(visual);
                DestroySprite(portrait);
            }
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private static Sprite MakeSprite()
        {
            var texture = new Texture2D(4, 4);
            return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null) return;
            var texture = sprite.texture;
            UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
        }

        private AINode_Selector FindHandTable()
        {
            var table = _root.Children.OfType<AINode_Selector>()
                .FirstOrDefault(s => s.Children.OfType<AINode_If>()
                    .Any(i => i.Conditions.OfType<PcBossHandCombo>().Any()));
            Assert.IsNotNull(table, "No se encontró la tabla combo → telegraph.");
            return table;
        }

        private AINode_GeneralaCupSlam FindCupSlam()
        {
            var cup = Descendants(_root).OfType<AINode_GeneralaCupSlam>().FirstOrDefault();
            Assert.IsNotNull(cup, "No se encontró el cubilete en el árbol.");
            return cup;
        }

        /// <summary>Hijo del Sequence raíz que cuelga del cubilete — su Selector de aislamiento.</summary>
        private AIDecisionNode FindCupBranch()
        {
            var branch = _root.Children.FirstOrDefault(c =>
                Descendants(c).Any(n => n is AINode_GeneralaCupSlam));
            Assert.IsNotNull(branch, "No se encontró la rama del cubilete en el Sequence raíz.");
            return branch;
        }

        private List<(PcBossHandCombo pc, AINode_TelegraphMark mark)> HandBranches()
        {
            var result = new List<(PcBossHandCombo pc, AINode_TelegraphMark mark)>();
            foreach (var branch in FindHandTable().Children.OfType<AINode_If>())
            {
                var pc = branch.Conditions.OfType<PcBossHandCombo>().FirstOrDefault();
                var mark = Descendants(branch.Then).OfType<AINode_TelegraphMark>().FirstOrDefault();
                if (pc != null && mark != null) result.Add((pc, mark));
            }
            return result;
        }

        private void AssertHandBranch(string comboId, ThreatShape shape, int damage, int size, int count = -1)
        {
            var branch = HandBranches().FirstOrDefault(b =>
                b.pc.Match == PcBossHandCombo.HandMatch.Combo &&
                string.Equals(b.pc.ComboId, comboId, StringComparison.Ordinal));

            Assert.IsNotNull(branch.mark, $"Falta la rama de '{comboId}'.");
            Assert.AreEqual(shape, branch.mark.Shape, $"Shape equivocada para '{comboId}'.");
            Assert.AreEqual(damage, branch.mark.Damage, $"Daño equivocado para '{comboId}'.");
            Assert.AreEqual(size, branch.mark.Size, $"Size equivocado para '{comboId}'.");
            if (count >= 0)
                Assert.AreEqual(count, branch.mark.Count, $"Cantidad de cuadrados equivocada para '{comboId}'.");
        }

        /// <summary>Tree-walker por reflexión (mismo helper que SunkenGrandPhaseWiringTests).</summary>
        private static List<object> Descendants(object root)
        {
            var all = new List<object>();
            var visited = new HashSet<object>(RefComparer.Instance);

            void Walk(object o)
            {
                if (o == null || o is string || o is UnityEngine.Object) return;

                var type = o.GetType();
                if (type.IsPrimitive || type.IsEnum) return;
                if (!type.IsValueType && !visited.Add(o)) return;

                all.Add(o);

                if (o is IEnumerable enumerable)
                {
                    foreach (var item in enumerable) Walk(item);
                    return;
                }

                if (!(type.Namespace ?? string.Empty).StartsWith("Rollgeon")) return;

                foreach (var field in type.GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    object value;
                    try { value = field.GetValue(o); }
                    catch { continue; }
                    Walk(value);
                }
            }

            Walk(root);
            return all;
        }

        private sealed class RefComparer : IEqualityComparer<object>
        {
            public static readonly RefComparer Instance = new RefComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
