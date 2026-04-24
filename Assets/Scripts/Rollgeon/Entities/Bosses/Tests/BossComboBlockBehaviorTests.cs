using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.ComboBlock;
using Rollgeon.Combos;
using Rollgeon.Combos.Concretes;
using Rollgeon.Combos.Tests;
using Rollgeon.Entities;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Entities.Bosses;
using Rollgeon.Heroes;
using UnityEngine;

namespace Rollgeon.Entities.Bosses.Tests
{
    /// <summary>
    /// Tests de <see cref="BossComboBlockBehavior"/>: counter++ por turno, dispara Block cada
    /// Interval, respeta combos bloqueados/tachados, no falla con candidates vacios.
    /// </summary>
    [TestFixture]
    public class BossComboBlockBehaviorTests
    {
        private ComboBlockService _service;
        private BossFloorManagerSO _bossSO;
        private ContractSheet _sheet;
        private Combo_Par _par;
        private Combo_DoblePar _doblePar;
        private Combo_Trio _trio;
        private List<object[]> _blockedEvents;
        private EventManager.EventReceiver _blockedHandler;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _service = new ComboBlockService();
            _service.ConfigureForTests(() => Guid.Empty);
            ServiceLocator.AddService<IComboBlockService>(_service);

            _bossSO = ScriptableObject.CreateInstance<BossFloorManagerSO>();
            _bossSO.ComboBlockIntervalTurns = 3;
            _bossSO.ComboBlockDurationTurns = 2;

            _par = ComboTestUtils.CreateCombo<Combo_Par>(ComboId.Par, 10);
            _doblePar = ComboTestUtils.CreateCombo<Combo_DoblePar>(ComboId.DoublePair, 18);
            _trio = ComboTestUtils.CreateCombo<Combo_Trio>(ComboId.Triple, 28);

            _sheet = new ContractSheet
            {
                Combos = new List<BaseComboSO> { _par, _doblePar, _trio },
            };

            _blockedEvents = new List<object[]>();
            _blockedHandler = args => _blockedEvents.Add(args);
            EventManager.Subscribe(EventName.OnComboBlocked, _blockedHandler);
        }

        [TearDown]
        public void TearDown()
        {
            EventManager.UnSubscribe(EventName.OnComboBlocked, _blockedHandler);
            _service.Dispose();
            UnityEngine.Object.DestroyImmediate(_par);
            UnityEngine.Object.DestroyImmediate(_doblePar);
            UnityEngine.Object.DestroyImmediate(_trio);
            UnityEngine.Object.DestroyImmediate(_bossSO);
            ServiceLocator.Clear();
        }

        private sealed class TestCtx : BehaviorContext { }

        private BehaviorContext CtxFor(Guid guid)
            => new TestCtx { SourceEntity = new Entity { Guid = guid } };

        private BossComboBlockBehavior BuildBehavior(int pickIndex = 0)
        {
            return new BossComboBlockBehavior
            {
                BossDataOverride = _bossSO,
                SheetResolver = () => _sheet,
                RandomSource = _ => pickIndex,
            };
        }

        [Test]
        public void Execute_BeforeInterval_DoesNotBlock()
        {
            var behavior = BuildBehavior();
            var ctx = CtxFor(Guid.NewGuid());

            behavior.Execute(ctx); // counter = 1
            behavior.Execute(ctx); // counter = 2

            Assert.AreEqual(0, _service.ActiveBlocks.Count);
            Assert.AreEqual(0, _blockedEvents.Count);
            Assert.AreEqual(2, behavior.DebugTurnCounter);
        }

        [Test]
        public void Execute_AtInterval_BlocksOneCombo()
        {
            var behavior = BuildBehavior(pickIndex: 0);
            var ctx = CtxFor(Guid.NewGuid());

            behavior.Execute(ctx); // 1
            behavior.Execute(ctx); // 2
            behavior.Execute(ctx); // 3 → block

            Assert.AreEqual(1, _service.ActiveBlocks.Count);
            Assert.IsTrue(_service.IsBlocked(ComboId.Par));
            Assert.AreEqual(2, _service.GetRemainingTurns(ComboId.Par));
            Assert.AreEqual(1, _blockedEvents.Count);
        }

        [Test]
        public void Execute_SkipsAlreadyBlockedCombo()
        {
            // Pre-bloquea Par. Pickea index 0 dentro de los NO bloqueados (debe ser DoblePar).
            _service.Block(ComboId.Par, 5);
            _blockedEvents.Clear();

            var behavior = BuildBehavior(pickIndex: 0);
            var ctx = CtxFor(Guid.NewGuid());

            behavior.Execute(ctx);
            behavior.Execute(ctx);
            behavior.Execute(ctx); // 3 → block — debe elegir DoblePar (primer no-bloqueado)

            Assert.IsTrue(_service.IsBlocked(ComboId.DoublePair),
                "BossComboBlockBehavior debe filtrar bloqueados y elegir del resto.");
        }

        [Test]
        public void Execute_AllBlocked_NoopAndCounterNotReset()
        {
            _service.Block(ComboId.Par, 10);
            _service.Block(ComboId.DoublePair, 10);
            _service.Block(ComboId.Triple, 10);
            _blockedEvents.Clear();

            var behavior = BuildBehavior();
            var ctx = CtxFor(Guid.NewGuid());

            behavior.Execute(ctx);
            behavior.Execute(ctx);
            behavior.Execute(ctx); // 3 → todos bloqueados → no-op

            Assert.AreEqual(3, behavior.DebugTurnCounter, "Counter NO se resetea si no hubo candidates.");
            Assert.AreEqual(0, _blockedEvents.Count);
            // Pre-existing 3 blocks remain untouched.
            Assert.AreEqual(3, _service.ActiveBlocks.Count);
        }

        [Test]
        public void Execute_SecondIntervalBlocksAnother()
        {
            var behavior = BuildBehavior(pickIndex: 0);
            var ctx = CtxFor(Guid.NewGuid());

            // First block at turn 3.
            for (int i = 0; i < 3; i++) behavior.Execute(ctx);
            var firstBlocked = new List<string>(_service.ActiveBlocks.Keys);
            Assert.AreEqual(1, firstBlocked.Count);

            // Continue — second block at turn 6.
            for (int i = 0; i < 3; i++) behavior.Execute(ctx);
            Assert.AreEqual(2, _service.ActiveBlocks.Count,
                "Dos intervals = dos bloqueos distintos (el primer combo sigue bloqueado).");
        }

        [Test]
        public void Execute_SheetResolverNull_NoopGracefully()
        {
            var behavior = new BossComboBlockBehavior
            {
                BossDataOverride = _bossSO,
                SheetResolver = null,
                RandomSource = _ => 0,
            };
            var ctx = CtxFor(Guid.NewGuid());

            // 3 Executes para llegar al interval.
            Assert.DoesNotThrow(() => { for (int i = 0; i < 3; i++) behavior.Execute(ctx); });
            Assert.AreEqual(0, _service.ActiveBlocks.Count);
        }

        [Test]
        public void Execute_WithoutBossSO_NoopGracefully()
        {
            var behavior = new BossComboBlockBehavior
            {
                BossDataOverride = null,
                SheetResolver = () => _sheet,
            };
            var ctx = CtxFor(Guid.NewGuid());

            Assert.DoesNotThrow(() => behavior.Execute(ctx));
            Assert.AreEqual(0, behavior.DebugTurnCounter, "Sin SO resuelto, counter NO avanza.");
        }
    }
}
