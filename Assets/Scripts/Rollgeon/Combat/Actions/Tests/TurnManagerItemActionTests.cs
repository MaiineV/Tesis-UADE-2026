using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects;
using UnityEngine;

namespace Rollgeon.Combat.Actions.Tests
{
    /// <summary>
    /// Reglas de turno que el GDD pide <b>solo</b> para items activos con
    /// <c>ConsumesAction</c>: se usan en tu turno y una vez por <c>ActionId</c>
    /// ("si dos items comparten el mismo ActionId, solo uno de ellos puede usarse por
    /// turno"). El resto de las acciones sigue sin límite por turno — el único
    /// presupuesto es el pool de rolls.
    /// </summary>
    [TestFixture]
    public sealed class TurnManagerItemActionTests
    {
        private TurnManager _tm;
        private FakeRollPoolService _rolls;
        private List<ActionDefinitionSO> _created;
        private Guid _player;
        private Guid _enemy;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _player = Guid.NewGuid();
            _enemy = Guid.NewGuid();

            _rolls = new FakeRollPoolService();
            _rolls.Current[_player] = 5;

            _tm = new TurnManager();
            _tm.ConfigureForTests(_rolls, actions: null, ruleset: null);
            _tm.SetActingGuidForTests(_player);

            _created = new List<ActionDefinitionSO>();
        }

        [TearDown]
        public void TearDown()
        {
            _tm?.Dispose();
            _tm = null;
            foreach (var d in _created) if (d != null) UnityEngine.Object.DestroyImmediate(d);
            _created = null;
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ------------------------------------------------------------------
        // Turno propio
        // ------------------------------------------------------------------

        [Test]
        public void test_useItem_duringYourTurn_isAllowed()
        {
            // Act + Assert
            Assert.IsTrue(_tm.CanExecute(UseItem("item.potion"), _player, out _));
        }

        [Test]
        public void test_useItem_duringAnotherActorsTurn_isRejected()
        {
            // Arrange — el turno pasa al enemigo.
            _tm.SetActingGuidForTests(_enemy);

            // Act
            bool ok = _tm.CanExecute(UseItem("item.potion"), _player, out var reason);

            // Assert
            Assert.IsFalse(ok);
            StringAssert.Contains("turn", reason);
        }

        [Test]
        public void test_outOfCombat_theTurnGateDoesNotApply()
        {
            // Arrange — fuera de combate nadie dispara OnTurnStarted; los items no
            // pueden quedar bloqueados por eso.
            _rolls.InCombat = false;
            _tm.SetActingGuidForTests(Guid.Empty);

            // Act + Assert
            Assert.IsTrue(_tm.CanExecute(UseItem("item.potion"), _player, out _));
        }

        [Test]
        public void test_nonItemActions_areNotGatedByTheTurn()
        {
            // Arrange — el gate es exclusivo de items: mover o atacar no lo mira.
            _tm.SetActingGuidForTests(_enemy);
            var attack = UseItem("action.attack");
            attack.Type = ActionType.Attack;

            // Act + Assert
            Assert.IsTrue(_tm.CanExecute(attack, _player, out _));
        }

        // ------------------------------------------------------------------
        // ActionId compartido — uno por turno
        // ------------------------------------------------------------------

        [Test]
        public void test_sameActionId_cannotBeUsedTwiceInATurn()
        {
            // Arrange
            Assert.IsTrue(_tm.TryExecute(UseItem("item.potion"), _player, Ctx()));

            // Act
            bool ok = _tm.CanExecute(UseItem("item.potion"), _player, out var reason);

            // Assert
            Assert.IsFalse(ok);
            StringAssert.Contains("already used", reason);
        }

        [Test]
        public void test_twoItemsSharingAnActionId_blockEachOther()
        {
            // Arrange — el caso que el GDD nombra: todas las pociones con el mismo
            // ActionId se limitan a una por turno aunque sean items distintos.
            Assert.IsTrue(_tm.TryExecute(UseItem("item.potion"), _player, Ctx()));

            // Act — otro ActionDefinition, mismo ActionId.
            bool ok = _tm.CanExecute(UseItem("item.potion"), _player, out _);

            // Assert
            Assert.IsFalse(ok);
        }

        [Test]
        public void test_differentActionIds_doNotBlockEachOther()
        {
            // Arrange
            Assert.IsTrue(_tm.TryExecute(UseItem("item.potion"), _player, Ctx()));

            // Act + Assert
            Assert.IsTrue(_tm.CanExecute(UseItem("item.bomb"), _player, out _));
        }

        [Test]
        public void test_aNewTurn_clearsTheUsedActionIds()
        {
            // Arrange
            _tm.TryExecute(UseItem("item.potion"), _player, Ctx());
            Assert.IsFalse(_tm.CanExecute(UseItem("item.potion"), _player, out _));

            // Act — arranca un turno nuevo por el bus, como en el juego. Register()
            // corta temprano sin IRollPoolService en el locator, y sin llegar al final
            // no se suscribe a OnTurnStarted.
            ServiceLocator.AddService<Rollgeon.Combat.Rolls.IRollPoolService>(_rolls);
            _tm.Register();
            EventManager.Trigger(EventName.OnTurnStarted, _player);

            // Assert
            Assert.IsTrue(_tm.CanExecute(UseItem("item.potion"), _player, out _));
        }

        [Test]
        public void test_theActionIdIsSpentEvenIfTheEffectFails()
        {
            // Arrange — el roll ya se cobro y el turno ya se gasto: reintentar seria
            // gratis y rompe la economia.
            var action = UseItem("item.potion");
            action.Effect.Effects.Add(new Eff_Fail());

            // Act
            bool ok = _tm.TryExecute(action, _player, Ctx());

            // Assert
            Assert.IsFalse(ok, "el efecto devolvio false");
            Assert.IsTrue(_tm.IsItemActionUsedThisTurn("item.potion"),
                "el ActionId igual quedo gastado");
        }

        [Test]
        public void test_nonItemActions_canRepeatInTheSameTurn()
        {
            // Arrange — sin limite de acciones por turno para lo que no es item.
            var attack = UseItem("action.attack");
            attack.Type = ActionType.Attack;

            // Act
            _tm.TryExecute(attack, _player, Ctx());

            // Assert
            Assert.IsTrue(_tm.CanExecute(attack, _player, out _));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private ActionDefinitionSO UseItem(string actionId)
        {
            var def = ScriptableObject.CreateInstance<ActionDefinitionSO>();
            def.ActionId = actionId;
            def.Type = ActionType.UseItem;
            def.Effect = new EffectData();
            _created.Add(def);
            return def;
        }

        private EffectContext Ctx()
        {
            return new EffectContext { SourceGuid = _player, TargetGuid = _player, lastResult = true };
        }

        [Serializable]
        private sealed class Eff_Fail : BaseEffect
        {
            public override string GetEffectName() => "Fail";
            public override bool ApplyEffect(EffectContext context) => false;
        }
    }
}
