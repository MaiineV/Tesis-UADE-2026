using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Grid;

namespace Rollgeon.EditorTools.Playtest.Tests
{
    /// <summary>
    /// Tests de <see cref="BossBotPolicy"/>: la decisión de cada turno del bot.
    /// </summary>
    /// <remarks>
    /// Lo que protegen: que el índice de behavior salga del <c>ActionName</c> y nunca de una
    /// posición fija (un índice equivocado dispararía otra acción y la corrida entera mentiría
    /// sobre lo que pasó), y que el bot no se quede parado cuando el tile pegado al jefe está
    /// bloqueado — que es exactamente la situación que crea la mesa de La Generala.
    /// </remarks>
    [TestFixture]
    public class BossBotPolicyTests
    {
        private static readonly GridCoord Origin = new GridCoord(0, 0);

        /// <summary>El kit real del Warrior, en su orden real (<c>CH_Warrior.asset</c>).</summary>
        private static List<BotBehaviorSlot> WarriorKit() => new List<BotBehaviorSlot>
        {
            new BotBehaviorSlot(0, "Movement", false, 1),
            new BotBehaviorSlot(1, "ExpMovement", false, 1),
            new BotBehaviorSlot(2, "Base Attack", true, 1),
            new BotBehaviorSlot(3, "Special Attack", true, 2),
            new BotBehaviorSlot(4, "Healing", true, 2),
        };

        private static bool AllOpen(GridCoord _) => true;

        // ---- Resolución por nombre -------------------------------------------

        [Test]
        public void TheAttackIndex_ComesFromTheName_NotAPosition()
        {
            var kit = WarriorKit();

            Assert.AreEqual(2, BossBotPolicy.IndexOf(kit, "Base Attack"));
            Assert.AreEqual(0, BossBotPolicy.IndexOf(kit, "Movement"));
        }

        [Test]
        public void TheIndexSurvivesAReorderedKit()
        {
            // Si mañana el kit se reordena, un índice hardcodeado pegaría un Healing creyendo
            // que ataca y nadie se enteraría mirando las capturas.
            var reordered = new List<BotBehaviorSlot>
            {
                new BotBehaviorSlot(0, "Healing", true, 2),
                new BotBehaviorSlot(1, "Base Attack", true, 1),
                new BotBehaviorSlot(2, "Movement", false, 1),
            };

            var decision = BossBotPolicy.Decide(Origin, new GridCoord(1, 0), reordered, AllOpen);

            Assert.AreEqual(BotActionKind.Attack, decision.Kind);
            Assert.AreEqual(1, decision.BehaviorIndex);
        }

        [Test]
        public void IndexOf_ReturnsMinusOne_WhenAbsent()
        {
            Assert.AreEqual(-1, BossBotPolicy.IndexOf(WarriorKit(), "Teleport"));
            Assert.AreEqual(-1, BossBotPolicy.IndexOf(null, "Movement"));
        }

        // ---- A rango: pega ---------------------------------------------------

        [TestCase(1, 0)]
        [TestCase(0, 1)]
        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        public void OrthogonallyAdjacentToTheBoss_ItAttacks(int dx, int dy)
        {
            var decision = BossBotPolicy.Decide(Origin, new GridCoord(dx, dy), WarriorKit(), AllOpen);

            Assert.AreEqual(BotActionKind.Attack, decision.Kind);
            Assert.AreEqual(2, decision.BehaviorIndex);
        }

        [Test]
        public void DiagonallyAdjacent_IsNotInRange_ItRepositions()
        {
            // Con Chebyshev la esquina contaba como rango 1 y el bot intentaba pegar desde ahí; el
            // juego contestaba "no valid targets" y la corrida terminaba con el jefe intacto.
            var boss = new GridCoord(1, 1);

            var decision = BossBotPolicy.Decide(Origin, boss, WarriorKit(), AllOpen);

            Assert.AreEqual(BotActionKind.Move, decision.Kind);
            Assert.AreEqual(1, BossBotPolicy.Distance(decision.Candidates[0], boss),
                "El destino tiene que quedar ortogonal al jefe, no en diagonal.");
        }

        [Test]
        public void Distance_IsOrthogonalSteps_NotChebyshev()
        {
            Assert.AreEqual(2, BossBotPolicy.Distance(Origin, new GridCoord(1, 1)));
            Assert.AreEqual(1, BossBotPolicy.Distance(Origin, new GridCoord(1, 0)));
        }

        [Test]
        public void WithoutABaseAttack_ItDoesNotInventAnIndex()
        {
            var kit = new List<BotBehaviorSlot> { new BotBehaviorSlot(0, "Movement", false, 1) };

            var decision = BossBotPolicy.Decide(Origin, new GridCoord(1, 0), kit, AllOpen);

            Assert.AreEqual(BotActionKind.None, decision.Kind);
            Assert.AreEqual(-1, decision.BehaviorIndex);
        }

