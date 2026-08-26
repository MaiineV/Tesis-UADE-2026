using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.PreConditions.Concretes;
using Rollgeon.Tiles;
using UnityEngine;

namespace Rollgeon.PreConditions.Tests
{
    [TestFixture]
    public class PcOwnerAtRoomCenterTests
    {
        private GridManager _grid;
        private Guid _ownerId;

        [SetUp]
        public void Setup()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _grid = new GridManager();
            ServiceLocator.AddService<IGridManager>(_grid);
            _ownerId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private static PreConditionContext Ctx(Guid owner) => new PreConditionContext { OwnerGuid = owner };

        [Test]
        public void Evaluate_OwnerOnCenterTile_ReturnsTrue()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            _grid.Register(_ownerId, new GridCoord(4, 4));

            Assert.IsTrue(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_OwnerOneStepFromCenter_ReturnsFalse()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            _grid.Register(_ownerId, new GridCoord(5, 4));

            Assert.IsFalse(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_NoRoomLoaded_ReturnsFalse()
        {
            _grid.Register(_ownerId, new GridCoord(0, 0));

            Assert.IsFalse(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_OwnerNotRegistered_ReturnsFalse()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));

            Assert.IsFalse(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_NoGrid_ReturnsFalse()
        {
            ServiceLocator.Clear();

            Assert.IsFalse(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }

        [Test]
        public void Evaluate_EmptyGuid_ReturnsFalse()
        {
            Assert.IsFalse(new PcOwnerAtRoomCenter().Evaluate(new PreConditionContext()));
        }

        /// <summary>
        /// Ancla el requisito crítico: la precondición y <see cref="AINode_TeleportToRoomCenter"/>
        /// tienen que estar de acuerdo en qué casilla es "el centro". Si divergen, el gate de fase 2
        /// del Croupier se abre en una casilla a la que el teleport no lleva: el salto no mueve nada,
        /// el AINode_Once latchea igual y el ataque se gasta mudo.
        /// </summary>
        [Test]
        public void Evaluate_AfterTeleportNodeRuns_AgreesWithTeleportDestination()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            _grid.Register(_ownerId, new GridCoord(0, 0));

            var movement = new MovementService(_grid);
            var context = new AIContext { SelfGuid = _ownerId, Grid = _grid, Movement = movement };

            var result = new AINode_TeleportToRoomCenter().Tick(context);

            Assert.AreEqual(AIResult.Succeeded, result);
            Assert.IsTrue(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)));
        }

        /// <summary>
        /// El mismo ancla con el centro ardiendo: el teleport lo esquiva, así que la precondición
        /// tiene que esquivarlo también o el par vuelve a divergir.
        /// </summary>
        [Test]
        public void Evaluate_AfterTeleportNodeRuns_AgreesEvenWhenTheCenterBurns()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            _grid.Register(_ownerId, new GridCoord(0, 0));

            var tiles = new SpecialTileService();
            tiles.ConfigureForTests(() => Guid.Empty);
            ServiceLocator.AddService<ISpecialTileService>(tiles, ServiceScope.Global);

            var fire = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            fire.hideFlags = HideFlags.HideAndDontSave;
            fire.TileId = "TILE_FIRE";
            fire.TileType = SpecialTileType.Fire;
            fire.Triggers = TileTrigger.OnEnter | TileTrigger.OnTurnStart;
            fire.Category = TileEffectCategory.Damage;
            fire.EnterDamage = 6;
            fire.TurnStartDamage = 10;
            tiles.Place(fire, new[] { new GridCoord(4, 4) });

            try
            {
                var context = new AIContext
                {
                    SelfGuid = _ownerId,
                    Grid = _grid,
                    Movement = new MovementService(_grid),
                };

                Assert.AreEqual(AIResult.Succeeded, new AINode_TeleportToRoomCenter().Tick(context));
                Assert.IsTrue(_grid.TryGetPosition(_ownerId, out var landed));
                Assert.AreNotEqual(new GridCoord(4, 4), landed, "Se plantó adentro del fuego.");

                Assert.IsTrue(new PcOwnerAtRoomCenter().Evaluate(Ctx(_ownerId)),
                    "El teleport esquivó el fuego y la precondición no: el gate queda abierto en una " +
                    "casilla a la que el salto no lleva.");
            }
            finally
            {
                tiles.Dispose();
                UnityEngine.Object.DestroyImmediate(fire);
            }
        }

        /// <summary>Con el flag apagado en los dos lados el par sigue coincidiendo, en el centro
        /// ardiendo de siempre.</summary>
        [Test]
        public void Evaluate_WithTheFilterOffOnBothSides_StillAgrees()
        {
            _grid.LoadRoom(NavGraph.Rect(9, 9));
            _grid.Register(_ownerId, new GridCoord(0, 0));

            var context = new AIContext
            {
                SelfGuid = _ownerId,
                Grid = _grid,
                Movement = new MovementService(_grid),
            };

            var node = new AINode_TeleportToRoomCenter { AvoidHarmfulTiles = false };
            Assert.AreEqual(AIResult.Succeeded, node.Tick(context));

            Assert.IsTrue(new PcOwnerAtRoomCenter { AvoidHarmfulTiles = false }.Evaluate(Ctx(_ownerId)));
        }
    }
}
