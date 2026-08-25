using NUnit.Framework;
using Rollgeon.UI.HUD;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Clasificador de transiciones del vaso de generala: deltas del pool de
    /// rolls → animación, y el re-chequeo de frontera del flip-down.
    /// </summary>
    [TestFixture]
    public class RollCupMathTests
    {
        [Test]
        public void Classify_SpendAboveZero_ReturnsSpend()
        {
            // Arrange
            const int previous = 3;
            const int current = 2;

            // Act
            var transition = RollCupMath.Classify(previous, current);

            // Assert
            Assert.AreEqual(RollCupTransition.Spend, transition);
        }

        [Test]
        public void Classify_SpendToZero_ReturnsSpendToEmpty()
        {
            // Arrange
            const int previous = 1;
            const int current = 0;

            // Act
            var transition = RollCupMath.Classify(previous, current);

            // Assert
            Assert.AreEqual(RollCupTransition.SpendToEmpty, transition);
        }

        [Test]
        public void Classify_MultiRollDrainToZero_ReturnsSpendToEmpty()
        {
            // Arrange: el peaje de la Bandida drena varios rolls en un solo evento.
            const int previous = 4;
            const int current = 0;

            // Act
            var transition = RollCupMath.Classify(previous, current);

            // Assert
            Assert.AreEqual(RollCupTransition.SpendToEmpty, transition);
        }

        [Test]
        public void Classify_RecoverFromZero_ReturnsRecoverFromEmpty()
        {
            // Arrange
            const int previous = 0;
            const int current = 5;

            // Act
            var transition = RollCupMath.Classify(previous, current);

            // Assert
            Assert.AreEqual(RollCupTransition.RecoverFromEmpty, transition);
        }

        [Test]
        public void Classify_RecoverAboveZero_ReturnsRecover()
        {
            // Arrange
            const int previous = 2;
            const int current = 5;

            // Act
            var transition = RollCupMath.Classify(previous, current);

            // Assert
            Assert.AreEqual(RollCupTransition.Recover, transition);
        }

        [Test]
        public void Classify_NoPreviousData_ReturnsNone()
        {
            // Arrange: -1 = primer fetch o reentrada a combate — pose sin animación.
            const int previous = -1;
            const int current = 5;

            // Act
            var transition = RollCupMath.Classify(previous, current);

            // Assert
            Assert.AreEqual(RollCupTransition.None, transition);
        }

        [Test]
        public void Classify_EqualValues_ReturnsNone()
        {
            // Arrange
            const int previous = 3;
            const int current = 3;

            // Act
            var transition = RollCupMath.Classify(previous, current);

            // Assert
            Assert.AreEqual(RollCupTransition.None, transition);
        }

        [Test]
        public void IsFaceDown_Zero_ReturnsTrue()
        {
            // Act + Assert
            Assert.IsTrue(RollCupMath.IsFaceDown(0));
            Assert.IsFalse(RollCupMath.IsFaceDown(1));
        }

        [Test]
        public void ShouldChainFlipDown_TargetStillFaceDown_ReturnsTrue()
        {
            // Act
            bool chain = RollCupMath.ShouldChainFlipDown(RollCupTransition.SpendToEmpty, targetFaceDownNow: true);

            // Assert
            Assert.IsTrue(chain);
        }

        [Test]
        public void ShouldChainFlipDown_TargetChangedAtBoundary_ReturnsFalse()
        {
            // Arrange: durante el shake se coló un recupero — el vaso ya no va boca abajo.

            // Act
            bool chain = RollCupMath.ShouldChainFlipDown(RollCupTransition.SpendToEmpty, targetFaceDownNow: false);

            // Assert
            Assert.IsFalse(chain);
        }

        [Test]
        public void ShouldChainFlipDown_PlainSpend_ReturnsFalse()
        {
            // Act
            bool chain = RollCupMath.ShouldChainFlipDown(RollCupTransition.Spend, targetFaceDownNow: true);

            // Assert
            Assert.IsFalse(chain);
        }
    }
}
