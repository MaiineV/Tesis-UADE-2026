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
