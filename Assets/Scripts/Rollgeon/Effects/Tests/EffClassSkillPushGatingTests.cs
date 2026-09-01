using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Damage;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Feedback;
using Rollgeon.Grid;
using Rollgeon.Heroes;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Gate del botón (Feature#0055) para un behavior de una sola fase con
    /// <see cref="EffClassSkillPush"/> — mismo criterio que <c>EffChainSelectionGatingTests</c>
    /// (Selection real vive en la fase, no en el chain). El filtro Enemies pasa sin
    /// <c>IEntityQueryService</c> registrado (default permisivo de <c>SelectionSettings</c>).
    /// </summary>
    [TestFixture]
    public sealed class EffClassSkillPushGatingTests
    {
        private GridManager _grid;
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 1));
            _owner = Guid.NewGuid();
            _grid.Register(_owner, new GridCoord(0, 0));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        private static HeroActionBehavior BuildPushBehavior(int range)
        {
            var push = new EffClassSkillPush
            {
                Selection = new SelectionSettings
                {
                    SlotState = SlotState.Occupied,
                    EntityFilter = EntityFilterMask.Enemies,
                    Timing = SelectionTiming.BeforeRoll,
                    Range = range,
                },
            };
            var chain = new EffChain
            {
                Phases = new List<ChainPhase>
                {
                    new ChainPhase { Effects = new EffectData { Effects = new List<IEffect> { push } } },
                },
            };
            return new HeroActionBehavior
            {
                Effects = new List<EffectData>
                {
                    new EffectData { Effects = new List<IEffect> { chain } },
                },
            };
        }

        [Test]
        public void HasUsableEffectGroup_EnemyAdjacent_IsUsable()
        {
            var enemy = Guid.NewGuid();
            _grid.Register(enemy, new GridCoord(1, 0));
            var behavior = BuildPushBehavior(range: 1);

            var usable = behavior.HasUsableEffectGroup(_owner, enemy, out var reason);

            Assert.IsTrue(usable, $"Enemigo a Manhattan 1 con Range 1 debe habilitar el chip: {reason}");
        }

        [Test]
        public void HasUsableEffectGroup_EnemyAtManhattan2_IsNotUsable()
        {
            var enemy = Guid.NewGuid();
            _grid.Register(enemy, new GridCoord(2, 0));
            var behavior = BuildPushBehavior(range: 1);

            var usable = behavior.HasUsableEffectGroup(_owner, enemy, out var reason);

            Assert.IsFalse(usable, "Enemigo fuera del Range 1 de la fase no debe habilitar el chip.");
            Assert.IsNotNull(reason);
        }
    }

    /// <summary>
    /// Regresión: una fase de chain que SOLO tiene <see cref="EffClassSkillPush"/> (sin
    /// <c>EffDealDamage</c> ni <c>EffAddShield</c>) no debe disparar la secuencia de breakdown
    /// N×M — el Empuje es data pura (casillas), no una fórmula de daño/escudo animable.
    /// </summary>
    [TestFixture]
    public sealed class EffClassSkillPushBreakdownTests
    {
        private bool _received;
        private Action<DamageBreakdownComputedPayload> _handler;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _received = false;
            _handler = _ => _received = true;
            TypedEvent<DamageBreakdownComputedPayload>.Subscribe(_handler);
        }

        [TearDown]
        public void TearDown()
        {
            TypedEvent<DamageBreakdownComputedPayload>.Unsubscribe(_handler);
            ServiceLocator.Clear();
        }

        [Test]
        public void PushOnlyPhase_AnnouncesNothing_GateStaysClosed()
        {
            var push = new EffClassSkillPush();
            var group = new EffectData { Effects = new List<IEffect> { push } };

            var dealDamage = DamageBreakdownAnnouncer.FindDealDamage(group);
            var addShield = DamageBreakdownAnnouncer.FindAddShield(group);
            Assert.IsNull(dealDamage, "Una fase de Empuje no contiene EffDealDamage.");
            Assert.IsNull(addShield, "Una fase de Empuje no contiene EffAddShield.");

            var effCtx = new EffectContext
            {
                SourceGuid = Guid.NewGuid(),
                TargetGuid = Guid.NewGuid(),
            };

            DamageBreakdownAnnouncer.Announce(effCtx, dealDamage);
            DamageBreakdownAnnouncer.AnnounceShield(effCtx, addShield);

            Assert.IsFalse(_received, "Ninguna fase de Empuje debe emitir DamageBreakdownComputedPayload.");
            Assert.IsFalse(BreakdownUiGate.Pending, "El gate de breakdown no debe quedar levantado.");
        }
    }
}
