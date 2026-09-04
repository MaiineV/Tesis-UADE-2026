using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Entities.Visuals.Tests
{
    /// <summary>
    /// Las layers de targeting se asignan al spawnear, no en los prefabs: si el root o un
    /// collider quedan en Default, el raycast de selección no puede distinguir al héroe de
    /// un enemigo ni a ninguno de una pared.
    /// </summary>
    [TestFixture]
    public sealed class PawnLayersTests
    {
        private const int DefaultLayer = 0;
        private const int WorldUiLayer = 9;

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        private GameObject Make(string name, GameObject parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent.transform);
            _created.Add(go);
            return go;
        }

        [Test]
        public void Layers_ExistInTagManager()
        {
            // El fallback por índice existe para no romper un clon sin la layer; acá se
            // exige la real para que un TagManager sin editar no pase desapercibido.
            Assert.GreaterOrEqual(LayerMask.NameToLayer(PawnLayers.PlayerLayerName), 0,
                "Falta la layer 'Player' en ProjectSettings/TagManager.asset.");
            Assert.GreaterOrEqual(LayerMask.NameToLayer(PawnLayers.EntityLayerName), 0,
                "Falta la layer 'Entity' en ProjectSettings/TagManager.asset.");
            Assert.AreNotEqual(PawnLayers.PlayerLayer, PawnLayers.EntityLayer);
        }

        [Test]
        public void LayerFor_Hero_IsPlayer_AndEveryOtherKind_IsEntity()
        {
            Assert.AreEqual(PawnLayers.PlayerLayer, PawnLayers.LayerFor(EntityPawn.PawnKind.Hero));
            Assert.AreEqual(PawnLayers.EntityLayer, PawnLayers.LayerFor(EntityPawn.PawnKind.Enemy));
            Assert.AreEqual(PawnLayers.EntityLayer, PawnLayers.LayerFor(EntityPawn.PawnKind.Boss));
            Assert.AreEqual(PawnLayers.EntityLayer, PawnLayers.LayerFor(EntityPawn.PawnKind.Prop));
        }

        [Test]
        public void Apply_Hero_SetsRootAndColliderChild()
        {
            var root = Make("Hero");
            var model = Make("Model", root);
            model.AddComponent<BoxCollider>();

            PawnLayers.Apply(root, EntityPawn.PawnKind.Hero);

            Assert.AreEqual(PawnLayers.PlayerLayer, root.layer,
                "El root va siempre: el collider que se cuelga después del spawn hereda su layer.");
            Assert.AreEqual(PawnLayers.PlayerLayer, model.layer);
        }

        [Test]
        public void Apply_Enemy_SetsEntityOnNestedCollider()
        {
            var root = Make("Enemy");
            var wrapper = Make("Wrapper", root);
            var body = Make("Body", wrapper);
            body.AddComponent<CapsuleCollider>();

            PawnLayers.Apply(root, EntityPawn.PawnKind.Enemy);

            Assert.AreEqual(PawnLayers.EntityLayer, root.layer);
            Assert.AreEqual(PawnLayers.EntityLayer, body.layer);
            Assert.AreEqual(DefaultLayer, wrapper.layer,
                "Un hijo sin collider ni renderer relevante no se toca.");
        }

        [Test]
        public void Apply_LeavesNonDefaultChildrenUntouched()
        {
            var root = Make("Enemy");
            var healthBar = Make("HealthBar", root);
            healthBar.layer = WorldUiLayer;
            healthBar.AddComponent<BoxCollider>();

            PawnLayers.Apply(root, EntityPawn.PawnKind.Enemy);

            Assert.AreEqual(WorldUiLayer, healthBar.layer,
                "La barra de HP vive en WorldUI para su cámara propia; pisarla la saca de render.");
        }

        [Test]
        public void Apply_NullRoot_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => PawnLayers.Apply(null, EntityPawn.PawnKind.Enemy));
        }
    }
}
