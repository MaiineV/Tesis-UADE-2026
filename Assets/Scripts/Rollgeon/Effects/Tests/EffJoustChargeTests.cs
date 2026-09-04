using System;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Effects.Selection;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Effects.Concretes;
using Rollgeon.Entities;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Items.Active;
using Rollgeon.Movement;
using Rollgeon.Tiles.Forced;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// EffJoustCharge (Feature#0084 §4, D12 Bandas, dirección): carga = Face, daño = Face al
    /// primer enemigo vivo golpeado, empuje según <see cref="JoustPushMode"/> y colisión que
    /// repite daño en la banda positiva.
    /// </summary>
    [TestFixture]
    public sealed class EffJoustChargeTests
    {
        private sealed class FakeEntityQuery : IEntityQueryService
        {
            public readonly List<Entity> Enemies = new List<Entity>();
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Enemies;
            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Enumerable.Empty<Entity>();
            public EntityFilterMask GetRelationship(Guid owner, Guid target) => EntityFilterMask.Enemies;
        }

        private GridManager _grid;
        private MovementService _movement;
        private UnitTraitService _traits;
        private AttributesManager _attributes;
        private DamagePipeline _damagePipeline;
        private ForcedMovementService _forced;
        private FakeEntityQuery _query;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(20, 5));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _movement = new MovementService(_grid);
            ServiceLocator.AddService<IMovementService>(_movement, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            _attributes = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attributes, ServiceScope.Global);

            _damagePipeline = new DamagePipeline(_attributes);
            ServiceLocator.AddService<IDamagePipeline>(_damagePipeline, ServiceScope.Global);

            _forced = new ForcedMovementService();
            ServiceLocator.AddService<IForcedMovementService>(_forced, ServiceScope.Global);

            _query = new FakeEntityQuery();
            ServiceLocator.AddService<IEntityQueryService>(_query, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 2));
            _traits.Register(_player, UnitTraits.DefaultGround);
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            _traits?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private Guid SpawnEnemy(GridCoord coord, int hp = 100, bool immovable = false)
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, coord);
            _traits.Register(guid, new UnitTraits(false, false, immovable: immovable));

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(hp));
            attrs.SetAttribute<Shield>(new Shield(0));
            _attributes.Register(guid, attrs);

            _query.Enemies.Add(new Entity { Guid = guid });
            return guid;
        }

        private static EffectContext BuildContext(Guid player, int face, Cardinal dir, GridCoord origin)
        {
            return new EffectContext
            {
                SourceGuid = player,
                TargetGuid = player,
                lastResult = true,
                TriggerContext = new ActiveItemRollTriggerContext
                {
                    Face = face,
                    RawFace = face,
                    Faces = 12,
                    Structure = ActiveItemResolution.Bands,
                    Direction = dir,
                    Origin = origin,
                },
            };
        }

        [Test]
        public void ApplyEffect_NoEnemy_PlayerChargesFaceTiles_NoDamage()
        {
            // Arrange — camino libre, sin nada que golpear.
            var effect = new EffJoustCharge { PushMode = JoustPushMode.OneForward, Rng = new System.Random(1) };
            var ctx = BuildContext(_player, face: 5, Cardinal.East, new GridCoord(0, 2));

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(_player, out var coord));
            Assert.AreEqual(new GridCoord(5, 2), coord, "cargó las 5 tiles de la cara");
        }

        [Test]
        public void ApplyEffect_ImpactsEnemy_DealsFaceDamage_PlayerStopsAdjacent()
        {
            // Arrange — enemigo exactamente a Face(5) de distancia: la carga llega entera.
            var enemy = SpawnEnemy(new GridCoord(5, 2));
            var effect = new EffJoustCharge { PushMode = JoustPushMode.OneForward, Rng = new System.Random(1) };
            var ctx = BuildContext(_player, face: 5, Cardinal.East, new GridCoord(0, 2));

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(_player, out var playerCoord));
            Assert.AreEqual(new GridCoord(4, 2), playerCoord, "se frena adyacente al enemigo");
            Assert.AreEqual(95, _attributes.GetAttribute<Health>(enemy).Value, "daño = Face");
        }

        [Test]
        public void ApplyEffect_RandomAdjacentMode_PushesEnemyOneTileToAFreeCardinal()
        {
            // Arrange — banda negativa: empuje a una cardinal libre al azar.
            var enemy = SpawnEnemy(new GridCoord(5, 2));
            var effect = new EffJoustCharge { PushMode = JoustPushMode.RandomAdjacent, Rng = new System.Random(7) };
            var ctx = BuildContext(_player, face: 5, Cardinal.East, new GridCoord(0, 2));
            var before = new GridCoord(5, 2);

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(enemy, out var after));
            Assert.AreNotEqual(before, after, "se empujó a alguna cardinal");
            Assert.AreEqual(1, before.Manhattan(after), "el empuje aleatorio es de 1 sola tile");
        }

        [Test]
        public void ApplyEffect_OneForwardMode_PushesEnemyOneTileInChargeDirection()
        {
            // Arrange — banda mixta: empuje 1 en la dirección de la carga.
            var enemy = SpawnEnemy(new GridCoord(5, 2));
            var effect = new EffJoustCharge { PushMode = JoustPushMode.OneForward, Rng = new System.Random(1) };
            var ctx = BuildContext(_player, face: 5, Cardinal.East, new GridCoord(0, 2));

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(enemy, out var coord));
            Assert.AreEqual(new GridCoord(6, 2), coord);
        }

        [Test]
        public void ApplyEffect_TwoForwardMode_OpenSpace_PushesTwoTiles_NoCollisionBonus()
        {
            // Arrange — banda positiva con espacio libre detrás del enemigo: sin colisión.
            var enemy = SpawnEnemy(new GridCoord(5, 2));
            var effect = new EffJoustCharge { PushMode = JoustPushMode.TwoForwardWithCollision, Rng = new System.Random(1) };
            var ctx = BuildContext(_player, face: 5, Cardinal.East, new GridCoord(0, 2));

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(enemy, out var coord));
            Assert.AreEqual(new GridCoord(7, 2), coord, "avanzó las 2 tiles completas");
            Assert.AreEqual(95, _attributes.GetAttribute<Health>(enemy).Value, "un solo golpe de daño — sin bono de colisión");
        }

        [Test]
        public void ApplyEffect_TwoForwardMode_BlockedByWall_DealsDamageAgain()
        {
            // Arrange — sala angosta (ancho 8): el enemigo queda pegado al borde, el empuje de
            // 2 choca contra la pared en el primer paso.
            _grid.LoadRoom(NavGraph.Rect(8, 5));
            _grid.Register(_player, new GridCoord(0, 2));
            var enemy = SpawnEnemy(new GridCoord(7, 2));

            var effect = new EffJoustCharge { PushMode = JoustPushMode.TwoForwardWithCollision, Rng = new System.Random(1) };
            var ctx = BuildContext(_player, face: 7, Cardinal.East, new GridCoord(0, 2));

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(enemy, out var coord));
            Assert.AreEqual(new GridCoord(7, 2), coord, "bloqueado contra la pared — no se movió");
            Assert.AreEqual(86, _attributes.GetAttribute<Health>(enemy).Value, "daño de carga (7) + bono de colisión (7)");
        }

        [Test]
        public void ApplyEffect_ImmovableEnemy_DealsDamageWithoutPush()
        {
            // Arrange
            var enemy = SpawnEnemy(new GridCoord(5, 2), immovable: true);
            var effect = new EffJoustCharge { PushMode = JoustPushMode.OneForward, Rng = new System.Random(1) };
            var ctx = BuildContext(_player, face: 5, Cardinal.East, new GridCoord(0, 2));

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.AreEqual(95, _attributes.GetAttribute<Health>(enemy).Value, "recibe daño igual");
            Assert.IsTrue(_grid.TryGetPosition(enemy, out var coord));
            Assert.AreEqual(new GridCoord(5, 2), coord, "inamovible — no se empuja");
        }
    }
}
