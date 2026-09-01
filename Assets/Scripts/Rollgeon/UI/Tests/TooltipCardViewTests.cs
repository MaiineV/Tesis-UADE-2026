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
        private GameObject _labelRow;
        private GameObject _headerRow;
        private TextMeshProUGUI _eyebrow;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TooltipCard", typeof(RectTransform));

            _labelRow = Child("LabelRow");
            _iconRoot = Child("Icon", _labelRow.transform);
            _icon = _iconRoot.AddComponent<Image>();
            _badge = Child("Badge", _iconRoot.transform);
            _badgeLabel = Label("Value", _badge.transform);
            _eyebrow = Label("Eyebrow", _labelRow.transform);
            _divider = Child("Divider");
            _headerRow = Child("Header");
            _title = Label("Title", _headerRow.transform);
            _rule = Label("Rule");

            _sprite = MakeSprite();

            _view = _go.AddComponent<TooltipCardView>();
            SetPrivate("_titleLabel", _title);
            SetPrivate("_ruleLabel", _rule);
            SetPrivate("_iconRoot", _iconRoot);
            SetPrivate("_icon", _icon);
            SetPrivate("_badge", _badge);
            SetPrivate("_badgeLabel", _badgeLabel);
            SetPrivate("_divider", _divider);
            SetPrivate("_labelRow", _labelRow);
            SetPrivate("_headerRow", _headerRow);
            SetPrivate("_eyebrowLabel", _eyebrow);
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
        public void ConArte_ElIconoSeDibuja()
        {
            // La alineación del título ya no se toca acá: es del prefab (siempre izquierda),
            // así que lo único que Show decide con el arte es si el bloque del ícono vive.
            _view.Show(new StatusIconState("intent.ignite", "Bola de fuego",
                                           "Prende la banda que marcó.", _sprite, active: true));

            Assert.IsTrue(_iconRoot.activeSelf);
            Assert.AreSame(_sprite, _icon.sprite);
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
        }

        [Test]
        public void UnaTarjetaSinArte_ApagaElBloqueDelIcono()
        {
            _view.Show(new StatusIconState("intent.bomb_field", "Bombas",
                                           "Siembra 3 bombas al azar.", icon: null, active: true));

            Assert.IsFalse(_iconRoot.activeSelf,
                "Sin arte el bloque del ícono se va entero: prendido reservaría su ancho y el " +
                "título arrancaría corrido contra un hueco.");
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
        public void ConLabelYContenido_ElDivisorSubrayaElLabel()
        {
            // Arrange — el bloque de próximo turno del mockup: NEXT TURN / línea / título + daño.
            var state = new StatusIconState("intent.ranged_shot", "Card Throw", null,
                                            null, active: true, damage: 18,
                                            eyebrow: "Next turn");

            // Act
            _view.Show(state);

            // Assert
            Assert.IsTrue(_divider.activeSelf,
                "El divisor no subrayó el label del bloque: NEXT TURN quedó pegado al título.");
            Assert.IsTrue(_labelRow.activeSelf);
            Assert.IsTrue(_headerRow.activeSelf);
        }

        [Test]
        public void SinLabel_ElDivisorNoParteElContenido()
        {
            // Arrange — una tarjeta de un solo bloque: título y regla, sin eyebrow. La línea
            // dejó de vivir entre título y regla — subraya labels, no parte contenido.
            var state = new StatusIconState("status.stun", "Aturdido", "Este turno no ataca.",
                                            null, active: true);

            // Act
            _view.Show(state);

            // Assert
            Assert.IsFalse(_divider.activeSelf,
                "El divisor partió una tarjeta sin label: la línea es del nombre del bloque.");
            Assert.IsFalse(_labelRow.activeSelf,
                "La fila del label quedó prendida sin ícono ni eyebrow: un renglón de aire.");
        }

        [Test]
        public void LaMaldicion_EsLabelMasRegla_SinFilaDeTitulo()
        {
            // Arrange — el bloque PLAYER CURSE del mockup: ícono + label, línea, y la regla.
            // Sin título: el nombre del curse no tiene renglón propio.
            var state = new StatusIconState("status.dice_block", null, "Te traba un dado.",
                                            _sprite, active: true, eyebrow: "Player Curse");

            // Act
            _view.Show(state);

            // Assert
            Assert.IsTrue(_labelRow.activeSelf);
            Assert.IsTrue(_iconRoot.activeSelf, "El ícono del curse va en la fila del label.");
            Assert.IsTrue(_divider.activeSelf);
            Assert.IsFalse(_headerRow.activeSelf,
                "La fila del título quedó viva vacía: entre el label y la regla queda un " +
                "renglón de aire.");
            Assert.IsFalse(_title.gameObject.activeSelf);
            Assert.IsTrue(_rule.gameObject.activeSelf);
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
