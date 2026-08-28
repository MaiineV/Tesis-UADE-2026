using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD.Status;
using Rollgeon.UI.Tooltips;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Qué muestra y qué apaga una tarjeta de la columna del tooltip.
    /// </summary>
    [TestFixture]
    public class TooltipCardViewTests
    {
        private GameObject _go;
        private TooltipCardView _view;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _rule;
        private GameObject _iconRoot;
        private Image _icon;
        private GameObject _badge;
        private TextMeshProUGUI _badgeLabel;
        private GameObject _divider;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TooltipCard", typeof(RectTransform));

            _iconRoot = Child("Icon");
            _icon = _iconRoot.AddComponent<Image>();
            _badge = Child("Badge", _iconRoot.transform);
            _badgeLabel = Label("Value", _badge.transform);
            _title = Label("Title");
            _rule = Label("Rule");
            _divider = Child("Divider");

            _sprite = MakeSprite();

            _view = _go.AddComponent<TooltipCardView>();
            SetPrivate("_titleLabel", _title);
            SetPrivate("_ruleLabel", _rule);
            SetPrivate("_iconRoot", _iconRoot);
            SetPrivate("_icon", _icon);
            SetPrivate("_badge", _badge);
            SetPrivate("_badgeLabel", _badgeLabel);
            SetPrivate("_divider", _divider);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_sprite != null)
            {
                if (_sprite.texture != null) Object.DestroyImmediate(_sprite.texture);
                Object.DestroyImmediate(_sprite);
            }
        }

        [Test]
        public void SinArte_ElBloqueDelIconoSeVaEntero()
        {
            _view.Show(new StatusIconState("intent.bomb_field", "Siembra bombas",
                                           "Siembra 3 bombas.", null, active: true));

            Assert.IsFalse(_iconRoot.activeSelf,
                "El bloque del ícono quedó prendido sin sprite. En una fila horizontal eso " +
                "reserva su ancho igual, y el título arranca corrido contra un hueco que el " +
                "jugador lee como un ícono que no cargó.");
        }

        [Test]
        public void ConArte_ElIconoSeDibujaYElTituloVaALaIzquierda()
        {
            _view.Show(new StatusIconState("intent.ignite", "Prende el suelo",
                                           "Prende la banda que marcó.", _sprite, active: true));

            Assert.IsTrue(_iconRoot.activeSelf);
            Assert.AreSame(_sprite, _icon.sprite);
            Assert.AreEqual(TextAlignmentOptions.Left, _title.alignment);
        }

        [Test]
        public void UnaTarjetaDeTerreno_LlevaSuIconoIgual()
        {
            _view.Show(new StatusIconState("status.burn", "Fuego de Bomba", "15 al entrar.",
                                           _sprite, active: true,
                                           style: StatusCardStyle.Terrain));

            Assert.IsTrue(_iconRoot.activeSelf,
                "Terrain dice de QUÉ habla la tarjeta, no cómo se dibuja: lo que le cobra ser " +
                "del suelo es que la fila sobre la cabeza la saltee, no perder su arte.");
            Assert.AreEqual(TextAlignmentOptions.Left, _title.alignment,
                "Con ícono el título se alinea a él; centrado dejaría un hueco entre los dos.");
        }

        [Test]
        public void UnaTarjetaSinArte_CentraElTitulo()
        {
            _view.Show(new StatusIconState("intent.bomb_field", "Siembra bombas",
                                           "Siembra 3 bombas.", icon: null, active: true));

            Assert.IsFalse(_iconRoot.activeSelf);
            Assert.AreEqual(TextAlignmentOptions.Center, _title.alignment,
                "Sin ícono, un título a la izquierda arranca contra el borde y no contra nada.");
        }

        [Test]
        public void SinRegla_ElDivisorTambienSeApaga()
        {
            _view.Show(new StatusIconState("intent.bomb_blast", "Estalla", null,
                                           _sprite, active: true));

            Assert.IsFalse(_rule.gameObject.activeSelf);
            Assert.IsFalse(_divider.activeSelf,
                "Quedó una línea partiendo la tarjeta en dos sin nada debajo.");
        }

        [Test]
        public void ElBadgeLlevaLosTurnos_YNoLaFrase()
        {
            _view.Show(new StatusIconState("intent.bomb_blast", "Estalla", "Estalla en cruz.",
                                           _sprite, active: true, remainingTurns: 2));

            Assert.IsTrue(_badge.activeSelf);
            StringAssert.Contains("2", _badgeLabel.text);
            StringAssert.DoesNotContain("2", _rule.text,
                "La mecha se escribió adentro de la regla. Va en el badge para que una bomba " +
                "que mañana dure otra cosa cambie el número y no la frase.");
        }

        private GameObject Child(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent != null ? parent : _go.transform, worldPositionStays: false);
            return go;
        }

        private TextMeshProUGUI Label(string name, Transform parent = null)
            => Child(name, parent).AddComponent<TextMeshProUGUI>();

        private static Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 4);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        private void SetPrivate(string field, object value)
            => typeof(TooltipCardView)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_view, value);
    }
}
