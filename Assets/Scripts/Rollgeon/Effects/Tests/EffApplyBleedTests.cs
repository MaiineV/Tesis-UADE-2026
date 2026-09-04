using System;
using System.Collections.Generic;
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
    /// <see cref="EffApplyBleed"/>: agrega stacks a los targets resueltos por selección (o
    /// <c>TargetGuid</c> sin selección), con <c>SourceGuid</c> como fuente del stack; sin
    /// <see cref="IBleedService"/> registrado, warning + <c>true</c>.
    /// </summary>
    [TestFixture]
    public sealed class EffApplyBleedTests
    {
        private sealed class FakeBleedService : IBleedService
        {
            public readonly Dictionary<Guid, int> Stacks = new Dictionary<Guid, int>();
            public readonly List<(Guid entity, Guid source, int stacks)> Calls = new List<(Guid, Guid, int)>();

            public void AddStack(Guid entity, Guid source, int stacks = 1)
            {
                Calls.Add((entity, source, stacks));
                Stacks[entity] = Stacks.TryGetValue(entity, out var s) ? s + stacks : stacks;
            }

            public bool IsBleeding(Guid entity) => Stacks.TryGetValue(entity, out var s) && s > 0;
            public int GetStacks(Guid entity) => Stacks.TryGetValue(entity, out var s) ? s : 0;
            public int GetMaxRemainingTurns(Guid entity) => IsBleeding(entity) ? 3 : 0;
            public void Clear(Guid entity) => Stacks.Remove(entity);
            public void ClearAll() => Stacks.Clear();
        }

        private GridManager _grid;
        private FakeBleedService _bleed;
        private Guid _source;
        private Guid _target;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(4, 3));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _bleed = new FakeBleedService();
            ServiceLocator.AddService<IBleedService>(_bleed, ServiceScope.Global);

            _source = Guid.NewGuid();
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
        public void ApplyEffect_WithSelectedCell_AddsStackFromSourceGuid()
        {
            var effect = new EffApplyBleed();
            var ctx = new EffectContext
            {
                SourceGuid = _source,
                SelectionResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new List<TargetRef> { TargetRef.At(new GridCoord(1, 0)) },
                },
            };

            Assert.IsTrue(effect.ApplyEffect(ctx));

            Assert.AreEqual(1, _bleed.GetStacks(_target));
            Assert.AreEqual(_source, _bleed.Calls[0].source);
        }

        [Test]
        public void ApplyEffect_WithoutSelection_FallsBackToTargetGuid()
        {
            var effect = new EffApplyBleed();
            var ctx = new EffectContext { SourceGuid = _source, TargetGuid = _target };

            Assert.IsTrue(effect.ApplyEffect(ctx));

            Assert.IsTrue(_bleed.IsBleeding(_target));
        }

        [Test]
        public void ApplyEffect_WithoutBleedService_WarnsAndReturnsTrue()
        {
            ServiceLocator.Clear();
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            var effect = new EffApplyBleed();
            var ctx = new EffectContext { SourceGuid = _source, TargetGuid = _target };

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("IBleedService no registrado"));
            Assert.IsTrue(effect.ApplyEffect(ctx));
        }
    }
}
