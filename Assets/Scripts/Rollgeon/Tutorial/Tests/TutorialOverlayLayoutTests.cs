using NUnit.Framework;
using Rollgeon.Tutorial.UI;
using UnityEngine;

namespace Rollgeon.Tutorial.Tests
{
    [TestFixture]
    public class TutorialOverlayLayoutTests
    {
        private static readonly Vector2 Screen1080 = new Vector2(1920f, 1080f);

        // ── ResolvePopupCenter ────────────────────────────────────────────

        [Test]
        public void ResolvePopupCenter_AnchorBottomLeft_PlacesPopupTopRight()
        {
            var center = TutorialOverlayLayout.ResolvePopupCenter(
                new Vector2(200f, 150f), Screen1080, margin: 40f);

            Assert.Greater(center.x, Screen1080.x * 0.5f, "Popup debe ir a la mitad derecha.");
            Assert.Greater(center.y, Screen1080.y * 0.5f, "Popup debe ir a la mitad superior.");
        }

        [Test]
        public void ResolvePopupCenter_AnchorTopRight_PlacesPopupBottomLeft()
        {
            var center = TutorialOverlayLayout.ResolvePopupCenter(
                new Vector2(1700f, 950f), Screen1080, margin: 40f);

            Assert.Less(center.x, Screen1080.x * 0.5f);
            Assert.Less(center.y, Screen1080.y * 0.5f);
        }

        // ── Quadrant hysteresis ───────────────────────────────────────────

        [Test]
        public void ResolveQuadrant_FirstResolve_UsesPlainMidlines()
        {
            int q = TutorialOverlayLayout.ResolveQuadrantWithHysteresis(
                new Vector2(1500f, 900f), Screen1080, previousQuadrant: -1, hysteresisFraction: 0.08f);

            Assert.AreEqual(3, q, "Anchor arriba-derecha → bits derecha(1) + arriba(2).");
        }

        [Test]
        public void ResolveQuadrant_SmallCrossWithinBand_KeepsPreviousQuadrant()
        {
            // Anchor en cuadrante derecho (bit 0 = 1). Cruza apenas la línea media
            // hacia la izquierda (960 - 30 px), dentro de la banda de 8% (153.6 px).
            int q = TutorialOverlayLayout.ResolveQuadrantWithHysteresis(
                new Vector2(930f, 900f), Screen1080, previousQuadrant: 3, hysteresisFraction: 0.08f);

            Assert.AreEqual(3, q, "Un cruce menor a la banda no debe flipear el cuadrante.");
        }

        [Test]
        public void ResolveQuadrant_CrossBeyondBand_Flips()
        {
            int q = TutorialOverlayLayout.ResolveQuadrantWithHysteresis(
                new Vector2(700f, 900f), Screen1080, previousQuadrant: 3, hysteresisFraction: 0.08f);

            Assert.AreEqual(2, q, "Un cruce mayor a la banda debe flipear el bit horizontal.");
        }

        [Test]
        public void PopupCenterForQuadrant_IsOppositeOfAnchorQuadrant()
        {
            // Anchor arriba-derecha (q=3) → popup abajo-izquierda.
            var center = TutorialOverlayLayout.PopupCenterForQuadrant(3, Screen1080, margin: 40f);

            Assert.Less(center.x, Screen1080.x * 0.5f);
            Assert.Less(center.y, Screen1080.y * 0.5f);
        }

        // ── Colocación adyacente (ResolveSideWithHysteresis / PopupCenterForSide) ──

        [Test]
        public void ResolveSide_AnchorOnLeftEdge_PlacesPopupToTheRightWithoutOverlap()
        {
            // Arrange — anchor pegado al borde izquierdo, popup de 500×220.
            var anchorPos = new Vector2(150f, 540f);
            var anchorHalf = new Vector2(80f, 80f);
            var popupSize = new Vector2(500f, 220f);

            // Act
            int side = TutorialOverlayLayout.ResolveSideWithHysteresis(
                anchorPos, anchorHalf, popupSize, Screen1080, gap: 90f, margin: 40f, previousSide: -1);
            var center = TutorialOverlayLayout.PopupCenterForSide(
                side, anchorPos, anchorHalf, popupSize, Screen1080, gap: 90f, margin: 40f);

            // Assert — lado derecha (0), borde izquierdo del popup fuera del anchor+gap.
            Assert.AreEqual(0, side, "El único lado con espacio de sobra es la derecha.");
            Assert.AreEqual(150f + 80f + 90f + 250f, center.x, 0.01f,
                "anchor.x + half + gap + mitad del popup.");
            Assert.AreEqual(540f, center.y, 0.01f, "Centrado sobre el anchor en el eje perpendicular.");
        }

