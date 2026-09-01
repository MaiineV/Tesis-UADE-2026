using NUnit.Framework;
using Rollgeon.Localization;
using Rollgeon.UI.HUD.Status;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Formato del tooltip de un estado. Sin escena: es una función pura.
    /// </summary>
    /// <remarks>
    /// Los asserts resuelven la clave esperada por el MISMO camino que la producción
    /// (<see cref="LocalizedContent.Ui"/>) en vez de comparar contra un literal en español. El
    /// locale activo en EditMode sale de un PlayerPref (<c>selected-locale</c>), así que hardcodear
    /// el idioma ponía estos tests en rojo con el editor en inglés — y peor, los asserts de
    /// ausencia pasaban en falso (buscaban "Activada" mientras el sistema devolvía "Active").
    /// Lo que se testea acá es el armado y la elección de clave, no la traducción.
    /// </remarks>
    [TestFixture]
    public class StatusTooltipTextTests
    {
        private static string ActiveLabel => LocalizedContent.Ui(StatusTextKeys.Active, "Activada");
        private static string InactiveLabel => LocalizedContent.Ui(StatusTextKeys.Inactive, "Desactivada");
        private static string LastTurnLabel => LocalizedContent.Ui(StatusTextKeys.DurationLastTurn, "Último turno");
        private static string DurationLabel(int turns) =>
            string.Format(LocalizedContent.Ui(StatusTextKeys.Duration, "Dura {0} turnos"), turns);

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
            StringAssert.Contains(ActiveLabel, text);
        }

        [Test]
        public void should_say_inactive_for_a_latent_passive()
        {
            // Arrange + Act
            string text = StatusTooltipText.Build(Passive(active: false));

            // Assert
            StringAssert.Contains(InactiveLabel, text);
        }

        [Test]
        public void should_not_mention_duration_for_a_passive()
        {
            // Arrange + Act — una pasiva no vence; hablar de turnos sería mentira.
            string text = StatusTooltipText.Build(Passive(active: true));

            // Assert — ninguno de los dos textos de duración, en el idioma que sea.
            StringAssert.DoesNotContain(LastTurnLabel, text);
            StringAssert.DoesNotContain(DurationLabel(1), text);
        }

        [Test]
        public void should_report_the_remaining_turns_for_a_timed_effect()
        {
            // Arrange + Act
            string text = StatusTooltipText.Build(Timed(3));

            // Assert
            StringAssert.Contains("Quemadura", text);
            StringAssert.Contains("3", text);
            StringAssert.Contains(DurationLabel(3), text);
        }

        [Test]
        public void should_use_the_singular_wording_on_the_last_turn()
        {
            // Arrange + Act — sin esto decía "Dura 1 turnos".
            string text = StatusTooltipText.Build(Timed(1));

            // Assert
            StringAssert.Contains(LastTurnLabel, text);
            StringAssert.DoesNotContain(DurationLabel(1), text);
        }

        [Test]
        public void should_not_say_active_for_a_timed_effect()
        {
            // Arrange + Act — activada/desactivada es vocabulario de pasiva; un efecto con
            // timer ya se explica con lo que le queda.
            string text = StatusTooltipText.Build(Timed(2));

            // Assert
            StringAssert.DoesNotContain(ActiveLabel, text);
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
            Assert.AreEqual($"<b>Sin descripción</b>\n{ActiveLabel}", text.Replace("\r\n", "\n"));
        }

        [Test]
        public void should_badge_only_what_has_a_duration()
        {
            // Arrange + Act + Assert — el badge dice la unidad ("N Turnos"), no el número pelado,
            // y el caso 1 va en singular (spec de UI de estados de Casillas Especiales).
            Assert.AreEqual("3 Turnos", StatusTooltipText.ResolveDurationBadge(Timed(3)));
            Assert.AreEqual("1 Turno", StatusTooltipText.ResolveDurationBadge(Timed(1)));
            Assert.AreEqual(string.Empty, StatusTooltipText.ResolveDurationBadge(Passive(true)));
        }

        [Test]
        public void should_badge_a_card_with_the_bare_number()
        {
            // La ficha de la tarjeta es redonda y del ancho de un dígito: con "1 Turno" adentro
            // TMP parte la frase en una letra por renglón y el badge se lee vertical al lado del
            // título. La palabra la sigue diciendo la fila que flota sobre la cabeza.
            Assert.AreEqual("3", StatusTooltipText.ResolveCardBadge(Timed(3)));
            Assert.AreEqual("1", StatusTooltipText.ResolveCardBadge(Timed(1)));
            Assert.AreEqual(string.Empty, StatusTooltipText.ResolveCardBadge(Passive(true)));
        }
    }
}
