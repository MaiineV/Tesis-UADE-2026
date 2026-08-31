using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Effects;
using UnityEngine;

namespace Rollgeon.Combat.Actions.Tests
{
    /// <summary>
    /// PRE-01 del GDD de Ítems Activos: el ítem solo se usa en el turno propio, dentro
    /// de combate. Es una regla exclusiva de items — el resto de las acciones no mira
    /// el turno.
    /// <para>
    /// El GDD <b>no</b> pone tope de usos: "el jugador intenta activar el ítem dos veces
    /// en el mismo turno → Permitido, mientras haya rolls disponibles". El único
    /// presupuesto sigue siendo el pool de rolls.
    /// </para>
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
        // Sin limite de repeticiones
        // ------------------------------------------------------------------

        [Test]
        public void test_theSameItemCanBeUsedTwiceInOneTurn()
        {
            // El GDD lo dice explicitamente en edge cases: "el jugador intenta activar
            // el item dos veces en el mismo turno -> Permitido, mientras haya rolls".
            var action = UseItem("item.potion");
            Assert.IsTrue(_tm.TryExecute(action, _player, Ctx()));

            Assert.IsTrue(_tm.CanExecute(action, _player, out _));
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
    }
}