        [Test]
        public void ResolveSide_AnchorTopRightCorner_ChoosesASideWhereThePopupFits()
        {
            // Arrange — la cola de turnos: esquina superior derecha.
            var anchorPos = new Vector2(1670f, 1010f);
            var anchorHalf = new Vector2(200f, 50f);
            var popupSize = new Vector2(500f, 220f);

            // Act
            int side = TutorialOverlayLayout.ResolveSideWithHysteresis(
                anchorPos, anchorHalf, popupSize, Screen1080, gap: 90f, margin: 40f, previousSide: -1);
            var center = TutorialOverlayLayout.PopupCenterForSide(
                side, anchorPos, anchorHalf, popupSize, Screen1080, gap: 90f, margin: 40f);

            // Assert — arriba (20 px) y derecha (50 px) no entran; entre izquierda
            // (1470 px libres) y abajo (960) gana la izquierda por espacio.
            Assert.AreEqual(1, side, "El lado con más espacio libre que entra es la izquierda.");
            Assert.AreEqual(1670f - 200f - 90f - 250f, center.x, 0.01f,
                "anchor.x - half - gap - mitad del popup.");
            Assert.AreEqual(1080f - 40f - 110f, center.y, 0.01f,
                "El clamp perpendicular baja el popup para que entre bajo el margen superior.");
        }

        [Test]
        public void ResolveSide_PreviousSideStillValid_KeepsItDespiteBetterCandidates()
        {
            // Arrange — anchor centrado: entran los 4 lados; el previo era arriba (2).
            var anchorPos = new Vector2(960f, 400f);
            var anchorHalf = new Vector2(100f, 100f);
            var popupSize = new Vector2(400f, 200f);

            // Act
            int side = TutorialOverlayLayout.ResolveSideWithHysteresis(
                anchorPos, anchorHalf, popupSize, Screen1080, gap: 60f, margin: 40f, previousSide: 2);

            // Assert
            Assert.AreEqual(2, side, "El lado previo válido se conserva (histéresis anti flip-flop).");
        }

        [Test]
        public void ResolveSide_PopupFitsNowhere_ReturnsMinusOne()
        {
            // Arrange — recorte gigante (MoveTiles, radio 320) en pantalla chica.
            var screen = new Vector2(800f, 600f);
            var anchorPos = new Vector2(400f, 300f);
            var anchorHalf = new Vector2(320f, 320f);
            var popupSize = new Vector2(400f, 250f);

            // Act
            int side = TutorialOverlayLayout.ResolveSideWithHysteresis(
                anchorPos, anchorHalf, popupSize, screen, gap: 90f, margin: 40f, previousSide: -1);

            // Assert
            Assert.AreEqual(-1, side, "Sin lado válido el caller cae al layout de cuadrante.");
        }

        [Test]
        public void PopupCenterForSide_AnchorNearBottomEdge_ClampsPerpendicularAxisInsideMargin()
        {
            // Arrange — anchor abajo a la izquierda, popup a su derecha: sin clamp la
            // mitad inferior del popup (110 px) se saldría de pantalla.
            var anchorPos = new Vector2(300f, 60f);
            var anchorHalf = new Vector2(60f, 60f);
            var popupSize = new Vector2(500f, 220f);

            // Act
            var center = TutorialOverlayLayout.PopupCenterForSide(
                0, anchorPos, anchorHalf, popupSize, Screen1080, gap: 90f, margin: 40f);

            // Assert
            Assert.AreEqual(40f + 110f, center.y, 0.01f, "margin + mitad del alto del popup.");
            Assert.AreEqual(300f + 60f + 90f + 250f, center.x, 0.01f, "El eje del lado no se toca.");
        }

        // ── Arrow ─────────────────────────────────────────────────────────

        [Test]
        public void ResolveArrowPosition_SitsOnCutoutEdgeTowardPopup()
        {
            var cutout = new Vector2(400f, 400f);
            var popup = new Vector2(1400f, 400f); // directamente a la derecha

            var arrowPos = TutorialOverlayLayout.ResolveArrowPosition(
                cutout, cutoutRadius: 100f, popup, gap: 20f);

            Assert.AreEqual(520f, arrowPos.x, 0.01f, "radio 100 + gap 20 hacia el popup (+x).");
            Assert.AreEqual(400f, arrowPos.y, 0.01f);
        }

        // ── Arrow contra la caja ──────────────────────────────────────────

