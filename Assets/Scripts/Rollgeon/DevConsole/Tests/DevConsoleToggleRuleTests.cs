using NUnit.Framework;
using Rollgeon.DevConsole.UI;

namespace Rollgeon.DevConsole.Tests
{
    /// <summary>
    /// Tests de <see cref="DevConsoleToggleRule"/> — cuándo la P cierra la consola y cuándo es una
    /// letra del comando.
    /// </summary>
    [TestFixture]
    public class DevConsoleToggleRuleTests
    {
        [Test]
        public void PIsTheToggle_OpenedWithPAndEmptyInput_Closes()
        {
            // Arrange — el caso que se pidió: abrir con P y cerrar con P.
            const bool openedWithP = true;

            // Act
            bool isToggle = DevConsoleToggleRule.PIsTheToggle(openedWithP, string.Empty);

            // Assert
            Assert.IsTrue(isToggle);
        }

        [Test]
        public void PIsTheToggle_OpenedWithPAndNullInput_Closes()
        {
            // Arrange — el campo arranca en null antes de que TMP lo inicialice.
            // Act + Assert
            Assert.IsTrue(DevConsoleToggleRule.PIsTheToggle(true, null));
        }

        [Test]
        public void PIsTheToggle_TypingACommand_LetsThePThrough()
        {
            // Arrange — 'p' en medio de una palabra tiene que escribirse, no cerrar.
            // Act + Assert
            Assert.IsFalse(DevConsoleToggleRule.PIsTheToggle(true, "hel"),
                "Con un comando a medio escribir la P es texto.");
            Assert.IsFalse(DevConsoleToggleRule.PIsTheToggle(true, "boss.crou"));
        }

        [Test]
        public void PIsTheToggle_ASingleSpaceCounts_AsTyping()
        {
            // Arrange — el vacío es lo único que habilita el toggle; un espacio ya es contenido y
            // deja de serlo, así 'setdiceroll p…' nunca se corta a mitad de argumento.
            // Act + Assert
            Assert.IsFalse(DevConsoleToggleRule.PIsTheToggle(true, " "));
        }

        [Test]
        public void PIsTheToggle_OpenedWithBackquoteOrF1_NeverStealsTheLetter()
        {
            // Arrange — quien abre con ` o F1 no pidió que la P fuera especial: `potion` se tipea
            // entero incluso con el campo vacío.
            // Act + Assert
            Assert.IsFalse(DevConsoleToggleRule.PIsTheToggle(false, string.Empty));
            Assert.IsFalse(DevConsoleToggleRule.PIsTheToggle(false, "potio"));
        }
    }
}
