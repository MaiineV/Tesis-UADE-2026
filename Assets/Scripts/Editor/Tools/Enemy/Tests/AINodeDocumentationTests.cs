using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.AITree;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class AINodeDocumentationTests
    {
        /// <summary>
        /// Guardia: todo nodo que aparece en el buscador del canvas tiene que explicar qué hace.
        /// Un nodo nuevo sin entrada rompe este test con su nombre en el mensaje.
        /// </summary>
        [Test]
        public void Get_EveryRegisteredNodeType_HasEntry()
        {
            var missing = new List<string>();
            foreach (var meta in AINodeRegistry.All)
            {
                if (string.IsNullOrWhiteSpace(AINodeDocumentation.Get(meta.Type)))
                    missing.Add(meta.Type.Name);
            }
            Assert.IsEmpty(missing, "Nodos sin doc en AINodeDocumentation: " + string.Join(", ", missing));
        }

        [Test]
        public void Get_NullOrUnknownType_ReturnsNull()
        {
            Assert.IsNull(AINodeDocumentation.Get(null));
            Assert.IsNull(AINodeDocumentation.Get(typeof(string)));
        }
    }
}
