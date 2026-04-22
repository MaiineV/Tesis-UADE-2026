using NUnit.Framework;
using Rollgeon.Combat.AI.Conditions;
using Rollgeon.Combat.AI.Decisions;

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
        public void If_EvaluatesThenBranchOnTrue()
        {
            var thenNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var elseNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_If
            {
                Condition = new ConstCondition { Value = true },
                Then = thenNode,
                Else = elseNode,
            };
            node.Tick(new AIContext());
            Assert.AreEqual(1, thenNode.TickCount);
            Assert.AreEqual(0, elseNode.TickCount);
        }

        [Test]
        public void If_EvaluatesElseBranchOnFalse()
        {
            var thenNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var elseNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_If
            {
                Condition = new ConstCondition { Value = false },
                Then = thenNode,
                Else = elseNode,
            };
            node.Tick(new AIContext());
            Assert.AreEqual(0, thenNode.TickCount);
            Assert.AreEqual(1, elseNode.TickCount);
        }

        [Test]
        public void If_NullCondition_TreatsAsFalse()
        {
            var thenNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var elseNode = new CountingNode { ResultToReturn = AIResult.Succeeded };
            var node = new AINode_If { Condition = null, Then = thenNode, Else = elseNode };
            node.Tick(new AIContext());
            Assert.AreEqual(0, thenNode.TickCount);
            Assert.AreEqual(1, elseNode.TickCount);
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

        private sealed class ConstCondition : AICondition
        {
            public bool Value;
            public override bool Evaluate(AIContext context) => Value;
        }
    }
}
