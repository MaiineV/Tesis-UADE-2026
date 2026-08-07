using System.Text.RegularExpressions;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.PreConditions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests estructurales de los nodos del AI tree (sin attributes/grid/damage reales).
    /// </summary>
    [TestFixture]
    public class AIDecisionNodeTests
    {
        // ---- Action nodes ------------------------------------------------

        [Test]
        public void Wait_AlwaysSucceeds()
        {
            var node = new AINode_Wait();
            Assert.AreEqual(AIResult.Succeeded, node.Tick(new AIContext()));
        }

        // ---- Sequence ----------------------------------------------------

        [Test]
        public void Sequence_AllSucceed_ReturnsSucceeded()
        {
            var node = new AINode_Sequence
            {
                Children = { new AINode_Wait(), new AINode_Wait(), new AINode_Wait() }
            };
            Assert.AreEqual(AIResult.Succeeded, node.Tick(new AIContext()));
        }

        [Test]
        public void Sequence_ShortCircuitsOnFailure()
        {
            var counter = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_Sequence
            {
                Children =
                {
                    counter,
                    new CountingNode { ResultToReturn = AIResult.Failed },
                    counter
                }
            };
            Assert.AreEqual(AIResult.Failed, node.Tick(new AIContext()));
            Assert.AreEqual(1, counter.TickCount, "Tercer hijo NO debe correr tras Failed.");
        }

        // ---- Selector ----------------------------------------------------

        [Test]
        public void Selector_AllFail_ReturnsFailed()
        {
            var node = new AINode_Selector
            {
                Children =
                {
                    new CountingNode { ResultToReturn = AIResult.Failed },
                    new CountingNode { ResultToReturn = AIResult.Failed }
                }
            };
            Assert.AreEqual(AIResult.Failed, node.Tick(new AIContext()));
        }

        [Test]
        public void Selector_ShortCircuitsOnSuccess()
        {
            var tail = new CountingNode { ResultToReturn = AIResult.Failed };
            var node = new AINode_Selector
            {
                Children =
                {
                    new CountingNode { ResultToReturn = AIResult.Failed },
                    new CountingNode { ResultToReturn = AIResult.Succeeded },
                    tail
                }
            };
            Assert.AreEqual(AIResult.Succeeded, node.Tick(new AIContext()));
            Assert.AreEqual(0, tail.TickCount, "Tras el success del 2do hijo, el 3ro NO debe correr.");
        }

        // ---- If ----------------------------------------------------------

        [Test]
        public void If_AllConditionsTrue_TakesThenBranch()
        {
            var thenNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var elseNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_If
            {
                Conditions =
                {
                    new ConstPC { Value = true },
                    new ConstPC { Value = true },
                },
                Then = thenNode,
                Else = elseNode,
            };
            node.Tick(new AIContext());
            Assert.AreEqual(1, thenNode.TickCount);
            Assert.AreEqual(0, elseNode.TickCount);
        }

        [Test]
        public void If_AnyConditionFalse_TakesElseBranch()
        {
            var thenNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var elseNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_If
            {
                Conditions =
                {
                    new ConstPC { Value = true },
                    new ConstPC { Value = false },
                },
                Then = thenNode,
                Else = elseNode,
            };
            node.Tick(new AIContext());
            Assert.AreEqual(0, thenNode.TickCount);
            Assert.AreEqual(1, elseNode.TickCount);
        }

        [Test]
        public void If_EmptyConditions_TreatsAsTrue()
        {
            // Plan: List<BasePreCondition> empty/null sigue la semántica AND-empty=true
            // de BasePreCondition.EvaluateAll. Diferencia con el viejo AICondition que
            // trataba null como "false → else".
            var thenNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var elseNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_If { Then = thenNode, Else = elseNode };
            node.Tick(new AIContext());
            Assert.AreEqual(1, thenNode.TickCount);
            Assert.AreEqual(0, elseNode.TickCount);
        }

        [Test]
        public void If_NullContext_ReturnsFailed()
        {
            var node = new AINode_If { Then = new CountingNode(), Else = new CountingNode() };
            Assert.AreEqual(AIResult.Failed, node.Tick(null));
        }

        // ---- Random ------------------------------------------------------

        [Test]
        public void Random_WithSeededRng_IsDeterministic()
        {
            var a = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var b = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_Random
            {
                Options =
                {
                    new AINode_Random.Option { Weight = 1f, Node = a },
                    new AINode_Random.Option { Weight = 1f, Node = b },
                }
            };
            var ctx = new AIContext { Rng = new System.Random(42) };
            node.Tick(ctx);
            node.Tick(ctx);
            node.Tick(ctx);
            Assert.AreEqual(3, a.TickCount + b.TickCount);
        }

        [Test]
        public void Random_WeightedBias_FavorsHigherWeight()
        {
            var rare = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var common = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_Random
            {
                Options =
                {
                    new AINode_Random.Option { Weight = 1f, Node = rare },
                    new AINode_Random.Option { Weight = 99f, Node = common },
                }
            };
            var ctx = new AIContext { Rng = new System.Random(7) };
            for (int i = 0; i < 200; i++) node.Tick(ctx);
            Assert.Greater(common.TickCount, rare.TickCount * 5, "Weight 99:1 debe favorecer fuerte al común.");
        }

        [Test]
        public void Random_EmptyOptions_ReturnsFailed()
        {
            var node = new AINode_Random();
            Assert.AreEqual(AIResult.Failed, node.Tick(new AIContext()));
        }

        // ---- While -------------------------------------------------------

        [Test]
        public void While_ConditionFalseInitially_ReturnsSucceededWithZeroTicks()
        {
            // Arrange
            var body = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_While
            {
                Conditions = { new ConstPC { Value = false } },
                Body = body,
                MaxIterations = 16,
            };

            // Act
            var result = node.Tick(new AIContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(0, body.TickCount);
        }

        [Test]
        public void While_RunsBodyUntilConditionFlipsFalse()
        {
            // Arrange — condition pasa 3 veces y luego falla
            var body = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var stateful = new StatefulPC { TrueForFirstNCalls = 3 };
            var node = new AINode_While
            {
                Conditions = { stateful },
                Body = body,
                MaxIterations = 16,
            };

            // Act
            var result = node.Tick(new AIContext());

            // Assert
            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(3, body.TickCount);
        }

        [Test]
        public void While_ConditionStaysTrue_HitsCapAndReturnsFailed()
        {
            // Arrange
            var body = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_While
            {
                Conditions = { new ConstPC { Value = true } },
                Body = body,
                MaxIterations = 5,
            };
            LogAssert.Expect(LogType.Warning, new Regex(".*MaxIterations.*"));

            // Act
            var result = node.Tick(new AIContext());

            // Assert — cap señala bug de configuración → Failed (regression guard)
            Assert.AreEqual(AIResult.Failed, result);
            Assert.AreEqual(5, body.TickCount);
        }

        [Test]
        public void While_BodyFailsMidLoop_ReturnsFailed()
        {
            // Arrange
            var body = new CountingNode { ResultToReturn = AIResult.Failed };
            var node = new AINode_While
            {
                Conditions = { new ConstPC { Value = true } },
                Body = body,
                MaxIterations = 16,
            };

            // Act
            var result = node.Tick(new AIContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result);
            Assert.AreEqual(1, body.TickCount, "Loop debe cortar inmediatamente cuando el body falla.");
        }

        [Test]
        public void While_NullContext_ReturnsFailed()
        {
            // Arrange
            var node = new AINode_While { Body = new CountingNode() };

            // Act + Assert
            Assert.AreEqual(AIResult.Failed, node.Tick(null));
        }

        [Test]
        public void While_EmptyConditions_IteratesUntilCapAndReturnsFailed()
        {
            // Arrange — lista vacía = AND-empty = true (semántica permisiva). Cap es el único safeguard.
            var body = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_While
            {
                Body = body,
                MaxIterations = 3,
            };
            LogAssert.Expect(LogType.Warning, new Regex(".*MaxIterations.*"));

            // Act
            var result = node.Tick(new AIContext());

            // Assert
            Assert.AreEqual(AIResult.Failed, result);
            Assert.AreEqual(3, body.TickCount);
        }

        [Test]
        public void While_NullBody_ReturnsSucceededWithoutLooping()
        {
            // Arrange
            var node = new AINode_While
            {
                Conditions = { new ConstPC { Value = true } },
                Body = null,
                MaxIterations = 5,
            };

            // Act
            var result = node.Tick(new AIContext());

            // Assert — sin body el loop no tiene nada que ejecutar; sale limpio
            Assert.AreEqual(AIResult.Succeeded, result);
        }

        // ---- Fakes -------------------------------------------------------

        private sealed class CountingNode : AIDecisionNode
        {
            public AIResult ResultToReturn = AIResult.Succeeded;
            public int TickCount;
            public override AIResult Tick(AIContext context)
            {
                TickCount++;
                return ResultToReturn;
            }
        }

        private sealed class ConstPC : BasePreCondition
        {
            public bool Value;
            public override string ConditionName => $"Const({Value})";
            public override bool Evaluate(PreConditionContext context) => Value;
        }

        /// <summary>Returns true for the first N Evaluate calls, then false. Used by While tests.</summary>
        private sealed class StatefulPC : BasePreCondition
        {
            public int TrueForFirstNCalls;
            private int _calls;
            public override string ConditionName => $"StatefulPC({TrueForFirstNCalls})";
            public override bool Evaluate(PreConditionContext context) => _calls++ < TrueForFirstNCalls;
        }
    }
}
