using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.Damage;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Dice;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
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
        private int _lastShieldPreview;
        private IReadOnlyList<DiceType> _lastContributingDice;
        private Action<ComboMatchedPayload> _onComboMatched;

        // Enemigo objetivo elegido antes de tirar (CNF-002). Guid.Empty = sin target →
        // el preview muestra el daño pre-mitigación. Con target, aplica weakness + escudo real.
        private Guid _currentTargetGuid;
        private EventManager.EventReceiver _onCombatTargetChanged;
        private IActionRollService _actionRollService;
        private Action<ActionRollPhase> _onActionRollPhase;

        // Fase de defensa del chain (Spec Escudo v2): la fórmula muestra el escudo
        // esperado de la tirada de escudo, no el daño de la fase anterior.
        private bool _inDefensePhase;
        private EventManager.EventReceiver _onChainPhaseStarted;
        private EventManager.EventReceiver _onChainCompleted;

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

            // args: [playerGuid, phaseIndex, phaseCount] — fase > 0 = defensa post-attack.
            _onChainPhaseStarted = args =>
            {
                if (args.Length < 2 || (Guid)args[0] != _playerGuid) return;
                _inDefensePhase = (int)args[1] > 0;
                UpdateFormula();
            };
            _onChainCompleted = args =>
            {
                _inDefensePhase = false;
                _lastShieldPreview = 0;
            };
            EventManager.Subscribe(EventName.OnChainPhaseStarted, _onChainPhaseStarted);
            EventManager.Subscribe(EventName.OnChainCompleted, _onChainCompleted);

            // args: [playerGuid, targetGuid]. Cachear el enemigo apuntado y refrescar la
            // fórmula para que el preview refleje weakness/escudo aunque no cambie la tirada.
            _onCombatTargetChanged = args =>
            {
                if (args.Length < 2 || (Guid)args[0] != _playerGuid) return;
                _currentTargetGuid = (Guid)args[1];
                UpdateFormula();
            };
            EventManager.Subscribe(EventName.OnCombatTargetChanged, _onCombatTargetChanged);

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
            if (_onChainPhaseStarted != null)
            {
                EventManager.UnSubscribe(EventName.OnChainPhaseStarted, _onChainPhaseStarted);
                _onChainPhaseStarted = null;
            }
            if (_onChainCompleted != null)
            {
                EventManager.UnSubscribe(EventName.OnChainCompleted, _onChainCompleted);
                _onChainCompleted = null;
            }
            if (_onCombatTargetChanged != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatTargetChanged, _onCombatTargetChanged);
                _onCombatTargetChanged = null;
            }
            _currentBehavior = null;
            _lastComboDisplayName = null;
            _lastComboId = null;
            _lastComboBaseDamage = 0;
            _lastShieldPreview = 0;
            _lastContributingDice = null;
            _currentTargetGuid = Guid.Empty;
            _inDefensePhase = false;
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
            _lastShieldPreview = 0;
            _lastContributingDice = null;
            _inDefensePhase = false;
            ClearFormula();
            HideThreshold();
        }

        private void OnComboMatched(ComboMatchedPayload payload)
        {
            if (payload.SourceGuid != _playerGuid) return;
            _lastComboDisplayName = payload.DisplayName;
            _lastComboId = payload.ComboId;
            _lastComboBaseDamage = payload.BaseDamage;
            _lastShieldPreview = payload.ShieldPreview;
            _lastContributingDice = payload.ContributingDice;
            UpdateFormula();
        }

        private void UpdateFormula()
        {
            if (_formulaLabel == null) return;

            // Si hay una ActionRoll activa, mostrar threshold + combo seleccionado y SALIR
            // (no se evalúa la fórmula de daño, que no aplica para Heal/ForceDoor).
            if (TryShowActionRollMode()) return;

            // Fase de defensa del chain: la tirada activa genera ESCUDO, no daño — mostrar
            // el escudo esperado (tabla por clase × multi, con cap) en vivo con los holds.
            if (_inDefensePhase)
            {
                HideThreshold();
                _formulaLabel.text = !string.IsNullOrEmpty(_lastComboDisplayName)
                    ? $"{_lastComboDisplayName}: escudo {_lastShieldPreview} (máx {PlayerComboShield.ShieldCap})"
                    : "Defensa - armá un combo para generar escudo";
                return;
            }

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

            // Daño pre-mitigación EXACTO: misma función que el golpe real, así el número
            // arrastra ATQ base del PJ, scratchMultiplier de encantamientos y el bono de
            // combo sin re-derivar la fórmula acá (era la causa del desfase reportado).
            // Nota de orden: Resolve lee LastComboScratch de los services de passives/
            // enchants, que se pueblan al procesar el MISMO ComboMatchedPayload — depende de
            // que esos services estén suscriptos antes que esta view (misma dependencia que
            // tenía el viejo ResolveComboBonusDamage).
            int preMitigation = PlayerComboDamage.Resolve(
                _playerGuid, _lastComboBaseDamage, _lastContributingDice, dmgEff.ComboMultiplier);

            // Con enemigo apuntado (elegido antes de tirar), aplicar la mitigación real
            // (weakness + escudo) SIN side-effects para que el label == golpe que va a recibir.
            int shown = preMitigation;
            if (_currentTargetGuid != Guid.Empty
                && ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline)
                && pipeline != null)
            {
                var ctx = new DamageContext
                {
                    SourceId = _playerGuid,
                    TargetId = _currentTargetGuid,
                    BaseDamage = preMitigation,
                    ComboId = _lastComboId,
                    // Enemigos sin debilidad ("None") resuelven a ×1.0 dentro del Preview.
                    IsWeaknessHit = !string.IsNullOrEmpty(_lastComboId),
                };
                pipeline.Preview(ctx);
                shown = ctx.FinalDamage;
            }

            _formulaLabel.text = $"{comboName}: {shown}";
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
                _formulaLabel.text = $"{actionTag} - {Rollgeon.Localization.LocalizedContent.Name(combo.ComboId, combo.DisplayName)} ({effective})";
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
