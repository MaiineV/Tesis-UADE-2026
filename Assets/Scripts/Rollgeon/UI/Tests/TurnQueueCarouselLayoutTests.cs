using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.UI.HUD;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Matemática pura del carrusel de turnos (activo a la izquierda + próximos):
    /// resolución de guids por posición relativa (wrap circular, repetición con
    /// pocas entidades) y posiciones / escalas / alphas exactas por offset.
    /// </summary>
    [TestFixture]
    public class TurnQueueCarouselLayoutTests
    {
        private static TurnCarouselConfig MakeConfig()
        {
            return new TurnCarouselConfig
            {
                ActiveScale = 1.25f,
                UpcomingScale = 0.85f,
                UpcomingAlpha = 0.7f,
                SlotWidth = 100f,
                Spacing = 8f,
            };
        }

        private static List<Guid> MakeOrder(int count)
        {
            var order = new List<Guid>(count);
            for (int i = 0; i < count; i++) order.Add(Guid.NewGuid());
            return order;
        }

        [Test]
        public void GuidAt_FiveParticipantsCursorZero_MapsWindowWithoutWrap()
        {
            // Arrange
            var order = MakeOrder(5);

            // Act + Assert — cursor 0: la ventana 0..+4 es exactamente order[0..4].
            Assert.AreEqual(order[0], TurnQueueCarouselLayout.GuidAt(order, 0, 0));
            Assert.AreEqual(order[1], TurnQueueCarouselLayout.GuidAt(order, 0, +1));
            Assert.AreEqual(order[2], TurnQueueCarouselLayout.GuidAt(order, 0, +2));
            Assert.AreEqual(order[3], TurnQueueCarouselLayout.GuidAt(order, 0, +3));
            Assert.AreEqual(order[4], TurnQueueCarouselLayout.GuidAt(order, 0, +4));
        }

        [Test]
        public void GuidAt_CursorAtEnd_WrapsUpcomingToHead()
        {
            // Arrange
            var order = MakeOrder(5);

            // Act
            var atPlusOne = TurnQueueCarouselLayout.GuidAt(order, 4, +1);
            var atPlusTwo = TurnQueueCarouselLayout.GuidAt(order, 4, +2);

            // Assert — después del último viene el head del round siguiente.
            Assert.AreEqual(order[0], atPlusOne);
            Assert.AreEqual(order[1], atPlusTwo);
        }

        [Test]
        public void GuidAt_ThreeParticipants_RepeatsGuidsAcrossWindow()
        {
            // Arrange — con N=3 la ventana de 5 repite actores (loop de repetición).
            var order = MakeOrder(3);

            // Act
            var atPlusThree = TurnQueueCarouselLayout.GuidAt(order, 0, +3);
            var atPlusFour = TurnQueueCarouselLayout.GuidAt(order, 0, +4);

            // Assert — (+3) repite al activo y (+4) al que le sigue.
            Assert.AreEqual(order[0], atPlusThree);
            Assert.AreEqual(order[1], atPlusFour);
        }

        [Test]
        public void GuidAt_EmptyOrder_Throws()
        {
            // Arrange
            var empty = new List<Guid>();

            // Act + Assert
            Assert.Throws<InvalidOperationException>(
                () => TurnQueueCarouselLayout.GuidAt(empty, 0, 0));
        }

        [Test]
        public void GetScale_PerRelativePosition_ReturnsConfiguredValues()
        {
            // Arrange
            var cfg = MakeConfig();

            // Act + Assert
            Assert.AreEqual(1.25f, TurnQueueCarouselLayout.GetScale(0, cfg));
            Assert.AreEqual(0.85f, TurnQueueCarouselLayout.GetScale(+1, cfg));
            Assert.AreEqual(0.85f, TurnQueueCarouselLayout.GetScale(+4, cfg));
        }

        [Test]
        public void GetAlpha_PerRelativePosition_ReturnsConfiguredValues()
        {
            // Arrange
            var cfg = MakeConfig();

            // Act + Assert — el activo siempre es plenamente visible.
            Assert.AreEqual(1f, TurnQueueCarouselLayout.GetAlpha(0, cfg));
            Assert.AreEqual(0.7f, TurnQueueCarouselLayout.GetAlpha(+1, cfg));
            Assert.AreEqual(0.7f, TurnQueueCarouselLayout.GetAlpha(+4, cfg));
        }

        [Test]
        public void GetX_ActivePosition_IsZero()
        {
            // Arrange
            var cfg = MakeConfig();

            // Act + Assert
            Assert.AreEqual(0f, TurnQueueCarouselLayout.GetX(0, cfg));
        }

        [Test]
        public void GetX_UpcomingOffsets_AccumulateScaledHalfWidthsPlusSpacing()
        {
            // Arrange
            var cfg = MakeConfig();

            // Act
            float xPlusOne = TurnQueueCarouselLayout.GetX(+1, cfg);
            float xPlusTwo = TurnQueueCarouselLayout.GetX(+2, cfg);
            float xPlusFour = TurnQueueCarouselLayout.GetX(+4, cfg);

            // Assert — +1: 62.5 (mitad activo) + 8 + 42.5 (mitad upcoming) = 113;
            // cada paso upcoming-upcoming suma 42.5 + 8 + 42.5 = 93.
            Assert.AreEqual(113f, xPlusOne, 0.001f);
            Assert.AreEqual(206f, xPlusTwo, 0.001f);
            Assert.AreEqual(392f, xPlusFour, 0.001f);
        }
    }
}
