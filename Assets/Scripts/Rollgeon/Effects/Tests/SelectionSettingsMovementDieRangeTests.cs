using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Effects.Selection;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Movement.Die;

namespace Rollgeon.Effects.Tests
{
    /// <summary>
    /// §6.6: con <see cref="SelectionSettings.RangeFromMovementDie"/> el rango de un
    /// Movimiento lo define la cara del dado de Movimiento; sin flag / sin servicio el
    /// <c>Range</c> autorado sigue mandando (exploración intacta).
    /// </summary>
    [TestFixture]
    public sealed class SelectionSettingsMovementDieRangeTests
    {
        private GridManager _grid;
        private Guid _owner;
        private FakeMovementDie _die;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            _grid = new GridManager();
            _owner = Guid.NewGuid();
            _die = new FakeMovementDie();

            // Pasillo 1x8: desde x=0 la distancia a cada celda es su x.
            _grid.LoadRoom(NavGraph.FromSnapshot(new GridSnapshot(8, 1, Enumerable.Repeat(true, 8).ToArray())));
            _grid.Register(_owner, new GridCoord(0, 0));
            ServiceLocator.AddService<IGridManager>(_grid, ServiceScope.Global);
            ServiceLocator.AddService<IMovementService>(new MovementService(_grid), ServiceScope.Global);
        }

        [TearDown]
        public void TearDown() => ServiceLocator.Clear();

        private static SelectionSettings Movement(int range, bool fromDie)
        {
            return new SelectionSettings
            {
                SlotState = SlotState.Empty,
                Timing = SelectionTiming.BeforeRoll,
                Range = range,
                RangeMode = RangeMode.PathReachable,
                RangeFromMovementDie = fromDie,
            };
        }

        private static int MaxX(IEnumerable<TargetRef> tiles) => tiles.Max(t => t.Coord.X);

        [Test]
        public void FlagOff_UsesAuthoredRange_EvenWithActiveRoll()
        {
            ServiceLocator.AddService<IMovementDieService>(_die, ServiceScope.Global);
            _die.Active[_owner] = 2;

            var settings = Movement(range: 4, fromDie: false);

            Assert.AreEqual(4, settings.ResolveEffectiveRange(_owner));
            Assert.AreEqual(4, MaxX(settings.ResolveValidTiles(new GridCoord(0, 0), _owner)));
        }

        [Test]
        public void FlagOn_WithoutService_FallsBackToAuthoredRange()
        {
            var settings = Movement(range: 4, fromDie: true);

            Assert.AreEqual(4, settings.ResolveEffectiveRange(_owner));
            Assert.AreEqual(4, MaxX(settings.ResolveValidTiles(new GridCoord(0, 0), _owner)));
        }

        [Test]
        public void FlagOn_WithActiveRoll_UsesRolledFace()
        {
            ServiceLocator.AddService<IMovementDieService>(_die, ServiceScope.Global);
            _die.Active[_owner] = 2;

            var settings = Movement(range: 4, fromDie: true);

            Assert.AreEqual(2, settings.ResolveEffectiveRange(_owner));
            Assert.AreEqual(2, MaxX(settings.ResolveValidTiles(new GridCoord(0, 0), _owner)));
            Assert.AreEqual(2, settings.ResolveRangeTiles(new GridCoord(0, 0), _owner).Max(c => c.X));
        }

        [Test]
        public void FlagOn_ActiveRollForOtherGuid_IsIgnored()
        {
            ServiceLocator.AddService<IMovementDieService>(_die, ServiceScope.Global);
            _die.Active[Guid.NewGuid()] = 1;
            _die.Type = DiceType.D6;

            var settings = Movement(range: 4, fromDie: true);

            Assert.AreEqual(6, settings.ResolveEffectiveRange(_owner));
        }

        [Test]
        public void FlagOn_WithoutActiveRoll_UsesDieMaxFace_AsPotentialRange()
        {
            ServiceLocator.AddService<IMovementDieService>(_die, ServiceScope.Global);
            _die.Type = DiceType.D6;

            var settings = Movement(range: 4, fromDie: true);

            Assert.AreEqual(6, settings.ResolveEffectiveRange(_owner));
            Assert.AreEqual(6, MaxX(settings.ResolveValidTiles(new GridCoord(0, 0), _owner)));
        }

