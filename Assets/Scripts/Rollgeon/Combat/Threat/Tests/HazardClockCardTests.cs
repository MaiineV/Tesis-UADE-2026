using System;
using System.Collections.Generic;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.AI;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using UnityEngine;

namespace Rollgeon.Combat.Threat.Tests
{
    /// <summary>
    /// La moneda del Cajero se vencía sin avisar. El reloj lo publica su dueño y el hover lo busca
    /// por subject, igual que la mecha de cada bomba.
    /// </summary>
    [TestFixture]
    public class HazardClockCardTests
    {
        private GameObject _host;
        private HazardTooltipInfo _tooltip;
        private HazardDefinitionSO _coin;
        private FakeIntentService _intents;

        private Guid _owner;
        private Guid _instance;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _owner = Guid.NewGuid();
            _instance = Guid.NewGuid();

            _intents = new FakeIntentService();
            ServiceLocator.AddService<IEnemyIntentService>(_intents, ServiceScope.Global);

            _coin = ScriptableObject.CreateInstance<HazardDefinitionSO>();
            _coin.hideFlags = HideFlags.HideAndDontSave;
            _coin.Damage = 0; // La ficha no pega: sin reloj, su panel no tiene ni una tarjeta.
            _coin.SourceId = Guid.NewGuid().ToString();

            _host = new GameObject("HazardHover") { hideFlags = HideFlags.HideAndDontSave };
            _tooltip = _host.AddComponent<HazardTooltipInfo>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            if (_coin != null) UnityEngine.Object.DestroyImmediate(_coin);

            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void test_hazard_clock_with_turns_left_shows_the_countdown_badge()
        {
            // Arrange
            _tooltip.Bind(_coin, _instance, _owner);
            _intents.Publish(_instance, turnsAway: 2);

            // Act
            var cards = _tooltip.CollectCards();

            // Assert
            Assert.AreEqual(1, cards.Count);
            Assert.AreEqual(HazardTooltipInfo.ClockTicksKey, cards[0].Id);
            Assert.AreEqual(2, cards[0].RemainingTurns);
        }

        [Test]
        public void test_hazard_clock_at_zero_drops_the_badge_and_says_it_goes_now()
        {
            // Arrange
            _tooltip.Bind(_coin, _instance, _owner);
            _intents.Publish(_instance, turnsAway: 0);

            // Act
            var cards = _tooltip.CollectCards();

            // Assert
            Assert.AreEqual(HazardTooltipInfo.ClockDueKey, cards[0].Id);
            Assert.IsNull(cards[0].RemainingTurns, "Un 0 al lado del título se lee como cero turnos.");
        }

        [Test]
        public void test_hazard_clock_of_another_instance_is_never_shown_as_mine()
        {
            // Arrange
            _tooltip.Bind(_coin, _instance, _owner);
            _intents.Publish(Guid.NewGuid(), turnsAway: 1);

            // Act
            var cards = _tooltip.CollectCards();

            // Assert
            Assert.IsEmpty(cards, "El reloj de otra moneda no es el de ésta.");
        }

        [Test]
        public void test_room_hazard_without_an_owner_shows_no_clock()
        {
            // Arrange
            _tooltip.Bind(_coin);
            _intents.Publish(_instance, turnsAway: 1);

            // Act
            var cards = _tooltip.CollectCards();

            // Assert
            Assert.IsEmpty(cards, "Sin dueño no hay árbol a quien preguntarle qué le va a pasar.");
        }

        private sealed class FakeIntentService : IEnemyIntentService
        {
            private readonly List<AIIntent> _standing = new List<AIIntent>();

            public void Publish(Guid subject, int turnsAway) =>
                _standing.Add(new AIIntent(
                    AIIntentTextKeys.CashierVault, "Se la lleva la caja",
                    damage: 0, kind: AttackKind.Environmental,
                    turnsAway: turnsAway, subjectGuid: subject));

            public bool TryRead(Guid enemyId, List<AIIntent> standing, List<AIIntent> next,
                                List<AIIntent> options = null)
            {
                standing?.Clear();
                next?.Clear();
                options?.Clear();
                standing?.AddRange(_standing);
                return true;
            }

            public bool TryReadReach(Guid enemyId, HashSet<GridCoord> into)
            {
                into?.Clear();
                return false;
            }
        }
    }
}
