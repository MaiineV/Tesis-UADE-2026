using NUnit.Framework;
using Rollgeon.Timing;

namespace Rollgeon.Timing.Tests
{
    /// <summary>
    /// Solo la lógica pura del ciclo/sanitización — el lado PlayerPrefs/timeScale
    /// se cubre en <c>OptionsScreenTests</c> y en el smoke de Play Mode.
    /// </summary>
    public sealed class GameSpeedPrefsTests
    {
        [Test]
        public void NextSpeed_CiclaUnoDosCuatro_YVuelveAlPrincipio()
        {
            // Arrange: el ciclo completo esperado, wrap incluido.
            // Act + Assert
            Assert.AreEqual(2, GameSpeedPrefs.NextSpeed(1));
            Assert.AreEqual(4, GameSpeedPrefs.NextSpeed(2));
            Assert.AreEqual(1, GameSpeedPrefs.NextSpeed(4));
        }

        [Test]
        public void NextSpeed_ValorFueraDelCiclo_ReseteaAUno()
        {
            Assert.AreEqual(1, GameSpeedPrefs.NextSpeed(0));
            Assert.AreEqual(1, GameSpeedPrefs.NextSpeed(3));
            Assert.AreEqual(1, GameSpeedPrefs.NextSpeed(-4));
            // x8 fue un speed válido: un valor persistido viejo cae al ciclo actual.
            Assert.AreEqual(1, GameSpeedPrefs.NextSpeed(8));
            Assert.AreEqual(1, GameSpeedPrefs.NextSpeed(16));
        }

        [Test]
        public void SanitizeSpeed_ValoresValidos_PasanIntactos()
        {
            foreach (int speed in GameSpeedPrefs.Speeds)
            {
                Assert.AreEqual(speed, GameSpeedPrefs.SanitizeSpeed(speed));
            }
        }

        [Test]
        public void SanitizeSpeed_ValoresInvalidos_ColapsanAUno()
        {
            Assert.AreEqual(1, GameSpeedPrefs.SanitizeSpeed(0));
            Assert.AreEqual(1, GameSpeedPrefs.SanitizeSpeed(-1));
            Assert.AreEqual(1, GameSpeedPrefs.SanitizeSpeed(3));
            // Cubre prefs persistidas cuando x8 todavía era válido.
            Assert.AreEqual(1, GameSpeedPrefs.SanitizeSpeed(8));
            Assert.AreEqual(1, GameSpeedPrefs.SanitizeSpeed(16));
        }
    }
}
