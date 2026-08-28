using NUnit.Framework;
using Rollgeon.Editor.Tools.Enemy.Templates;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class EnemyTemplateCatalogTests
    {
        [Test]
        public void IsTemplatePath_OnlyUnderTemplatesFolder()
        {
            Assert.IsTrue(EnemyTemplateCatalog.IsTemplatePath(EnemyTemplateCatalog.TemplatesFolder + "/ET_Pursuer.asset"));
            Assert.IsFalse(EnemyTemplateCatalog.IsTemplatePath("Assets/Rollgeon/Enemies/ED_Healer.asset"));
            Assert.IsFalse(EnemyTemplateCatalog.IsTemplatePath("Assets/Rollgeon/Enemies/TemplatesOld/ET_X.asset"));
            Assert.IsFalse(EnemyTemplateCatalog.IsTemplatePath(null));
        }
    }
}
