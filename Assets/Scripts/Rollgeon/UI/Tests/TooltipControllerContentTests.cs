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
    /// Qué prende y qué apaga el panel según lo que le den — banda de identidad, párrafo,
    /// columna y pie.
    /// </summary>
    /// <remarks>
    /// El test que más importa acá es el último: los siete tooltips que ya existen entran por
    /// <c>Show(string, …)</c> y tienen que seguir viéndose exactamente igual. Todo lo nuevo del
    /// panel es opcional justamente para eso.
    /// </remarks>
    [TestFixture]
    public class TooltipControllerContentTests
    {
        private GameObject _go;
        private TooltipController _controller;
        private RectTransform _panel;
        private TextMeshProUGUI _text;
        private TextMeshProUGUI _name;
        private TextMeshProUGUI _type;
        private GameObject _vitals;
        private TextMeshProUGUI _hp;
        private GameObject _shield;
        private TextMeshProUGUI _shieldValue;
        private TextMeshProUGUI _footer;
        private RectTransform _cards;
        private GameObject _cardPrefabGo;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TooltipController", typeof(RectTransform));

            _panel = Child("Panel", _go.transform);
            _name = Label("Name", _panel);
            _type = Label("Type", _panel);
            _vitals = Child("Vitals", _panel).gameObject;
            _hp = Label("Hp", _vitals.transform);
            _shield = Child("Shield", _vitals.transform).gameObject;
            _shieldValue = Label("Value", _shield.transform);
            _text = Label("Text", _panel);
            _cards = Child("Cards", _panel);
            _footer = Label("Footer", _panel);

            _controller = _go.AddComponent<TooltipController>();
            SetPrivate("_root", _panel);
            SetPrivate("_text", _text);
            SetPrivate("_nameLabel", _name);
            SetPrivate("_typeLabel", _type);
            SetPrivate("_vitalsRoot", _vitals);
            SetPrivate("_hpLabel", _hp);
            SetPrivate("_shieldRoot", _shield);
            SetPrivate("_shieldLabel", _shieldValue);
            SetPrivate("_footerLabel", _footer);
            SetPrivate("_cardsContainer", _cards);
            SetPrivate("_cardPrefab", MakeCardPrefab());
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_cardPrefabGo != null) Object.DestroyImmediate(_cardPrefabGo);
        }

        [Test]
        public void ConIdentidad_LaBandaMuestraNombreYVitales()
        {
            _controller.Show(Boss(health: 180, maxHealth: 250), Vector2.zero, 1,
                             TooltipPlacementMode.Fixed);

            Assert.IsTrue(_name.gameObject.activeSelf);
            Assert.AreEqual("The Croupier", _name.text);
            Assert.IsTrue(_vitals.activeSelf);
            StringAssert.Contains("180", _hp.text);
            StringAssert.Contains("250", _hp.text);
        }

        [Test]
        public void SinEscudo_ElParDelEscudoNoSeDibuja()
        {
            _controller.Show(Boss(health: 250, maxHealth: 250), Vector2.zero, 1,
                             TooltipPlacementMode.Fixed);

            Assert.IsFalse(_shield.activeSelf,
                "Dibujó el escudo de una unidad que no usa escudo. Un \"0\" al lado del ícono se " +
                "lee como un escudo que existe y está roto, no como que no hay.");
        }

        [Test]
        public void ConEscudo_ElParSeDibujaConSuNumero()
        {
            _controller.Show(Boss(health: 250, maxHealth: 250, shield: 8), Vector2.zero, 1,
                             TooltipPlacementMode.Fixed);

            Assert.IsTrue(_shield.activeSelf);
            Assert.AreEqual("8", _shieldValue.text);
        }

        [Test]
        public void ElFlavorVaAlPie_YNoAlEncabezado()
        {
            _controller.Show(Boss(health: 250, maxHealth: 250), Vector2.zero, 1,
                             TooltipPlacementMode.Fixed);

            Assert.IsTrue(_footer.gameObject.activeSelf);
            StringAssert.Contains("Siembra bombas por el paño", _footer.text);
            Assert.IsFalse(_text.gameObject.activeSelf,
                "El párrafo quedó prendido y vacío arriba de la columna: un renglón alto de nada " +
                "entre el nombre y las tarjetas.");
        }

        [Test]
        public void ConFamilia_LaFilaDeTipoSeDibuja()
        {
            _controller.Show(
                new TooltipContent(name: "El Croupier", type: "Jefe · Rango",
                                   health: 168, maxHealth: 250),
                Vector2.zero, 1, TooltipPlacementMode.Fixed);

            Assert.IsTrue(_type.gameObject.activeSelf);
            Assert.AreEqual("Jefe · Rango", _type.text);
        }

        [Test]
        public void SinFamilia_LaFilaDeTipoNoSeDibuja()
        {
            // Es el caso de los enemigos que nadie autoró: la fila se va entera en vez de
            // quedar prendida y vacía debajo del nombre.
            _controller.Show(
                new TooltipContent(name: "CardEnemy", health: 20, maxHealth: 20),
                Vector2.zero, 1, TooltipPlacementMode.Fixed);

            Assert.IsFalse(_type.gameObject.activeSelf);
        }

        [Test]
        public void LaColumnaSePrendeConLasTarjetasQueLeDan()
        {
            var cards = new[]
            {
                new StatusIconState("intent.bomb_field", "Siembra bombas", "Siembra 3 bombas.",
                                    null, active: true),
                new StatusIconState("intent.ranged_shot", "Te dispara", "Te dispara por 24.",
                                    null, active: true),
            };

            _controller.Show(new TooltipContent(name: "The Croupier", cards: cards),
                             Vector2.zero, 1, TooltipPlacementMode.Fixed);

            Assert.IsTrue(_cards.gameObject.activeSelf);
            int shown = 0;
            foreach (Transform child in _cards) if (child.gameObject.activeSelf) shown++;
            Assert.AreEqual(2, shown);
        }

        [Test]
        public void UnTooltipDeTextoSigueSiendoElDeSiempre()
        {
            _controller.Show("<b>Puerta</b>\nLleva a la próxima sala.", Vector2.zero, 1,
                             TooltipPlacementMode.Fixed);

            Assert.IsTrue(_text.gameObject.activeSelf,
                "El párrafo de un tooltip de texto se apagó: puerta, casilla, acción, cofre e " +
                "ítem se quedaron sin contenido.");
            StringAssert.Contains("Puerta", _text.text);
            Assert.IsFalse(_name.gameObject.activeSelf,
                "El párrafo se escribió también en el renglón grande de la banda de identidad.");
            Assert.IsFalse(_vitals.activeSelf);
            Assert.IsFalse(_footer.gameObject.activeSelf);
            Assert.IsFalse(_cards.gameObject.activeSelf);
        }

        private static TooltipContent Boss(int health, int maxHealth, int shield = 0)
            => new TooltipContent(
                name: "The Croupier",
                flavor: "Siembra bombas por el paño y prende el suelo delante suyo.",
                health: health, maxHealth: maxHealth, shield: shield);

        private TooltipCardView MakeCardPrefab()
        {
            _cardPrefabGo = new GameObject("TooltipCard", typeof(RectTransform));
            var iconRoot = Child("Icon", _cardPrefabGo.transform);
            iconRoot.gameObject.AddComponent<Image>();
            var badge = Child("Badge", iconRoot);
            var badgeLabel = Label("Value", badge);
            var title = Label("Title", (RectTransform)_cardPrefabGo.transform);
            var rule = Label("Rule", (RectTransform)_cardPrefabGo.transform);
            var divider = Child("Divider", _cardPrefabGo.transform);

            var view = _cardPrefabGo.AddComponent<TooltipCardView>();
            SetPrivate(view, "_titleLabel", title);
            SetPrivate(view, "_ruleLabel", rule);
            SetPrivate(view, "_iconRoot", iconRoot.gameObject);
            SetPrivate(view, "_icon", iconRoot.GetComponent<Image>());
            SetPrivate(view, "_badge", badge.gameObject);
            SetPrivate(view, "_badgeLabel", badgeLabel);
            SetPrivate(view, "_divider", divider.gameObject);
            return view;
        }

        private static RectTransform Child(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI Label(string name, Transform parent)
            => Child(name, parent).gameObject.AddComponent<TextMeshProUGUI>();

        private void SetPrivate(string field, object value)
            => SetPrivate(_controller, field, value);

        private static void SetPrivate(Object target, string field, object value)
            => target.GetType()
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
    }
}
