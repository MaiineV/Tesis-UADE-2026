using NUnit.Framework;

namespace Rollgeon.Input.Tests
{
    /// <summary>
    /// Decisión pura del click derecho en combate (QoL). Prioridad:
    /// selección de acción cancelable &gt; deselect-all de dados &gt; nada; los gates de
    /// contexto (fuera del HUD de combate, frame claimeado por un cancel de agarre de
    /// dados) apagan todo.
    /// </summary>
    [TestFixture]
    public class RightClickCancelPolicyTests
    {
        [Test]
        public void should_do_nothing_when_combat_hud_is_not_active()
        {
            // Arrange + Act — aunque haya selección y dados marcados, fuera del HUD de
            // combate (pausa, exploración, altar) el click derecho no toca nada.
            var action = RightClickCancelPolicy.Decide(
                combatHudActive: false,
                claimedByDiceGrab: false,
                hasCancellableSelection: true,
                anyDieSelected: true);

            // Assert
            Assert.AreEqual(RightClickAction.None, action);
        }

        [Test]
        public void should_do_nothing_when_frame_was_claimed_by_dice_grab()
        {
            // Arrange + Act — el presenter de throw ya usó este right-click para
            // cancelar un agarre; el router no debe double-handlear.
            var action = RightClickCancelPolicy.Decide(
                combatHudActive: true,
                claimedByDiceGrab: true,
                hasCancellableSelection: true,
                anyDieSelected: true);

            // Assert
            Assert.AreEqual(RightClickAction.None, action);
        }

        [Test]
        public void should_cancel_selection_before_deselecting_dice()
        {
            // Arrange + Act — con una selección de acción abierta Y dados marcados, el
            // cancel de la selección tiene prioridad: un solo right-click no debe hacer
            // las dos cosas.
            var action = RightClickCancelPolicy.Decide(
                combatHudActive: true,
                claimedByDiceGrab: false,
                hasCancellableSelection: true,
                anyDieSelected: true);

            // Assert
            Assert.AreEqual(RightClickAction.CancelSelection, action);
        }

        [Test]
        public void should_deselect_all_dice_when_no_selection_is_open()
        {
            // Arrange + Act — fase de dados: sin selección de acción, el right-click
            // limpia todos los holds (Balatro-style).
            var action = RightClickCancelPolicy.Decide(
                combatHudActive: true,
                claimedByDiceGrab: false,
                hasCancellableSelection: false,
                anyDieSelected: true);

            // Assert
            Assert.AreEqual(RightClickAction.DeselectAllDice, action);
        }

        [Test]
        public void should_do_nothing_when_nothing_is_selected()
        {
            // Arrange + Act — sin selección de acción ni dados marcados, no-op real.
            var action = RightClickCancelPolicy.Decide(
                combatHudActive: true,
                claimedByDiceGrab: false,
                hasCancellableSelection: false,
                anyDieSelected: false);

            // Assert
            Assert.AreEqual(RightClickAction.None, action);
        }
    }
}
