using System;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
using Rollgeon.Upgrades.Combos;
using Rollgeon.Upgrades.Dice;
using TMPro;
using UnityEngine;

namespace Rollgeon.UI.HUD
{
    /// <summary>
    /// View que muestra la fórmula del próximo daño/curación/skill-check. Tiene dos
    /// modos según el behavior activo:
    /// <list type="bullet">
    ///   <item><b>Damage</b> (combate normal con EffDealDamage): muestra
    ///   <c>{combo} × {action} = {total}</c>. El threshold label permanece oculto.</item>
    ///   <item><b>ActionRoll</b> (Heal / Forzar Puerta): muestra "Necesitás ≥ {threshold}"
    ///   en el thresholdLabel y el combo actual seleccionado en formulaLabel.</item>
    /// </list>
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/HUD/Damage Formula View")]
    public class DamageFormulaView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _formulaLabel;
        [Tooltip("Label de threshold para ActionRolls (Heal/ForceDoor). Si no está cableado, " +
                 "se intenta auto-resolver buscando un hijo llamado 'ThresholdLabel'.")]
        [SerializeField] private TextMeshProUGUI _thresholdLabel;

        private Guid _playerGuid;
        private HeroActionBehavior _currentBehavior;
        private string _lastComboDisplayName;
        private string _lastComboId;
        private int _lastComboBaseDamage;
        private Action<ComboMatchedPayload> _onComboMatched;
        private IActionRollService _actionRollService;
        private Action<ActionRollPhase> _onActionRollPhase;

        private void Awake()
        {
            // Auto-resolve del threshold label si no se cableó en Inspector.
            if (_thresholdLabel == null)
            {
                var t = transform.Find("ThresholdLabel");
                if (t != null) _thresholdLabel = t.GetComponent<TextMeshProUGUI>();
            }
        }

        private bool _bound;

        // La view es un singleton de escena compartido por CombatHUDView y
        // ExplorationHUDView; cada HUD hace Bind/Unbind en su push/pop. Sin contar
        // owners, el Unbind de un HUD desuscribía la view mientras el otro seguía
        // activo (ej. exploración popea DESPUÉS de que combate pushea) → la fórmula
        // dejaba de actualizarse en combate. Ref-count: solo desuscribimos al último.
        private int _bindCount;

        public void Bind(Guid playerGuid)
        {
            // Player distinto = reset total antes de re-suscribir (no debería pasar en
            // single-player, pero deja la view consistente si cambia el target).
            if (_bound && _playerGuid != playerGuid)
                ForceUnbind();

            _playerGuid = playerGuid;
            _bindCount++;
            if (_bound) return; // ya suscripto por otro owner al mismo guid

            _onComboMatched = OnComboMatched;
            TypedEvent<ComboMatchedPayload>.Subscribe(_onComboMatched);

            // Subscribir al action roll service para detectar modo ActionRoll y refrescar
            // el threshold/combo cuando el service cambia de fase.
            if (ServiceLocator.TryGetService<IActionRollService>(out _actionRollService)
                && _actionRollService != null)
            {
                _onActionRollPhase = _ => UpdateFormula();
                _actionRollService.OnPhaseChanged += _onActionRollPhase;
            }

            _bound = true;
            ClearFormula();
            HideThreshold();
        }

        public void Unbind()
        {
            if (!_bound) return;
            _bindCount--;
            if (_bindCount > 0) return; // otro HUD sigue usando la view
            ForceUnbind();
        }

        private void ForceUnbind()
        {
            if (!_bound) { _bindCount = 0; return; }
            if (_onComboMatched != null)
            {
                TypedEvent<ComboMatchedPayload>.Unsubscribe(_onComboMatched);
                _onComboMatched = null;
            }
            if (_actionRollService != null && _onActionRollPhase != null)
            {
                _actionRollService.OnPhaseChanged -= _onActionRollPhase;
                _onActionRollPhase = null;
                _actionRollService = null;
            }
            _currentBehavior = null;
            _lastComboDisplayName = null;
            _lastComboId = null;
            _lastComboBaseDamage = 0;
            _bound = false;
            _bindCount = 0;
            ClearFormula();
            HideThreshold();
        }

        public void SetBehavior(HeroActionBehavior behavior)
        {
            _currentBehavior = behavior;
            UpdateFormula();
        }

        public void ClearBehavior()
        {
            _currentBehavior = null;
            _lastComboDisplayName = null;
            _lastComboId = null;
            _lastComboBaseDamage = 0;
            ClearFormula();
            HideThreshold();
        }

        private void OnComboMatched(ComboMatchedPayload payload)
        {
            if (payload.SourceGuid != _playerGuid) return;
            _lastComboDisplayName = payload.DisplayName;
            _lastComboId = payload.ComboId;
            _lastComboBaseDamage = payload.BaseDamage;
            UpdateFormula();
        }

