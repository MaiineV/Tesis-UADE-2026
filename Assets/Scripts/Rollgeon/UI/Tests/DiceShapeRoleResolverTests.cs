using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.UI.HUD;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Cubre <see cref="DiceShapeRoleResolver"/>: la tabla de precedencia completa entre spin,
    /// blocked, held y hovered. Es el contrato que hace que el sprite del slot no dependa del
    /// orden en que llegan las llamadas.
    /// </summary>
    [TestFixture]
    public class DiceShapeRoleResolverTests
    {
        // -------------------------------------------------------------------
        // Reposo
        // -------------------------------------------------------------------

        [Test]
        public void Resolve_ReturnsFront_WhenNothingIsActive()
        {
            // Act
            var role = DiceShapeRoleResolver.Resolve(blocked: false, held: false, hovered: false, spin: null);

            // Assert
            Assert.AreEqual(DiceShapeRole.Front, role);
        }

        [Test]
        public void Resolve_ReturnsHover_WhenOnlyHovered()
        {
            // Act
            var role = DiceShapeRoleResolver.Resolve(blocked: false, held: false, hovered: true, spin: null);

            // Assert
            Assert.AreEqual(DiceShapeRole.Hover, role);
        }

        [Test]
        public void Resolve_ReturnsSelected_WhenOnlyHeld()
        {
            // Act
            var role = DiceShapeRoleResolver.Resolve(blocked: false, held: true, hovered: false, spin: null);

            // Assert
            Assert.AreEqual(DiceShapeRole.Selected, role);
        }

        // -------------------------------------------------------------------
        // Precedencia
        // -------------------------------------------------------------------

        [Test]
        public void Resolve_PrefersHeldOverHover_WhenBothAreActive()
        {
            // Act — el puntero encima de un dado ya holdeado no lo degrada a "candidato".
            var role = DiceShapeRoleResolver.Resolve(blocked: false, held: true, hovered: true, spin: null);

            // Assert
            Assert.AreEqual(DiceShapeRole.Selected, role);
        }

        /// <summary>
        /// Un dado bloqueado por boss es inerte: el bloqueo se lee por el tint gris y el candado,
        /// y no debe verse elegido aunque el estado de hold siga puesto por debajo.
        /// </summary>
        [Test]
        public void Resolve_PrefersFrontOverHeld_WhenBlocked()
        {
            // Act
            var role = DiceShapeRoleResolver.Resolve(blocked: true, held: true, hovered: false, spin: null);

            // Assert
            Assert.AreEqual(DiceShapeRole.Front, role);
        }

        [Test]
        public void Resolve_PrefersFrontOverHover_WhenBlocked()
        {
            // Act
            var role = DiceShapeRoleResolver.Resolve(blocked: true, held: false, hovered: true, spin: null);

            // Assert
            Assert.AreEqual(DiceShapeRole.Front, role);
        }

        /// <summary>
        /// El caso que justifica que el spin sea un override y no un estado más: sin esto, el
        /// hover durante el giro pelearía tick a tick con el ciclado y haría flicker.
        /// </summary>
        [Test]
        public void Resolve_PrefersSpin_OverEveryOtherState()
        {
            // Act
            var role = DiceShapeRoleResolver.Resolve(
                blocked: true, held: true, hovered: true, spin: DiceShapeRole.SideA);

            // Assert
            Assert.AreEqual(DiceShapeRole.SideA, role);
        }

        [Test]
        public void Resolve_ReturnsRestingRole_WhenSpinIsReleased()
        {
            // Arrange & Act — soltar el spin devuelve el rol que corresponde al estado real.
            var role = DiceShapeRoleResolver.Resolve(blocked: false, held: true, hovered: false, spin: null);

            // Assert
            Assert.AreEqual(DiceShapeRole.Selected, role);
        }
    }
}