        [Test]
        public void ResolveArrowPositionForBox_PopupToTheSide_SitsOnHorizontalEdge()
        {
            // Arrange — caja de 900×140 (la tira de dados del armado de bolsa).
            var cutout = new Vector2(960f, 400f);
            var halfSize = new Vector2(450f, 70f);
            var popup = new Vector2(1900f, 400f); // directamente a la derecha

            // Act
            var arrowPos = TutorialOverlayLayout.ResolveArrowPositionForBox(
                cutout, halfSize, popup, gap: 20f);

            // Assert
            Assert.AreEqual(1430f, arrowPos.x, 0.01f, "Media caja 450 + gap 20 hacia +x.");
            Assert.AreEqual(400f, arrowPos.y, 0.01f);
        }

        [Test]
        public void ResolveArrowPositionForBox_PopupAbove_SitsOnShortEdgeNotOnCircumscribedCircle()
        {
            // Arrange — misma tira, pero el popup arriba: el lado corto (70) es el que
            // cruza el rayo. Con el círculo circunscripto la flecha se iba a ~473 px.
            var cutout = new Vector2(960f, 400f);
            var halfSize = new Vector2(450f, 70f);
            var popup = new Vector2(960f, 1000f);

            // Act
            var arrowPos = TutorialOverlayLayout.ResolveArrowPositionForBox(
                cutout, halfSize, popup, gap: 20f);

            // Assert
            Assert.AreEqual(960f, arrowPos.x, 0.01f);
            Assert.AreEqual(490f, arrowPos.y, 0.01f, "Media caja 70 + gap 20 hacia +y.");
        }

        [Test]
        public void ResolveArrowPositionForBox_DiagonalPopup_CrossesTheNearerPairOfSides()
        {
            // Arrange — caja ancha y popup en diagonal a 45°: el rayo sale por arriba
            // (lado corto), no por el costado.
            var cutout = new Vector2(500f, 500f);
            var halfSize = new Vector2(400f, 100f);
            var popup = new Vector2(1000f, 1000f);

            // Act
            var arrowPos = TutorialOverlayLayout.ResolveArrowPositionForBox(
                cutout, halfSize, popup, gap: 0f);

            // Assert — a 45° el borde horizontal se cruza en y = +100, o sea x = +100.
            Assert.AreEqual(600f, arrowPos.x, 0.01f);
            Assert.AreEqual(600f, arrowPos.y, 0.01f);
        }

        [Test]
        public void ResolveArrowPositionForBox_PopupOnCenter_FallsBackToUpwards()
        {
            // Arrange — popup exactamente sobre el centro: sin dirección que seguir.
            var cutout = new Vector2(500f, 500f);
            var halfSize = new Vector2(400f, 100f);

            // Act
            var arrowPos = TutorialOverlayLayout.ResolveArrowPositionForBox(
                cutout, halfSize, popupCenter: cutout, gap: 20f);

            // Assert
            Assert.AreEqual(500f, arrowPos.x, 0.01f);
            Assert.AreEqual(620f, arrowPos.y, 0.01f, "Default Vector2.up: media caja 100 + gap 20.");
        }

        [Test]
        public void ResolveArrowPositionForBox_ZeroSizedBox_SitsAtGapFromCenter()
        {
            // Arrange — un rect degenerado (aún sin layout) no debe tirar NaN.
            var cutout = new Vector2(500f, 500f);

            // Act
            var arrowPos = TutorialOverlayLayout.ResolveArrowPositionForBox(
                cutout, Vector2.zero, new Vector2(1000f, 500f), gap: 20f);

            // Assert
            Assert.AreEqual(520f, arrowPos.x, 0.01f);
            Assert.AreEqual(500f, arrowPos.y, 0.01f);
        }

        [Test]
        public void ResolveArrowRotationZ_ArrowRightOfCutout_PointsLeft()
        {
            float z = TutorialOverlayLayout.ResolveArrowRotationZ(
                arrowPos: new Vector2(520f, 400f), cutoutCenter: new Vector2(400f, 400f));

            Assert.AreEqual(180f, Mathf.Abs(z), 0.01f, "Flecha a la derecha del recorte apunta a la izquierda (±180°).");
        }

        [Test]
        public void ResolveArrowRotationZ_ArrowBelowCutout_PointsUp()
        {
            float z = TutorialOverlayLayout.ResolveArrowRotationZ(
                arrowPos: new Vector2(400f, 250f), cutoutCenter: new Vector2(400f, 400f));

            Assert.AreEqual(90f, z, 0.01f);
        }
    }
}
