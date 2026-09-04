using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Status;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// <see cref="EffApplyStun"/>: aplica a los targets resueltos por selección (o
    /// <c>TargetGuid</c> sin selección); sin <see cref="IStunService"/> registrado, warning +
    /// <c>true</c> (el roll ya se pagó, nunca corta la cadena).
    /// </summary>
    [TestFixture]
    public sealed class EffApplyStunTests
    {
        private sealed class FakeStunService : IStunService
        {
            public readonly Dictionary<Guid, int> Applied = new Dictionary<Guid, int>();
            public void ApplyStun(Guid entity, int turns = 1) => Applied[entity] = turns;
            public bool IsStunned(Guid entity) => Applied.ContainsKey(entity);
            public int GetStunTurns(Guid entity) => Applied.TryGetValue(entity, out var t) ? t : 0;
            public bool ConsumeTurn(Guid entity) => false;
            public void Clear(Guid entity) => Applied.Remove(entity);
            public void ClearAll() => Applied.Clear();
        }

        private GridManager _grid;
        private FakeStunService _stun;
        private Guid _target;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(4, 3));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _stun = new FakeStunService();
            ServiceLocator.AddService<IStunService>(_stun, ServiceScope.Global);

            _target = Guid.NewGuid();
            _grid.Register(_target, new GridCoord(1, 0));
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void ApplyEffect_WithSelectedCell_StunsOccupant()
        {
            var effect = new EffApplyStun();
            var ctx = new EffectContext
            {
                SelectionResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new List<TargetRef> { TargetRef.At(new GridCoord(1, 0)) },
                },
            };

            Assert.IsTrue(effect.ApplyEffect(ctx));

            Assert.IsTrue(_stun.IsStunned(_target));
            Assert.AreEqual(1, _stun.GetStunTurns(_target), "default Turns = 1.");
        }

        [Test]
        public void ApplyEffect_WithoutSelection_FallsBackToTargetGuid()
        {
            var effect = new EffApplyStun();
            var ctx = new EffectContext { TargetGuid = _target };

            Assert.IsTrue(effect.ApplyEffect(ctx));

            Assert.IsTrue(_stun.IsStunned(_target));
        }

        [Test]
        public void ApplyEffect_NoTargets_ReturnsTrue_NoStun()
        {
            var effect = new EffApplyStun();
            var ctx = new EffectContext();

            Assert.IsTrue(effect.ApplyEffect(ctx));
            CollectionAssert.IsEmpty(_stun.Applied);
        }

        [Test]
        public void ApplyEffect_WithoutStunService_WarnsAndReturnsTrue()
        {
            ServiceLocator.Clear();
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            var effect = new EffApplyStun();
            var ctx = new EffectContext { TargetGuid = _target };

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("IStunService no registrado"));
            Assert.IsTrue(effect.ApplyEffect(ctx));
        }
    }
}
