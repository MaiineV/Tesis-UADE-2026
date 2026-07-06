using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.Tooltips;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Cubre el fix de tooltips cortados: AutoFit re-posiciona el panel para que entre
    /// completo en el canvas; Fixed respeta la posición configurada exacta.
    /// </summary>
    [TestFixture]
    public sealed class TooltipPlacementTests
    {
        private static readonly Rect Bounds = new Rect(-400f, -300f, 800f, 600f);

        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
                if (go != null) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [Test]
        public void ComputeClampShift_RectFullyInsideBounds_ReturnsZero()
        {
            // Arrange
            var min = new Vector2(-100f, -50f);
            var max = new Vector2(100f, 50f);

            // Act
            var shift = TooltipController.ComputeClampShift(min, max, Bounds, 8f);

            // Assert
            Assert.AreEqual(Vector2.zero, shift);
        }

        [Test]
        public void ComputeClampShift_OverflowRight_ShiftsLeftExactly()
        {
            // Arrange — el caso reportado: el tooltip de attack se dibuja abajo-derecha
            // y se sale por el borde derecho del canvas.
            var min = new Vector2(350f, 0f);
            var max = new Vector2(550f, 100f); // 158 px fuera del borde derecho (392 con padding)

            // Act
            var shift = TooltipController.ComputeClampShift(min, max, Bounds, 8f);

            // Assert
            Assert.AreEqual(-158f, shift.x, 1e-3f, "Debe correr el panel justo hasta el borde con padding.");
            Assert.AreEqual(0f, shift.y, 1e-3f);
        }

        [Test]
        public void ComputeClampShift_OverflowBottomRightCorner_ShiftsBothAxes()
        {
            // Arrange
            var min = new Vector2(300f, -350f);
            var max = new Vector2(500f, -250f);

            // Act
            var shift = TooltipController.ComputeClampShift(min, max, Bounds, 8f);

            // Assert
            Assert.AreEqual(-108f, shift.x, 1e-3f);
            Assert.AreEqual(58f, shift.y, 1e-3f);
        }

        [Test]
        public void ComputeClampShift_RectLargerThanBounds_PrioritizesMinEdge()
        {
            // Arrange — panel más ancho que el canvas: al menos el borde izquierdo
            // (donde arranca el texto) debe quedar visible.
            var min = new Vector2(-500f, 0f);
            var max = new Vector2(500f, 50f);

            // Act
            var shift = TooltipController.ComputeClampShift(min, max, Bounds, 8f);

            // Assert
            Assert.AreEqual(Bounds.xMin + 8f, min.x + shift.x, 1e-3f,
                "El borde izquierdo debe quedar dentro aunque el panel no entre entero.");
        }

        [Test]
        public void Show_FixedPlacement_PositionsRootExactlyAtScreenPos()
        {
            // Arrange — en Fixed no se aplica ni el offset global ni el clamp: la
            // posición configurada por el diseñador es exacta.
            var controller = CreateOverlayTooltipController(out var root);
            var target = new Vector2(123f, 456f);

            // Act
            controller.Show("texto", target, ownerId: 1, TooltipPlacementMode.Fixed);

            // Assert
            Assert.IsTrue(root.gameObject.activeSelf, "El panel debe mostrarse.");
            Assert.AreEqual(target.x, root.position.x, 1e-3f);
            Assert.AreEqual(target.y, root.position.y, 1e-3f);
        }

        private TooltipController CreateOverlayTooltipController(out RectTransform root)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            _objects.Add(canvasGo);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var controllerGo = new GameObject("TooltipController", typeof(RectTransform));
            controllerGo.transform.SetParent(canvasGo.transform, false);
            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(controllerGo.transform, false);
            root = (RectTransform)panelGo.transform;

            var controller = controllerGo.AddComponent<TooltipController>();
            // Awake no corre en EditMode — resolver refs como lo haría el preview de editor.
            typeof(TooltipController)
                .GetMethod("EnsureRefs", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(controller, null);
            return controller;
        }
    }
}
