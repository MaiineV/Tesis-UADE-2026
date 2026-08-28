using System.IO;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Entities;
using UnityEditor;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    /// <summary>
    /// Escribe assets reales bajo una carpeta propia y la borra al final (mismo patrón que
    /// <c>TahurVisualWiringTests</c>).
    /// </summary>
    [TestFixture]
    public class EnemyAssetOpsTests
    {
        const string TestRoot = "Assets/Rollgeon/__EnemyAssetOpsTests";
        const string LayoutsDir = TestRoot + "/_layouts";

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Rollgeon")) AssetDatabase.CreateFolder("Assets", "Rollgeon");
            if (!AssetDatabase.IsValidFolder(TestRoot)) AssetDatabase.CreateFolder("Assets/Rollgeon", "__EnemyAssetOpsTests");
            Directory.CreateDirectory(LayoutsDir);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
        }

        static EnemyDataSO CreateSource(string fileName, string entityId)
        {
            var so = ScriptableObject.CreateInstance<EnemyDataSO>();
            so.EntityId = entityId;
            so.DisplayName = "Origen";
            var seq = new AINode_Sequence();
            seq.Children.Add(new AINode_Wait());
            so.AIRoot = seq;
            AssetDatabase.CreateAsset(so, $"{TestRoot}/{fileName}.asset");
            AssetDatabase.SaveAssets();
            return so;
        }

        const string TemplatesDir = TestRoot + "/Templates";
        const string EnemiesDir = TestRoot + "/Enemies";

        [Test]
        public void SaveAsTemplate_WritesUnderTemplatesFolder_WithTplId()
        {
            var src = CreateSource("ED_Tpl", "test.tpl");

            var tpl = EnemyAssetOps.SaveAsTemplate(src, TemplatesDir, LayoutsDir);

            Assert.IsNotNull(tpl);
            StringAssert.StartsWith(TemplatesDir + "/ET_Tpl", AssetDatabase.GetAssetPath(tpl));
            Assert.AreEqual("tpl.test.tpl", tpl.EntityId);
            Assert.AreEqual("Origen", tpl.DisplayName);
        }

        [Test]
        public void CreateFromAsset_CopiesTemplateBackAsEnemy_WithoutSuffixes()
        {
            var src = CreateSource("ED_Src2", "test.src2");
            var tpl = EnemyAssetOps.SaveAsTemplate(src, TemplatesDir, LayoutsDir);

            var enemy = EnemyAssetOps.CreateFromAsset(tpl, EnemiesDir, LayoutsDir);

            Assert.IsNotNull(enemy);
            StringAssert.StartsWith(EnemiesDir + "/ED_Src2", AssetDatabase.GetAssetPath(enemy));
            Assert.AreEqual("enemy.test.src2", enemy.EntityId);
            Assert.AreEqual("Origen", enemy.DisplayName);
            Assert.IsInstanceOf<AINode_Sequence>(enemy.AIRoot, "CopyAsset tiene que preservar el blob Odin");
        }

        [Test]
        public void CreateFromTemplate_AppliesArchetype_AndPersists()
        {
            var template = Templates.EnemyArchetypeTemplates.Find("pursuer");

            var so = EnemyAssetOps.CreateFromTemplate(template, EnemiesDir);

            Assert.IsNotNull(so);
            StringAssert.StartsWith(EnemiesDir + "/ED_Pursuer", AssetDatabase.GetAssetPath(so));
            Assert.AreEqual("enemy.pursuer", so.EntityId);
            Assert.AreEqual(EnemyArchetype.Melee, so.Design.Archetype);
            var reloaded = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetDatabase.GetAssetPath(so));
            Assert.IsInstanceOf<AINode_Sequence>(reloaded.AIRoot);
        }

        [Test]
        public void Duplicate_CreatesSiblingAssetWithCopiaSuffixes()
        {
            var src = CreateSource("ED_Src", "test.src");

            var copy = EnemyAssetOps.Duplicate(src, LayoutsDir);

            Assert.IsNotNull(copy);
            Assert.AreNotEqual(AssetDatabase.GetAssetPath(src), AssetDatabase.GetAssetPath(copy));
            StringAssert.StartsWith(TestRoot, AssetDatabase.GetAssetPath(copy));
            Assert.AreEqual("test.src_copia", copy.EntityId);
            Assert.AreEqual("Origen (copia)", copy.DisplayName);
        }

        [Test]
        public void Duplicate_PreservesOdinBlob_AfterReload()
        {
            var src = CreateSource("ED_Blob", "test.blob");

            var copy = EnemyAssetOps.Duplicate(src, LayoutsDir);
            string path = AssetDatabase.GetAssetPath(copy);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reloaded = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);

            var root = reloaded.AIRoot as AINode_Sequence;
            Assert.IsNotNull(root, "el árbol vive en el blob Odin y CopyAsset lo tiene que preservar");
            Assert.AreEqual(1, root.Children.Count);
            Assert.AreEqual("test.blob_copia", reloaded.EntityId, "el id nuevo tiene que sobrevivir al reimport");
        }

        [Test]
        public void Duplicate_CopiesLayoutSidecarToNewId()
        {
            var src = CreateSource("ED_Layout", "test.layout");
            string srcLayout = AITreeLayoutSidecar.PathForId("test.layout", LayoutsDir);
            File.WriteAllText(srcLayout, "{\"Entries\":[]}");

            EnemyAssetOps.Duplicate(src, LayoutsDir);

            Assert.IsTrue(File.Exists(AITreeLayoutSidecar.PathForId("test.layout_copia", LayoutsDir)));
        }

        [Test]
        public void Duplicate_NullOrUnsavedSource_ReturnsNull()
        {
            Assert.IsNull(EnemyAssetOps.Duplicate(null, LayoutsDir));
            var unsaved = ScriptableObject.CreateInstance<EnemyDataSO>();
            Assert.IsNull(EnemyAssetOps.Duplicate(unsaved, LayoutsDir));
            Object.DestroyImmediate(unsaved);
        }
    }
}
