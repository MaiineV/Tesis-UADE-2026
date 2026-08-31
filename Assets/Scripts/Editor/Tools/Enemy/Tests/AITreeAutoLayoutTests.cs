using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rollgeon.Combat.AI.Decisions;
using Rollgeon.Editor.Tools.Enemy.AITree;
using Rollgeon.Entities;
using UnityEngine;

namespace Rollgeon.Editor.Tools.Enemy.Tests
{
    [TestFixture]
    public class AITreeAutoLayoutTests
    {
        [Test]
        public void Compute_IncludesDetachedNodes()
        {
            var root = new AINode_Sequence();
            root.Children.Add(new AINode_Wait());
            var detached = new AINode_Wait();
            var snap = AITreeSerializer.Load(root, new List<AIDecisionNode> { detached });

            var positions = AITreeAutoLayout.Compute(snap);

            Assert.AreEqual(3, positions.Count);
            Assert.IsTrue(positions.ContainsKey(detached));
        }

        [Test]
        public void Compute_DetachedSubtreeBelowMainTree_AtDepthZero()
        {
            var root = new AINode_Sequence();
            var leaf = new AINode_Wait();
            root.Children.Add(leaf);
            var detached = new AINode_Wait();
            var snap = AITreeSerializer.Load(root, new List<AIDecisionNode> { detached });

            var positions = AITreeAutoLayout.Compute(snap);

            Assert.AreEqual(0f, positions[detached].x, "un suelto arranca en profundidad 0");
            Assert.Greater(positions[detached].y, positions[leaf].y, "los sueltos se apilan debajo del árbol");
        }

        [Test]
        public void Compute_NoRootButDetached_StillLaysOut()
        {
            var snap = AITreeSerializer.Load(null, new List<AIDecisionNode> { new AINode_Wait() });
            Assert.AreEqual(1, AITreeAutoLayout.Compute(snap).Count);
        }

        [Test]
        public void PathForId_UsesGivenDirAndForwardSlashes()
        {
            Assert.AreEqual("Some/Dir/x.json", AITreeLayoutSidecar.PathForId("x", "Some/Dir"));
            StringAssert.StartsWith(AITreeLayoutSidecar.LayoutsDir, AITreeLayoutSidecar.PathForId("x"));
        }

        // =====================================================================
        // Sidecar por id estable (con fallback legacy por índice de preorden)
        // =====================================================================

        string _dir;
        EnemyDataSO _so;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "rollgeon_layout_tests_" + System.Guid.NewGuid().ToString("N"))
                .Replace('\\', '/');
            _so = ScriptableObject.CreateInstance<EnemyDataSO>();
            _so.EntityId = "test.layout";
        }

        [TearDown]
        public void TearDown()
        {
            if (_so != null) Object.DestroyImmediate(_so);
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }

        static Dictionary<AIDecisionNode, Vector2> PositionsOf(GraphSnapshot snap)
        {
            var positions = new Dictionary<AIDecisionNode, Vector2>();
            int i = 0;
            foreach (var n in snap.PreOrder()) positions[n] = new Vector2(100 * i, 50 * i++);
            return positions;
        }

        [Test]
        public void SaveLoad_RoundTrip_KeepsEveryPosition_AndAssignsIds()
        {
            // Arrange
            var root = new AINode_Sequence();
            var a = new AINode_Wait();
            var b = new AINode_Wait();
            root.Children.Add(a);
            root.Children.Add(b);
            _so.AIRoot = root;
            var snap = AITreeSerializer.Load(root);
            var positions = PositionsOf(snap);

            // Act
            AITreeLayoutSidecar.Save(_so, snap, positions, _dir);
            var loaded = AITreeLayoutSidecar.Load(_so, snap, _dir);

            // Assert
            Assert.IsNotNull(loaded);
            Assert.AreEqual(positions[a], loaded[a]);
            Assert.AreEqual(positions[b], loaded[b]);
            Assert.IsFalse(string.IsNullOrEmpty(a.EditorNodeId), "Save asigna el id faltante");
            Assert.AreNotEqual(a.EditorNodeId, b.EditorNodeId);
        }

        [Test]
        public void Load_InsertSameTypeNodeAtFront_KeepsPositionsById()
        {
            // Arrange — el caso que el índice de preorden perdía EN SILENCIO: un Wait nuevo
            // adelante de otro Wait corre todos los índices sin cambiar la firma de tipos.
            var root = new AINode_Sequence();
            var a = new AINode_Wait();
            var b = new AINode_Wait();
            root.Children.Add(a);
            root.Children.Add(b);
            var snap = AITreeSerializer.Load(root);
            var positions = PositionsOf(snap);
            AITreeLayoutSidecar.Save(_so, snap, positions, _dir);

            var inserted = new AINode_Wait();
            root.Children.Insert(0, inserted);
            var snap2 = AITreeSerializer.Load(root);

            // Act
            var loaded = AITreeLayoutSidecar.Load(_so, snap2, _dir);

            // Assert — a y b conservan SU posición; el nuevo no está (cae al auto-layout).
            Assert.AreEqual(positions[a], loaded[a]);
            Assert.AreEqual(positions[b], loaded[b]);
            Assert.IsFalse(loaded.ContainsKey(inserted));
        }

        [Test]
        public void Load_LegacyFileWithoutIds_FallsBackToPreorderIndex()
        {
            // Arrange — archivo escrito por la versión vieja: solo Index + TypeName.
            var root = new AINode_Sequence();
            var leaf = new AINode_Wait();
            root.Children.Add(leaf);
            var snap = AITreeSerializer.Load(root);
            Directory.CreateDirectory(_dir);
            File.WriteAllText(AITreeLayoutSidecar.PathForId("test.layout", _dir),
                "{\"Entries\":[{\"Index\":1,\"TypeName\":\"AINode_Wait\",\"Position\":{\"x\":7.0,\"y\":9.0}}]}");

            // Act
            var loaded = AITreeLayoutSidecar.Load(_so, snap, _dir);

            // Assert
            Assert.IsNotNull(loaded);
            Assert.AreEqual(new Vector2(7, 9), loaded[leaf]);
        }

        [Test]
        public void Load_UnknownId_FallsBackToIndexGuard_NotAnException()
        {
            // Arrange — id huérfano (nodo con id aún no persistido, o reemplazado): la
            // entrada cae al camino por índice; si el tipo tampoco matchea, se descarta.
            var root = new AINode_Sequence();
            var leaf = new AINode_Wait();
            root.Children.Add(leaf);
            var snap = AITreeSerializer.Load(root);
            Directory.CreateDirectory(_dir);
            File.WriteAllText(AITreeLayoutSidecar.PathForId("test.layout", _dir),
                "{\"Entries\":[" +
                "{\"Id\":\"deadbeef\",\"Index\":1,\"TypeName\":\"AINode_Wait\",\"Position\":{\"x\":3.0,\"y\":4.0}}," +
                "{\"Id\":\"cafebabe\",\"Index\":0,\"TypeName\":\"AINode_Move\",\"Position\":{\"x\":1.0,\"y\":1.0}}]}");

            // Act
            var loaded = AITreeLayoutSidecar.Load(_so, snap, _dir);

            // Assert — la primera entra por índice+tipo; la segunda (tipo equivocado) no.
            Assert.AreEqual(new Vector2(3, 4), loaded[leaf]);
            Assert.AreEqual(1, loaded.Count);
        }
    }
}
