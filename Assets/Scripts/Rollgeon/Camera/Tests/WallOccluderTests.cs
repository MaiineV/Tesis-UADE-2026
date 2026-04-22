using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.GameCamera.Tests
{
    [TestFixture]
    public class WallOccluderTests
    {
        private GameObject _root;
        private WallOccluder _occluder;
        private Renderer _renderer;

        [SetUp]
        public void SetUp()
        {
            _root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _renderer = _root.GetComponent<Renderer>();
            _renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            _occluder = _root.AddComponent<WallOccluder>();
            _occluder.Direction = WallDirection.N;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void SetHidden_InstantApply_WritesAlphaZero()
        {
            _occluder.SetHidden(true, fadeSeconds: 0f);
            Assert.IsTrue(_occluder.IsHidden);
            Assert.AreEqual(0f, _renderer.sharedMaterial.color.a, delta: 0.001f);
        }

        [Test]
        public void SetHidden_False_RestoresAlphaOne()
        {
            _occluder.SetHidden(true, fadeSeconds: 0f);
            _occluder.SetHidden(false, fadeSeconds: 0f);

            Assert.IsFalse(_occluder.IsHidden);
            Assert.AreEqual(1f, _renderer.sharedMaterial.color.a, delta: 0.001f);
        }

        [Test]
        public void Direction_IsPersisted()
        {
            _occluder.Direction = WallDirection.SW;
            Assert.AreEqual(WallDirection.SW, _occluder.Direction);
        }
    }
}
