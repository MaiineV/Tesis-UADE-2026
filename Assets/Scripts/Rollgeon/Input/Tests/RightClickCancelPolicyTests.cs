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
                anyDieSelected: true,
                uiSequencePending: false);

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
                anyDieSelected: true,
                uiSequencePending: false);

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
                anyDieSelected: true,
                uiSequencePending: false);

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
                anyDieSelected: true,
                uiSequencePending: false);

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
                anyDieSelected: false,
                uiSequencePending: false);

            // Assert
            Assert.AreEqual(RightClickAction.None, action);
        }

        [Test]
        public void should_do_nothing_when_ui_sequence_is_pending()
        {
            // Arrange + Act — BUG-070: rotar la cámara (mismo botón derecho) durante
            // la suma N×M o el outro de dados no debe cancelar la fase del chain…
            var action = RightClickCancelPolicy.Decide(
                combatHudActive: true,
                claimedByDiceGrab: false,
                hasCancellableSelection: true,
                anyDieSelected: false,
                uiSequencePending: true);

            // Assert
            Assert.AreEqual(RightClickAction.None, action);
        }

        [Test]
        public void should_not_deselect_dice_when_ui_sequence_is_pending()
        {
            // Arrange + Act — …ni borrar los "+N"/holds en plena animación de la suma.
            var action = RightClickCancelPolicy.Decide(
                combatHudActive: true,
                claimedByDiceGrab: false,
                hasCancellableSelection: false,
                anyDieSelected: true,
                uiSequencePending: true);

            // Assert
            Assert.AreEqual(RightClickAction.None, action);
        }

        [Test]
        public void should_still_cancel_selection_when_no_sequence_is_pending()
        {
            // Arrange + Act — regresión: fuera de la ventana de la secuencia el
            // cancel legítimo del targeting sigue vivo.
            var action = RightClickCancelPolicy.Decide(
                combatHudActive: true,
                claimedByDiceGrab: false,
                hasCancellableSelection: true,
                anyDieSelected: false,
                uiSequencePending: false);

            // Assert
            Assert.AreEqual(RightClickAction.CancelSelection, action);
        }
    }
}
