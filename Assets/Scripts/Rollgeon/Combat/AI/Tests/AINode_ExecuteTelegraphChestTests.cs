using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Threat;
using Rollgeon.Grid;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests del daño AoE incidental al cofre (Feature#0046): un telegraph que cubre
    /// la tile del cofre lo golpea; otros enemigos en el área NO reciben daño
    /// (sin friendly fire).
    /// </summary>
    [TestFixture]
    public class AINode_ExecuteTelegraphChestTests
    {
        private AttributesManager _attrs;
        private GridManager _grid;
        private DamagePipeline _pipeline;
        private ThreatenedAreaService _threat;
        private Guid _boss;
        private Guid _player;
        private Guid _chest;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();

            _attrs = new AttributesManager();
            ServiceLocator.AddService<AttributesManager>(_attrs);

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(8, 8));

            _pipeline = new DamagePipeline(_attrs);

            _threat = new ThreatenedAreaService();
            ServiceLocator.AddService<IThreatenedAreaService>(_threat);

            _boss = Register(new GridCoord(0, 0), hp: 100);
            _player = Register(new GridCoord(7, 7), hp: 100);
            _chest = Register(new GridCoord(3, 3), hp: 20);
        }

        [TearDown]
        public void TearDown()
        {
            _attrs?.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<DamageResolvedPayload>.Clear();
        }

        private Guid Register(GridCoord coord, int hp)
        {
            var guid = Guid.NewGuid();
            var ma = new ModifiableAttributes();
            ma.EnsureInitialized();
            ma.SetAttribute<Health>(new Health(hp));
            _attrs.Register(guid, ma);
            _grid.Register(guid, coord);
            return guid;
        }

        private void RegisterChestRegistry() =>
            ServiceLocator.AddService<Rollgeon.Chests.IChestRegistry>(new StubChestRegistry(_chest));

        private AIContext BuildContext() => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Attributes = _attrs,
            Grid = _grid,
            DamagePipeline = _pipeline,
        };

        private void MarkAndExecute(params GridCoord[] tiles)
        {
            _threat.Mark(_boss, tiles, damage: 6, AttackKind.ScriptedAbility);
            new AINode_ExecuteTelegraph().Tick(BuildContext());
        }

        [Test]
        public void Resolve_ShouldDamageChest_WhenItsTileIsInsideArea()
        {
            // Arrange
            RegisterChestRegistry();

            // Act — área cubre al cofre (3,3), no al player.
            MarkAndExecute(new GridCoord(3, 3), new GridCoord(3, 4));

            // Assert
            Assert.AreEqual(14, _attrs.GetAttribute<Health>(_chest).Value);
            Assert.AreEqual(100, _attrs.GetAttribute<Health>(_player).Value);
        }

        [Test]
        public void Resolve_ShouldNotDamageChest_WhenOutsideArea()
        {
            // Arrange
            RegisterChestRegistry();

            // Act — área lejos del cofre.
            MarkAndExecute(new GridCoord(6, 6), new GridCoord(6, 7));

            // Assert
            Assert.AreEqual(20, _attrs.GetAttribute<Health>(_chest).Value);
        }

        [Test]
        public void Resolve_ShouldNotDamageOtherEnemies_InsideArea()
        {
            // Arrange — otro enemigo dentro del área: sin friendly fire.
            RegisterChestRegistry();
            var otherEnemy = Register(new GridCoord(3, 4), hp: 50);

            // Act
            MarkAndExecute(new GridCoord(3, 3), new GridCoord(3, 4));

            // Assert — cofre golpeado, enemigo intacto.
            Assert.AreEqual(14, _attrs.GetAttribute<Health>(_chest).Value);
            Assert.AreEqual(50, _attrs.GetAttribute<Health>(otherEnemy).Value);
        }

        [Test]
        public void Resolve_ShouldDamagePlayerAndChest_WhenBothInsideArea()
        {
            // Arrange
            RegisterChestRegistry();

            // Act — área cubre a ambos.
            MarkAndExecute(new GridCoord(3, 3), new GridCoord(7, 7));

            // Assert
            Assert.AreEqual(14, _attrs.GetAttribute<Health>(_chest).Value);
            Assert.AreEqual(94, _attrs.GetAttribute<Health>(_player).Value);
        }

        [Test]
        public void Resolve_ShouldBehaveAsBefore_WhenNoChestRegistry()
        {
            // Arrange — sin registry en el locator (legacy intacto).
            // Act
            MarkAndExecute(new GridCoord(3, 3), new GridCoord(7, 7));

            // Assert — el player recibe daño; el "cofre" (entidad común) no.
            Assert.AreEqual(94, _attrs.GetAttribute<Health>(_player).Value);
            Assert.AreEqual(20, _attrs.GetAttribute<Health>(_chest).Value);
        }

        private sealed class StubChestRegistry : Rollgeon.Chests.IChestRegistry
        {
            private readonly Guid _chest;
            public StubChestRegistry(Guid chest) => _chest = chest;
            public bool IsChest(Guid guid) => guid == _chest;
            public bool TryGetActiveChest(out Guid chestGuid)
            {
                chestGuid = _chest;
                return true;
            }
        }
    }
}
