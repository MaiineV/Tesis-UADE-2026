using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;

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

        // ---------------- swap de sprite a mitad del giro ----------------

        [TestCase(0f, false)]
        [TestCase(89.9f, false)]
        [TestCase(90f, true)]
        [TestCase(180f, true)]
        [TestCase(269.9f, true)]
        [TestCase(270f, false)]
        [TestCase(360f, false)]
        [TestCase(372f, false)] // overshoot del OutBack del flip-up
        [TestCase(450f, true)]  // segunda vuelta: mismo criterio módulo 360
        [TestCase(-100f, true)] // winding negativo: -100 ≡ 260, dentro de [90, 270)
        public void ShowsFlipSprite_SwitchesAtQuarterTurns(float logicalZ, bool expected)
        {
            // Act
            bool shows = RollCupMath.ShowsFlipSprite(logicalZ);

            // Assert
            Assert.AreEqual(expected, shows);
        }

        [Test]
        public void VisualZ_FaceDownWithFlipSprite_IsZero()
        {
            // Arrange: el Flip ya está dibujado boca abajo — a 180° lógico va sin rotar.

            // Act
            float visual = RollCupMath.VisualZ(RollCupMath.FaceDownZ, flipSpriteShown: true);

            // Assert
            Assert.AreEqual(0f, visual, 0.001f);
        }

        [Test]
        public void VisualZ_WithoutFlipSprite_IsLogical()
        {
            // Arrange: sin sprite Flip cableado se conserva el giro completo del dibujo único.

            // Act
            float visual = RollCupMath.VisualZ(RollCupMath.FaceDownZ, flipSpriteShown: false);

            // Assert
            Assert.AreEqual(RollCupMath.FaceDownZ, visual, 0.001f);
        }

        [TestCase(90f)]
        [TestCase(270f)]
        public void VisualZ_AtSwapBoundary_BothSpritesPointTheSameWay(float boundaryZ)
        {
            // Arrange: el sprite parado tiene la boca en +Y y el Flip en -Y. En la
            // frontera del swap la boca rotada debe apuntar al mismo lado con ambos.
            var mouthUpright = Quaternion.Euler(0f, 0f, RollCupMath.VisualZ(boundaryZ, false)) * Vector2.up;
            var mouthFlip = Quaternion.Euler(0f, 0f, RollCupMath.VisualZ(boundaryZ, true)) * Vector2.down;

            // Assert
            Assert.AreEqual(mouthUpright.x, mouthFlip.x, 0.001f);
            Assert.AreEqual(mouthUpright.y, mouthFlip.y, 0.001f);
        }
    }
}
