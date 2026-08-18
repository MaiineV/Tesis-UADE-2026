using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Dungeon.Components;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.Dungeon.Tests
{
    /// <summary>
    /// Visibilidad del cartel de salida (<see cref="DoorExitSignView"/>) según el estado
    /// que <see cref="DoorController.SetState"/> propaga. El juice (drop-in + bob) queda
    /// gated por <c>Application.isPlaying</c> — acá solo se cubre la lógica de estado.
    /// </summary>
    [TestFixture]
    public class DoorExitSignViewTests
    {
        private readonly List<Object> _createdObjects = new();

        private DoorController _controller;
        private GameObject _sign;
        private Vector3 _signBasePos;

        [SetUp]
        public void SetUp()
        {
            // Root inactivo: igual que los fixtures de DungeonManagerTests, evita que
            // Instantiate/AddComponent disparen los Awake de tooltips en EditMode.
            var root = new GameObject("DoorBossFixture");
            root.SetActive(false);
            _createdObjects.Add(root);

            _controller = root.AddComponent<DoorController>();
            var view = root.AddComponent<DoorExitSignView>();

            _sign = new GameObject("ExitSign");
            _sign.transform.SetParent(root.transform, false);
            _signBasePos = new Vector3(-0.088f, 2.115f, -0.314f);
            _sign.transform.localPosition = _signBasePos;
            _sign.SetActive(false);

            typeof(DoorExitSignView)
                .GetField(DoorExitSignView.EditorSignField,
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, _sign);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null)
                    Object.DestroyImmediate(obj);
            }
            _createdObjects.Clear();
        }

        [Test]
        public void SetState_ExitDoorOpens_ShowsSign()
        {
            // Arrange
            _controller.IsExit = true;

            // Act — la exit door abre cuando la sala queda Cleared (boss muerto).
            _controller.SetState(DoorVisualState.Open);

            // Assert
            Assert.IsTrue(_sign.activeSelf,
                "El ExitSign debe mostrarse cuando la puerta exit abre (boss derrotado).");
            Assert.AreEqual(_signBasePos, _sign.transform.localPosition,
                "En EditMode el cartel queda en su posición base, sin animación.");
        }

        [Test]
        public void SetState_ExitDoorLockedDuringBossFight_KeepsSignHidden()
        {
            // Arrange
            _controller.IsExit = true;

            // Act — con el boss vivo la exit door queda LockedCombat.
            _controller.SetState(DoorVisualState.LockedCombat);

            // Assert
            Assert.IsFalse(_sign.activeSelf,
                "El ExitSign no puede aparecer antes de matar al boss.");
        }

        [Test]
        public void SetState_NonExitDoorOpens_KeepsSignHidden()
        {
            // Arrange — DoorBoss usada como puerta HACIA la boss room (swap), no exit.
            _controller.IsExit = false;

            // Act
            _controller.SetState(DoorVisualState.Open);

            // Assert
            Assert.IsFalse(_sign.activeSelf,
                "Una DoorBoss no-exit (puerta hacia la boss room) no debe mostrar el cartel.");
        }

        [Test]
        public void SetState_OpenThenLocked_HidesSignAndRestoresPosition()
        {
            // Arrange
            _controller.IsExit = true;
            _controller.SetState(DoorVisualState.Open);
            Assert.IsTrue(_sign.activeSelf, "Precondición: el cartel se mostró al abrir.");

            // Act — vuelta atrás (ej. resync con estado no-cleared).
            _controller.SetState(DoorVisualState.LockedCombat);

            // Assert
            Assert.IsFalse(_sign.activeSelf, "El cartel debe ocultarse si la puerta deja de estar abierta.");
            Assert.AreEqual(_signBasePos, _sign.transform.localPosition,
                "Al ocultarse el cartel restaura su posición local base.");
        }
    }
}
