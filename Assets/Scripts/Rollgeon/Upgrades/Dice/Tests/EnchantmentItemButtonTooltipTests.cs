using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Rollgeon.UI.Tooltips;
using Rollgeon.Upgrades.Dice.UI;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice.Tests
{
    /// <summary>
    /// Cobertura CNF-011 (tooltip de hover en el altar de encantamientos):
    /// <see cref="EnchantmentItemButtonView.Configure"/> cablea/limpia el
    /// <see cref="UITooltipTrigger"/>, y <see cref="EnchantmentAltarView.BuildEnchantmentTooltip"/>
    /// arma el texto rich-text a partir de un <see cref="EnchantmentSO"/>.
    /// </summary>
    [TestFixture]
    public class EnchantmentItemButtonTooltipTests
    {
        private GameObject _go;
        private EnchantmentItemButtonView _view;
        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("EnchantmentItemButton");
            _view = _go.AddComponent<EnchantmentItemButtonView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            foreach (var obj in _created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _created.Clear();
        }

        // ---- Helpers ------------------------------------------------------

        private EnchantmentSO MakeEnchantment(string id, string displayName, string description)
        {
            var ench = ScriptableObject.CreateInstance<EnchantmentSO>();
            ench.name = id ?? "ench";
            _created.Add(ench);
            AssignPrivate(ench, "_upgradeId", id);
            AssignPrivate(ench, "_displayName", displayName);
            AssignPrivate(ench, "_description", description);
            return ench;
        }

        private static void AssignPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado.");
            field.SetValue(target, value);
        }

        private EnchantmentSO MakeCursedEnchantment(string id, string displayName, string description)
        {
            var ench = MakeEnchantment(id, displayName, description);
            AssignPrivate(ench, "_capabilities",
                new List<IEnchantmentCapability> { new CapCursed() });
            return ench;
        }

        // Títulos por tipo (EnchantmentPalette): maldición roja, bendición dorada.
        private const string BlessedHex = "D9A44E";
        private const string CursedHex = "D1365A";

        // ---- Configure + UITooltipTrigger wiring ---------------------------

        [Test]
        public void Configure_WithTooltipProvider_AddsTriggerAndSetsProvider()
        {
            _view.Configure("Cupo 1", "Encantado: Foo", () => { }, () => "hover text");

            // AddComponent en EditMode no corre Awake — chequeamos el campo público
            // TextProvider directamente, no el _ownerId (seteado en Awake).
            var trigger = _go.GetComponent<UITooltipTrigger>();
            Assert.IsNotNull(trigger, "Configure con provider debe agregar UITooltipTrigger.");
            Assert.AreEqual("hover text", trigger.TextProvider());
        }

        [Test]
        public void Configure_ReusedButtonWithoutProvider_ClearsToEmptyText()
        {
            _view.Configure("Cupo 1", "Encantado: Foo", () => { }, () => "hover text");

            // Botón reusado del pool que pasó a un estado sin tooltip (ej. slot
            // vaciado tras un apply) — no debe repetir el texto de la config anterior.
            _view.Configure("Cupo 1", "Vacío", () => { });

            var trigger = _go.GetComponent<UITooltipTrigger>();
            Assert.IsNotNull(trigger);
            Assert.AreEqual(string.Empty, trigger.TextProvider());
        }

        // ---- BuildEnchantmentTooltip ----------------------------------------

        [Test]
        public void BuildEnchantmentTooltip_WithNameAndDescription_FormatsBoldNamePlusDescription()
        {
            var ench = MakeEnchantment("ench.foo", "Foo", "Hace cosas.");

            string result = EnchantmentAltarView.BuildEnchantmentTooltip(ench);

            Assert.AreEqual($"<b><color=#{BlessedHex}>Foo</color></b>\n<size=80%>Hace cosas.</size>", result);
        }

        [Test]
        public void BuildEnchantmentTooltip_CursedEnchantment_TitleInCursedRed()
        {
            var ench = MakeCursedEnchantment("ench.curse", "Oxidado", "Hace daño.");

            string result = EnchantmentAltarView.BuildEnchantmentTooltip(ench);

            Assert.AreEqual($"<b><color=#{CursedHex}>Oxidado</color></b>\n<size=80%>Hace daño.</size>", result);
        }

        [Test]
        public void BuildEnchantmentTooltip_EmptyDescription_ReturnsOnlyBoldName()
        {
            var ench = MakeEnchantment("ench.foo", "Foo", string.Empty);

            string result = EnchantmentAltarView.BuildEnchantmentTooltip(ench);

            Assert.AreEqual($"<b><color=#{BlessedHex}>Foo</color></b>", result);
        }

        [Test]
        public void BuildEnchantmentTooltip_NullDisplayName_FallsBackToUpgradeId()
        {
            var ench = MakeEnchantment("ench.foo", null, "Hace cosas.");

            string result = EnchantmentAltarView.BuildEnchantmentTooltip(ench);

            Assert.AreEqual($"<b><color=#{BlessedHex}>ench.foo</color></b>\n<size=80%>Hace cosas.</size>", result);
        }

        [Test]
        public void BuildEnchantmentTooltip_NullEnchantment_ReturnsEmpty()
        {
            string result = EnchantmentAltarView.BuildEnchantmentTooltip(null);

            Assert.AreEqual(string.Empty, result);
        }

        // ---- BuildDiceTooltip -----------------------------------------------

        [Test]
        public void BuildDiceTooltip_NoOccupiedSlots_ReturnsPlaceholder()
        {
            var slots = new EnchantmentSO[] { null, null };

            string result = EnchantmentAltarView.BuildDiceTooltip(slots);

            Assert.AreEqual("Sin encantamientos", result);
        }

        [Test]
        public void BuildDiceTooltip_EmptySlotsArray_ReturnsPlaceholder()
        {
            string result = EnchantmentAltarView.BuildDiceTooltip(System.Array.Empty<EnchantmentSO>());

            Assert.AreEqual("Sin encantamientos", result);
        }

        [Test]
        public void BuildDiceTooltip_NullSlots_ReturnsPlaceholder()
        {
            string result = EnchantmentAltarView.BuildDiceTooltip(null);

            Assert.AreEqual("Sin encantamientos", result);
        }

        [Test]
        public void BuildDiceTooltip_OccupiedSlots_ListsOneLinePerEnchantment()
        {
            var a = MakeEnchantment("ench.a", "Alpha", "Hace A.");
            var b = MakeEnchantment("ench.b", "Beta", "Hace B.");
            var slots = new[] { a, null, b };

            string result = EnchantmentAltarView.BuildDiceTooltip(slots);

            Assert.AreEqual(
                $"• <color=#{BlessedHex}>Alpha</color> — Hace A.\n• <color=#{BlessedHex}>Beta</color> — Hace B.",
                result);
        }

        [Test]
        public void BuildDiceTooltip_MixedSlots_EachTitleUsesItsOwnColor()
        {
            var blessed = MakeEnchantment("ench.a", "Alpha", "Hace A.");
            var cursed = MakeCursedEnchantment("ench.b", "Beta", "Hace B.");
            var slots = new[] { blessed, cursed };

            string result = EnchantmentAltarView.BuildDiceTooltip(slots);

            Assert.AreEqual(
                $"• <color=#{BlessedHex}>Alpha</color> — Hace A.\n• <color=#{CursedHex}>Beta</color> — Hace B.",
                result);
        }
    }
}
