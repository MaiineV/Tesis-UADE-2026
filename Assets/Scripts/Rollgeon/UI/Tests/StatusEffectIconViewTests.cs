using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD.Status;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// El marco lo pone el sistema (activo/inactivo) y el ícono lo trae el estado.
    /// </summary>
    [TestFixture]
    public class StatusEffectIconViewTests
    {
        private GameObject _go;
        private StatusEffectIconView _view;
        private Image _background;
        private Image _icon;
        private Sprite _activeFrame;
        private Sprite _inactiveFrame;
        private Sprite _stateIcon;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("StatusEffectIcon", typeof(RectTransform));

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer));
            bgGo.transform.SetParent(_go.transform, worldPositionStays: false);
            _background = bgGo.AddComponent<Image>();

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer));
            iconGo.transform.SetParent(_go.transform, worldPositionStays: false);
            _icon = iconGo.AddComponent<Image>();

            _activeFrame = MakeSprite("activeFrame");
            _inactiveFrame = MakeSprite("inactiveFrame");
            _stateIcon = MakeSprite("stateIcon");

            _view = _go.AddComponent<StatusEffectIconView>();
            SetPrivate(_view, "_background", _background);
            SetPrivate(_view, "_icon", _icon);
            SetPrivate(_view, "_activeBackground", _activeFrame);
            SetPrivate(_view, "_inactiveBackground", _inactiveFrame);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            foreach (var s in new[] { _activeFrame, _inactiveFrame, _stateIcon })
            {
                if (s == null) continue;
                if (s.texture != null) Object.DestroyImmediate(s.texture);
                Object.DestroyImmediate(s);
            }
        }

        private static Sprite MakeSprite(string name)
        {
            var tex = new Texture2D(4, 4);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            return sprite;
        }

        [Test]
        public void should_use_the_active_frame_when_the_state_is_active()
        {
            // Arrange + Act
            _view.Show(new StatusIconState("passive.test", "Test", "Descripción", _stateIcon, active: true));

            // Assert
            Assert.AreSame(_activeFrame, _background.sprite);
            Assert.AreSame(_stateIcon, _icon.sprite);
        }

        [Test]
        public void should_use_the_inactive_frame_when_the_state_is_latent()
        {
            // Arrange + Act
            _view.Show(new StatusIconState("passive.test", "Test", "Descripción", _stateIcon, active: false));

            // Assert
            Assert.AreSame(_inactiveFrame, _background.sprite);
        }

        [Test]
        public void should_swap_the_frame_back_and_forth()
        {
            // Arrange — el mismo slot se reusa entre refreshes, así que el swap tiene que
            // ir en los dos sentidos y no solo "prender".
            _view.Show(new StatusIconState("passive.test", "Test", "Descripción", _stateIcon, active: true));

            // Act
            _view.Show(new StatusIconState("passive.test", "Test", "Descripción", _stateIcon, active: false));

            // Assert
            Assert.AreSame(_inactiveFrame, _background.sprite);
        }

        [Test]
        public void should_remember_the_state_id_it_is_showing()
        {
            // Arrange + Act
            _view.Show(new StatusIconState("passive.warrior.low_hp_rage", "Furia", "Descripción", _stateIcon, active: true));

            // Assert
            Assert.AreEqual("passive.warrior.low_hp_rage", _view.StateId);
        }

        [Test]
        public void should_hide_the_icon_image_when_the_state_has_no_art()
        {
            // Arrange + Act — sin sprite, un Image habilitado dibuja un cuadrado blanco.
            _view.Show(new StatusIconState("passive.test", "Test", "Descripción", null, active: true));

            // Assert
            Assert.IsFalse(_icon.enabled);
        }

        // ------------------------------------------------------------------
        // Duración bajo el ícono
        // ------------------------------------------------------------------

        [Test]
        public void should_hide_the_duration_label_for_something_without_duration()
        {
            // Arrange — las pasivas no vencen.
            var label = AttachDurationLabel();

            // Act
            _view.Show(new StatusIconState("passive.test", "Test", "Descripción", _stateIcon,
                                           active: true, remainingTurns: null));

            // Assert
            Assert.IsFalse(label.gameObject.activeSelf);
        }

        [Test]
        public void should_show_the_remaining_turns_for_a_timed_effect()
        {
            // Arrange
            var label = AttachDurationLabel();

            // Act
            _view.Show(new StatusIconState("status.burn", "Quemadura", "Descripción", _stateIcon,
                                           active: true, remainingTurns: 3));

            // Assert
            Assert.IsTrue(label.gameObject.activeSelf);
            Assert.AreEqual("3", label.text);
        }

        [Test]
        public void should_hide_the_duration_again_when_the_slot_is_reused_by_a_passive()
        {
            // Arrange — los slots se reciclan entre refreshes: un efecto con timer puede
            // dejarle el lugar a una pasiva y el número no puede quedar colgado.
            var label = AttachDurationLabel();
            _view.Show(new StatusIconState("status.burn", "Quemadura", "Descripción", _stateIcon,
                                           active: true, remainingTurns: 3));

            // Act
            _view.Show(new StatusIconState("passive.test", "Test", "Descripción", _stateIcon,
                                           active: true, remainingTurns: null));

            // Assert
            Assert.IsFalse(label.gameObject.activeSelf);
        }

        private TMPro.TextMeshProUGUI AttachDurationLabel()
        {
            var go = new GameObject("Duration", typeof(RectTransform));
            go.transform.SetParent(_go.transform, worldPositionStays: false);
            var label = go.AddComponent<TMPro.TextMeshProUGUI>();
            SetPrivate(_view, "_durationLabel", label);
            return label;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            info.SetValue(target, value);
        }
    }
}
