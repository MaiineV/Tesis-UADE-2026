using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects;
using Rollgeon.Entities.Traits;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Tiles;
using Rollgeon.Upgrades.Dice.Effects;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Stacking GDD de los rastros: dos copias del mismo encantamiento no duplican las
    /// casillas — solo la primera coloca, y la duración suma <c>ExtraRoundsPerCopy</c>.
    /// </summary>
    [TestFixture]
    public class EffPlaceTrailTilesStackingTests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private DiceEnchantmentService _enchSvc;
        private SpecialTileService _tiles;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
            var grid = new GridManager();
            grid.LoadRoom(NavGraph.Rect(5, 1));
            ServiceLocator.AddService<IGridManager>(grid, ServiceScope.Global);
            ServiceLocator.AddService<IMovementService>(new MovementService(grid), ServiceScope.Global);
            ServiceLocator.AddService<IUnitTraitService>(new UnitTraitService(), ServiceScope.Global);
            _tiles = new SpecialTileService();
            ServiceLocator.AddService<ISpecialTileService>(_tiles, ServiceScope.Global);
        }

        [TearDown]
        public void TearDown()
        {
            _tiles?.Dispose();
            _enchSvc?.Dispose();
            foreach (var o in _created) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _created.Clear();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        private EffectContext WalkCtx(Guid player, int enchSlot)
        {
            var path = new List<GridCoord> { new GridCoord(0, 0), new GridCoord(1, 0), new GridCoord(2, 0) };
            return new EffectContext
            {
                SourceGuid = player,
                TriggerContext = new ScratchTriggerContext
                {
                    Scratch = new EnchantmentScratch(),
                    Slot = new EnchantmentSlotRef(DiceType.D6, EnchantmentSlotRef.MovementDieSlot, enchSlot),
                    Channel = Upgrades.ScratchChannel.DiceEnchantment,
                    Path = path,
                    TilesTraversed = 2,
                    TilesTraversedThisTurn = 2,
                },
            };
        }

        [Test]
        public void SecondCopy_DoesNotDuplicateTiles_AndExtendsDuration()
        {
            var bag = ScriptableObject.CreateInstance<DiceBagSO>();
            bag.Dice = new List<DiceType> { DiceType.D6 };
            _created.Add(bag);
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            _created.Add(ench);
            var def = ScriptableObject.CreateInstance<SpecialTileDefinitionSO>();
            def.TileId = "TILE_TEST_TRAIL";
            def.Triggers = TileTrigger.OnEnter;
            def.DefaultDurationRounds = 1;
            _created.Add(def);

            _enchSvc = new DiceEnchantmentService(config: null);
            _enchSvc.InitializeFromBag(bag);
            _enchSvc.Bag.AddEnchantment(EnchantmentSlotRef.MovementDieSlot, ench);
            _enchSvc.Bag.AddEnchantment(EnchantmentSlotRef.MovementDieSlot, ench);
            ServiceLocator.AddService<IDiceEnchantmentService>(_enchSvc, ServiceScope.Global);
            var player = Guid.NewGuid();

            var eff = new EffPlaceTrailTiles { Definition = def, DurationRounds = 1, ExtraRoundsPerCopy = 1 };
            Assert.IsTrue(eff.ApplyEffect(WalkCtx(player, enchSlot: 0)));
            Assert.IsTrue(eff.ApplyEffect(WalkCtx(player, enchSlot: 1)));

            var instances = _tiles.ActiveInstances().ToList();
            Assert.AreEqual(2, instances.Count, "Dos celdas abandonadas, una casilla por celda.");
            Assert.IsTrue(instances.All(i => i.RemainingRounds == 2), "Dos copias ⇒ 1 + 1 rondas.");
        }
    }
}