        private void UpdateFormula()
        {
            if (_formulaLabel == null) return;

            // Si hay una ActionRoll activa, mostrar threshold + combo seleccionado y SALIR
            // (no se evalúa la fórmula de daño, que no aplica para Heal/ForceDoor).
            if (TryShowActionRollMode()) return;

            HideThreshold();
            if (_currentBehavior == null) { ClearFormula(); return; }

            var dmgEff = _currentBehavior.FindFirstDealDamageEffect();
            if (dmgEff == null) { ClearFormula(); return; }

            if (dmgEff.Source == DamageSource.Constant)
            {
                _formulaLabel.text = $"{_currentBehavior.ActionName} ({dmgEff.BaseAmount})";
                return;
            }

            if (dmgEff.Source == DamageSource.FromReader)
            {
                _formulaLabel.text = $"{_currentBehavior.ActionName} (stat)";
                Debug.Log($"[DamageFormulaView] UpdateFormula — FromReader → \"{_formulaLabel.text}\"");
                return;
            }

            if (_lastComboBaseDamage <= 0)
            {
                _formulaLabel.text = $"{_currentBehavior.ActionName} (sin combo)";
                return;
            }

            string comboName = !string.IsNullOrEmpty(_lastComboDisplayName) ? _lastComboDisplayName : "Combo";
            int comboPart = Mathf.RoundToInt(_lastComboBaseDamage * dmgEff.ComboMultiplier);
            int bonus = ResolveComboBonusDamage(_lastComboId);
            int total = comboPart + bonus;

            // Resumen completo en el formula label: combo + daño base, multiplicador (si
            // ≠ 1), agregados de mejoras (si los hay) y el total final. Mismo cálculo que
            // EffDealDamage.ResolveArgs (pre-mitigación). Ejemplos:
            //   "Par: 50"                  (sin multiplicador ni mejoras)
            //   "Par: 50 + 60 = 110"       (mejora de +60)
            //   "Par: 50 × 2 = 100"        (multiplicador 2)
            //   "Par: 50 × 2 + 60 = 160"   (ambos)
            bool hasMultiplier = !Mathf.Approximately(dmgEff.ComboMultiplier, 1f);
            string formula = $"{comboName}: {_lastComboBaseDamage}";
            if (hasMultiplier) formula += $" × {dmgEff.ComboMultiplier}";
            if (bonus > 0) formula += $" + {bonus}";
            if (hasMultiplier || bonus > 0) formula += $" = {total}";
            _formulaLabel.text = formula;
        }

        // Suma los bonuses de combo passives (tienda) y dice enchantments igual que
        // EffDealDamage.ResolveArgs — así el total mostrado coincide con el golpe real
        // (antes de mitigación del pipeline, que depende del target). GetBonusDamage es
        // una query sin side-effects; el scratch de enchantments ya quedó computado por
        // su service al procesar el mismo ComboMatchedPayload.
        private int ResolveComboBonusDamage(string comboId)
        {
            int bonus = 0;
            if (!string.IsNullOrEmpty(comboId)
                && ServiceLocator.TryGetService<IComboPassiveService>(out var passives)
                && passives != null)
                bonus += passives.GetBonusDamage(comboId);

            if (ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchants)
                && enchants?.LastComboScratch != null)
                bonus += enchants.LastComboScratch.BonusComboDamage;

            return bonus;
        }

        private bool TryShowActionRollMode()
        {
            if (_actionRollService == null || !_actionRollService.IsActive) return false;
            var spec = _actionRollService.CurrentSpec;

            // Threshold label visible con el puntaje a superar.
            if (_thresholdLabel != null)
            {
                _thresholdLabel.gameObject.SetActive(true);
                _thresholdLabel.text = $"Necesitás >= {spec.Threshold}";
            }

            // Formula label: combo actual seleccionado del action roll service.
            var combo = _actionRollService.CurrentCombo;
            int effective = _actionRollService.CurrentEffectiveTotal;
            string actionTag = string.IsNullOrEmpty(spec.ActionLabel) ? "Acción" : spec.ActionLabel;

            if (combo != null)
                _formulaLabel.text = $"{actionTag} - {combo.DisplayName} ({effective})";
            else
                _formulaLabel.text = $"{actionTag} - seleccioná los dados de tu combo";
            return true;
        }

        private void HideThreshold()
        {
            if (_thresholdLabel != null) _thresholdLabel.gameObject.SetActive(false);
        }

        private void ClearFormula()
        {
            if (_formulaLabel != null) _formulaLabel.text = string.Empty;
        }
    }
}
