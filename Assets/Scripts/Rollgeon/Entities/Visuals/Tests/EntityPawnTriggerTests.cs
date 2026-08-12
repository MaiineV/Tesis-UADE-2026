using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Entities.Visuals.Tests
{
    [TestFixture]
    public class EntityPawnTriggerTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        private EntityPawn MakePawn()
        {
            var go = new GameObject("Pawn");
            _created.Add(go);
            return go.AddComponent<EntityPawn>();
        }

        [Test]
        public void TrySetTrigger_ReturnsFalse_WhenPawnHasNoAnimator()
        {
            // Arrange
            var pawn = MakePawn();

            // Act
            bool result = pawn.TrySetTrigger("Awaken");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void TrySetTrigger_ReturnsFalse_WhenControllerLacksTriggerParam()
        {
            // Arrange
            var pawn = MakePawn();
            var animator = pawn.gameObject.AddComponent<Animator>();
            var controller = new UnityEditor.Animations.AnimatorController();
            _created.Add(controller);
            controller.AddLayer("Base");
            animator.runtimeAnimatorController = controller;

            // Act
            bool result = pawn.TrySetTrigger("Awaken");

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void TrySetTrigger_ReturnsTrue_WhenChildAnimatorDeclaresTrigger()
        {
            // Arrange — el Animator vive en el hijo del modelo, como en los prefabs reales.
            var pawn = MakePawn();
            var model = new GameObject("Model");
            model.transform.SetParent(pawn.transform);
            var animator = model.AddComponent<Animator>();
            var controller = new UnityEditor.Animations.AnimatorController();
            _created.Add(controller);
            controller.AddLayer("Base");
            controller.AddParameter("Awaken", AnimatorControllerParameterType.Trigger);
            animator.runtimeAnimatorController = controller;

            // Act
            bool result = pawn.TrySetTrigger("Awaken");

            // Assert — el side-effect del SetTrigger es de Unity (el Animator no corre
            // en EditMode); acá se contrata que el pawn resolvió animator + param.
            Assert.IsTrue(result);
        }
    }
}
