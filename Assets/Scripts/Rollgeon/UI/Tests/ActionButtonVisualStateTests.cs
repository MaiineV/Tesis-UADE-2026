using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.HUD;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Verifica el visual de <see cref="ActionButton"/> en el estado
    /// <see cref="ActionButtonState.Unaffordable"/>: el jugador tiene que ver POR QUÉ no
    /// puede usar la acción, y ese rojo tiene que irse cuando vuelve a poder.
    /// </summary>
    [TestFixture]
    public class ActionButtonVisualStateTests
    {
        private GameObject _go;
        private ActionButton _button;
        private TextMeshProUGUI _costLabel;
        private Outline _outline;
        private Color _authoredCostColor;

        [SetUp]
        public void Setup()
        {
            // Orden importante: el Outline que agrega ActionButton en su Awake necesita
            // un Graphic, así que la Image va antes que el Button.
            _go = new GameObject("Chip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var uiButton = _go.AddComponent<Button>();
            uiButton.targetGraphic = _go.GetComponent<Image>();

            var labelGo = new GameObject("Cost", typeof(RectTransform));
            labelGo.transform.SetParent(_go.transform, worldPositionStays: false);
            _costLabel = labelGo.AddComponent<TextMeshProUGUI>();
            _authoredCostColor = Color.white;
            _costLabel.color = _authoredCostColor;

            _button = _go.AddComponent<ActionButton>();
            AssignPrivate(_button, "_button", uiButton);
            AssignPrivate(_button, "_costLabel", _costLabel);

            // El AddComponent ya corrió Awake sin ver el label; re-disparamos para que
            // capture el color de autoría con el wiring completo.
            InvokePrivate(_button, "Awake");

            _outline = _go.GetComponent<Outline>();
        }

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        [Test]
        public void test_actionButton_externalScaleMultiplier_composesWithSelectedScale()
        {
            // Arrange
            float selectedScale = (float)GetPrivate(_button, "_selectedScale");
            _button.SetState(ActionButtonState.Selected);

            // Act
            _button.SetExternalScaleMultiplier(1.1f);

            // Assert
            Assert.AreEqual(selectedScale * 1.1f, _button.transform.localScale.x, 1e-4f,
                "el multiplicador externo se compone con la escala del estado Selected");
        }

        [Test]
        public void test_actionButton_externalScaleMultiplier_resetRestoresStateScale()
        {
            // Arrange
            float selectedScale = (float)GetPrivate(_button, "_selectedScale");
            _button.SetState(ActionButtonState.Selected);
            _button.SetExternalScaleMultiplier(1.1f);

            // Act
            _button.SetExternalScaleMultiplier(1f);

            // Assert
            Assert.AreEqual(selectedScale, _button.transform.localScale.x, 1e-4f,
                "resetear el multiplicador a 1 debe dejar la escala del estado intacta");
        }

        [Test]
        public void test_actionButton_stateChange_preservesExternalMultiplier()
        {
            // Arrange
            _button.SetExternalScaleMultiplier(1.1f);

            // Act — el state machine reescribe la escala en cada cambio de estado.
            _button.SetState(ActionButtonState.Available);

            // Assert
            Assert.AreEqual(1.1f, _button.transform.localScale.x, 1e-4f,
                "el cambio de estado no debe pisar el multiplicador externo (breath en curso)");
        }

        [Test]
        public void test_actionButton_unaffordable_paintsCostAndOutlineRed()
        {
            // Arrange
            var expected = (Color)GetPrivate(_button, "_unaffordableColor");

            // Act
            _button.SetState(ActionButtonState.Unaffordable);

            // Assert
            Assert.AreEqual(expected, _costLabel.color, "el costo tiene que quedar rojo");
            Assert.IsTrue(_outline.enabled, "el outline tiene que encenderse");
            Assert.AreEqual(expected, _outline.effectColor, "el outline tiene que quedar rojo");
        }

        [Test]
        public void test_actionButton_unaffordable_staysNonInteractable()
        {
            // Act
            _button.SetState(ActionButtonState.Unaffordable);

            // Assert — funcionalmente es un Locked: no arranca drag ni responde al hotkey.
            Assert.IsFalse(_button.Button.interactable);
        }

        [Test]
        public void test_actionButton_leavingUnaffordable_restoresAuthoredCostColor()
        {
            // Arrange
            _button.SetState(ActionButtonState.Unaffordable);

            // Act
            _button.SetState(ActionButtonState.Available);

            // Assert — sin esto el número quedaba rojo para siempre.
            Assert.AreEqual(_authoredCostColor, _costLabel.color);
            Assert.IsFalse(_outline.enabled);
        }

        [Test]
        public void test_actionButton_pointerDownWhileUnaffordable_raisesRejected()
        {
            // Arrange
            int rejections = 0;
            _button.OnRejected += () => rejections++;
            _button.SetState(ActionButtonState.Unaffordable);

            // Act
            _button.OnPointerDown(null);

            // Assert
            Assert.AreEqual(1, rejections);
        }

        [Test]
        public void test_actionButton_pointerDownWhileAvailable_doesNotRaiseRejected()
        {
            // Arrange
            int rejections = 0;
            _button.OnRejected += () => rejections++;
            _button.SetState(ActionButtonState.Available);

            // Act
            _button.OnPointerDown(null);

            // Assert
            Assert.AreEqual(0, rejections);
        }

        // ------------------------------------------------------------------
        // Affordability — ortogonal al estado
        // ------------------------------------------------------------------
        //
        // Regresión: el rojo y el shake colgaban del estado Unaffordable, que es
        // EXCLUYENTE con Locked y Used. Un chip bloqueado por otra razón (Heal a vida
        // llena) y encima impagable no mostraba nada y no contestaba al click.

        [Test]
        public void test_actionButton_lockedAndUnaffordable_stillPaintsCostRed()
        {
            // Arrange
            var expected = (Color)GetPrivate(_button, "_unaffordableColor");
            _button.SetState(ActionButtonState.Locked);

            // Act
            _button.SetAffordable(false);

            // Assert
            Assert.AreEqual(expected, _costLabel.color, "el costo tiene que quedar rojo aunque esté Locked");
            Assert.IsFalse(_outline.enabled, "el outline sigue siendo del estado, no de la plata");
        }

        [Test]
        public void test_actionButton_usedAndUnaffordable_stillPaintsCostRed()
        {
            // Arrange
            var expected = (Color)GetPrivate(_button, "_unaffordableColor");
            _button.SetAffordable(false);

            // Act
            _button.SetState(ActionButtonState.Used);

            // Assert — el cambio de estado no debe pisar el rojo de la plata.
            Assert.AreEqual(expected, _costLabel.color);
        }

        [Test]
        public void test_actionButton_pointerDownWhileLockedAndUnaffordable_raisesRejected()
        {
            // Arrange
            int rejections = 0;
            _button.OnRejected += () => rejections++;
            _button.SetState(ActionButtonState.Locked);
            _button.SetAffordable(false);

            // Act
            _button.OnPointerDown(null);

            // Assert — antes era un botón mudo.
            Assert.AreEqual(1, rejections);
        }

        [Test]
        public void test_actionButton_pointerDownWhileLockedButAffordable_staysSilent()
        {
            // Arrange — Locked por rango o por vida llena, con energía de sobra: la
            // pila de energía no tiene nada que decir acá.
            int rejections = 0;
            _button.OnRejected += () => rejections++;
            _button.SetState(ActionButtonState.Locked);
            _button.SetAffordable(true);

            // Act
            _button.OnPointerDown(null);

            // Assert
            Assert.AreEqual(0, rejections);
        }

        [Test]
        public void test_actionButton_becomingAffordableAgain_restoresAuthoredCostColor()
        {
            // Arrange
            _button.SetState(ActionButtonState.Locked);
            _button.SetAffordable(false);

            // Act
            _button.SetAffordable(true);

            // Assert
            Assert.AreEqual(_authoredCostColor, _costLabel.color);
        }

        // ------------------------------------------------------------------
        // Sprites por estado — base vs highlight (ChipWarrior_0 / ChipWarrior_1)
        // ------------------------------------------------------------------

        private (Sprite baseSprite, Sprite highlight, Image image) SetupSprites()
        {
            var image = _go.GetComponent<Image>();
            var tex = new Texture2D(4, 4);
            var baseSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            baseSprite.name = "base";
            var highlight = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            highlight.name = "highlight";
            _spawned.Add(tex);
            _spawned.Add(baseSprite);
            _spawned.Add(highlight);

            image.sprite = baseSprite;
            AssignPrivate(_button, "_highlightSprite", highlight);
            // Re-Awake para que capture el sprite base con el wiring completo.
            InvokePrivate(_button, "Awake");
            return (baseSprite, highlight, image);
        }

        [Test]
        public void test_actionButton_hoverWhileAvailable_swapsToHighlightSprite()
        {
            // Arrange
            var (baseSprite, highlight, image) = SetupSprites();
            _button.SetState(ActionButtonState.Available);
            Assert.AreSame(baseSprite, image.sprite, "sanity: en reposo usa el base");

            // Act + Assert — enter swapea, exit restaura.
            _button.OnPointerEnter(null);
            Assert.AreSame(highlight, image.sprite);
            _button.OnPointerExit(null);
            Assert.AreSame(baseSprite, image.sprite);
        }

        [Test]
        public void test_actionButton_selected_usesHighlightSpriteAtFullAlpha()
        {
            // Arrange
            var (_, highlight, image) = SetupSprites();

            // Act
            _button.SetState(ActionButtonState.Selected);

            // Assert
            Assert.AreSame(highlight, image.sprite);
            Assert.AreEqual(1f, image.color.a, 0.001f);
        }

        [Test]
        public void test_actionButton_locked_showsBaseSpriteAtFullAlpha()
        {
            // Arrange — nada de atenuar (feedback playtest): Locked queda con el
            // sprite base; la distinción es interactiva (tap → shake + motivo).
            var (baseSprite, _, image) = SetupSprites();

            // Act
            _button.SetState(ActionButtonState.Locked);

            // Assert
            Assert.AreSame(baseSprite, image.sprite);
            Assert.AreEqual(1f, image.color.a, 0.001f);
        }

        [Test]
        public void test_actionButton_unaffordable_keepsFullAlphaSoTheOutlineReads()
        {
            // Arrange — el Outline de uGUI multiplica su alpha por el del gráfico:
            // con el cuerpo atenuado el recuadro rojo salía fantasma (regresión de
            // playtest). Unaffordable va a alpha pleno.
            var (_, highlight, image) = SetupSprites();

            // Act
            _button.SetState(ActionButtonState.Unaffordable);

            // Assert
            Assert.AreSame(highlight, image.sprite);
            Assert.AreEqual(1f, image.color.a, 0.001f, "el cuerpo no se atenúa — el outline debe leerse");
            Assert.IsTrue(_outline.enabled);
        }

        [Test]
        public void test_actionButton_used_showsDedicatedSpriteAtFullAlpha()
        {
            // Arrange — la ficha usada NO se atenúa: sprite propio + hundimiento.
            var (_, _, image) = SetupSprites();
            var tex = new Texture2D(4, 4);
            var usedSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _spawned.Add(tex);
            _spawned.Add(usedSprite);
            AssignPrivate(_button, "_usedSprite", usedSprite);

            // Act
            _button.SetState(ActionButtonState.Used);

            // Assert
            Assert.AreSame(usedSprite, image.sprite);
            Assert.AreEqual(1f, image.color.a, 0.001f, "Used no atenúa el alpha");
        }

        [Test]
        public void test_actionButton_pointerDownWhileLocked_raisesBlockedPressed()
        {
            // Arrange — el tap sobre cualquier chip no usable avisa al view para que
            // muestre el motivo; la pila de energía (OnRejected) NO se sacude si el
            // problema no es la plata.
            int blocked = 0, rejections = 0;
            _button.OnBlockedPressed += _ => blocked++;
            _button.OnRejected += () => rejections++;
            _button.SetState(ActionButtonState.Locked);
            _button.SetAffordable(true);

            // Act
            _button.OnPointerDown(null);

            // Assert
            Assert.AreEqual(1, blocked, "el view tiene que enterarse para mostrar el motivo");
            Assert.AreEqual(0, rejections, "sin problema de energía la pila no se sacude");
        }

        [Test]
        public void test_actionButton_leavingDisabled_restoresBaseSpriteAndAlpha()
        {
            // Arrange
            var (baseSprite, _, image) = SetupSprites();
            _button.SetState(ActionButtonState.Used);

            // Act
            _button.SetState(ActionButtonState.Available);

            // Assert
            Assert.AreSame(baseSprite, image.sprite);
            Assert.AreEqual(1f, image.color.a, 0.001f);
        }

        [Test]
        public void test_actionButton_withoutHighlightSprite_neverSwapsTheSprite()
        {
            // Arrange — compat: chips sin el sprite wireado (installer sin correr)
            // conservan su sprite en todos los estados.
            var image = _go.GetComponent<Image>();
            var tex = new Texture2D(4, 4);
            var authored = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _spawned.Add(tex);
            _spawned.Add(authored);
            image.sprite = authored;
            InvokePrivate(_button, "Awake");

            // Act
            _button.SetState(ActionButtonState.Selected);
            _button.SetState(ActionButtonState.Used);

            // Assert
            Assert.AreSame(authored, image.sprite);
        }

        // ------------------------------------------------------------------
        // Helpers (patrón de RollPoolChipStackViewTests)
        // ------------------------------------------------------------------

        private static void AssignPrivate(object target, string field, object value)
            => Field(target, field).SetValue(target, value);

        private static object GetPrivate(object target, string field)
            => Field(target, field).GetValue(target);

        private static FieldInfo Field(object target, string field)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            return info;
        }

        private static void InvokePrivate(object target, string method)
        {
            var info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"método {method} no encontrado en {target.GetType().Name}");
            info.Invoke(target, null);
        }
    }
}
