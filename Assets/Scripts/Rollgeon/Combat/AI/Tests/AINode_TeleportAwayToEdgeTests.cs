using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Grid;
using Rollgeon.Movement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de <see cref="AINode_TeleportAwayToEdge"/>: el jefe se escapa a una casilla lejos del
    /// jugador y contra el borde, con los servicios reales de grilla y movimiento.
    /// </summary>
    [TestFixture]
    public class AINode_TeleportAwayToEdgeTests
    {
        private const int RoomSide = 11;

        /// <summary>Las keys del presupuesto de movimiento del turno, que el nodo consume y respeta.</summary>
        private const string MoveActionKey = "__move";
        private const string KeepDistanceActionKey = "__keep_distance";

        private GridManager _grid;
        private MovementService _movement;
        private Guid _boss;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            _grid.LoadRoom(NavGraph.Rect(RoomSide, RoomSide));

            // El servicio real: es el que decide si el destino era legal, y un stub que nunca falla
            // escondería justo eso.
            _movement = new MovementService(_grid);

            _boss = Guid.NewGuid();
            _player = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
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

        private static AINode_TeleportAwayToEdge NewNode() => new AINode_TeleportAwayToEdge();

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

        /// <summary>Anillos desde el perímetro de la sala: 0 = contra la pared.</summary>
        private static int EdgeDepth(GridCoord c) =>
            Mathf.Min(Mathf.Min(c.X, RoomSide - 1 - c.X), Mathf.Min(c.Y, RoomSide - 1 - c.Y));

        [Test]
        public void Tick_MovesAwayFromThePlayer()
        {
            var player = new GridCoord(4, 5);
            Place(new GridCoord(5, 5), player);
            var node = NewNode();

            var result = node.Tick(NewContext(1));

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.GreaterOrEqual(BossCoord().Manhattan(player), node.MinPlayerDistance,
                "El salto tiene que dejarlo al menos a MinPlayerDistance del jugador.");
        }

        [Test]
        public void Tick_LandsInsideTheEdgeBand()
        {
            Place(new GridCoord(5, 5), new GridCoord(4, 5));
            var node = NewNode();

            node.Tick(NewContext(1));

            Assert.LessOrEqual(EdgeDepth(BossCoord()), node.EdgeBandDepth,
                "El salto tiene que terminar contra el borde, no en el medio del paño.");
        }

        /// <summary>
        /// Las dos condiciones juntas y no una sola: escapar al punto más lejano solo daría siempre
        /// la misma esquina, y buscar sólo el borde lo dejaría al lado del jugador si está pegado a
        /// una pared.
        /// </summary>
        [Test]
        public void Tick_KeepsBothConditions_WithThePlayerCorneredAgainstAWall()
        {
            var player = new GridCoord(0, 0);
            Place(new GridCoord(1, 0), player);
            var node = NewNode();

            node.Tick(NewContext(3));

            var landed = BossCoord();
            Assert.GreaterOrEqual(landed.Manhattan(player), node.MinPlayerDistance);
            Assert.LessOrEqual(EdgeDepth(landed), node.EdgeBandDepth);
        }

        [Test]
        public void Tick_ConsumesTheTurnsMovementBudget()
        {
            Place(new GridCoord(5, 5), new GridCoord(4, 5));
            var context = NewContext(1);

            NewNode().Tick(context);

            Assert.IsTrue(context.HasExecuted(MoveActionKey));
            Assert.IsTrue(context.HasExecuted(KeepDistanceActionKey),
                "KeepDistance es un budget aparte: sin marcarlo, el paso de kiteo devuelve al jefe " +
                "hacia el jugador en el mismo turno en que escapó.");
        }

        [Test]
        public void Tick_LeavesTheBudgetFree_WhenConsumeMoveActionIsOff()
        {
            Place(new GridCoord(5, 5), new GridCoord(4, 5));
            var context = NewContext(1);

            new AINode_TeleportAwayToEdge { ConsumeMoveAction = false }.Tick(context);

            Assert.IsFalse(context.HasExecuted(MoveActionKey));
            Assert.IsFalse(context.HasExecuted(KeepDistanceActionKey));
        }

        /// <summary>
        /// El turno del cruce del 50%: el teleport al centro ya gastó el movimiento y este nodo no
        /// puede arrancar al jefe del hueco del Pleno.
        /// </summary>
        [Test]
        public void Tick_DoesNotMove_WhenTheMoveActionWasAlreadySpent()
        {
            var centre = new GridCoord(5, 5);
            Place(centre, new GridCoord(2, 5));
            var context = NewContext(1);
            context.MarkExecuted(MoveActionKey);

            var result = NewNode().Tick(context);

            Assert.AreEqual(AIResult.Succeeded, result,
                "Un Failed acá abortaría el Sequence que sigue al teleport del Pleno.");
            Assert.AreEqual(centre, BossCoord());
        }

        [Test]
        public void Tick_DoesNotMove_WhenTheKiteActionWasAlreadySpent()
        {
            var start = new GridCoord(5, 5);
            Place(start, new GridCoord(2, 5));
            var context = NewContext(1);
            context.MarkExecuted(KeepDistanceActionKey);

            var result = NewNode().Tick(context);

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.AreEqual(start, BossCoord());
        }

        [Test]
        public void Tick_FailsWhenTheRoomHasNoOtherFreeTile()
        {
            _grid.LoadRoom(NavGraph.Rect(2, 1));
            Place(new GridCoord(0, 0), new GridCoord(1, 0));

            var result = NewNode().Tick(NewContext(1));

            Assert.AreEqual(AIResult.Failed, result);
            Assert.AreEqual(new GridCoord(0, 0), BossCoord());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Tick_FailsWithoutAPlayerOnTheGrid()
        {
            _grid.Register(_boss, new GridCoord(5, 5));

            var result = NewNode().Tick(NewContext(1));

            Assert.AreEqual(AIResult.Failed, result);
            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// El aviso rompería a cualquier test que fije la consola con LogAssert, y el nodo corre en
        /// todos los turnos del jefe.
        /// </summary>
        [Test]
        public void Tick_FailsWithoutLogging_WhenThereIsNoMovementService()
        {
            var start = new GridCoord(5, 5);
            Place(start, new GridCoord(4, 5));
            var context = NewContext(1);
            context.Movement = null;

            var result = NewNode().Tick(context);

            Assert.AreEqual(AIResult.Failed, result);
            Assert.AreEqual(start, BossCoord(), "Sin servicio de movimiento no se reubica nada.");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Tick_SameSeedLandsOnTheSameTile()
        {
            var first = LandingFor(seed: 7);
            var second = LandingFor(seed: 7);

            Assert.AreEqual(first, second,
                "Con la misma seed el destino tiene que repetirse, o el combate no es reproducible.");
        }

        [Test]
        public void Tick_DifferentSeedsSpreadOverManyTiles()
        {
            var landings = new HashSet<GridCoord>();
            for (int seed = 0; seed < 20; seed++) landings.Add(LandingFor(seed));

            Assert.Greater(landings.Count, 4,
                "Si el salto cae siempre en la misma casilla la pelea se aprende de memoria.");
        }

        [Test]
        public void Tick_NeverLandsOnThePlayerNorOutsideTheRoom()
        {
            var player = new GridCoord(4, 5);
            for (int seed = 0; seed < 30; seed++)
            {
                var landed = LandingFor(seed, player);
                Assert.AreNotEqual(player, landed, "Se teletransportó encima del jugador.");
                Assert.IsTrue(_grid.InBounds(landed), $"{landed} está fuera de la sala.");
                Assert.IsTrue(_grid.IsWalkable(landed), $"{landed} no es caminable.");
            }
        }

        // =====================================================================
        // Techo de distancia
        // =====================================================================

        /// <summary>
        /// La sala partida separa las dos medidas: hay casillas de borde a Manhattan 6 del jugador
        /// que cuestan 26 pasos, y con el techo en Manhattan el jefe aterrizaría ahí.
        /// </summary>
        [Test]
        public void Tick_StaysInsideTheCap_MeasuredInGraphStepsAndNotManhattan()
        {
            const int cap = 6;
            var player = new GridCoord(0, 4);
            LoadRoomSplitByAWall();

            var trap = new GridCoord(0, 10);
            Assert.LessOrEqual(trap.Manhattan(player), cap, "La trampa dejó de ser una trampa.");
            Assert.Greater(StepsFromPlayer(player, trap), cap,
                "La sala no obliga a rodear: el test pasaría con el techo medido en Manhattan.");

            var node = new AINode_TeleportAwayToEdge { MaxDistanceFromPlayer = cap };
            for (int seed = 0; seed < 20; seed++)
            {
                var landed = LandingWith(node, seed, new GridCoord(5, 4), player);
                Assert.LessOrEqual(StepsFromPlayer(player, landed), cap,
                    $"Con seed {seed} cayó en {landed}: el jugador no llega a cruzar hasta ahí.");
            }
        }

        [Test]
        public void Tick_WithoutACap_KeepsTheOldReach()
        {
            var player = new GridCoord(4, 5);
            var node = new AINode_TeleportAwayToEdge { MaxDistanceFromPlayer = 0 };

            var landed = LandingWith(node, seed: 1, bossStart: new GridCoord(5, 5), player: player);

            Assert.GreaterOrEqual(landed.Manhattan(player), node.MinPlayerDistance);
            Assert.LessOrEqual(EdgeDepth(landed), node.EdgeBandDepth);
        }

        /// <summary>
        /// El techo es el guardrail y el piso es sabor: cuando se contradicen manda el techo, y el
        /// sorteo no queda vacío por la contradicción.
        /// </summary>
        [Test]
        public void Tick_TheCapWinsOverTheFloor_WhenTheyContradict()
        {
            const int cap = 3;
            var player = new GridCoord(4, 5);
            var node = new AINode_TeleportAwayToEdge { MinPlayerDistance = 8, MaxDistanceFromPlayer = cap };
            var start = new GridCoord(5, 5);
            Place(start, player);

            var result = node.Tick(NewContext(1));

            Assert.AreEqual(AIResult.Succeeded, result, "La contradicción no puede leerse como Failed.");
            var landed = BossCoord();
            Assert.AreNotEqual(start, landed, "Se quedó parado en vez de escapar corto.");
            Assert.LessOrEqual(StepsFromPlayer(player, landed), cap);
            Assert.Less(landed.Manhattan(player), node.MinPlayerDistance,
                "Con el techo por debajo del piso, el piso queda sin alcanzar y eso está bien.");
        }

        [Test]
        public void Tick_FailsWithoutMoving_WhenNothingIsFreeInsideTheCap()
        {
            var player = new GridCoord(0, 0);
            var start = new GridCoord(1, 0);
            Place(start, player);
            // Las dos únicas casillas a un paso del jugador ocupadas: adentro del techo no queda nada.
            _grid.Register(Guid.NewGuid(), new GridCoord(0, 1));

            var result = new AINode_TeleportAwayToEdge { MaxDistanceFromPlayer = 1 }.Tick(NewContext(1));

            Assert.AreEqual(AIResult.Failed, result);
            Assert.AreEqual(start, BossCoord());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Tick_WithACap_StillSpreadsOverManyTiles()
        {
            var node = new AINode_TeleportAwayToEdge { MaxDistanceFromPlayer = 6 };
            var landings = new HashSet<GridCoord>();

            for (int seed = 0; seed < 20; seed++)
                landings.Add(LandingWith(node, seed, new GridCoord(5, 5), new GridCoord(4, 5)));

            Assert.Greater(landings.Count, 4,
                "El techo no puede colapsar el sorteo a un puñado de casillas memorizables.");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private GridCoord LandingFor(int seed) => LandingFor(seed, new GridCoord(4, 5));

        private GridCoord LandingFor(int seed, GridCoord player)
        {
            Place(new GridCoord(5, 5), player);
            NewNode().Tick(NewContext(seed));
            return BossCoord();
        }

        private GridCoord LandingWith(
            AINode_TeleportAwayToEdge node, int seed, GridCoord bossStart, GridCoord player)
        {
            Place(bossStart, player);
            node.Tick(NewContext(seed));
            return BossCoord();
        }

        /// <summary>
        /// Sala partida en dos por una pared con un solo paso, en la columna de más a la derecha.
        /// </summary>
        private void LoadRoomSplitByAWall()
        {
            var walkable = new bool[RoomSide * RoomSide];
            for (int i = 0; i < walkable.Length; i++) walkable[i] = true;
            for (int x = 0; x < RoomSide - 1; x++) walkable[5 * RoomSide + x] = false;

            _grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(RoomSide, RoomSide, walkable)));
        }

        /// <summary>
        /// Pasos de grafo del jugador a la casilla, medidos con el A* del servicio real.
        /// </summary>
        /// <remarks>
        /// Saca al jefe de la grilla antes de medir: parado en el destino se tapa a sí mismo y el
        /// camino da vacío.
        /// </remarks>
        private int StepsFromPlayer(GridCoord player, GridCoord target)
        {
            _grid.Unregister(_boss);
            var path = _movement.FindPath(player, target);
            Assert.IsNotEmpty(path, $"No hay camino de {player} a {target}.");
            return path.Count - 1;
        }
    }
}
