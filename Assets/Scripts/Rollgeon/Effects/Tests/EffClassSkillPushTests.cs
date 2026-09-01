using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Skills.Push;
using Rollgeon.Combos;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Heroes;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Feature#0055 — EffClassSkillPush: traduce el combo a casillas con la tabla y delega en el
    /// resolver. Siempre devuelve true (el roll ya se cobró); sin combo no empuja.
    /// </summary>
    [TestFixture]
    public sealed class EffClassSkillPushTests
    {
        private sealed class SpyResolver : IClassSkillPushResolver
        {
            public readonly List<(Guid pusher, Guid target, int distance, int damage, int stun)> Calls = new();

            public PushOutcome Resolve(Guid pusher, Guid target, int distance, int collisionDamage, int stunTurns = 1)
            {
                Calls.Add((pusher, target, distance, collisionDamage, stunTurns));
                return new PushOutcome();
            }
        }

        private GridManager _grid;
        private SpyResolver _resolver;
        private ClassSkillPushTableSO _table;
        private EffClassSkillPush _effect;
        private Guid _player;
        private Guid _enemy;
        private readonly GridCoord _enemyCoord = new GridCoord(1, 0);

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 6));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _resolver = new SpyResolver();
            ServiceLocator.AddService<IClassSkillPushResolver>(_resolver, ServiceScope.Global);

            _player = Guid.NewGuid();
            _enemy = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));
            _grid.Register(_enemy, _enemyCoord);

            _table = ClassSkillPushTableSO.CreateDefault();
            _effect = new EffClassSkillPush { Table = _table };
        }

        [TearDown]
        public void TearDown()
        {
            if (_table != null) UnityEngine.Object.DestroyImmediate(_table);
            ServiceLocator.Clear();
        }

        private EffectContext Context(ComboDetectionResult? combo, bool withSelection = true)
        {
            var ctx = new EffectContext
            {
                SourceGuid = _player,
                ComboResult = combo,
            };
            if (withSelection)
            {
                ctx.SelectionResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new List<TargetRef> { TargetRef.At(_enemyCoord) },
                };
            }
            return ctx;
        }

        private static ComboDetectionResult Match(string comboId)
            => ComboDetectionResult.Match(comboId, 10, 2, new[] { 0, 1 });

        // ------------------------------------------------------------------

        [Test]
        public void ApplyEffect_NoComboResult_ReturnsTrueWithoutPushing()
        {
            bool result = _effect.ApplyEffect(Context(null));

            Assert.IsTrue(result);
            Assert.AreEqual(0, _resolver.Calls.Count);
        }

        [Test]
        public void ApplyEffect_NoMatch_ReturnsTrueWithoutPushing()
        {
            bool result = _effect.ApplyEffect(Context(ComboDetectionResult.NoMatch()));

            Assert.IsTrue(result);
            Assert.AreEqual(0, _resolver.Calls.Count);
        }

        [Test]
        public void ApplyEffect_Pair_PushesSelectedOccupantOneTileWithTableDamage()
        {
            bool result = _effect.ApplyEffect(Context(Match(ComboId.Par)));

            Assert.IsTrue(result);
            Assert.AreEqual(1, _resolver.Calls.Count);
            var call = _resolver.Calls[0];
            Assert.AreEqual(_player, call.pusher);
            Assert.AreEqual(_enemy, call.target);
            Assert.AreEqual(1, call.distance);
            Assert.AreEqual(10, call.damage);
        }

        [Test]
        public void ApplyEffect_Generala_PushesFive()
        {
            _effect.ApplyEffect(Context(Match(ComboId.Generala)));

            Assert.AreEqual(5, _resolver.Calls[0].distance);
        }

        [Test]
        public void ApplyEffect_BruteForce_NotInTable_DoesNotPush()
        {
            bool result = _effect.ApplyEffect(Context(Match(ComboId.BruteForce)));

            Assert.IsTrue(result);
            Assert.AreEqual(0, _resolver.Calls.Count);
        }

        [Test]
        public void ApplyEffect_NullTable_DoesNotPush()
        {
            _effect.Table = null;

            bool result = _effect.ApplyEffect(Context(Match(ComboId.Par)));

            Assert.IsTrue(result);
            Assert.AreEqual(0, _resolver.Calls.Count);
        }

        [Test]
        public void ApplyEffect_NoResolverRegistered_ReturnsTrueAndWarns()
        {
            ServiceLocator.Clear();
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            LogAssert.Expect(LogType.Warning, new Regex("IClassSkillPushResolver"));

            bool result = _effect.ApplyEffect(Context(Match(ComboId.Par)));

            Assert.IsTrue(result);
        }

        [Test]
        public void ApplyEffect_EmptySelection_FallsBackToTargetGuid()
        {
            var ctx = Context(Match(ComboId.Triple), withSelection: false);
            ctx.TargetGuid = _enemy;

            _effect.ApplyEffect(ctx);

            Assert.AreEqual(1, _resolver.Calls.Count);
            Assert.AreEqual(_enemy, _resolver.Calls[0].target);
            Assert.AreEqual(2, _resolver.Calls[0].distance);
        }

        [Test]
        public void ApplyEffect_CustomCollisionDamage_IsPassedThrough()
        {
            _table.CollisionDamage = 25;

            _effect.ApplyEffect(Context(Match(ComboId.Par)));

            Assert.AreEqual(25, _resolver.Calls[0].damage);
        }

        [Test]
        public void ResolveDistance_NoMatch_ReturnsZero()
        {
            Assert.AreEqual(0, _effect.ResolveDistance(ComboDetectionResult.NoMatch()));
            Assert.AreEqual(0, _effect.ResolveDistance(null));
        }
    }
}
