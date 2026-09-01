using System.Reflection;
using NUnit.Framework;
using Rollgeon.Dice;
using Rollgeon.Items;
using Rollgeon.Items.Active;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.DiceAnim;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// El giro de la ficha de ítem activo tiene que ser el del dado, no un contador de
    /// números: la silueta rota con <see cref="DiceAnimChoreographer"/> y el resultado se
    /// revela recién al asentarse.
    /// <para>
    /// Regresión: la primera versión ciclaba números durante todo el giro e ignoraba
    /// <c>ShowPreviewFacesDuringSpin</c>, que el proyecto shippea en <c>false</c>. El número
    /// rotando tapaba la rotación de la silueta, que es lo que se tiene que leer.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class ActiveItemChipViewSpinTests
    {
        private GameObject _go;
        private ActiveItemChipView _chip;
        private TextMeshProUGUI _label;
        private Image _die;
        private ItemSO _item;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Chip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _die = _go.GetComponent<Image>();

            var labelGo = new GameObject("RollLabel", typeof(RectTransform));
            labelGo.transform.SetParent(_go.transform, false);
            _label = labelGo.AddComponent<TextMeshProUGUI>();

            _chip = _go.AddComponent<ActiveItemChipView>();
            AssignPrivate(_chip, "_dieIcon", _die);
            AssignPrivate(_chip, "_rollLabel", _label);

            _item = ScriptableObject.CreateInstance<ItemSO>();
            _item.ItemId = "test.chip";
            _item.Type = ItemType.Active;
            _item.UsesActiveSlot = true;
            _item.ActiveDie = DiceType.D6;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_item != null) Object.DestroyImmediate(_item);
        }

        [Test]
        public void test_spin_withPreviewFacesOff_hidesTheNumberUntilTheDieSettles()
        {
            // Arrange
            BeginRoll(showPreviewFaces: false);

            // Act + Assert — durante el giro no hay número: se ve girar el dado.
            ApplyFrame(0.10f);
            Assert.IsFalse(_label.gameObject.activeSelf, "el número no va durante el giro");
            ApplyFrame(SpinSeconds - 0.01f);
            Assert.IsFalse(_label.gameObject.activeSelf, "el número no va durante el giro");

            // Al asentarse aparece el resultado.
            ApplyFrame(SpinSeconds);
            Assert.IsTrue(_label.gameObject.activeSelf, "al asentarse tiene que revelarse");
            Assert.AreEqual("4", _label.text);
        }

        [Test]
        public void test_spin_withPreviewFacesOn_cyclesFacesLikeTheCombatDice()
        {
            // Arrange — con el flag prendido la ficha sigue al proyecto, no al revés.
            BeginRoll(showPreviewFaces: true);

            // Act
            ApplyFrame(0.10f);

            // Assert
            Assert.IsTrue(_label.gameObject.activeSelf);
        }

        [Test]
        public void test_spin_rotatesTheDieSilhouetteThroughFrontAndSides()
        {
            // Arrange — es LA diferencia con la animación vieja: la silueta rota.
            BeginRoll(showPreviewFaces: false);
            var t = DiceAnimTimings.Defaults;
            int tickCount = DiceAnimChoreographer.TickCount(t.SpinSeconds, t.SpinTickSeconds);
            int sideSeed = (int)Field(_chip, "_sideSeed").GetValue(_chip);

            // Act — los ticks pares caen en lateral, los impares en frontal.
            var roles = new System.Collections.Generic.HashSet<DiceShapeRole>();
            for (int tick = 1; tick <= tickCount; tick++)
                roles.Add(DiceAnimChoreographer.SpinRole(tick, sideSeed));

            // Assert
            Assert.IsTrue(roles.Contains(DiceShapeRole.Front), "el giro tiene que pasar por el frontal");
            Assert.IsTrue(roles.Contains(DiceShapeRole.SideA) || roles.Contains(DiceShapeRole.SideB),
                "el giro tiene que pasar por al menos un lateral — si no, no rota");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static float SpinSeconds => ActiveItemRollFeelMath.SpinSeconds;

        /// <summary>Deja la ficha en medio de una tirada de 4, sin encantamiento.</summary>
        private void BeginRoll(bool showPreviewFaces)
        {
            // rawRoll == roll ⇒ WasEnchanted es false, que es el caso que interesa acá:
            // sin encantamiento el reveal es directo, sin la pausa en la cara cruda.
            var result = new ActiveItemActivationResult(
                _item, roll: 4, band: ActiveItemBand.Positive, effectsSucceeded: true, rawRoll: 4);
            AssignPrivate(_chip, "_lastResult", result);
            InvokePrivate(_chip, "BuildSpinPlan", result);
            // BuildSpinPlan levanta el asset del proyecto; el test fija el flag a mano para
            // cubrir las dos ramas sin depender de cómo esté tuneado hoy.
            AssignPrivate(_chip, "_showPreviewFaces", showPreviewFaces);
        }

        private void ApplyFrame(float elapsed)
            => InvokePrivate(_chip, "ApplyRollFrame", elapsed);

        private static void AssignPrivate(object target, string field, object value)
            => Field(target, field).SetValue(target, value);

        private static FieldInfo Field(object target, string field)
        {
            var info = target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"campo {field} no encontrado en {target.GetType().Name}");
            return info;
        }

        private static void InvokePrivate(object target, string method, params object[] args)
        {
            var info = target.GetType().GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, $"método {method} no encontrado en {target.GetType().Name}");
            info.Invoke(target, args);
        }
    }
}
