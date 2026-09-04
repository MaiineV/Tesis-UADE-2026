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
using Rollgeon.Tiles;
using Rollgeon.Tiles.Forced;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// EffGrappleClaw (Feature#0085 §3, D6 Gradiente): ancla movible se atrae adyacente al
    /// jugador; ancla sólida/inamovible hace avanzar al jugador cortado antes de una tile
    /// dañina; caras 1-2 agregan Cadena Inestable.
    /// </summary>
    [TestFixture]
    public sealed class EffGrappleClawTests
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
        private SpecialTileService _tiles;
        private FakeEntityQuery _query;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();
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

            _tiles = new SpecialTileService();
            _tiles.ConfigureForTests(() => _player);
            ServiceLocator.AddService<ISpecialTileService>(_tiles, ServiceScope.Global);

            _query = new FakeEntityQuery();
            ServiceLocator.AddService<IEntityQueryService>(_query, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 2));
            _traits.Register(_player, UnitTraits.DefaultGround);
        }

        [TearDown]
        public void TearDown()
        {
            _tiles?.Dispose();
            _attributes?.Dispose();
            _traits?.Dispose();

            foreach (var asset in _createdAssets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private Guid SpawnEnemy(GridCoord coord, bool immovable = false)
        {
            var guid = Guid.NewGuid();
            _grid.Register(guid, coord);
            _traits.Register(guid, new UnitTraits(false, false, immovable: immovable));

            var attrs = new ModifiableAttributes();
            attrs.EnsureInitialized();
            attrs.SetAttribute<Health>(new Health(30));
            attrs.SetAttribute<Shield>(new Shield(0));
            _attributes.Register(guid, attrs);

            _query.Enemies.Add(new Entity { Guid = guid });
            return guid;
        }

        private SpecialTileDefinitionSO Spikes(int enterDamage)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.TileType = SpecialTileType.Spikes;
            def.Triggers = TileTrigger.OnEnter | TileTrigger.OnForcedMovementInto;
            def.Category = TileEffectCategory.Damage;
            def.Affinity = TileAffinity.GroundOnly;
            def.EnterDamage = enterDamage;
            _createdAssets.Add(def);
            return def;
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
                    Faces = 6,
                    Structure = ActiveItemResolution.Gradient,
                    Magnitude = face,
                    Direction = dir,
                    Origin = origin,
                },
            };
        }

        [Test]
        public void PreviewTrajectory_NothingWithinSixTiles_ReturnsEmpty()
        {
            // Arrange — sala 20x5 vacía, jugador en (0,2): 6 tiles al Este siguen abiertas.
            var effect = new EffGrappleClaw();

            // Act
            var result = effect.PreviewTrajectory(_player, new GridCoord(0, 2), Cardinal.East);

            // Assert
            Assert.AreEqual(0, result.Count, "sin ancla dentro de rango — dirección inválida");
        }

        [Test]
        public void ApplyEffect_MovableEnemyAnchor_PullsEnemyAdjacentToPlayer()
        {
            // Arrange
            var enemy = SpawnEnemy(new GridCoord(5, 2));
            var effect = new EffGrappleClaw { Rng = new System.Random(1) };
            var ctx = BuildContext(_player, face: 6, Cardinal.East, new GridCoord(0, 2));

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(enemy, out var coord));
            Assert.AreEqual(new GridCoord(1, 2), coord, "el enemigo se frena adyacente al jugador, no encima");
            Assert.IsTrue(_grid.TryGetPosition(_player, out var playerCoord));
            Assert.AreEqual(new GridCoord(0, 2), playerCoord, "el jugador no se mueve — el ancla era movible");
        }

        [Test]
        public void ApplyEffect_WallAnchor_PlayerAdvancesStoppingBeforeHarmfulTile()
        {
            // Arrange — sala angosta (ancho 4): la pared del borde derecho (x=4) es el ancla
            // dentro de rango; una tile dañina en (3,2) corta la carga antes de llegar ahí.
            _grid.LoadRoom(NavGraph.Rect(4, 5));
            _grid.Register(_player, new GridCoord(0, 2));
            _tiles.Place(Spikes(999), new[] { new GridCoord(3, 2) });
            var effect = new EffGrappleClaw { Rng = new System.Random(1) };
            var ctx = BuildContext(_player, face: 6, Cardinal.East, new GridCoord(0, 2));

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(_player, out var coord));
            Assert.AreEqual(new GridCoord(2, 2), coord, "se frena en la última celda libre ANTES de la dañina");
        }

        [Test]
        public void ApplyEffect_ImmovableEnemy_ActsAsWallAnchor_PlayerAdvancesInstead()
        {
            // Arrange — enemigo inamovible en (3,2): funciona como ancla sólida, no se atrae.
            var enemy = SpawnEnemy(new GridCoord(3, 2), immovable: true);
            var effect = new EffGrappleClaw { Rng = new System.Random(1) };
            var ctx = BuildContext(_player, face: 6, Cardinal.East, new GridCoord(0, 2));

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(enemy, out var enemyCoord));
            Assert.AreEqual(new GridCoord(3, 2), enemyCoord, "el inamovible no se movió");
            Assert.IsTrue(_grid.TryGetPosition(_player, out var playerCoord));
            Assert.AreEqual(new GridCoord(2, 2), playerCoord, "el jugador avanzó hasta quedar adyacente");
        }

        [Test]
        public void ApplyEffect_FaceTwo_UnstableChainDragsSideEnemyOneTile()
        {
            // Arrange — ancla movible lejos (no participa de la cadena); un enemigo movible
            // parado justo al lado de una celda intermedia de la trayectoria (0,1) adyacente
            // a la celda de cadena (1,2).
            var anchor = SpawnEnemy(new GridCoord(6, 2));
            var sideEnemy = SpawnEnemy(new GridCoord(1, 1));
            var effect = new EffGrappleClaw { Rng = new System.Random(1) };
            var ctx = BuildContext(_player, face: 2, Cardinal.East, new GridCoord(0, 2));

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(sideEnemy, out var coord));
            Assert.AreEqual(new GridCoord(1, 2), coord, "la cadena arrastró al enemigo lateral 1 tile");
        }

        [Test]
        public void ApplyEffect_FaceThree_NoUnstableChain_SideEnemyUntouched()
        {
            // Arrange — misma formación que el caso anterior, pero con cara > 2: sin Cadena
            // Inestable, el enemigo lateral no se mueve.
            SpawnEnemy(new GridCoord(6, 2));
            var sideEnemy = SpawnEnemy(new GridCoord(1, 1));
            var effect = new EffGrappleClaw { Rng = new System.Random(1) };
            var ctx = BuildContext(_player, face: 3, Cardinal.East, new GridCoord(0, 2));

            // Act
            var ok = effect.ApplyEffect(ctx);

            // Assert
            Assert.IsTrue(ok);
            Assert.IsTrue(_grid.TryGetPosition(sideEnemy, out var coord));
            Assert.AreEqual(new GridCoord(1, 1), coord, "sin cadena — el lateral no se movió");
        }
    }
}
