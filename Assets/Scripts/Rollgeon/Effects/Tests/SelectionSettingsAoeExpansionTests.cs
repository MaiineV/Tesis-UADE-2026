using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// Cubre la expansión AoE (<see cref="TargetMode.Aoe"/>): ancla + área por radio o
    /// patrón custom, clipeada a la grilla (NO al Range del caster) y re-filtrada por
    /// SlotState + EntityFilter.
    /// </summary>
    [TestFixture]
    public sealed class SelectionSettingsAoeExpansionTests
    {
        private GridManager _grid;
        private Guid _owner;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _grid = new GridManager();
            _owner = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        private void RegisterGrid()
        {
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
        }

        private static SelectionSettings AoeAttack(int radius, int range = 3)
        {
            return new SelectionSettings
            {
                SlotState = SlotState.Occupied,
                EntityFilter = EntityFilterMask.Enemies,
                Range = range,
                TargetMode = TargetMode.Aoe,
                AoeShape = AoeShape.Radius,
                AoeRadius = radius,
            };
        }

        private static bool Contains(List<TargetRef> targets, int x, int z)
        {
            return targets.Any(t => t.Coord == new GridCoord(x, z));
        }

        [Test]
        public void TargetMode_DefaultValue_IsSingle()
        {
            // Arrange + Act
            var settings = new SelectionSettings();

            // Assert — back-compat: assets serializados sin el campo (CH_Warrior, ítems)
            // deben caer en Single = comportamiento previo al rework.
            Assert.AreEqual(TargetMode.Single, settings.TargetMode);
            Assert.AreEqual(AoeShape.Radius, settings.AoeShape);
            Assert.IsTrue(settings.IsConstantSelectionCount);
            Assert.IsNull(settings.SelectionCountReader);
        }

        [Test]
        public void ExpandAoe_SingleMode_ReturnsAnchorOnly()
        {
            // Arrange — en Single la expansión es identidad (solo el ancla).
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            RegisterGrid();
            var settings = new SelectionSettings
            {
                SlotState = SlotState.Occupied,
                EntityFilter = EntityFilterMask.Enemies,
                TargetMode = TargetMode.Single,
            };

            // Act
            var targets = settings.ExpandAoe(new GridCoord(2, 0), _owner);

            // Assert
            Assert.AreEqual(1, targets.Count);
            Assert.IsTrue(Contains(targets, 2, 0));
        }

        [Test]
        public void ExpandAoe_RadiusOne_IncludesAnchorAndFilteredNeighbors()
        {
            // Arrange — enemigos en (2,1) [ancla] y (3,1); (2,0) queda vacía.
            _grid.LoadRoom(NavGraph.Rect(5, 3));
            _grid.Register(_owner, new GridCoord(0, 1));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 1));
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 1));
            RegisterGrid();
            var settings = AoeAttack(radius: 1);

            // Act
            var targets = settings.ExpandAoe(new GridCoord(2, 1), _owner);

            // Assert — el área re-aplica Occupied+Enemies: la celda vacía no entra.
            Assert.IsTrue(Contains(targets, 2, 1), "El ancla siempre entra.");
            Assert.IsTrue(Contains(targets, 3, 1), "El enemigo adyacente dentro del radio entra.");
            Assert.IsFalse(Contains(targets, 2, 0), "Las celdas vacías del área no entran (Occupied).");
            Assert.AreEqual(2, targets.Count);
        }

        [Test]
        public void ExpandAoe_Radius_ClipsToGridBounds()
        {
            // Arrange — ancla en la esquina (0,0) de una sala 3x3: medio diamante cae
            // fuera de la grilla. SlotState.Both para que las celdas vacías cuenten.
            _grid.LoadRoom(NavGraph.Rect(3, 3));
            _grid.Register(_owner, new GridCoord(2, 2));
            RegisterGrid();
            var settings = AoeAttack(radius: 1);
            settings.SlotState = SlotState.Both;

            // Act
            var targets = settings.ExpandAoe(new GridCoord(0, 0), _owner);

            // Assert — solo ancla + (1,0) + (0,1); nada con coordenadas negativas.
            Assert.AreEqual(3, targets.Count);
            Assert.IsTrue(Contains(targets, 0, 0));
            Assert.IsTrue(Contains(targets, 1, 0));
            Assert.IsTrue(Contains(targets, 0, 1));
            Assert.IsFalse(targets.Any(t => t.Coord.X < 0 || t.Coord.Y < 0),
                "El área nunca incluye celdas fuera de la grilla.");
        }

        [Test]
        public void ExpandAoe_Radius_NotClippedByCasterRange()
        {
            // Arrange — Range 1 (el ancla solo puede estar pegada al caster) pero radio 2:
            // la explosión en el borde del alcance derrama más allá del Range.
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(1, 0)); // ancla (enemigo a rango 1)
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 0)); // a Manhattan 3 del caster
            RegisterGrid();
            var settings = AoeAttack(radius: 2, range: 1);

            // Act
            var targets = settings.ExpandAoe(new GridCoord(1, 0), _owner);

            // Assert — (3,0) está fuera del Range del caster pero dentro del radio del ancla.
            Assert.IsTrue(Contains(targets, 3, 0),
                "El área AoE se clipea a la grilla, no al Range del caster.");
        }

        [Test]
        public void ExpandAoe_Radius_ExcludesOwnerCell()
        {
            // Arrange — el owner pegado al ancla: el splash nunca lo incluye (mismo
            // criterio que PassesSlotFilters en la selección normal).
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_owner, new GridCoord(1, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 0)); // ancla
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 0));
            RegisterGrid();
            var settings = AoeAttack(radius: 1);
            settings.SlotState = SlotState.Both;

            // Act
            var targets = settings.ExpandAoe(new GridCoord(2, 0), _owner);

            // Assert
            Assert.IsFalse(Contains(targets, 1, 0), "La celda del owner nunca entra al área.");
            Assert.IsTrue(Contains(targets, 3, 0));
        }

        [Test]
        public void ExpandAoe_Radius_AllyInArea_ExcludedByEntityFilter()
        {
            // Arrange — enemigo en el ancla, aliado adyacente: con EntityFilter=Enemies
            // el aliado no recibe el splash (área "filtrada", decisión de diseño).
            var enemyGuid = Guid.NewGuid();
            var allyGuid = Guid.NewGuid();
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            _grid.Register(enemyGuid, new GridCoord(2, 0));
            _grid.Register(allyGuid, new GridCoord(3, 0));
            RegisterGrid();
            var query = new FakeEntityQueryService();
            query.Relationships[enemyGuid] = EntityFilterMask.Enemies;
            query.Relationships[allyGuid] = EntityFilterMask.Allies;
            ServiceLocator.AddService<IEntityQueryService>(query, ServiceScope.Global);
            var settings = AoeAttack(radius: 1);

            // Act
            var targets = settings.ExpandAoe(new GridCoord(2, 0), _owner);

            // Assert
            Assert.IsTrue(Contains(targets, 2, 0));
            Assert.IsFalse(Contains(targets, 3, 0),
                "El aliado dentro del área queda excluido por EntityFilter.");
        }

        [Test]
        public void ExpandAoe_CustomPattern_ExpandsRelativeToPatternCenter()
        {
            // Arrange — patrón cruz 3x3 con centro (1,1) apoyado sobre el ancla (2,1).
            _grid.LoadRoom(NavGraph.Rect(5, 3));
            _grid.Register(_owner, new GridCoord(0, 0));
            RegisterGrid();
            var settings = AoeAttack(radius: 1);
            settings.SlotState = SlotState.Both;
            settings.AoeShape = AoeShape.Custom;
            settings.PatternRows = 3;
            settings.PatternCols = 3;
            settings.PatternCenter = new Vector2Int(1, 1);
            settings.PatternFlat = new[]
            {
                false, true, false,
                true, true, true,
                false, true, false,
            };

            // Act
            var targets = settings.ExpandAoe(new GridCoord(2, 1), _owner);

            // Assert — la cruz centrada en el ancla: (2,0),(1,1),(2,1),(3,1),(2,2).
            Assert.AreEqual(5, targets.Count);
            Assert.IsTrue(Contains(targets, 2, 1), "Ancla (centro del patrón).");
            Assert.IsTrue(Contains(targets, 2, 0));
            Assert.IsTrue(Contains(targets, 2, 2));
            Assert.IsTrue(Contains(targets, 1, 1));
            Assert.IsTrue(Contains(targets, 3, 1));
        }

        [Test]
        public void ExpandAoe_CustomPattern_EmptyOrNullPattern_ReturnsAnchorOnly()
        {
            // Arrange — PatternFlat degenerado (asset a medio autorar): solo el ancla.
            _grid.LoadRoom(NavGraph.Rect(3, 3));
            _grid.Register(_owner, new GridCoord(0, 0));
            RegisterGrid();
            var settings = AoeAttack(radius: 1);
            settings.SlotState = SlotState.Both;
            settings.AoeShape = AoeShape.Custom;

            // Act + Assert — null.
            settings.PatternFlat = null;
            Assert.AreEqual(1, settings.ExpandAoe(new GridCoord(1, 1), _owner).Count);

            // Act + Assert — vacío.
            settings.PatternFlat = Array.Empty<bool>();
            Assert.AreEqual(1, settings.ExpandAoe(new GridCoord(1, 1), _owner).Count);
        }

        [Test]
        public void AutoResolveTargets_Aoe_PicksAnchorAmongValidAndExpands()
        {
            // Arrange — dos enemigos adyacentes entre sí: sea cual sea el ancla random,
            // el radio 1 cubre a ambos → assert determinista pese al random.
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(2, 0));
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 0));
            RegisterGrid();
            var settings = AoeAttack(radius: 1);
            settings.AutoResolve = true;

            // Act
            var result = settings.AutoResolveTargets(new GridCoord(0, 0), _owner);

            // Assert
            Assert.IsTrue(result.WasCompleted);
            Assert.AreEqual(2, result.SelectedTargets.Count);
            Assert.IsTrue(Contains(result.SelectedTargets, 2, 0));
            Assert.IsTrue(Contains(result.SelectedTargets, 3, 0));
        }

        [Test]
        public void AutoResolveTargets_Aoe_NoValidAnchors_ReturnsNotCompleted()
        {
            // Arrange — sin enemigos no hay ancla posible.
            _grid.LoadRoom(NavGraph.Rect(5, 1));
            _grid.Register(_owner, new GridCoord(0, 0));
            RegisterGrid();
            var settings = AoeAttack(radius: 1);
            settings.AutoResolve = true;

            // Act
            var result = settings.AutoResolveTargets(new GridCoord(0, 0), _owner);

            // Assert
            Assert.IsFalse(result.WasCompleted);
            Assert.AreEqual(0, result.SelectedTargets.Count);
        }

        // Fake mínimo: relaciones por guid, default Enemies (mismo criterio permisivo
        // que la ausencia del servicio, pero controlable por test).
        private sealed class FakeEntityQueryService : IEntityQueryService
        {
            public readonly Dictionary<Guid, EntityFilterMask> Relationships =
                new Dictionary<Guid, EntityFilterMask>();

            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) => Array.Empty<Entity>();

            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) => Array.Empty<Entity>();

            public EntityFilterMask GetRelationship(Guid owner, Guid target)
                => Relationships.TryGetValue(target, out var mask) ? mask : EntityFilterMask.Enemies;
        }
    }
}
