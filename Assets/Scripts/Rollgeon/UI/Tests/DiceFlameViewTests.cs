using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.Combos.Tests;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica la máquina de frames de <see cref="DiceFlameView"/>: Born una vez → loop Tier 1,
    /// Tier 1 (o el fin de Born) → transición → loop Tier 2, bajada directa a Tier 1, apagado, y
    /// el tamaño por texel entero. EditMode no corre Update: se avanza con <c>Tick</c>.
    /// </summary>
    [TestFixture]
    public class DiceFlameViewTests
    {
        private const float Fps = 10f;
        private const float FrameStep = 1f / Fps + 0.001f;
        private const float ParentWidth = 100f;

        private GameObject _root;
        private GameObject _go;
        private Image _image;
        private DiceFlameView _view;
        private Sprite[] _born, _tier1, _transition, _tier2;
        private readonly List<Object> _created = new();

        [SetUp]
        public void Setup()
        {
            _root = new GameObject("Slot", typeof(RectTransform));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(ParentWidth, ParentWidth);

            _go = new GameObject("ComboFlame", typeof(RectTransform));
            _go.transform.SetParent(_root.transform, false);
            _image = _go.AddComponent<Image>();
            _image.enabled = false;
            _view = _go.AddComponent<DiceFlameView>();

            // Mismas cantidades que la hoja real: 4 / 10 / 2 / 10. El frame más ancho (4 px) es
            // la referencia; Born usa 2x3 para probar que las fases chicas escalan proporcional.
            _born = Frames(4, width: 2, height: 3);
            _tier1 = Frames(10, width: 4, height: 4);
            _transition = Frames(2, width: 3, height: 4);
            _tier2 = Frames(10, width: 4, height: 4);
            Wire(_born, _tier1, _transition, _tier2);
            ComboTestUtils.SetField(_view, "_fps", Fps);
        }

        [TearDown]
        public void Teardown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            foreach (var obj in _created)
                if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        [Test]
        public void SetTier_LowFromOff_StartsBornAtFrameZero()
        {
            // Act
            _view.SetTier(ComboFlameTier.Low);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Born, _view.CurrentPhase);
            Assert.AreEqual(0, _view.FrameIndex);
            Assert.IsTrue(_image.enabled);
            Assert.AreSame(_born[0], _image.sprite);
        }

        [Test]
        public void Tick_BornCompletesWithLowTarget_EntersTier1AndLoops()
        {
            // Arrange
            _view.SetTier(ComboFlameTier.Low);

            // Act — 3 pasos recorren Born 1..3; el 4º termina Born.
            TickFrames(4);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Tier1, _view.CurrentPhase);
            Assert.AreEqual(0, _view.FrameIndex);
            Assert.AreSame(_tier1[0], _image.sprite);

            // Act — una vuelta entera del loop de 10 vuelve al frame 0.
            TickFrames(10);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Tier1, _view.CurrentPhase);
            Assert.AreEqual(0, _view.FrameIndex);
        }

        [Test]
        public void Tick_BornCompletesWithHighTarget_SkipsTier1IntoTransition()
        {
            // Arrange
            _view.SetTier(ComboFlameTier.High);

            // Act
            TickFrames(4);

            // Assert — sin flash de un frame de Tier 1 en el medio.
            Assert.AreEqual(DiceFlameView.Phase.Transition, _view.CurrentPhase);
            Assert.AreEqual(0, _view.FrameIndex);
            Assert.AreSame(_transition[0], _image.sprite);
        }

        [Test]
        public void Tick_TransitionCompletes_EntersTier2AndLoops()
        {
            // Arrange
            _view.SetTier(ComboFlameTier.High);
            TickFrames(4);

            // Act — la transición tiene 2 frames: un paso al frame 1, otro la termina.
            TickFrames(2);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Tier2, _view.CurrentPhase);
            Assert.AreEqual(0, _view.FrameIndex);
            Assert.AreSame(_tier2[0], _image.sprite);

            // Act
            TickFrames(10);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Tier2, _view.CurrentPhase);
            Assert.AreEqual(0, _view.FrameIndex);
        }

        [Test]
        public void SetTier_HighWhileLoopingTier1_EntersTransitionImmediately()
        {
            // Arrange
            _view.SetTier(ComboFlameTier.Low);
            TickFrames(4 + 3);
            Assert.AreEqual(DiceFlameView.Phase.Tier1, _view.CurrentPhase);

            // Act
            _view.SetTier(ComboFlameTier.High);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Transition, _view.CurrentPhase);
            Assert.AreEqual(0, _view.FrameIndex);
        }

        [Test]
        public void SetTier_LowWhileTier2_CutsStraightToTier1()
        {
            // Arrange
            _view.SetTier(ComboFlameTier.High);
            TickFrames(4 + 2 + 5);
            Assert.AreEqual(DiceFlameView.Phase.Tier2, _view.CurrentPhase);

            // Act
            _view.SetTier(ComboFlameTier.Low);

            // Assert — no hay animación inversa autorada.
            Assert.AreEqual(DiceFlameView.Phase.Tier1, _view.CurrentPhase);
            Assert.AreEqual(0, _view.FrameIndex);
            Assert.AreSame(_tier1[0], _image.sprite);
        }

        [Test]
        public void SetTier_Off_DisablesImageAndStopsTicking()
        {
            // Arrange
            _view.SetTier(ComboFlameTier.Low);
            TickFrames(2);

            // Act
            _view.SetTier(ComboFlameTier.Off);
            TickFrames(3);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Off, _view.CurrentPhase);
            Assert.IsFalse(_image.enabled);
        }

        [Test]
        public void SetTier_SameTierDuringBorn_DoesNotRestartBorn()
        {
            // Arrange — el payload de combo llega en cada toggle de hold.
            _view.SetTier(ComboFlameTier.Low);
            TickFrames(2);

            // Act
            _view.SetTier(ComboFlameTier.Low);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Born, _view.CurrentPhase);
            Assert.AreEqual(2, _view.FrameIndex);
        }

        [Test]
        public void SetTier_HighDuringBorn_FinishesBornThenTransitions()
        {
            // Arrange
            _view.SetTier(ComboFlameTier.Low);
            TickFrames(2);

            // Act — subir de tier a mitad de Born no la corta.
            _view.SetTier(ComboFlameTier.High);
            Assert.AreEqual(DiceFlameView.Phase.Born, _view.CurrentPhase);
            Assert.AreEqual(2, _view.FrameIndex);
            TickFrames(2);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Transition, _view.CurrentPhase);
        }

        [Test]
        public void SetTier_WithoutBornFrames_StartsInTier1()
        {
            // Arrange
            Wire(System.Array.Empty<Sprite>(), _tier1, _transition, _tier2);

            // Act
            _view.SetTier(ComboFlameTier.Low);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Tier1, _view.CurrentPhase);
            Assert.AreSame(_tier1[0], _image.sprite);
        }

        [Test]
        public void SetTier_HighWithoutTransitionFrames_GoesStraightToTier2AfterBorn()
        {
            // Arrange
            Wire(_born, _tier1, System.Array.Empty<Sprite>(), _tier2);
            _view.SetTier(ComboFlameTier.High);

            // Act
            TickFrames(4);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Tier2, _view.CurrentPhase);
        }

        [Test]
        public void Tick_LargeDelta_AdvancesSeveralFramesAtOnce()
        {
            // Arrange
            _view.SetTier(ComboFlameTier.Low);

            // Act — 0.35 s a 10 fps son 3 frames enteros; el resto queda acumulado.
            _view.Tick(0.35f);

            // Assert
            Assert.AreEqual(DiceFlameView.Phase.Born, _view.CurrentPhase);
            Assert.AreEqual(3, _view.FrameIndex);
        }

        [Test]
        public void Show_SizesFrameByIntegerTexelOfWidestSprite()
        {
            // Arrange — slot de 100 px, frame más ancho 4 px → texel 25. Born es 2x3.
            var rect = (RectTransform)_go.transform;

            // Act
            _view.SetTier(ComboFlameTier.Low);

            // Assert
            Assert.AreEqual(new Vector2(50f, 75f), rect.sizeDelta);

            // Act — Tier 1 (4x4) llena el ancho del dado.
            TickFrames(4);

            // Assert
            Assert.AreEqual(new Vector2(100f, 100f), rect.sizeDelta);
        }

        // ---- Helpers -----------------------------------------------------------

        private void TickFrames(int frames)
        {
            for (int i = 0; i < frames; i++) _view.Tick(FrameStep);
        }

        private void Wire(Sprite[] born, Sprite[] tier1, Sprite[] transition, Sprite[] tier2)
        {
            ComboTestUtils.SetField(_view, "_born", born);
            ComboTestUtils.SetField(_view, "_tier1", tier1);
            ComboTestUtils.SetField(_view, "_transition", transition);
            ComboTestUtils.SetField(_view, "_tier2", tier2);
        }

        private Sprite[] Frames(int count, int width, int height)
        {
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                var tex = new Texture2D(4, 4);
                var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
                sprite.name = $"frame_{i}";
                _created.Add(tex);
                _created.Add(sprite);
                frames[i] = sprite;
            }
            return frames;
        }
    }
}
