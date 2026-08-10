using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Tutorial.UI;
using Rollgeon.UI.Help;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica <see cref="BuildHelpFlow"/>: orden de los pasos, salteo de anchors sin
    /// cablear, renumeración del "(i/n)", y degradación silenciosa sin overlay.
    /// </summary>
    [TestFixture]
    public class BuildHelpFlowTests
    {
        private GameObject _root;
        private FakeOverlay _overlay;

        [SetUp]
        public void Setup()
        {
            _root = new GameObject("HelpRig", typeof(RectTransform));
            _overlay = new FakeOverlay();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void test_buildHelpFlow_start_showsStepsInDeclaredOrder()
        {
            // Arrange
            var a = MakeRect("A");
            var b = MakeRect("B");
            var flow = new BuildHelpFlow(_overlay, Localize);

            // Act — el fake avanza solo, así que una llamada recorre la secuencia entera.
            bool started = flow.Start(new[]
            {
                new BuildHelpFlow.Step(a, "k.a", "A"),
                new BuildHelpFlow.Step(b, "k.b", "B"),
            });

            // Assert
            Assert.IsTrue(started);
            Assert.AreEqual(new[] { a, b }, _overlay.Targets);
        }

        [Test]
        public void test_buildHelpFlow_stepWithNullAnchor_isSkippedAndRenumbered()
        {
            // Arrange — el paso del medio simula un ref opcional sin cablear.
            var a = MakeRect("A");
            var c = MakeRect("C");
            var flow = new BuildHelpFlow(_overlay, Localize);

            // Act
            flow.Start(new[]
            {
                new BuildHelpFlow.Step(a, "k.a", "primero"),
                new BuildHelpFlow.Step(null, "k.b", "segundo"),
                new BuildHelpFlow.Step(c, "k.c", "tercero"),
            });

            // Assert — 2 pasos, y la numeración dice 1/2 y 2/2, no 1/3 y 3/3.
            Assert.AreEqual(2, _overlay.Texts.Count);
            StringAssert.StartsWith("(1/2)", _overlay.Texts[0]);
            StringAssert.StartsWith("(2/2)", _overlay.Texts[1]);
        }

        [Test]
        public void test_buildHelpFlow_extrasWithNulls_areFilteredOut()
        {
            // Arrange — un ref opcional sin cablear no debe viajar como hueco al recorte.
            var anchor = MakeRect("A");
            var real = MakeRect("Extra");

            // Act
            var step = new BuildHelpFlow.Step(anchor, "k.a", "A", null, real, null);

            // Assert
            Assert.AreEqual(new[] { real }, step.Extras);
        }

        [Test]
        public void test_buildHelpFlow_allExtrasNull_leavesExtrasNull()
        {
            // Arrange
            var anchor = MakeRect("A");

            // Act
            var step = new BuildHelpFlow.Step(anchor, "k.a", "A", null, null);

            // Assert
            Assert.IsNull(step.Extras);
        }

        [Test]
        public void test_buildHelpFlow_singleStep_omitsCounter()
        {
            // Arrange
            var a = MakeRect("A");
            var flow = new BuildHelpFlow(_overlay, Localize);

            // Act
            flow.Start(new[] { new BuildHelpFlow.Step(a, "k.a", "solo") });

            // Assert
            Assert.AreEqual(1, _overlay.Texts.Count);
            StringAssert.DoesNotStartWith("(", _overlay.Texts[0]);
        }

        [Test]
        public void test_buildHelpFlow_everyStep_blocksUntilContinue()
        {
            // Arrange — un paso PassThrough colgaría la cadena: sin click-catcher
            // nadie invoca onContinue y la guía nunca avanza ni se cierra.
            var flow = new BuildHelpFlow(_overlay, Localize);

            // Act
            flow.Start(new[]
            {
                new BuildHelpFlow.Step(MakeRect("A"), "k.a", "A"),
                new BuildHelpFlow.Step(MakeRect("B"), "k.b", "B"),
            });

            // Assert
            CollectionAssert.AreEqual(
                new[] { TutorialInputPolicy.BlockUntilContinue, TutorialInputPolicy.BlockUntilContinue },
                _overlay.Policies);
        }

        [Test]
        public void test_buildHelpFlow_lastStepAdvanced_hidesOverlayAndStops()
        {
            // Arrange
            var flow = new BuildHelpFlow(_overlay, Localize);

            // Act
            flow.Start(new[] { new BuildHelpFlow.Step(MakeRect("A"), "k.a", "A") });

            // Assert — sin este Hide el dim queda colgado sobre la pantalla siguiente.
            Assert.AreEqual(1, _overlay.HideCount);
            Assert.IsFalse(flow.IsRunning);
        }

        [Test]
        public void test_buildHelpFlow_secondRun_replaysEveryStep()
        {
            // Arrange — el botón de ayuda tiene que servir cuantas veces se quiera.
            var a = MakeRect("A");
            var flow = new BuildHelpFlow(_overlay, Localize);
            flow.Start(new[] { new BuildHelpFlow.Step(a, "k.a", "A") });

            // Act
            bool restarted = flow.Start(new[] { new BuildHelpFlow.Step(a, "k.a", "A") });

            // Assert
            Assert.IsTrue(restarted);
            Assert.AreEqual(2, _overlay.Texts.Count);
        }

        [Test]
        public void test_buildHelpFlow_withoutOverlayService_doesNotStart()
        {
            // Arrange — abrir la escena del menú sin pasar por 00_Bootstrap.
            var flow = new BuildHelpFlow(null, Localize);

            // Act
            bool started = flow.Start(new[] { new BuildHelpFlow.Step(MakeRect("A"), "k.a", "A") });

            // Assert — false es lo que evita que se marque "ya visto" sin haber mostrado nada.
            Assert.IsFalse(started);
            Assert.IsFalse(flow.IsRunning);
        }

        [Test]
        public void test_buildHelpFlow_allAnchorsNull_doesNotStart()
        {
            // Arrange
            var flow = new BuildHelpFlow(_overlay, Localize);

            // Act
            bool started = flow.Start(new[] { new BuildHelpFlow.Step(null, "k.a", "A") });

            // Assert
            Assert.IsFalse(started);
            Assert.AreEqual(0, _overlay.Texts.Count);
        }

        /// <summary>Devuelve el fallback tal cual: los tests no dependen del locale del editor.</summary>
        private static string Localize(string key, string fallback) => fallback;

        private RectTransform MakeRect(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root.transform, worldPositionStays: false);
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// Overlay de mentira que registra lo que le piden y avanza inmediatamente, para
        /// que un solo <c>Start</c> recorra la secuencia completa de forma determinista.
        /// </summary>
        private sealed class FakeOverlay : ITutorialOverlayService
        {
            public readonly List<RectTransform> Targets = new();
            public readonly List<string> Texts = new();
            public readonly List<TutorialInputPolicy> Policies = new();
            public int HideCount;

            public bool IsVisible { get; private set; }

            public void Show(TutorialStepDisplayRequest request, Action onContinue = null)
            {
                IsVisible = true;
                Targets.Add(request.UiTarget);
                Texts.Add(request.Text);
                Policies.Add(request.InputPolicy);
                onContinue?.Invoke();
            }

            public void Hide()
            {
                IsVisible = false;
                HideCount++;
            }
        }
    }
}