        [Test]
        public void FlagOn_ManhattanFallback_AlsoUsesRolledFace()
        {
            ServiceLocator.RemoveService<IMovementService>();
            ServiceLocator.AddService<IMovementDieService>(_die, ServiceScope.Global);
            _die.Active[_owner] = 3;

            var settings = Movement(range: 4, fromDie: true);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("IMovementService not registered"));
            Assert.AreEqual(3, MaxX(settings.ResolveValidTiles(new GridCoord(0, 0), _owner)));
        }

        // ---------------- Bonus MoveRange (reward "Movimiento+", BUG-85) --------

        private void RegisterMoveRange(int bonus)
        {
            var attrs = new Rollgeon.Attributes.AttributesManager();
            var a = new Rollgeon.Attributes.ModifiableAttributes();
            a.SetAttribute<Rollgeon.Attributes.Stats.MoveRange>(
                new Rollgeon.Attributes.Stats.MoveRange(bonus));
            attrs.Register(_owner, a);
            ServiceLocator.AddService<Rollgeon.Attributes.AttributesManager>(attrs, ServiceScope.Global);
        }

        [Test]
        public void should_add_move_range_bonus_to_rolled_face_when_range_from_movement_die()
        {
            // Arrange — cara 2 + bonus 1 = rango 3.
            ServiceLocator.AddService<IMovementDieService>(_die, ServiceScope.Global);
            _die.Active[_owner] = 2;
            RegisterMoveRange(1);
            var settings = Movement(range: 4, fromDie: true);

            // Act + Assert
            Assert.AreEqual(3, settings.ResolveEffectiveRange(_owner));
            Assert.AreEqual(3, MaxX(settings.ResolveValidTiles(new GridCoord(0, 0), _owner)));
        }

        [Test]
        public void should_add_move_range_bonus_to_max_face_fallback()
        {
            // Arrange — sin tirada: potencial = MaxFace(4) + bonus 1, para que el
            // gate del botón y el hover preview queden coherentes con el rango real.
            ServiceLocator.AddService<IMovementDieService>(_die, ServiceScope.Global);
            RegisterMoveRange(1);
            var settings = Movement(range: 2, fromDie: true);

            // Act + Assert
            Assert.AreEqual(5, settings.ResolveEffectiveRange(_owner));
        }

        [Test]
        public void should_ignore_move_range_when_flag_off()
        {
            // Arrange — el bonus es exclusivo del dado de Movimiento: un selection
            // normal (ataque, skill) no debe agrandarse.
            RegisterMoveRange(3);
            var settings = Movement(range: 4, fromDie: false);

            // Act + Assert
            Assert.AreEqual(4, settings.ResolveEffectiveRange(_owner));
        }

        [Test]
        public void should_degrade_to_zero_bonus_without_attributes_manager()
        {
            // Arrange — sin AttributesManager (tests, enemigos): bonus 0, sin logs.
            ServiceLocator.AddService<IMovementDieService>(_die, ServiceScope.Global);
            _die.Active[_owner] = 2;
            var settings = Movement(range: 4, fromDie: true);

            // Act + Assert
            Assert.AreEqual(2, settings.ResolveEffectiveRange(_owner));
        }

        private sealed class FakeMovementDie : IMovementDieService
        {
            public readonly Dictionary<Guid, int> Active = new Dictionary<Guid, int>();
            public DiceType Type = DiceType.D4;

            public DiceType CurrentType => Type;
            public int LastFace => 0;
            public void SetTypeOverride(DiceType? type) { }
            public void Roll(Guid playerGuid, Action<int> onRevealed) { }
            public bool TryGetActiveRange(Guid playerGuid, out int range) => Active.TryGetValue(playerGuid, out range);
            public void ClearActiveRange() => Active.Clear();
            public void SetPresenter(IMovementDiePresenter presenter) { }
#pragma warning disable 67
            public event Action<Guid, int> OnRolled;
            public event Action OnCleared;
#pragma warning restore 67
        }
    }
}
