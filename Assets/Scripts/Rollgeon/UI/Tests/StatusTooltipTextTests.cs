using NUnit.Framework;
using Rollgeon.UI.HUD.Status;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Formato del tooltip de un estado. Sin escena: es una función pura.
    /// </summary>
    /// <remarks>
    /// Los asserts van contra los FALLBACKS de <c>LocalizedContent</c>: en EditMode
    /// Localization no tiene locale activo, así que devuelve el texto por defecto. Eso es
    /// justo lo que queremos testear acá — el armado, no la traducción.
    /// </remarks>
    [TestFixture]
    public class StatusTooltipTextTests
    {
        private static StatusIconState Passive(bool active) =>
            new StatusIconState("passive.warrior.low_hp_rage", "Furia del Guerrero",
                                "+5 de ataque con poca vida.", null, active, remainingTurns: null);

        private static StatusIconState Timed(int turns) =>
            new StatusIconState("status.burn", "Quemadura", "Perdés vida al final del turno.",
                                null, active: true, remainingTurns: turns);

        [Test]
        public void should_say_active_for_a_passive_that_is_running()
        {
            // Arrange + Act
            string text = StatusTooltipText.Build(Passive(active: true));

            // Assert
            StringAssert.Contains("Furia del Guerrero", text);
            StringAssert.Contains("+5 de ataque con poca vida.", text);
            StringAssert.Contains("Activada", text);
        }

        [Test]
        public void should_say_inactive_for_a_latent_passive()
        {
            // Arrange + Act
            string text = StatusTooltipText.Build(Passive(active: false));

            // Assert
            StringAssert.Contains("Desactivada", text);
        }

        [Test]
        public void should_not_mention_duration_for_a_passive()
        {
            // Arrange + Act — una pasiva no vence; hablar de turnos sería mentira.
            string text = StatusTooltipText.Build(Passive(active: true));

            // Assert
            StringAssert.DoesNotContain("turno", text.ToLowerInvariant());
        }

        [Test]
        public void should_report_the_remaining_turns_for_a_timed_effect()
        {
            // Arrange + Act
            string text = StatusTooltipText.Build(Timed(3));

            // Assert
            StringAssert.Contains("Quemadura", text);
            StringAssert.Contains("3", text);
            StringAssert.Contains("turnos", text);
        }

        [Test]
        public void should_use_the_singular_wording_on_the_last_turn()
        {
            // Arrange + Act — sin esto decía "Dura 1 turnos".
            string text = StatusTooltipText.Build(Timed(1));

            // Assert
            StringAssert.Contains("Último turno", text);
            StringAssert.DoesNotContain("1 turnos", text);
        }

        [Test]
        public void should_not_say_active_for_a_timed_effect()
        {
            // Arrange + Act — activada/desactivada es vocabulario de pasiva; un efecto con
            // timer ya se explica con lo que le queda.
            string text = StatusTooltipText.Build(Timed(2));

            // Assert
            StringAssert.DoesNotContain("Activada", text);
        }

        [Test]
        public void should_survive_a_state_with_no_description()
        {
            // Arrange — hay pasivas sin descripción autorada todavía.
            var state = new StatusIconState("passive.x", "Sin descripción", null, null,
                                            active: true, remainingTurns: null);

            // Act
            string text = StatusTooltipText.Build(state);

            // Assert — sin línea en blanco de más entre el nombre y el pie.
            Assert.AreEqual("<b>Sin descripción</b>\nActivada", text.Replace("\r\n", "\n"));
        }

        [Test]
        public void should_badge_only_what_has_a_duration()
        {
            // Arrange + Act + Assert
            Assert.AreEqual("3", StatusTooltipText.ResolveDurationBadge(Timed(3)));
            Assert.AreEqual(string.Empty, StatusTooltipText.ResolveDurationBadge(Passive(true)));
        }
    }
}
