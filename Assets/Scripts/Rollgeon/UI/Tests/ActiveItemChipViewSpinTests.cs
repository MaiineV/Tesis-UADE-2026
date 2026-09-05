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
    /// revela recién al asentarse. El giro vive en el segmento <b>pendiente</b>
    /// (<c>OnRolled</c>): la cara cruda queda sostenida hasta que llegue la
    /// resolucion del servicio.
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
        private Image _background;
        private ItemSO _item;

        [SetUp]
        public void SetUp()
        {
            // Espeja el prefab: el fondo de la ficha en el GO raiz (es el que se escala) y
            // el dado como hijo.
            _go = new GameObject("Chip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _background = _go.GetComponent<Image>();

            var dieGo = new GameObject("DieIcon", typeof(RectTransform), typeof(CanvasRenderer));
            dieGo.transform.SetParent(_go.transform, false);
            _die = dieGo.AddComponent<Image>();

            var labelGo = new GameObject("RollLabel", typeof(RectTransform));
            labelGo.transform.SetParent(_go.transform, false);
            _label = labelGo.AddComponent<TextMeshProUGUI>();

            _chip = _go.AddComponent<ActiveItemChipView>();
            AssignPrivate(_chip, "_chip", _background);
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

            // Al asentarse aparece la cara cruda, que queda sostenida hasta la decision.
            ApplyFrame(SpinSeconds);
            Assert.IsTrue(_label.gameObject.activeSelf, "al asentarse tiene que revelarse");
            Assert.AreEqual("4", _label.text);
            ApplyFrame(SpinSeconds + 5f);
            Assert.IsTrue(_label.gameObject.activeSelf,
                "la cara pendiente no se apaga por tiempo: espera la decision");
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
        // Escala de la ficha
        // ------------------------------------------------------------------

        [Test]
        public void test_repeatedRolls_withoutLettingTheAnimationFinish_doNotGrowTheChip()
        {
            // Arrange — el bug: la view re-muestreaba la escala de reposo del transform
            // que este mismo componente anima. Cada tirada cortada en pleno pop horneaba
            // ese pop como nueva base y la ficha crecia por uso, hasta tapar el HUD.
            // Activaciones seguidas hacen ese corte: cada una reinicia el giro.
            InvokePrivate(_chip, "Awake");
            var rest = _go.transform.localScale;

            // Act — ocho tiradas seguidas, cada una interrumpiendo el pop de la anterior,
            // y una resolucion cortada en pleno pop del payoff.
            for (int i = 0; i < 8; i++)
            {
                InvokePrivate(_chip, "HandleRolled", NewPending());
                ApplyFrame(SpinSeconds + 0.001f); // el instante de maxima escala del asentado
            }
            InvokePrivate(_chip, "HandleResolved", NewResult());
            InvokePrivate(_chip, "ApplyResolveFrame", 0.001f);
            InvokePrivate(_chip, "EndRollAnimation");

            // Assert
            Assert.AreEqual(rest.x, _go.transform.localScale.x, 0.0001f,
                "la ficha tiene que volver a su escala de reposo, no acumular los pops");
        }

        [Test]
        public void test_aNewRoll_returnsToRestBeforeAnimating()
        {
            // Arrange — una tirada nueva cancela la anterior; si arrancara
            // desde el pop en curso, el pop nuevo saldria montado sobre el viejo.
            InvokePrivate(_chip, "Awake");
            var rest = _go.transform.localScale;
            InvokePrivate(_chip, "HandleRolled", NewPending());
            ApplyFrame(SpinSeconds + 0.001f);
            Assert.Greater(_go.transform.localScale.x, rest.x, "el pop tiene que haber ocurrido");

            // Act
            InvokePrivate(_chip, "HandleRolled", NewPending());

            // Assert
            Assert.AreEqual(rest.x, _go.transform.localScale.x, 0.0001f);
        }

        [Test]
        public void test_resolve_popsScaledByBandAndShowsTheFinalFace()
        {
            // Arrange — el payoff va en la resolucion, escalado por banda.
            InvokePrivate(_chip, "Awake");
            var rest = _go.transform.localScale;
            InvokePrivate(_chip, "HandleRolled", NewPending());
            ApplyFrame(SpinSeconds + 5f);

            // Act
            InvokePrivate(_chip, "HandleResolved", NewResult());
            InvokePrivate(_chip, "ApplyResolveFrame", 0.001f);

            // Assert
            Assert.AreEqual("6", _label.text, "la cara final, no la cruda");
            Assert.Greater(_go.transform.localScale.x, rest.x, "el pop del payoff");
        }

        private ActiveItemRoll NewPending() => new ActiveItemRoll(_item, rawRoll: 4);

        /// <summary>Resolucion de banda positiva: la de pop mas grande, el peor caso.</summary>
        private ActiveItemActivationResult NewResult()
            => new ActiveItemActivationResult(
                _item, roll: 6, band: ActiveItemBand.Positive, effectsSucceeded: true, rawRoll: 6);

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static float SpinSeconds => ActiveItemRollFeelMath.SpinSeconds;

        /// <summary>Deja la ficha en medio de una tirada pendiente de 4.</summary>
        private void BeginRoll(bool showPreviewFaces)
        {
            var pending = NewPending();
            AssignPrivate(_chip, "_pending", pending);
            InvokePrivate(_chip, "BuildSpinPlan", pending);
            // BuildSpinPlan levanta el asset del proyecto; el test fija el flag a mano para
            // cubrir las dos ramas sin depender de cómo esté tuneado hoy.
            AssignPrivate(_chip, "_showPreviewFaces", showPreviewFaces);
        }

        private void ApplyFrame(float elapsed)
            => InvokePrivate(_chip, "ApplyPendingFrame", elapsed);

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
