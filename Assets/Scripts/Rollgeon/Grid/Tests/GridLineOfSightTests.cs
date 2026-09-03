using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects.Selection;
using Rollgeon.Entities;

namespace Rollgeon.Grid.Tests
{
    /// <summary>
    /// La línea de visión del proyecto: qué bloquea, qué se ignora y el corte de esquina
    /// (nacido de un bug de playtest que hasta hoy no tenía regresión).
    /// </summary>
    [TestFixture]
    public sealed class GridLineOfSightTests
    {
        private sealed class StubQuery : IEntityQueryService
        {
            public Func<Guid, Guid, EntityFilterMask> Relationship;
            public IEnumerable<Entity> GetAllEnemiesOf(Guid ownerGuid) { yield break; }
            public IEnumerable<Entity> GetAllAlliesOf(Guid ownerGuid) { yield break; }
            public EntityFilterMask GetRelationship(Guid owner, Guid target)
                => Relationship?.Invoke(owner, target) ?? EntityFilterMask.None;
        }

        private GridManager _grid;
        private Guid _self;
        private Guid _target;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();

            // Arrange compartido: sala 9×9, atacante y objetivo en la misma fila.
            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            _self = Guid.NewGuid();
            _target = Guid.NewGuid();
            _grid.Register(_self, new GridCoord(0, 4));
            _grid.Register(_target, new GridCoord(6, 4));
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        [Test]
        public void should_be_clear_when_nothing_stands_between()
        {
            Assert.IsTrue(GridLineOfSight.HasClearLine(
                _grid, new GridCoord(0, 4), new GridCoord(6, 4), _self, _target));
        }

        [Test]
        public void should_block_when_an_occupant_stands_on_the_line()
        {
            // Arrange: un prop bloqueante en el medio de la fila.
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 4));

            // Act + Assert
            Assert.IsFalse(GridLineOfSight.HasClearLine(
                _grid, new GridCoord(0, 4), new GridCoord(6, 4), _self, _target));
        }

        [Test]
        public void should_not_block_when_the_occupant_is_an_ally_of_the_attacker()
        {
            // Arrange: otro enemigo (aliado de _self) parado en el medio de la fila, con un
            // IEntityQueryService que los declara aliados entre sí.
            var otherEnemy = Guid.NewGuid();
            _grid.Register(otherEnemy, new GridCoord(3, 4));
            ServiceLocator.AddService<IEntityQueryService>(new StubQuery
            {
                Relationship = (owner, target) => EntityFilterMask.Allies,
            }, ServiceScope.Global);

            // Act + Assert
            Assert.IsTrue(GridLineOfSight.HasClearLine(
                _grid, new GridCoord(0, 4), new GridCoord(6, 4), _self, _target),
                "Los enemigos entre ellos no se tapan la línea — generaba situaciones raras " +
                "con varios amontonados (feedback de playtest).");
        }

        [Test]
        public void should_still_block_when_the_occupant_is_not_an_ally_of_the_attacker()
        {
            // Arrange: mismo escenario, pero el servicio dice que NO son aliados (ej. un prop
            // neutral, o una entidad de otro bando) — sigue bloqueando como siempre.
            var neutral = Guid.NewGuid();
            _grid.Register(neutral, new GridCoord(3, 4));
            ServiceLocator.AddService<IEntityQueryService>(new StubQuery
            {
                Relationship = (owner, target) => EntityFilterMask.None,
            }, ServiceScope.Global);

            Assert.IsFalse(GridLineOfSight.HasClearLine(
                _grid, new GridCoord(0, 4), new GridCoord(6, 4), _self, _target),
                "Un ocupante que NO es aliado del atacante sigue bloqueando la línea.");
        }

        [Test]
        public void should_block_when_a_wall_cell_interrupts_the_line()
        {
            // Arrange: (3,4) no caminable — pared.
            var walkable = new bool[81];
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 9; x++)
                    walkable[y * 9 + x] = !(x == 3 && y == 4);
            _grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(9, 9, walkable)));

            Assert.IsFalse(GridLineOfSight.HasClearLine(
                _grid, new GridCoord(0, 4), new GridCoord(6, 4), _self, _target));
        }

        [Test]
        public void should_ignore_the_two_ignored_guids_as_blockers()
        {
            // Arrange: el "bloqueo" del medio es el propio target (empujes, footprints raros).
            _grid.Unregister(_target);
            _grid.Register(_target, new GridCoord(3, 4));

            Assert.IsTrue(GridLineOfSight.HasClearLine(
                _grid, new GridCoord(0, 4), new GridCoord(6, 4), _self, _target),
                "Los dos guids ignorados (atacante/objetivo) no bloquean la línea.");
        }

        [Test]
        public void should_not_evaluate_origin_nor_destination_cells()
        {
            // El propio origen y el propio destino nunca bloquean, aunque estén ocupados.
            Assert.IsTrue(GridLineOfSight.HasClearLine(
                _grid, new GridCoord(0, 4), new GridCoord(6, 4),
                Guid.NewGuid(), Guid.NewGuid()),
                "Origen y destino no se evalúan — sólo las celdas estrictamente intermedias.");
        }

        [Test]
        public void should_return_true_when_from_equals_to()
        {
            Assert.IsTrue(GridLineOfSight.HasClearLine(
                _grid, new GridCoord(2, 2), new GridCoord(2, 2), _self, _target));
        }

        [Test]
        public void should_block_diagonal_corner_cut_when_a_flank_cell_is_occupied()
        {
            // Regresión del bug de playtest: paso diagonal (0,0)→(1,1) con la celda de flanco
            // (1,0) ocupada — la línea pasa JUSTO por la esquina del obstáculo sin pisarlo.
            _grid.Register(Guid.NewGuid(), new GridCoord(1, 0));

            Assert.IsFalse(GridLineOfSight.HasClearLine(
                _grid, new GridCoord(0, 0), new GridCoord(2, 2), _self, _target),
                "El corte de esquina tiene que bloquear: 'el enemigo veía a través del " +
                "borde de una mesa'.");
        }

        [Test]
        public void should_filter_visible_removing_only_shadowed_tiles()
        {
            // Arrange: bloqueo en (3,4); detrás de él (4,4)..(6,4) quedan en sombra.
            _grid.Register(Guid.NewGuid(), new GridCoord(3, 4));
            var tiles = new System.Collections.Generic.HashSet<GridCoord>
            {
                new GridCoord(1, 4), new GridCoord(2, 4),
                new GridCoord(4, 4), new GridCoord(5, 4),
                new GridCoord(1, 5),
            };

            // Act
            GridLineOfSight.FilterVisible(_grid, new GridCoord(0, 4), tiles, _self, _target);

            // Assert
            CollectionAssert.AreEquivalent(
                new[] { new GridCoord(1, 4), new GridCoord(2, 4), new GridCoord(1, 5) },
                tiles,
                "Sólo las celdas detrás del bloqueo salen del set; el resto queda intacto.");
        }
    }
}
