using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Tiles;
using Rollgeon.UI.HUD.Status;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Tests de <see cref="OwnedTilesStatusProvider"/>: el fuego que un jefe dejó en el paño se
    /// lee en el jefe, desde que arde y hasta que se apaga.
    /// </summary>
    /// <remarks>
    /// El nodo que prende sólo se describe mientras marca la banda, así que su tarjeta se apagaba
    /// justo cuando empezaba a haber fuego. Esto cubre el otro lado: las casillas ya puestas.
    /// </remarks>
    [TestFixture]
    public class OwnedTilesStatusProviderTests
    {
        private GridManager _grid;
        private UnitTraitService _traits;
        private SpecialTileService _svc;
        private OwnedTilesStatusProvider _provider;
        private List<StatusIconState> _states;
        private Guid _boss;
        private Guid _player;
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(6, 6));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _traits = new UnitTraitService();
            ServiceLocator.AddService<IUnitTraitService>(_traits, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));
            _traits.Register(_player, UnitTraits.DefaultGround);

            _boss = Guid.NewGuid();

            _svc = new SpecialTileService();
            _svc.ConfigureForTests(() => _player);
            ServiceLocator.AddService<ISpecialTileService>(_svc, ServiceScope.Global);

            _provider = new OwnedTilesStatusProvider(catalog: null);
            _states = new List<StatusIconState>();
        }

        [TearDown]
        public void TearDown()
        {
            _svc?.Dispose();
            _svc = null;

            foreach (var asset in _createdAssets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _createdAssets.Clear();

            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        [Test]
        public void test_owned_tiles_fire_on_the_floor_publishes_burn_for_its_owner()
        {
            // Arrange
            PlaceFire(_boss, new GridCoord(3, 3), rounds: 2);

            // Act
            _provider.Collect(_boss, _states);

            // Assert
            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(OwnedTilesStatusProvider.FireTilesId, _states[0].Id,
                "La key es propia: status.burn es el estado del que PISA el fuego, y esta " +
                "tarjeta dice que el jefe lo mantiene.");
            Assert.AreEqual(StatusCardStyle.Terrain, _states[0].Style,
                "Habla del suelo y no del bicho, que es lo que hace que la fila sobre su cabeza " +
                "la saltee: un ícono de fuego encima del jefe diría que el jefe se quema.");
        }

        [Test]
        public void test_owned_tiles_fire_of_someone_else_stays_out_of_this_panel()
        {
            // Arrange
            PlaceFire(Guid.NewGuid(), new GridCoord(3, 3), rounds: 2);

            // Act
            _provider.Collect(_boss, _states);

            // Assert
            Assert.IsEmpty(_states,
                "El fuego de otro se lee en ESE otro. Sin el filtro por owner, cada bicho de la " +
                "sala mostraría el incendio del jefe como si fuera suyo.");
        }

        [Test]
        public void test_owned_tiles_a_whole_fire_is_one_card_with_the_longest_countdown()
        {
            // Arrange — un incendio son muchas instancias; el jugador pregunta si el piso quema,
            // no cuántas casillas hay.
            PlaceFire(_boss, new GridCoord(3, 3), rounds: 1);
            PlaceFire(_boss, new GridCoord(3, 4), rounds: 4);
            PlaceFire(_boss, new GridCoord(3, 5), rounds: 2);

            // Act
            _provider.Collect(_boss, _states);

            // Assert
            Assert.AreEqual(1, _states.Count);
            Assert.AreEqual(4, _states[0].RemainingTurns,
                "El badge dice cuándo deja de quemar, así que manda la que más dura.");
        }

        [Test]
        public void test_owned_tiles_a_floor_that_is_not_fire_publishes_nothing()
        {
            // Arrange
            Place(_boss, SpecialTileType.Heal, TileEffectCategory.Heal, new GridCoord(3, 3), 2);

            // Act
            _provider.Collect(_boss, _states);

            // Assert
            Assert.IsEmpty(_states,
                "Sólo el fuego: una casilla de curación del jefe no es algo que el jugador tenga " +
                "que esquivar.");
        }

        private void PlaceFire(Guid owner, GridCoord coord, int rounds)
            => Place(owner, SpecialTileType.Fire, TileEffectCategory.Damage, coord, rounds);

        private void Place(Guid owner, SpecialTileType type, TileEffectCategory category,
                           GridCoord coord, int rounds)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.TileType = type;
            def.Triggers = TileTrigger.OnEnter;
            def.Category = category;
            def.Affinity = TileAffinity.All;
            _createdAssets.Add(def);

            _svc.Place(def, new[] { coord },
                new TilePlacementOptions { Owner = owner, DurationRounds = rounds });
        }
    }
}
