using System;
using NUnit.Framework;
using UnityEngine;

namespace Rollgeon.Entities.Visuals.Tests
{
    /// <summary>
    /// Soft-stop de la caminata (<see cref="EntityPawn.RequestStopAtStepEnd"/>,
    /// cancel de exploración con X). El camino feliz — frenar a mitad de un path
    /// animado — es PlayMode-only (corutinas); acá se cubren los guards.
    /// </summary>
    [TestFixture]
    public class EntityPawnStopRequestTests
    {
        private GameObject _go;
        private EntityPawn _pawn;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("pawn");
            _pawn = _go.AddComponent<EntityPawn>();
            _pawn.Bind(Guid.NewGuid(), EntityPawn.PawnKind.Hero);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void RequestStopAtStepEnd_WhenNotMoving_ReturnsFalse()
        {
            // Arrange — pawn quieto (IsMoving == false).
            bool callbackRan = false;

            // Act
            bool result = _pawn.RequestStopAtStepEnd(_ => callbackRan = true);

            // Assert — nada que frenar: ni acepta el pedido ni invoca el callback.
            Assert.IsFalse(result);
            Assert.IsFalse(callbackRan);
        }
    }
}
