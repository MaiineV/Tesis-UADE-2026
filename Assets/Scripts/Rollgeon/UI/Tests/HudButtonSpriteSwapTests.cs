using System.Collections.Generic;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Cubre <see cref="HudButtonSpriteSwap"/>: sprite por estado (normal/hover/disabled)
    /// con el <see cref="ButtonSpriteSet"/> intercambiable en runtime.
    /// </summary>
    [TestFixture]
    public class HudButtonSpriteSwapTests
    {
        private readonly List<Object> _cleanup = new List<Object>();

        private GameObject _go;
        private HudButtonSpriteSwap _swap;
        private Button _button;
        private Image _image;
        private Sprite _normal;
        private Sprite _hover;
        private Sprite _disabled;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("SwapButton");
            _image = _go.AddComponent<Image>();
            _button = _go.AddComponent<Button>();
            _swap = _go.AddComponent<HudButtonSpriteSwap>();

            _normal = MakeSprite();
            _hover = MakeSprite();
            _disabled = MakeSprite();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            foreach (var o in _cleanup)
                if (o != null) Object.DestroyImmediate(o);
            _cleanup.Clear();
        }

        [Test]
        public void test_sprite_swap_apply_paints_the_normal_sprite()
        {
            // Arrange
            var set = new ButtonSpriteSet(_normal, _hover);

            // Act
            _swap.Apply(set);

            // Assert
            Assert.AreSame(_normal, _image.sprite);
        }

        [Test]
        public void test_sprite_swap_pointer_enter_paints_hover_and_exit_restores_normal()
        {
            // Arrange
            _swap.Apply(new ButtonSpriteSet(_normal, _hover));

            // Act + Assert
            _swap.OnPointerEnter(null);
            Assert.AreSame(_hover, _image.sprite, "Hover al entrar el puntero.");

            _swap.OnPointerExit(null);
            Assert.AreSame(_normal, _image.sprite, "Normal al salir el puntero.");
        }

        [Test]
        public void test_sprite_swap_changing_set_while_hovered_keeps_the_hover_state()
        {
            // Arrange — el contexto puede cambiar con el puntero encima
            // (ej. el último free roll se gasta y el botón pasa a pago).
            var otherNormal = MakeSprite();
            var otherHover = MakeSprite();
            _swap.Apply(new ButtonSpriteSet(_normal, _hover));
            _swap.OnPointerEnter(null);

            // Act
            _swap.Apply(new ButtonSpriteSet(otherNormal, otherHover));

            // Assert
            Assert.AreSame(otherHover, _image.sprite);
        }

        [Test]
        public void test_sprite_swap_disabled_button_uses_the_disabled_sprite_when_present()
        {
            // Arrange
            _swap.Apply(new ButtonSpriteSet(_normal, _hover, _disabled));

            // Act
            _button.interactable = false;
            _swap.Repaint();

            // Assert
            Assert.AreSame(_disabled, _image.sprite);
        }

        [Test]
        public void test_sprite_swap_disabled_button_without_disabled_art_falls_back_to_normal()
        {
            // Arrange — sin arte de disabled el feedback lo pone el tint del Button;
            // el hover no debe aplicar sobre un botón deshabilitado.
            _swap.Apply(new ButtonSpriteSet(_normal, _hover));
            _swap.OnPointerEnter(null);

            // Act
            _button.interactable = false;
            _swap.Repaint();

            // Assert
            Assert.AreSame(_normal, _image.sprite);
        }

        [Test]
        public void test_sprite_swap_ignores_sets_without_a_normal_sprite()
        {
            // Arrange
            _swap.Apply(new ButtonSpriteSet(_normal, _hover));

            // Act — un set vacío (prefab sin wirear) no debe pisar el arte vigente.
            _swap.Apply(default);

            // Assert
            Assert.AreSame(_normal, _image.sprite);
        }

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(2, 2);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            _cleanup.Add(tex);
            _cleanup.Add(sprite);
            return sprite;
        }
    }
}
