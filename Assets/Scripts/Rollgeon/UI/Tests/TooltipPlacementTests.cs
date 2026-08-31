using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.Tooltips;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Cubre el fix de tooltips cortados y descentrados (BUG-029): AutoFit re-posiciona
    /// el panel para que entre completo en el canvas; Fixed no suma el offset global de
    /// AutoFit pero sí se clampea al canvas como red de seguridad. También cubre el
    /// anclaje X centrado (no al pivot) que usan ambos modos, y el fix BUG-041: AutoFit
    /// ancla al borde SUPERIOR del rect (no al centro) para que el tooltip no quede
    /// tapado por el propio elemento/cursor que lo disparó; Fixed sigue centrando.
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

        // ------------------------------------------------------------------
        // ComputeAnchorTarget — lado vertical del panel (BUG-075)
        // ------------------------------------------------------------------

        [Test]
        public void ComputeAnchorTarget_Above_ReturnsAnchorPlusOffset()
        {
            // Arrange
            var anchor = new Vector2(400f, 300f);
            var offset = new Vector2(0f, 12f);

            // Act
            var target = TooltipVerticalPlacement.ComputeAnchorTarget(
                anchor, offset, panelScreenHeight: 100f, TooltipVerticalSide.Above);

            // Assert — comportamiento histórico intacto.
            Assert.AreEqual(anchor + offset, target);
        }

        [Test]
        public void ComputeAnchorTarget_Below_PlacesPanelTopBelowAnchor()
        {
            // Arrange — pivot inferior-centro: el panel ocupa [pivot.y, pivot.y + alto].
            var anchor = new Vector2(400f, 300f);
            var offset = new Vector2(0f, 12f);
            const float panelHeight = 100f;

            // Act
            var target = TooltipVerticalPlacement.ComputeAnchorTarget(
                anchor, offset, panelHeight, TooltipVerticalSide.Below);

            // Assert — el TOPE del panel (pivot + alto) queda offset.y por debajo del
            // anclaje: 300 - 12 = 288; pivot en 188.
            Assert.AreEqual(400f, target.x, 1e-3f, "X no cambia con el lado vertical");
            Assert.AreEqual(188f, target.y, 1e-3f);
            Assert.AreEqual(anchor.y - offset.y, target.y + panelHeight, 1e-3f,
                "el tope del panel debe caer justo debajo del anclaje");
        }

        [Test]
        public void ComputeAnchorTarget_BelowNearScreenBottom_ClampShiftsBackInside()
        {
            // Arrange — enemigo pegado al borde inferior: el panel colgado hacia abajo
            // queda fuera de los bounds y el clamp existente lo tiene que devolver.
            var target = TooltipVerticalPlacement.ComputeAnchorTarget(
                new Vector2(0f, -330f), new Vector2(0f, 12f), 100f, TooltipVerticalSide.Below);
            var min = new Vector2(-100f, target.y);
            var max = new Vector2(100f, target.y + 100f);

            // Act
            var shift = TooltipController.ComputeClampShift(min, max, Bounds, 8f);

            // Assert — shift positivo en Y: lo empuja de vuelta a pantalla.
            Assert.Greater(shift.y, 0f, "el clamp debe empujar el panel de vuelta dentro del canvas");
        }

        [Test]
        public void Show_FixedPlacement_FarFromEdge_DoesNotClamp()
        {
            // Arrange — en Fixed no se suma el offset global, pero el clamp SIGUE
            // aplicando como red de seguridad. Lejos del borde no debería moverla.
            var controller = CreateOverlayTooltipController(out var root, panelSize: new Vector2(100f, 40f));
            var target = new Vector2(0f, 0f); // centro del canvas, margen de sobra en todos los ejes

            // Act
            controller.Show("texto", target, ownerId: 1, TooltipPlacementMode.Fixed);

            // Assert
            Assert.IsTrue(root.gameObject.activeSelf, "El panel debe mostrarse.");
            Assert.AreEqual(target.x, root.position.x, 1e-3f);
            Assert.AreEqual(target.y, root.position.y, 1e-3f);
        }

        [Test]
        public void Show_FixedPlacement_NearEdge_ClampsToStayOnScreen()
        {
            // Arrange — BUG-029: Fixed nunca clampeaba y el panel podía quedar cortado
            // contra el borde del canvas. Target pegado al borde derecho: el panel de
            // 100px de ancho se saldría del canvas si no se clampea.
            var controller = CreateOverlayTooltipController(out var root, panelSize: new Vector2(100f, 40f));
            var target = new Vector2(380f, 0f); // panel iría de 330 a 430; borde útil = 400 - 8 (padding)

            // Act
            controller.Show("texto", target, ownerId: 1, TooltipPlacementMode.Fixed);

            // Assert — shift = (Bounds.xMax - padding) - max.x = (400 - 8) - 430 = -38.
            Assert.AreEqual(342f, root.position.x, 1e-3f);
            Assert.AreEqual(0f, root.position.y, 1e-3f, "El eje Y no debía moverse.");
        }

        [Test]
        public void ResolveFixedScreenPos_OffsetScalesWithCanvasScaleFactor()
        {
            // Arrange — el offset se ingresa en píxeles de REFERENCIA del canvas: al
            // cambiar la resolución (scaleFactor del CanvasScaler), la distancia relativa
            // al anchor debe mantenerse constante en unidades de canvas.
            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            _objects.Add(canvasGo);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var anchorGo = new GameObject("Anchor", typeof(RectTransform));
            anchorGo.transform.SetParent(canvasGo.transform, false);
            var anchor = (RectTransform)anchorGo.transform;

            var settings = new TooltipPlacementSettings
            {
                Mode = TooltipPlacementMode.Fixed,
                FixedAnchor = anchor,
                FixedOffset = new Vector2(30f, -20f),
            };

            // Act — misma escena a dos "resoluciones" (scaleFactor 1x y 2x).
            canvas.scaleFactor = 1f;
            Vector2 deltaAt1x = (settings.ResolveFixedScreenPos(null) - (Vector2)anchor.position)
                                / canvas.scaleFactor;
            canvas.scaleFactor = 2f;
            Vector2 deltaAt2x = (settings.ResolveFixedScreenPos(null) - (Vector2)anchor.position)
                                / canvas.scaleFactor;

            // Assert — en unidades de canvas la posición relativa no cambia con la resolución.
            Assert.AreEqual(settings.FixedOffset.x, deltaAt1x.x, 1e-3f);
            Assert.AreEqual(settings.FixedOffset.y, deltaAt1x.y, 1e-3f);
            Assert.AreEqual(deltaAt1x.x, deltaAt2x.x, 1e-3f,
                "El offset en píxeles de referencia debe mantenerse al cambiar la resolución.");
            Assert.AreEqual(deltaAt1x.y, deltaAt2x.y, 1e-3f,
                "El offset en píxeles de referencia debe mantenerse al cambiar la resolución.");
        }

        [Test]
        public void ScreenPosOf_TriggerWithPivotZeroZero_AnchorsAboveRectXCenteredNotPivot()
        {
            // Arrange — BUG-029: ScreenPosOf usaba rect.position (el PIVOT), no el
            // centro visual. Triggers con pivot (0,0), como PocionSlot, quedaban con el
            // tooltip descolgado hacia la esquina en vez de centrado sobre el elemento.
            // BUG-041: el anchor Y ya no es el centro sino el borde SUPERIOR del rect —
            // con el offset chico del controller, anclar al centro dejaba el tooltip de
            // curación con el borde inferior tapado por el propio botón/cursor.
            var go = new GameObject("Trigger", typeof(RectTransform));
            _objects.Add(go);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(60f, 20f);
            rect.position = new Vector3(100f, 100f, 0f); // posición del pivot: esquina inferior-izquierda

            // Act
            var screenPos = TooltipPlacementSettings.ScreenPosOf(rect);

            // Assert — X centrado (mitad del ancho); Y en el borde superior (alto completo).
            Assert.AreEqual(130f, screenPos.x, 1e-3f);
            Assert.AreEqual(120f, screenPos.y, 1e-3f);
        }

        [Test]
        public void ScreenPosOf_TriggerWithPivotCenter_AnchorsAboveRectPosition()
        {
            // Arrange — caso control: con pivot (0.5, 0.5) el centro coincide con
            // rect.position; el anchor de AutoFit queda medio-alto (10) por encima.
            var go = new GameObject("Trigger", typeof(RectTransform));
            _objects.Add(go);
            var rect = (RectTransform)go.transform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(60f, 20f);
            rect.position = new Vector3(200f, 300f, 0f);

            // Act
            var screenPos = TooltipPlacementSettings.ScreenPosOf(rect);

            // Assert
            Assert.AreEqual(200f, screenPos.x, 1e-3f);
            Assert.AreEqual(310f, screenPos.y, 1e-3f);
        }

        [Test]
        public void ScreenPosOf_TallerRect_AnchorsHigherThanShorterRect()
        {
            // Arrange — BUG-041 regression: el punto de anclaje debe escalar con la
            // altura real del trigger (HealButton 100x100 vs PocionSlot 80x92), no ser
            // un offset fijo — sino un botón alto seguiría con el tooltip pisándole el borde.
            var shortGo = new GameObject("Short", typeof(RectTransform));
            _objects.Add(shortGo);
            var shortRect = (RectTransform)shortGo.transform;
            shortRect.pivot = new Vector2(0.5f, 0.5f);
            shortRect.sizeDelta = new Vector2(80f, 20f);
            shortRect.position = new Vector3(0f, 0f, 0f);

            var tallGo = new GameObject("Tall", typeof(RectTransform));
            _objects.Add(tallGo);
            var tallRect = (RectTransform)tallGo.transform;
            tallRect.pivot = new Vector2(0.5f, 0.5f);
            tallRect.sizeDelta = new Vector2(80f, 100f);
            tallRect.position = new Vector3(0f, 0f, 0f);

            // Act
            var shortAnchor = TooltipPlacementSettings.ScreenPosOf(shortRect);
            var tallAnchor = TooltipPlacementSettings.ScreenPosOf(tallRect);

            // Assert
            Assert.Greater(tallAnchor.y, shortAnchor.y,
                "Un rect más alto debe anclar más arriba en pantalla.");
            Assert.AreEqual(10f, shortAnchor.y, 1e-3f);
            Assert.AreEqual(50f, tallAnchor.y, 1e-3f);
        }

        [Test]
        public void ResolveFixedScreenPos_StillCentersAnchor_UnaffectedByAutoFitTopEdgeFix()
        {
            // Arrange — BUG-041: el fix de AutoFit (anclar al borde superior) NO debe
            // mover los triggers Fixed (ej. offset autorado a mano de los chips de
            // combate) — ResolveFixedScreenPos pasa por el helper de CENTRO, no por
            // el público ScreenPosOf que ahora devuelve el borde superior.
            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            _objects.Add(canvasGo);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var anchorGo = new GameObject("Anchor", typeof(RectTransform));
            anchorGo.transform.SetParent(canvasGo.transform, false);
            var anchor = (RectTransform)anchorGo.transform;
            anchor.pivot = new Vector2(0.5f, 0.5f);
            anchor.sizeDelta = new Vector2(60f, 100f); // rect alto: si usara el borde superior, movería el Y bastante.
            anchor.position = new Vector3(50f, 50f, 0f);

            var settings = new TooltipPlacementSettings
            {
                Mode = TooltipPlacementMode.Fixed,
                FixedAnchor = anchor,
                FixedOffset = Vector2.zero,
            };

            // Act
            var pos = settings.ResolveFixedScreenPos(null);

            // Assert — coincide con el CENTRO del anchor (rect.position con pivot 0.5,0.5), no con el borde superior.
            Assert.AreEqual(50f, pos.x, 1e-3f);
            Assert.AreEqual(50f, pos.y, 1e-3f);
        }

        [Test]
        public void ResolveFixedScreenPos_NoAnchorConfigured_FallsBackToProvidedRect()
        {
            // Arrange
            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            _objects.Add(canvasGo);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var fallbackGo = new GameObject("Trigger", typeof(RectTransform));
            fallbackGo.transform.SetParent(canvasGo.transform, false);
            var fallback = (RectTransform)fallbackGo.transform;
            fallback.position = new Vector3(200f, 100f, 0f);

            var settings = new TooltipPlacementSettings
            {
                Mode = TooltipPlacementMode.Fixed,
                FixedAnchor = null,
                FixedOffset = new Vector2(10f, 5f),
            };

            // Act
            var pos = settings.ResolveFixedScreenPos(fallback);

            // Assert — sin anchor explícito usa el rect del trigger.
            Assert.AreEqual(210f, pos.x, 1e-3f);
            Assert.AreEqual(105f, pos.y, 1e-3f);
        }

        /// <param name="panelSize">
        /// Tamaño del panel (sizeDelta), relevante para los tests de clamp — sin tamaño
        /// el rect queda en un punto y el clamp nunca dispara.
        /// </param>
        private TooltipController CreateOverlayTooltipController(out RectTransform root, Vector2? panelSize = null)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            _objects.Add(canvasGo);
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            // El canvas raíz se dimensiona igual que Bounds (pivot default 0.5,0.5) para
            // que los tests de clamp reusen la misma constante que ComputeClampShift.
            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(Bounds.width, Bounds.height);
            canvasRect.position = new Vector3(Bounds.center.x, Bounds.center.y, 0f);

            var controllerGo = new GameObject("TooltipController", typeof(RectTransform));
            controllerGo.transform.SetParent(canvasGo.transform, false);
            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(controllerGo.transform, false);
            root = (RectTransform)panelGo.transform;
            if (panelSize.HasValue) root.sizeDelta = panelSize.Value;

            var controller = controllerGo.AddComponent<TooltipController>();
            // Awake no corre en EditMode — resolver refs como lo haría el preview de editor.
            typeof(TooltipController)
                .GetMethod("EnsureRefs", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(controller, null);
            return controller;
        }
    }
}
