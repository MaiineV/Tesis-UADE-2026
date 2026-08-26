using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Grid.Tests
{
    /// <summary>
    /// El filtro de casillas dañinas del centro de sala. Vive dentro del resolver y no en el nodo
    /// que reubica porque el gate que pregunta "¿ya está en el centro?" resuelve por la misma
    /// función: leyendo distinto, el salto no movería nada y el ataque se gastaría mudo.
    /// </summary>
    [TestFixture]
    public class RoomCenterResolverHarmfulTilesTests
    {
        /// <summary>Centro exacto de una sala 9x9 — lo que devuelve el resolver sin fuego.</summary>
        private static readonly GridCoord Center = new GridCoord(4, 4);

        private GridManager _grid;
        private SpecialTileService _tiles;
        private Guid _self;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _self = Guid.NewGuid();
            _grid.Register(_self, new GridCoord(0, 0));

            _tiles = new SpecialTileService();
            _tiles.ConfigureForTests(() => Guid.Empty);
            ServiceLocator.AddService<ISpecialTileService>(_tiles, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _tiles?.Dispose();
            foreach (var asset in _created)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            _created.Clear();

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private SpecialTileDefinitionSO Fire()
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.hideFlags = HideFlags.HideAndDontSave;
            def.TileId = "TILE_FIRE";
            def.TileType = SpecialTileType.Fire;
            def.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            def.Category = TileEffectCategory.Damage;
            def.EnterDamage = 6;
            def.TurnStartDamage = 10;
            _created.Add(def);
            return def;
        }

        private void Burn(params GridCoord[] coords) => _tiles.Place(Fire(), coords);

        private GridCoord Resolve(bool avoidHarmful)
        {
            Assert.IsTrue(_grid.TryGetPosition(_self, out var selfCoord));
            Assert.IsTrue(RoomCenterResolver.TryResolve(
                _grid, _self, selfCoord, out var destination, avoidHarmful));
            return destination;
        }

        [Test]
        public void WithNothingBurning_TheCenterIsTheCenter()
        {
            Assert.AreEqual(Center, Resolve(avoidHarmful: true));
        }

        [Test]
        public void WithTheCenterBurning_ItLandsOnTheNearestCleanTile()
        {
            Burn(Center);

            var destination = Resolve(avoidHarmful: true);

            Assert.AreNotEqual(Center, destination, "Se plantó adentro del fuego.");
            Assert.AreEqual(1, destination.Manhattan(Center),
                "Se fue más lejos de lo necesario: la casilla limpia más cercana está a 1.");
        }

        [Test]
        public void WithTheWholeMiddleBurning_ItStepsOutOfTheFire_NotOutOfTheRoom()
        {
            var burning = new List<GridCoord>();
            for (int x = 3; x <= 5; x++)
                for (int y = 3; y <= 5; y++)
                    burning.Add(new GridCoord(x, y));
            Burn(burning.ToArray());

            var destination = Resolve(avoidHarmful: true);

            Assert.AreEqual(2, destination.Manhattan(Center),
                "El bloque 3x3 arde entero, así que lo más cerca que hay del centro es 2.");
        }

        /// <summary>
        /// El filtro es preferencia y no requisito: un <c>false</c> acá aborta la Sequence del turno
        /// del jefe, así que con la sala entera ardiendo se reubica igual.
        /// </summary>
        [Test]
        public void WithTheWholeRoomBurning_ItStillResolves()
        {
            var burning = new List<GridCoord>();
            foreach (var coord in _grid.Graph.AllCoords()) burning.Add(coord);
            Burn(burning.ToArray());

            Assert.AreEqual(Center, Resolve(avoidHarmful: true));
        }

        [Test]
        public void WithTheFlagOff_ItLandsOnTheBurningCenter()
        {
            Burn(Center);

            Assert.AreEqual(Center, Resolve(avoidHarmful: false),
                "Apagar el flag tiene que devolver exactamente el comportamiento de antes.");
        }

        /// <summary>
        /// El invariante que hace que el ataque exista: el gate y el salto leen la misma casilla,
        /// arda o no arda el centro.
        /// </summary>
        [Test]
        public void TheAnswerIsStable_SoTheGateAndTheJumpAgree()
        {
            Burn(Center);

            var first = Resolve(avoidHarmful: true);
            _grid.Register(_self, first);

            Assert.IsTrue(RoomCenterResolver.TryResolve(_grid, _self, first, out var again, true));
            Assert.AreEqual(first, again,
                "Parado en el destino, el resolver tiene que seguir devolviendo esa casilla — si no, " +
                "el gate se abre y el salto no mueve nada.");
        }
    }
}
