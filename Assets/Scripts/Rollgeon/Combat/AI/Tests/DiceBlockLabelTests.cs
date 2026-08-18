using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.DiceBlock;

namespace Rollgeon.Combat.AI.Tests
{
    /// <summary>
    /// Tests de la etiqueta del candado: <see cref="IDiceBlockService.Block(int, string)"/> guarda
    /// <b>quién</b> se llevó el dado, para que la UI pueda decirlo.
    /// </summary>
    /// <remarks>
    /// Con el Croupier, el número que canta la ruleta es a la vez el sector que detona y el dado que
    /// confisca. Sin la etiqueta, esas dos mitades de la misma frase no se tocan en pantalla: el
    /// jugador ve un bloque encendido por un lado y un candado por el otro.
    /// </remarks>
    [TestFixture]
    public class DiceBlockLabelTests
    {
        private DiceBlockService _dice;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();

            _player = Guid.NewGuid();
            _dice = new DiceBlockService();
            _dice.ConfigureForTests(() => _player);
        }

        [TearDown]
        public void TearDown()
        {
            _dice.Dispose();
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
        }

        [Test]
        public void Block_WithoutLabel_KeepsThePadlockBare()
        {
            // Arrange / Act — es el caso de los jefes que sortean el dado al azar (Security Boss,
            // Sunken Grand): ahí no hay nada que explicar.
            _dice.Block(2);

            // Assert
            Assert.IsTrue(_dice.IsBlocked(2));
            Assert.IsNull(_dice.LabelOf(2), "Sin etiqueta el candado va pelado, no con un texto vacío.");
        }

        [Test]
        public void Block_WithLabel_RemembersWhoTookTheDie()
        {
            // Arrange / Act
            _dice.Block(2, "3");

            // Assert
            Assert.AreEqual("3", _dice.LabelOf(2));
        }

        [Test]
        public void LabelOf_UnblockedOrNegativeIndex_IsNull()
        {
            // Arrange
            _dice.Block(0, "5");

            // Act / Assert
            Assert.IsNull(_dice.LabelOf(1), "Un dado libre no tiene etiqueta.");
            Assert.IsNull(_dice.LabelOf(-1), "Un índice negativo no explota.");
        }

        [Test]
        public void Block_SameIndexWithANewLabel_OverwritesAndNotifies()
        {
            // Arrange — en fase 2 el Croupier puede cantar un número nuevo que caiga en el mismo
            // slot, y el candado tiene que decir el número de ESTE turno.
            int notifications = 0;
            EventManager.EventReceiver count = _ => notifications++;
            EventManager.Subscribe(EventName.OnDiceBlockChanged, count);

            try
            {
                _dice.Block(1, "2");
                _dice.Block(1, "6");

                // Assert
                Assert.AreEqual("6", _dice.LabelOf(1), "El candado quedó anunciando el número viejo.");
                Assert.AreEqual(2, notifications, "El cambio de etiqueta tiene que repintar la UI.");
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnDiceBlockChanged, count);
            }
        }

        [Test]
        public void Block_SameIndexAndSameLabel_DoesNotSpamTheUi()
        {
            // Arrange
            int notifications = 0;
            EventManager.EventReceiver count = _ => notifications++;
            EventManager.Subscribe(EventName.OnDiceBlockChanged, count);

            try
            {
                _dice.Block(1, "2");
                _dice.Block(1, "2");

                // Assert
                Assert.AreEqual(1, notifications, "Re-bloquear lo mismo no es un cambio.");
            }
            finally
            {
                EventManager.UnSubscribe(EventName.OnDiceBlockChanged, count);
            }
        }

        [Test]
        public void Clear_DropsTheLabelsWithTheBlocks()
        {
            // Arrange — el auto-release del fin de turno del jugador.
            _dice.Block(0, "4");

            // Act
            _dice.Clear();

            // Assert
            Assert.IsFalse(_dice.IsBlocked(0));
            Assert.IsNull(_dice.LabelOf(0), "Una etiqueta huérfana reaparecería en el próximo bloqueo.");
        }

        [Test]
        public void BlockedIndices_StillEnumeratesEveryBlockedSlot()
        {
            // Arrange — el contrato viejo no cambia: la detección de combos y el HUD lo recorren.
            _dice.Block(0, "1");
            _dice.Block(3);

            // Act / Assert
            CollectionAssert.AreEquivalent(new[] { 0, 3 }, _dice.BlockedIndices);
        }
    }
}
