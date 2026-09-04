using System;
using NUnit.Framework;
using Patterns;
using Rollgeon.Combat.Damage;
using Rollgeon.Effects;
using Rollgeon.Effects.Concretes;
using Rollgeon.Feedback;
using Rollgeon.Heroes;
using Rollgeon.UI.HUD;
using Rollgeon.UI.HUD.Breakdown;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.Tests
{
    /// <summary>
    /// Regresión playtest 2026-09-04: al terminar la secuencia N×M el total del choque
    /// quedaba en pantalla un instante y después volvía a aparecer el preview inicial
    /// (N = base, M = perilla) mientras la acción resolvía. Causa: al liberar el
    /// <see cref="BreakdownUiGate"/> la fórmula repintaba siempre, y el combo recién
    /// jugado seguía en su estado. Rig mínimo: view + label + breakdown sin CanvasGroup
    /// (la visibilidad togglea el GameObject, así <c>IsShowing</c> es la fuente de verdad).
    /// </summary>
    [TestFixture]
    public class DamageFormulaViewConsumedComboTests
    {
        private GameObject _go;
        private DamageFormulaView _view;
        private DamageBreakdownView _breakdown;
        private TextMeshProUGUI _label;
        private HeroActionBehavior _behavior;
        private Guid _player;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.Clear();
            EventManager.ResetEventDictionary();
            TypedEvent<ComboMatchedPayload>.Clear();
            TypedEvent<DamageBreakdownComputedPayload>.Clear();
            DrainGate();

            _player = Guid.NewGuid();

            _go = new GameObject("DamageFormula", typeof(RectTransform));
            _view = _go.AddComponent<DamageFormulaView>();

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(_go.transform, false);
            _label = labelGo.AddComponent<TextMeshProUGUI>();

            var breakdownGo = new GameObject("Breakdown", typeof(RectTransform));
            breakdownGo.transform.SetParent(_go.transform, false);
            _breakdown = breakdownGo.AddComponent<DamageBreakdownView>();

            SetField(_view, "_formulaLabel", _label);
            SetField(_view, "_breakdownView", _breakdown);

            // Behavior de ataque por combo (DamageSource.ComboValue) — la rama N×M.
            // HeroActionBehavior es una clase plana (BaseBehavior), no un ScriptableObject.
            _behavior = new HeroActionBehavior { ActionName = "Atacar" };
            var dmg = new EffDealDamage();
            SetField(dmg, "_damageSource", DamageSource.ComboValue);
            var group = new EffectData();
            group.Effects.Add(dmg);
            _behavior.Effects.Add(group);

            _view.Bind(_player);
        }

        [TearDown]
        public void TearDown()
        {
            _view.Unbind();
            DrainGate();
            _behavior = null;
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            TypedEvent<ComboMatchedPayload>.Clear();
            TypedEvent<DamageBreakdownComputedPayload>.Clear();
            EventManager.ResetEventDictionary();
            ServiceLocator.Clear();
        }

        // ================================================================
        // Helpers
        // ================================================================

        private void ShowAttackPreview()
        {
            _view.SetBehavior(_behavior);
            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = _player,
                ComboId = "combo.par",
                DisplayName = "Par",
                BaseDamage = 10,
            });
        }

        // Lo que hace el confirm: el announcer publica el breakdown, el director levanta el
        // gate, anima, oculta el N×M en el choque y libera el gate.
        private void PlaySequenceForCurrentCombo()
        {
            TypedEvent<DamageBreakdownComputedPayload>.Raise(new DamageBreakdownComputedPayload
            {
                SourceGuid = _player,
                ComboId = "combo.par",
            });
            BreakdownUiGate.Begin();
            _breakdown.Hide();
            BreakdownUiGate.End();
        }

        private static void DrainGate()
        {
            int guard = 0;
            while (BreakdownUiGate.Pending && guard++ < 16) BreakdownUiGate.End();
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' no encontrado en {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        // ================================================================
        // Tests
        // ================================================================

        [Test]
        public void test_damage_formula_matched_combo_shows_nxm_preview()
        {
            // Act
            ShowAttackPreview();

            // Assert — precondición del resto: el rig llega a la rama N×M.
            Assert.IsTrue(_breakdown.IsShowing);
            Assert.AreEqual(string.Empty, _label.text);
        }

        [Test]
        public void test_damage_formula_gate_release_after_sequence_does_not_reshow_consumed_preview()
        {
            // Arrange
            ShowAttackPreview();

            // Act — la secuencia completa del confirm.
            PlaySequenceForCurrentCombo();

            // Assert — el N×M queda como lo dejó el director (oculto); el total del choque
            // es del director y no se toca desde acá.
            Assert.IsFalse(_breakdown.IsShowing,
                "Al liberar el gate no debe volver el preview del combo ya jugado.");
        }

        [Test]
        public void test_damage_formula_ungated_repaint_after_sequence_keeps_consumed_preview_hidden()
        {
            // Arrange
            ShowAttackPreview();
            PlaySequenceForCurrentCombo();

            // Act — CombatHandoffService apaga el crease del target post-resolución.
            EventManager.Trigger(EventName.OnCombatTargetChanged, _player, Guid.Empty);

            // Assert
            Assert.IsFalse(_breakdown.IsShowing);
            Assert.AreEqual(string.Empty, _label.text);
        }

        [Test]
        public void test_damage_formula_clear_behavior_after_sequence_hides_and_next_combo_previews_again()
        {
            // Arrange
            ShowAttackPreview();
            PlaySequenceForCurrentCombo();
            int hiddenRaised = 0;
            _breakdown.Hidden += () => hiddenRaised++;

            // Act — la acción terminó: el HUD limpia; después llega otra tirada.
            _view.ClearBehavior();
            Assert.IsFalse(_breakdown.IsShowing);
            Assert.GreaterOrEqual(hiddenRaised, 1, "Clear debe avisar al director para apagar el total.");

            ShowAttackPreview();

            // Assert
            Assert.IsTrue(_breakdown.IsShowing, "Un combo nuevo vuelve a previsualizarse.");
        }

        [Test]
        public void test_damage_formula_repaint_swallowed_by_gate_is_recovered_on_release()
        {
            // Arrange — gate ajeno (ActionRollService sostiene el gate alrededor del resolve).
            _view.SetBehavior(_behavior);
            BreakdownUiGate.Begin();

            // Act — el match llega con el gate pendiente: se traga y se difiere.
            TypedEvent<ComboMatchedPayload>.Raise(new ComboMatchedPayload
            {
                SourceGuid = _player,
                ComboId = "combo.par",
                DisplayName = "Par",
                BaseDamage = 10,
            });
            Assert.IsFalse(_breakdown.IsShowing, "Con el gate pendiente no se repinta.");
            BreakdownUiGate.End();

            // Assert — el repintado diferido se recupera al liberar.
            Assert.IsTrue(_breakdown.IsShowing);
        }

        [Test]
        public void test_damage_formula_chain_defense_phase_after_sequence_previews_shield_again()
        {
            // Arrange
            ShowAttackPreview();
            PlaySequenceForCurrentCombo();

            // Act — fase de escudo del chain: mismo combo, preview nuevo.
            EventManager.Trigger(EventName.OnChainPhaseStarted, _player, 1, 2);

            // Assert
            Assert.IsTrue(_breakdown.IsShowing);
        }

        [Test]
        public void test_breakdown_view_raises_preview_shown_and_hidden_events()
        {
            // Arrange
            int shown = 0, hidden = 0;
            _breakdown.PreviewShown += () => shown++;
            _breakdown.Hidden += () => hidden++;

            // Act
            _breakdown.ShowPreview(10, 1f);
            _breakdown.Hide();

            // Assert
            Assert.AreEqual(1, shown);
            Assert.AreEqual(1, hidden);
        }
    }
}
