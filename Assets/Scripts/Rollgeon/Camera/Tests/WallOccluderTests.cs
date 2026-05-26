using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.GameCamera.Tests
{
    [TestFixture]
    public class WallOccluderTests
    {
        private static readonly int s_AlphaCutoff = Shader.PropertyToID("_AlphaCutoff");

        private GameObject _root;
        private WallOccluder _occluder;
        private Renderer _renderer;
        private MaterialPropertyBlock _probe;

        [SetUp]
        public void SetUp()
        {
            _root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _renderer = _root.GetComponent<Renderer>();
            _renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            _occluder = _root.AddComponent<WallOccluder>();
            _occluder.Direction = WallDirection.N;
            _probe = new MaterialPropertyBlock();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void SetHidden_InstantApply_SetsAlphaCutoffToZeroViaPropertyBlock()
        {
            // Arrange
            // Act
            _occluder.SetHidden(true, fadeSeconds: 0f);

            // Assert
            Assert.IsTrue(_occluder.IsHidden);
            _renderer.GetPropertyBlock(_probe);
            Assert.AreEqual(0f, _probe.GetFloat(s_AlphaCutoff), delta: 0.001f);
        }

        [Test]
        public void SetHidden_False_RestoresAlphaCutoffToOne()
        {
            // Arrange
            _occluder.SetHidden(true, fadeSeconds: 0f);

            // Act
            _occluder.SetHidden(false, fadeSeconds: 0f);

            // Assert
            Assert.IsFalse(_occluder.IsHidden);
            _renderer.GetPropertyBlock(_probe);
            Assert.AreEqual(1f, _probe.GetFloat(s_AlphaCutoff), delta: 0.001f);
        }

        [Test]
        public void SetHidden_DoesNotMutateSharedMaterial()
        {
            // Regression: el bug original mutaba sharedMaterial.color.a (afectaba a
            // todas las paredes que compartían Mat_Wall y persistía en el asset).
            // Arrange
            var sharedColorBefore = _renderer.sharedMaterial.color;

            // Act
            _occluder.SetHidden(true, fadeSeconds: 0f);

            // Assert
            Assert.AreEqual(sharedColorBefore, _renderer.sharedMaterial.color,
                "sharedMaterial.color no debe mutarse — el fade va por MPB.");
        }

        [Test]
        public void Direction_IsPersisted()
        {
            // Arrange / Act
            _occluder.Direction = WallDirection.SW;

            // Assert
            Assert.AreEqual(WallDirection.SW, _occluder.Direction);
        }
    }
}