        // ---- Lejos: se acerca -------------------------------------------------

        [Test]
        public void FarFromTheBoss_ItMoves()
        {
            var decision = BossBotPolicy.Decide(Origin, new GridCoord(4, 0), WarriorKit(), AllOpen);

            Assert.AreEqual(BotActionKind.Move, decision.Kind);
            Assert.AreEqual(0, decision.BehaviorIndex);
            CollectionAssert.IsNotEmpty((ICollection)decision.Candidates);
        }

        [Test]
        public void TheBestCandidate_IsInAttackRange()
        {
            var boss = new GridCoord(4, 0);

            var decision = BossBotPolicy.Decide(Origin, boss, WarriorKit(), AllOpen);

            Assert.AreEqual(1, BossBotPolicy.Distance(decision.Candidates[0], boss),
                "El primer destino tiene que dejarlo pegado al jefe: es el que más avanza la pelea.");
        }

        [Test]
        public void CandidatesAreOrdered_NearestToThePlayerFirst()
        {
            // El rango de movimiento puede rechazar el tile más ambicioso; el orden es lo que
            // hace que el fallback sirva de algo.
            var decision = BossBotPolicy.Decide(Origin, new GridCoord(5, 0), WarriorKit(), AllOpen);

            for (int i = 1; i < decision.Candidates.Count; i++)
            {
                Assert.LessOrEqual(
                    BossBotPolicy.Distance(decision.Candidates[i - 1], Origin),
                    BossBotPolicy.Distance(decision.Candidates[i], Origin));
            }
        }

        [Test]
        public void ItNeverTargetsTheBossOwnTile_NorItsOwn()
        {
            var boss = new GridCoord(4, 0);

            var decision = BossBotPolicy.Decide(Origin, boss, WarriorKit(), AllOpen);

            CollectionAssert.DoesNotContain((ICollection)decision.Candidates, boss);
            CollectionAssert.DoesNotContain((ICollection)decision.Candidates, Origin);
        }

        [Test]
        public void WithoutAMovement_ItDoesNotInventAnIndex()
        {
            var kit = new List<BotBehaviorSlot> { new BotBehaviorSlot(0, "Base Attack", true, 1) };

            var decision = BossBotPolicy.Decide(Origin, new GridCoord(4, 0), kit, AllOpen);

            Assert.AreEqual(BotActionKind.None, decision.Kind);
        }

        // ---- La mesa de La Generala ------------------------------------------

        [Test]
        public void WhenTheBossIsWalledIn_ItStillClosesDistance()
        {
            // Los 5 dados ocupan el anillo pegado a ella: ningún tile a rango 1 está libre.
            // Quedarse parado sería el peor resultado posible — el bot nunca llegaría a
            // romper un dado y la corrida no validaría nada.
            var boss = new GridCoord(6, 0);
            bool IsOpen(GridCoord tile) => BossBotPolicy.Distance(tile, boss) != 1;

            var decision = BossBotPolicy.Decide(Origin, boss, WarriorKit(), IsOpen);

            Assert.AreEqual(BotActionKind.Move, decision.Kind);
            Assert.AreEqual(2, BossBotPolicy.Distance(decision.Candidates[0], boss),
                "Si el anillo está tomado, se pega lo más posible: a distancia 2.");
        }

        [Test]
        public void WithEverythingBlocked_ItReportsItInsteadOfPickingAWall()
        {
            var decision = BossBotPolicy.Decide(Origin, new GridCoord(4, 0), WarriorKit(), _ => false);

            Assert.AreEqual(BotActionKind.None, decision.Kind);
            Assert.That(decision.Reason, Does.Contain("no hay tile libre"));
        }

        // ---- Determinismo ----------------------------------------------------

        [Test]
        public void TheSameSituation_GivesTheSameCandidates()
        {
            // Sin esto dos corridas de la misma seed podrían moverse distinto y las imágenes
            // no serían comparables, que es el único motivo para fijar las tiradas.
            var boss = new GridCoord(5, 3);

            var first = BossBotPolicy.Decide(Origin, boss, WarriorKit(), AllOpen);
            var second = BossBotPolicy.Decide(Origin, boss, WarriorKit(), AllOpen);

            CollectionAssert.AreEqual((ICollection)first.Candidates, (ICollection)second.Candidates);
        }

        [Test]
        public void AReasonIsAlwaysProvided_ForTheTurnLog()
        {
            var far = BossBotPolicy.Decide(Origin, new GridCoord(4, 0), WarriorKit(), AllOpen);
            var near = BossBotPolicy.Decide(Origin, new GridCoord(1, 0), WarriorKit(), AllOpen);

            Assert.IsNotEmpty(far.Reason);
            Assert.IsNotEmpty(near.Reason);
        }
    }
}
