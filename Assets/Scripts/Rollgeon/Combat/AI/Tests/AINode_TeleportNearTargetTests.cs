using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de <see cref="AINode_TeleportNearTarget"/>: la cara opuesta de la fuga. El jefe cierra
    /// la distancia en vez de huir, sin llegar a entregarse pegado ni caer en una casilla que arde.
    /// </summary>
    [TestFixture]
    public class AINode_TeleportNearTargetTests
    {
        private const int RoomSide = 11;

        /// <summary>Las keys del presupuesto de movimiento del turno, que el nodo consume y respeta.</summary>
        private const string MoveActionKey = "__move";
        private const string KeepDistanceActionKey = "__keep_distance";

        private GridManager _grid;
        private MovementService _movement;
        private SpecialTileService _tiles;
        private Guid _boss;
        private Guid _player;
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomSide, RoomSide));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);

            // El servicio real: es el que decide si el destino era legal, y un stub que nunca falla
            // escondería justo eso.
            _movement = new MovementService(_grid);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();

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

        private AIContext NewContext(int seed) => new AIContext
        {
            SelfGuid = _boss,
            PlayerGuid = _player,
            Grid = _grid,
            Movement = _movement,
            Rng = new System.Random(seed),
        };

        private void Place(GridCoord boss, GridCoord player)
        {
            _grid.Register(_boss, boss);
            _grid.Register(_player, player);
        }

        private GridCoord BossCoord()
        {
            Assert.IsTrue(_grid.TryGetPosition(_boss, out var coord), "El jefe salió de la grilla.");
            return coord;
        }

        private void Burn(params GridCoord[] coords)
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

            _tiles.Place(def, coords);
        }

        [Test]
        public void Tick_LandsInsideTheBand()
        {
            var player = new GridCoord(5, 5);
            Place(new GridCoord(0, 0), player);
            var node = new AINode_TeleportNearTarget();

            var result = node.Tick(NewContext(1));

            Assert.AreEqual(AIResult.Succeeded, result);
            int distance = BossCoord().Manhattan(player);
            Assert.GreaterOrEqual(distance, node.MinDistance);
            Assert.LessOrEqual(distance, node.MaxDistance);
        }

        /// <summary>
        /// Pegado sería regalarle al jugador un turno franco: el kit del Croupier es todo a
        /// distancia, así que el piso de la banda es lo que lo mantiene fuera de su alcance directo.
        /// </summary>
        [Test]
        public void Tick_NeverLandsOnTopOfThePlayer_NorNextToHim()
        {
            var player = new GridCoord(5, 5);
            Place(new GridCoord(0, 0), player);

            for (int seed = 0; seed < 40; seed++)
            {
                _grid.Register(_boss, new GridCoord(0, 0));
                new AINode_TeleportNearTarget().Tick(NewContext(seed));

                Assert.AreNotEqual(player, BossCoord());
                Assert.GreaterOrEqual(BossCoord().Manhattan(player), 2);
            }
        }

        [Test]
        public void Tick_SkipsTheTilesThatBurn()
        {
            var player = new GridCoord(5, 5);
            Place(new GridCoord(0, 0), player);

            // Toda la banda menos una casilla: el salto sólo tiene ese lugar donde caer sin quemarse.
            var clean = new GridCoord(5, 7);
            var burning = new List<GridCoord>();
            foreach (var coord in _grid.Graph.AllCoords())
            {
                if (coord == clean || coord == player) continue;
                int distance = coord.Manhattan(player);
                if (distance >= 2 && distance <= 3) burning.Add(coord);
            }
            Burn(burning.ToArray());

            for (int seed = 0; seed < 20; seed++)
            {
                _grid.Register(_boss, new GridCoord(0, 0));
                new AINode_TeleportNearTarget().Tick(NewContext(seed));

                Assert.AreEqual(clean, BossCoord(), "Cayó en una casilla que arde.");
            }
        }

        /// <summary>
        /// Preferencia y no requisito: un <c>Failed</c> acá se comería el resto del turno, así que
        /// con la banda entera ardiendo salta igual.
        /// </summary>
        [Test]
        public void WithTheWholeBandBurning_ItJumpsAnyway()
        {
            var player = new GridCoord(5, 5);
            Place(new GridCoord(0, 0), player);

            var burning = new List<GridCoord>();
            foreach (var coord in _grid.Graph.AllCoords())
            {
                int distance = coord.Manhattan(player);
                if (distance >= 2 && distance <= 3) burning.Add(coord);
            }
            Burn(burning.ToArray());

            var node = new AINode_TeleportNearTarget();
            var result = node.Tick(NewContext(1));

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreNotEqual(new GridCoord(0, 0), BossCoord(), "No saltó.");
            int landed = BossCoord().Manhattan(player);
            Assert.GreaterOrEqual(landed, node.MinDistance);
            Assert.LessOrEqual(landed, node.MaxDistance);
        }

        [Test]
        public void WithTheFlagOff_TheFireDoesNotFilterAnything()
        {
            var player = new GridCoord(5, 5);
            Place(new GridCoord(0, 0), player);

            var burning = new List<GridCoord>();
            foreach (var coord in _grid.Graph.AllCoords())
            {
                int distance = coord.Manhattan(player);
                if (distance >= 2 && distance <= 3) burning.Add(coord);
            }
            Burn(burning.ToArray());

            var node = new AINode_TeleportNearTarget { AvoidHarmfulTiles = false };

            Assert.AreEqual(AIResult.Succeeded, node.Tick(NewContext(1)));
        }

        /// <summary>El salto ES el movimiento del turno: sin esto un paso posterior lo deshace.</summary>
        [Test]
        public void Tick_SpendsTheTurnsMovement()
        {
            Place(new GridCoord(0, 0), new GridCoord(5, 5));
            var context = NewContext(1);

            new AINode_TeleportNearTarget().Tick(context);

            Assert.IsTrue(context.HasExecuted(MoveActionKey));
            Assert.IsTrue(context.HasExecuted(KeepDistanceActionKey));
        }

        /// <summary>
        /// Un paso anterior ya gastó el movimiento (el teleport al centro del Pleno lo consume):
        /// Succeeded y sin mover, para no arrancarlo de donde lo plantó ese paso.
        /// </summary>
        [Test]
        public void WithTheMovementAlreadySpent_ItStaysPut()
        {
            var start = new GridCoord(0, 0);
            Place(start, new GridCoord(5, 5));

            var context = NewContext(1);
            context.MarkExecuted(MoveActionKey);

            Assert.AreEqual(AIResult.Succeeded, new AINode_TeleportNearTarget().Tick(context));
            Assert.AreEqual(start, BossCoord());
        }

        [Test]
        public void WithoutAPlayerOnTheGrid_ItFailsQuietly()
        {
            _grid.Register(_boss, new GridCoord(0, 0));

            Assert.AreEqual(AIResult.Failed, new AINode_TeleportNearTarget().Tick(NewContext(1)));
        }
    }
}
