using NUnit.Framework;

namespace Rollgeon.Localization.Tests
{
    /// <summary>
    /// Cubre el comportamiento de fallback del resolver de contenido. Estos casos son
    /// deterministas independientemente de si Localization inicializó: id nulo/vacío o
    /// key ausente siempre devuelve el fallback (nunca la key cruda ni excepción).
    /// </summary>
    public class LocalizedContentTests
    {
        [Test]
        public void Name_null_id_returns_fallback()
        {
            Assert.AreEqual("FB", LocalizedContent.Name(null, "FB"));
        }

        [Test]
        public void Name_empty_id_returns_fallback()
        {
            Assert.AreEqual("FB", LocalizedContent.Name("", "FB"));
        }

        [Test]
        public void Description_missing_key_returns_fallback()
        {
            Assert.AreEqual("FB", LocalizedContent.Description("rollgeon.tests.__missing__", "FB"));
        }

        [Test]
        public void Ui_missing_key_returns_fallback()
        {
            Assert.AreEqual("FB", LocalizedContent.Ui("rollgeon.tests.__missing_ui__", "FB"));
        }
    }
}
