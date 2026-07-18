using NUnit.Framework;
using Rollgeon.UI.Cursor;

namespace Rollgeon.UI.Tests
{
    [TestFixture]
    public class CursorStateResolverTests
    {
        [Test]
        public void Resolve_NotPressedNotHoverable_ReturnsDefault()
        {
            // Arrange & Act
            var state = CursorStateResolver.Resolve(pressed: false, hoverable: false);

            // Assert
            Assert.AreEqual(CursorState.Default, state);
            Assert.AreEqual(0, (int)state);
        }

        [Test]
        public void Resolve_NotPressedHoverable_ReturnsHover()
        {
            // Arrange & Act
            var state = CursorStateResolver.Resolve(pressed: false, hoverable: true);

            // Assert
            Assert.AreEqual(CursorState.Hover, state);
            Assert.AreEqual(2, (int)state);
        }

        [Test]
        public void Resolve_PressedHoverable_ReturnsClickHover()
        {
            // Arrange & Act
            var state = CursorStateResolver.Resolve(pressed: true, hoverable: true);

            // Assert
            Assert.AreEqual(CursorState.ClickHover, state);
            Assert.AreEqual(3, (int)state);
        }

        [Test]
        public void Resolve_PressedNotHoverable_ReturnsClickEmpty()
        {
            // Arrange & Act
            var state = CursorStateResolver.Resolve(pressed: true, hoverable: false);

            // Assert
            Assert.AreEqual(CursorState.ClickEmpty, state);
            Assert.AreEqual(1, (int)state);
        }
    }
}
