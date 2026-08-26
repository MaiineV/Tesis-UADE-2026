using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Tiles.Tests
{
    /// <summary>
    /// Qué casilla cuenta como dañina para los reacomodos de los jefes. Sale de la data de la
    /// definición y no de una lista de <see cref="SpecialTileType"/>.
    /// </summary>
    [TestFixture]
    public class HarmfulTileQueryTests
    {
        private GridManager _grid;
        private SpecialTileService _tiles;
        private Guid _player;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(7, 7));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            _player = Guid.NewGuid();
            _grid.Register(_player, new GridCoord(0, 0));

            _tiles = new SpecialTileService();
            _tiles.ConfigureForTests(() => _player);
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

        private SpecialTileDefinitionSO Definition(Action<SpecialTileDefinitionSO> configure)
        {
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.hideFlags = HideFlags.HideAndDontSave;
            configure(def);
            _created.Add(def);
            return def;
        }

        private SpecialTileDefinitionSO Fire() => Definition(d =>
        {
            d.TileId = "TILE_FIRE";
            d.TileType = SpecialTileType.Fire;
            d.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            d.Category = TileEffectCategory.Damage;
            d.EnterDamage = 6;
            d.TurnStartDamage = 10;
        });

        private SpecialTileDefinitionSO Telegraph() => Definition(d =>
        {
            d.TileId = "TILE_TELEGRAPH";
            d.TileType = SpecialTileType.Telegraph;
            d.Category = TileEffectCategory.Telegraph;
        });

        [Test]
        public void ATileThatBurnsOnEntry_CountsAsHarmful()
        {
            Assert.IsTrue(HarmfulTileQuery.DealsDamage(Fire()));
        }

        [Test]
        public void ATileThatOnlyPoisons_CountsAsHarmful()
        {
            var poison = Definition(d =>
            {
                d.TileType = SpecialTileType.Poison;
                d.StatusTurns = 3;
                d.StatusTickDamage = 5;
            });

            Assert.IsTrue(HarmfulTileQuery.DealsDamage(poison),
                "El veneno no pega al entrar, pero cobra igual: dejarlo pasar es pisarlo.");
        }

        /// <summary>
        /// El charco eléctrico no hace daño real, paraliza. <c>AIVirtualEnterDamage</c> es el campo
        /// con el que la data declara justamente eso, así que cuenta.
        /// </summary>
        [Test]
        public void ATileThatOnlyStuns_CountsAsHarmful_ThroughItsVirtualCost()
        {
            var puddle = Definition(d =>
            {
                d.TileType = SpecialTileType.ElectricPuddle;
                d.AIVirtualEnterDamage = 25;
            });

            Assert.IsTrue(HarmfulTileQuery.DealsDamage(puddle));
        }

        [Test]
        public void TilesThatCostNothing_AreNotHarmful()
        {
            Assert.IsFalse(HarmfulTileQuery.DealsDamage(Telegraph()));
            Assert.IsFalse(HarmfulTileQuery.DealsDamage(null),
                "Una instancia sin definición no puede prohibir una casilla.");
        }

        [Test]
        public void ACoordWithNothingOnIt_IsNotHarmful()
        {
            Assert.IsFalse(HarmfulTileQuery.IsHarmfulAt(new GridCoord(3, 3)));
        }

        [Test]
        public void ACoordUnderFire_IsHarmful()
        {
            _tiles.Place(Fire(), new[] { new GridCoord(3, 3) });

            Assert.IsTrue(HarmfulTileQuery.IsHarmfulAt(new GridCoord(3, 3)));
            Assert.IsFalse(HarmfulTileQuery.IsHarmfulAt(new GridCoord(4, 3)),
                "La casilla de al lado no está cubierta por esa instancia.");
        }

        /// <summary>
        /// Las casillas se solapan y <c>TryGetTileAt</c> devuelve una sola: leyendo por ahí, fuego
        /// debajo de un telegraph daría limpio y el jefe saltaría adentro.
        /// </summary>
        [Test]
        public void FireUnderAHarmlessTile_IsStillHarmful()
        {
            var coord = new GridCoord(3, 3);
            _tiles.Place(Fire(), new[] { coord });
            _tiles.Place(Telegraph(), new[] { coord });

            Assert.IsTrue(HarmfulTileQuery.IsHarmfulAt(coord));
        }

        /// <summary>Sin servicio degrada a "sin filtro", no a "todo prohibido".</summary>
        [Test]
        public void WithoutTheTileService_NothingIsHarmful()
        {
            ServiceLocator.RemoveService<ISpecialTileService>();

            Assert.IsFalse(HarmfulTileQuery.IsHarmfulAt(new GridCoord(3, 3)));
        }
    }
}
