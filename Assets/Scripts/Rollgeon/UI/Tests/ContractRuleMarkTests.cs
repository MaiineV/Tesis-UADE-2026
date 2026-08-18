using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.Combos;
using Rollgeon.UI.HUD.Contract;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Las marcas que los jefes le dejan a la tabla de combos: cómo se deducen del daño efectivo
    /// (<see cref="ContractRowStateResolver"/>) y cómo las pinta <see cref="ContractComboRowView"/>.
    /// El resolver va aparte porque el corrimiento sólo se reconoce mirando la tabla entera.
    /// </summary>
    [TestFixture]
    public class ContractRuleMarkTests
    {
        private readonly List<Object> _spawned = new();

        // Tabla de referencia, en el mismo orden que la muestra el drawer (por daño base).
        private static readonly ContractRowBase[] Table =
        {
            new ContractRowBase("combo.pair", "Par", 8),
            new ContractRowBase("combo.double_pair", "Doble Par", 14),
            new ContractRowBase("combo.trio", "Trío", 22),
            new ContractRowBase("combo.generala", "Generala", 90),
        };

        private const int Pair = 0;
        private const int DoublePair = 1;
        private const int Trio = 2;

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ======================================================================
        // Deducir la marca
        // ======================================================================

        [Test]
        public void Resolve_WithoutModifiers_LeavesTheRowUnmarked()
        {
            // Arrange + Act — el efectivo es igual al de la hoja.
            var state = ContractRowStateResolver.Resolve(Table, Trio, 22, forbidden: false, blockedTurns: 0);

            // Assert
            Assert.AreEqual(ContractRowMark.None, state.Mark);
            Assert.IsFalse(state.IsAltered);
            Assert.IsFalse(state.IsStruckThrough);
            Assert.AreEqual(22, state.EffectiveDamage);
        }

        [Test]
        public void Resolve_WhenTheValueLandsOnAnotherRow_ReadsAsShifted()
        {
            // Arrange — 14 es exactamente el base de la fila del Doble Par.

            // Act
            var state = ContractRowStateResolver.Resolve(Table, Trio, 14, forbidden: false, blockedTurns: 0);

            // Assert
            Assert.AreEqual(ContractRowMark.Shifted, state.Mark);
            Assert.AreEqual("combo.double_pair", state.ShiftedToComboId);
            Assert.AreEqual("Doble Par", state.ShiftedToDisplayName);
            Assert.AreEqual(14, state.EffectiveDamage);
            Assert.IsTrue(state.IsStruckThrough, "la fila corrida va tachada hasta revertirse");
        }

        [Test]
        public void Resolve_WhenTheValueMatchesNoRow_ReadsAsBuffedOrNerfed()
        {
            // Arrange + Act — un ×2 sobre el Par da 16, que no es el base de ninguna fila.
            var buffed = ContractRowStateResolver.Resolve(Table, Pair, 16, forbidden: false, blockedTurns: 0);
            var nerfed = ContractRowStateResolver.Resolve(Table, Trio, 11, forbidden: false, blockedTurns: 0);

            // Assert
            Assert.AreEqual(ContractRowMark.Buffed, buffed.Mark);
            Assert.AreEqual(8, buffed.Delta);
            Assert.IsTrue(buffed.IsFavorable);
            Assert.IsFalse(buffed.IsStruckThrough, "un buff no tacha la fila, la resalta");

            Assert.AreEqual(ContractRowMark.Nerfed, nerfed.Mark);
            Assert.AreEqual(-11, nerfed.Delta);
            Assert.IsFalse(nerfed.IsFavorable);
        }

        [Test]
        public void Resolve_AShiftUpwards_ReadsAsFavorable()
        {
            // Arrange + Act — el corrimiento sortea dirección, así que también puede subir.
            var up = ContractRowStateResolver.Resolve(Table, Pair, 14, forbidden: false, blockedTurns: 0);
            var down = ContractRowStateResolver.Resolve(Table, Trio, 14, forbidden: false, blockedTurns: 0);

            // Assert
            Assert.AreEqual(ContractRowMark.Shifted, up.Mark);
            Assert.IsTrue(up.IsFavorable);
            Assert.AreEqual(ContractRowMark.Shifted, down.Mark);
            Assert.IsFalse(down.IsFavorable);
        }

        [Test]
        public void Resolve_AForbiddenRow_IsStruckAndPaysZero()
        {
            // Arrange + Act
            var state = ContractRowStateResolver.Resolve(Table, Trio, 0, forbidden: true, blockedTurns: 0);

            // Assert
            Assert.AreEqual(ContractRowMark.Forbidden, state.Mark);
            Assert.AreEqual(0, state.EffectiveDamage);
            Assert.IsTrue(state.IsStruckThrough);
            Assert.IsFalse(state.IsFavorable);
        }

        [Test]
        public void Resolve_ABlockedRow_WinsOverForbidden_BecauseItCarriesTheCountdown()
        {
            // Arrange — las dos tachaduras se dibujan igual; sólo el bloqueo sabe cuándo se va.

            // Act
            var state = ContractRowStateResolver.Resolve(Table, Trio, 0, forbidden: true, blockedTurns: 2);

            // Assert
            Assert.AreEqual(ContractRowMark.Blocked, state.Mark);
            Assert.AreEqual(2, state.BlockedTurns);
            Assert.IsTrue(state.IsStruckThrough);
        }

        [Test]
        public void Resolve_OutOfRangeIndex_DoesNotThrow()
        {
            // Arrange + Act + Assert
            Assert.DoesNotThrow(() => ContractRowStateResolver.Resolve(Table, 99, 10, false, 0));
            Assert.DoesNotThrow(() => ContractRowStateResolver.Resolve(null, 0, 10, false, 0));
        }

        [Test]
        public void Resolve_WithASingleRowTable_FallsBackToBuffed()
        {
            // Arrange — sin vecinos no hay a quién señalar.
            var lonely = new[] { new ContractRowBase("combo.trio", "Trío", 22) };

            // Act
            var state = ContractRowStateResolver.Resolve(lonely, 0, 14, forbidden: false, blockedTurns: 0);

            // Assert
            Assert.AreEqual(ContractRowMark.Nerfed, state.Mark);
            Assert.IsNull(state.ShiftedToComboId);
        }

        // ======================================================================
        // Pintar la marca
        // ======================================================================

        [Test]
        public void Bind_ShowsTheEffectiveDamage_NotTheSheetValue()
        {
            // Arrange
            var row = MakeRow(out var damage, out _, out _);
            var combo = MakeCombo("combo.trio");
            var shifted = ContractRowStateResolver.Resolve(Table, Trio, 14, forbidden: false, blockedTurns: 0);

            // Act
            row.Bind(combo, null, shifted);

            // Assert
            Assert.AreEqual("14", damage.text);
        }

        [Test]
        public void Bind_WithAShiftedRow_StrikesItThroughAndShowsTheBadge()
        {
            // Arrange
            var row = MakeRow(out _, out var strike, out var badge);
            var combo = MakeCombo("combo.trio");
            var shifted = ContractRowStateResolver.Resolve(Table, Trio, 14, forbidden: false, blockedTurns: 0);

            // Act
            row.Bind(combo, null, shifted);

            // Assert
            Assert.IsTrue(strike.enabled);
            Assert.IsTrue(badge.activeSelf);
        }

        [Test]
        public void Bind_WithAnUnmarkedRow_LeavesTheRowClean()
        {
            // Arrange — los slots de fila se reusan entre repintados.
            var row = MakeRow(out var damage, out var strike, out var badge);
            var combo = MakeCombo("combo.trio");
            row.Bind(combo, null, ContractRowStateResolver.Resolve(Table, Trio, 14, false, 0));

            // Act
            row.Bind(combo, null, ContractRowState.Unmodified(22));

            // Assert
            Assert.IsFalse(strike.enabled);
            Assert.IsFalse(badge.activeSelf);
            Assert.AreEqual("22", damage.text);
        }

        [Test]
        public void Bind_ColorsTheDamage_GreenWhenItFavorsYouAndRedWhenItDoesNot()
        {
            // Arrange
            var row = MakeRow(out var damage, out _, out _);
            var combo = MakeCombo("combo.pair");

            // Act
            row.Bind(combo, null, ContractRowStateResolver.Resolve(Table, Pair, 16, false, 0));
            var buffedColor = damage.color;
            row.Bind(combo, null, ContractRowStateResolver.Resolve(Table, Pair, 3, false, 0));
            var nerfedColor = damage.color;

            // Assert
            Assert.AreNotEqual(buffedColor, nerfedColor);
            Assert.Greater(buffedColor.g, buffedColor.r, "el buff se lee verde");
            Assert.Greater(nerfedColor.r, nerfedColor.g, "el nerf se lee rojo");
        }

        [Test]
        public void BadgeText_OfABlockedRow_CarriesTheRemainingTurns()
        {
            // Arrange
            var state = ContractRowStateResolver.Resolve(Table, Trio, 22, forbidden: false, blockedTurns: 3);

            // Act
            var text = state.BadgeText();

            // Assert — el texto base viaja por la tabla de localización; sólo se fija el número.
            StringAssert.EndsWith("3", text);
        }

        [Test]
        public void BadgeText_OfAnUnmarkedRow_IsEmpty()
        {
            // Arrange + Act + Assert
            Assert.IsEmpty(ContractRowState.Unmodified(22).BadgeText());
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private BaseComboSO MakeCombo(string comboId)
        {
            var combo = ScriptableObject.CreateInstance<Rollgeon.Combos.Concretes.Combo_Par>();
            _spawned.Add(combo);
            var field = typeof(BaseComboSO).GetField("_comboId",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "campo _comboId no encontrado en BaseComboSO");
            field.SetValue(combo, comboId);
            return combo;
        }

        // Sin refs de dados: BindDice corta solo cuando no hay settings.
        private ContractComboRowView MakeRow(out TextMeshProUGUI damage, out Image strike,
            out GameObject badge)
        {
            var go = new GameObject("ContractComboRow", typeof(RectTransform));
            _spawned.Add(go);

            var nameLabel = AddLabelChild(go, "Name");
            damage = AddLabelChild(go, "Damage");

            var strikeGo = new GameObject("Strike", typeof(RectTransform), typeof(CanvasRenderer));
            strikeGo.transform.SetParent(go.transform, worldPositionStays: false);
            strike = strikeGo.AddComponent<Image>();
            strike.enabled = false;

            badge = new GameObject("RuleBadge", typeof(RectTransform));
            badge.transform.SetParent(go.transform, worldPositionStays: false);
            var badgeLabel = AddLabelChild(badge, "Value");
            badge.SetActive(false);

            var row = go.AddComponent<ContractComboRowView>();
            SetPrivate(row, "_nameLabel", nameLabel);
            SetPrivate(row, "_damageLabel", damage);
            SetPrivate(row, "_strike", strike);
            SetPrivate(row, "_badge", badge);
            SetPrivate(row, "_badgeLabel", badgeLabel);
            return row;
        }

        private static TextMeshProUGUI AddLabelChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            return go.AddComponent<TextMeshProUGUI>();
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
