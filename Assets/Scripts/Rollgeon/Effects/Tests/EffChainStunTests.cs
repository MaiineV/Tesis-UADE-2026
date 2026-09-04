using System;
using Rollgeon.Combat.AI.Decisions;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Status;
using Rollgeon.Effects.Concretes;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Items.Active;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// EffChainStun (Bottle'o Thunder, Feature#0084 §7, D4 Jerarquía): aturde al primario y
    /// encadena hasta <c>Magnitude</c> rebotes — el más cercano no golpeado, dentro de
    /// <see cref="EffChainStun.BounceRange"/> y con línea de visión limpia.
    /// </summary>
    [TestFixture]
    public sealed class EffChainStunTests
    {
        private sealed class FakeEntityQuery : IEntityQueryService
        {
            public readonly List<Entity> Enemies = new List<Entity>();
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Enemies;
            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Enumerable.Empty<Entity>();
            public EntityFilterMask GetRelationship(Guid owner, Guid target) => EntityFilterMask.Enemies;
        }

        private GridManager _grid;
        private UnitTraitService _traits;
        private AttributesManager _attributes;
        private StunService _stun;
        private FakeEntityQuery _query;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(10, 10));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            _attributes = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attributes, ServiceScope.Global);

            _stun = new StunService();
            _stun.ConfigureForTests(() => _player);
            ServiceLocator.AddService<IStunService>(_stun, ServiceScope.Global);

            _query = new FakeEntityQuery();
            ServiceLocator.AddService<IEntityQueryService>(_query, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 5));
            _traits.Register(_player, UnitTraits.DefaultGround);
        }

        [TearDown]
        public void TearDown()
        {
            _stun?.Dispose();
            _attributes?.Dispose();
            _traits?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private Guid SpawnEnemy(GridCoord coord, bool stunImmune = false, bool addToQuery = true)
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, coord);
            _traits.Register(guid, new UnitTraits(false, false, stunImmune: stunImmune));

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(30));
            _attributes.Register(guid, attrs);

            if (addToQuery) _query.Enemies.Add(new Entity { Guid = guid });
            return guid;
        }

        private static EffectContext BuildContext(Guid player, GridCoord primaryCoord, int magnitude)
        {
            return new EffectContext
            {
                SourceGuid = player,
                TargetGuid = player,
                lastResult = true,
                SelectionResult = new TargetSelectionResult
                {
                    WasCompleted = true,
                    SelectedTargets = new List<TargetRef> { TargetRef.At(primaryCoord) },
                },
                TriggerContext = new ActiveItemRollTriggerContext
                {
                    Face = magnitude,
                    RawFace = magnitude,
                    Faces = 4,
                    Structure = ActiveItemResolution.Hierarchy,
                    Magnitude = magnitude,
                },
            };
        }

        [Test]
        public void ApplyEffect_MagnitudeFour_ChainsAllFourInARow()
        {
            // Arrange — 4 enemigos en línea, cada uno a distancia 2 del anterior (dentro de rango).
            var e1 = SpawnEnemy(new GridCoord(1, 5));
            var e2 = SpawnEnemy(new GridCoord(3, 5));
            var e3 = SpawnEnemy(new GridCoord(5, 5));
            var e4 = SpawnEnemy(new GridCoord(7, 5));
            var effect = new EffChainStun { Turns = 1, BounceRange = 2, Rng = new System.Random(1) };
            var ctx = BuildContext(_player, new GridCoord(1, 5), magnitude: 4);

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            foreach (var e in new[] { e1, e2, e3, e4 })
            {
                Assert.IsTrue(_stun.IsStunned(e), $"{e} debería estar aturdido");
                Assert.AreEqual(1, _stun.GetStunTurns(e));
            }
        }

        [Test]
        public void ApplyEffect_MagnitudeOne_OnlyPrimaryStunned()
        {
            // Arrange — mismos 4 enemigos, pero Magnitude 1: la cadena no rebota.
            var e1 = SpawnEnemy(new GridCoord(1, 5));
            var e2 = SpawnEnemy(new GridCoord(3, 5));
            var effect = new EffChainStun { Turns = 1, BounceRange = 2, Rng = new System.Random(1) };
            var ctx = BuildContext(_player, new GridCoord(1, 5), magnitude: 1);

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_stun.IsStunned(e1));
            Assert.IsFalse(_stun.IsStunned(e2), "sin rebote, el segundo no se toca");
        }

        [Test]
        public void ApplyEffect_NoCandidateWithinBounceRange_ChainStopsEarly()
        {
            // Arrange — el segundo enemigo está a distancia 2 (rebota), pero el tercero queda a
            // distancia 5 del segundo: fuera de BounceRange=2, la cadena corta ahí aunque
            // Magnitude pida 4.
            var e1 = SpawnEnemy(new GridCoord(0, 5));
            var e2 = SpawnEnemy(new GridCoord(2, 5));
            var e3 = SpawnEnemy(new GridCoord(2, 0));
            var effect = new EffChainStun { Turns = 1, BounceRange = 2, Rng = new System.Random(1) };
            var ctx = BuildContext(_player, new GridCoord(0, 5), magnitude: 4);

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_stun.IsStunned(e1));
            Assert.IsTrue(_stun.IsStunned(e2));
            Assert.IsFalse(_stun.IsStunned(e3), "fuera de rango del último golpeado — la cadena ya cortó");
        }

        [Test]
        public void ApplyEffect_BlockedLineOfSight_CandidateSkipped()
        {
            // Arrange — candidato a distancia 2, pero con un bloqueador ocupando la celda
            // intermedia: sin línea de visión limpia, no es candidato válido.
            var e1 = SpawnEnemy(new GridCoord(0, 5));
            var e2 = SpawnEnemy(new GridCoord(0, 7));
            var blocker = SpawnEnemy(new GridCoord(0, 6), addToQuery: false);
            var effect = new EffChainStun { Turns = 1, BounceRange = 2, Rng = new System.Random(1) };
            var ctx = BuildContext(_player, new GridCoord(0, 5), magnitude: 2);

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_stun.IsStunned(e1));
            Assert.IsFalse(_stun.IsStunned(e2), "línea de visión bloqueada — no es candidato válido");
            Assert.IsFalse(_stun.IsStunned(blocker), "el bloqueador ni siquiera es un candidato elegible");
        }

        [Test]
        public void IsValidTarget_HiddenCoord_ReturnsFalse()
        {
            // Arrange
            var effect = new EffChainStun();

            // Act + Assert — celda vacía, nada que aturdir.
            Assert.IsFalse(effect.IsValidTarget(_player, new GridCoord(3, 3)));
        }

        [Test]
        public void IsValidTarget_StunImmuneEnemy_ReturnsFalse()
        {
            // Arrange
            var immune = SpawnEnemy(new GridCoord(1, 5), stunImmune: true);
            var effect = new EffChainStun();

            // Act + Assert
            Assert.IsFalse(effect.IsValidTarget(_player, new GridCoord(1, 5)));
        }

        [Test]
        public void IsValidTarget_LiveStunnableEnemyWithClearLine_ReturnsTrue()
        {
            // Arrange
            var enemy = SpawnEnemy(new GridCoord(1, 5));
            var effect = new EffChainStun();

            // Act + Assert
            Assert.IsTrue(effect.IsValidTarget(_player, new GridCoord(1, 5)));
        }
    }
}
