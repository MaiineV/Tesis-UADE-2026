using System;
using NUnit.Framework;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Conditions;
using Rollgeon.Grid;

namespace Rollgeon.Combat.AI.Tests
{
    [TestFixture]
    public class AIConditionTests
    {
        private AttributesManager _attrs;
        private Guid _self;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            _attrs = new AttributesManager();
            _self = Guid.NewGuid();
            _player = Guid.NewGuid();

            var selfAttrs = new ModifiableAttributes();
            selfAttrs.EnsureInitialized();
            selfAttrs.SetAttribute<Health>(new Health(100));
            _attrs.Register(_self, selfAttrs);

            var playerAttrs = new ModifiableAttributes();
            playerAttrs.EnsureInitialized();
            playerAttrs.SetAttribute<Health>(new Health(50));
            _attrs.Register(_player, playerAttrs);
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
        }

        private AIContext NewContext() => new AIContext
        {
            SelfGuid = _self,
            PlayerGuid = _player,
            SelfMaxHp = 100,
            Attributes = _attrs,
        };

        // ---- HPBelow -----------------------------------------------------

        [Test]
        public void HPBelow_False_WhenAtFullHp()
        {
            var cond = new AICond_HPBelow { Percent = 0.5f };
            Assert.IsFalse(cond.Evaluate(NewContext()));
        }

        [Test]
        public void HPBelow_True_WhenBelowThreshold()
        {
            _attrs.Modify<Health, int>(_self, v => 40);
            var cond = new AICond_HPBelow { Percent = 0.5f };
            Assert.IsTrue(cond.Evaluate(NewContext()));
        }

        [Test]
        public void HPBelow_False_WhenAtThresholdExactly()
        {
            _attrs.Modify<Health, int>(_self, v => 50);
            var cond = new AICond_HPBelow { Percent = 0.5f };
            Assert.IsFalse(cond.Evaluate(NewContext()));
        }

        [Test]
        public void HPBelow_ZeroMaxHp_ReturnsFalse()
        {
            var ctx = NewContext();
            ctx.SelfMaxHp = 0;
            var cond = new AICond_HPBelow { Percent = 0.99f };
            Assert.IsFalse(cond.Evaluate(ctx));
        }

        // ---- PlayerInRange ----------------------------------------------

        [Test]
        public void PlayerInRange_True_WhenWithinRange()
        {
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(10, 10));
            grid.Register(_self, new GridCoord(0, 0));
            grid.Register(_player, new GridCoord(1, 0));

            var ctx = NewContext();
            ctx.Grid = grid;
            var cond = new AICond_PlayerInRange { Range = 1, Metric = AICond_PlayerInRange.Distance.Manhattan };
            Assert.IsTrue(cond.Evaluate(ctx));
        }

        [Test]
        public void PlayerInRange_False_WhenOutsideRange()
        {
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(10, 10));
            grid.Register(_self, new GridCoord(0, 0));
            grid.Register(_player, new GridCoord(3, 3));

            var ctx = NewContext();
            ctx.Grid = grid;
            var cond = new AICond_PlayerInRange { Range = 2, Metric = AICond_PlayerInRange.Distance.Manhattan };
            Assert.IsFalse(cond.Evaluate(ctx));
        }

        [Test]
        public void PlayerInRange_Chebyshev_DifferentResult()
        {
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(10, 10));
            grid.Register(_self, new GridCoord(0, 0));
            grid.Register(_player, new GridCoord(2, 2)); // manhattan=4, chebyshev=2

            var ctx = NewContext();
            ctx.Grid = grid;
            Assert.IsFalse(
                new AICond_PlayerInRange { Range = 3, Metric = AICond_PlayerInRange.Distance.Manhattan }.Evaluate(ctx));
            Assert.IsTrue(
                new AICond_PlayerInRange { Range = 2, Metric = AICond_PlayerInRange.Distance.Chebyshev }.Evaluate(ctx));
        }

        // ---- AllyAlive ---------------------------------------------------

        [Test]
        public void AllyAlive_False_WhenOnlySelfAndPlayer()
        {
            var cond = new AICond_AllyAlive();
            Assert.IsFalse(cond.Evaluate(NewContext()));
        }

        [Test]
        public void AllyAlive_True_WhenAllyRegisteredAndAlive()
        {
            var ally = Guid.NewGuid();
            var a = new ModifiableAttributes();
            a.EnsureInitialized();
            a.SetAttribute<Health>(new Health(10));
            _attrs.Register(ally, a);

            Assert.IsTrue(new AICond_AllyAlive().Evaluate(NewContext()));
        }

        [Test]
        public void AllyAlive_False_WhenAllyIsDead()
        {
            var ally = Guid.NewGuid();
            var a = new ModifiableAttributes();
            a.EnsureInitialized();
            a.SetAttribute<Health>(new Health(0));
            _attrs.Register(ally, a);

            Assert.IsFalse(new AICond_AllyAlive().Evaluate(NewContext()));
        }

        // ---- RoundNumber -------------------------------------------------

        [Test]
        public void RoundNumber_Equal_MatchesExact()
        {
            var ctx = NewContext();
            ctx.RoundIndex = 3;
            Assert.IsTrue(new AICond_RoundNumber { Mode = AICond_RoundNumber.CompareMode.Equal, Value = 3 }.Evaluate(ctx));
            Assert.IsFalse(new AICond_RoundNumber { Mode = AICond_RoundNumber.CompareMode.Equal, Value = 4 }.Evaluate(ctx));
        }

        [Test]
        public void RoundNumber_Multiple_MatchesEveryN()
        {
            var cond = new AICond_RoundNumber { Mode = AICond_RoundNumber.CompareMode.Multiple, Value = 3 };
            var ctx = NewContext();
            ctx.RoundIndex = 3; Assert.IsTrue(cond.Evaluate(ctx));
            ctx.RoundIndex = 6; Assert.IsTrue(cond.Evaluate(ctx));
            ctx.RoundIndex = 5; Assert.IsFalse(cond.Evaluate(ctx));
            ctx.RoundIndex = 0; Assert.IsFalse(cond.Evaluate(ctx));
        }

        // ---- And / Or / Not ---------------------------------------------

        [Test]
        public void And_AllTrue_ReturnsTrue()
        {
            var cond = new AICond_And
            {
                Conditions =
                {
                    new ConstCond { Value = true },
                    new ConstCond { Value = true }
                }
            };
            Assert.IsTrue(cond.Evaluate(NewContext()));
        }

        [Test]
        public void And_OneFalse_ReturnsFalse()
        {
            var cond = new AICond_And
            {
                Conditions =
                {
                    new ConstCond { Value = true },
                    new ConstCond { Value = false }
                }
            };
            Assert.IsFalse(cond.Evaluate(NewContext()));
        }

        [Test]
        public void Or_OneTrue_ReturnsTrue()
        {
            var cond = new AICond_Or
            {
                Conditions =
                {
                    new ConstCond { Value = false },
                    new ConstCond { Value = true }
                }
            };
            Assert.IsTrue(cond.Evaluate(NewContext()));
        }

        [Test]
        public void Not_InvertsInner()
        {
            Assert.IsFalse(new AICond_Not { Inner = new ConstCond { Value = true } }.Evaluate(NewContext()));
            Assert.IsTrue(new AICond_Not { Inner = new ConstCond { Value = false } }.Evaluate(NewContext()));
            Assert.IsTrue(new AICond_Not { Inner = null }.Evaluate(NewContext()));
        }

        private sealed class ConstCond : AICondition
        {
            public bool Value;
            public override bool Evaluate(AIContext context) => Value;
        }
    }
}
