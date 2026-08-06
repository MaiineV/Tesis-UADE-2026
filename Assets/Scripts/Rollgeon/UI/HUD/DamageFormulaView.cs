using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.AntiRepeat;
using Rollgeon.Combat.ComboLog;
using Rollgeon.Combat.Damage;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Dice;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
using Rollgeon.Items;
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

        [Tooltip("Feedback visual del value text (color por board type + efectos por momento). " +
                 "Opcional: sin cablear, el label se comporta como antes (sin color/efectos).")]
        [SerializeField] private ValueTextFeedbackController _feedback;

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

        // Board type vigente del value text (color + tag por tipo). Dedup para no re-lanzar
        // el tween de color en cada update.
        private DiceBoardType _boardType = DiceBoardType.Default;
        private bool _boardTypeSet;

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
                _onActionRollPhase = _ =>
                {
                    // Exploración (Heal/Forzar Puerta): el board type sale del spec activo.
                    if (_actionRollService != null && _actionRollService.IsActive)
                        SetBoardType(_actionRollService.CurrentSpec.BoardType);
                    UpdateFormula();
                };
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
                bool hasCombo = !string.IsNullOrEmpty(_lastComboDisplayName);
                string text = hasCombo
                    ? $"{_lastComboDisplayName}: escudo {_lastShieldPreview} (máx {PlayerComboShield.ShieldCap})"
                    : "Defensa - armá un combo para generar escudo";
                RenderLabel(text, hasCombo ? _lastShieldPreview : 0);
                return;
            }

            HideThreshold();
            if (_currentBehavior == null) { ClearFormula(); return; }

            var dmgEff = _currentBehavior.FindFirstDealDamageEffect();
            if (dmgEff == null) { ClearFormula(); return; }

            if (dmgEff.Source == DamageSource.Constant)
            {
                RenderLabel($"{_currentBehavior.ActionName} ({dmgEff.BaseAmount})", 0);
                return;
            }

            if (dmgEff.Source == DamageSource.FromReader)
            {
                string statText = $"{_currentBehavior.ActionName} (stat)";
                RenderLabel(statText, 0);
                Debug.Log($"[DamageFormulaView] UpdateFormula — FromReader → \"{statText}\"");
                return;
            }

            // BUG-040: gate por PRESENCIA de combo, no por base plano > 0 — un combo de
            // base dinámica (Higher Number: flat 0 en tabla + valor del dado) matchea con
            // base plano 0 y el label decía "(sin combo)" aunque el daño sí lo sumaba.
            if (string.IsNullOrEmpty(_lastComboId))
            {
                RenderLabel($"{_currentBehavior.ActionName} (sin combo)", 0);
                return;
            }

            string comboName = !string.IsNullOrEmpty(_lastComboDisplayName) ? _lastComboDisplayName : "Combo";

            // Pasivo anti-repetición (Mode Combo): repetir el ÚLTIMO combo confirmado hace 0
            // daño. Mostramos la advertencia explícita en vez del número — hacemos el chequeo
            // acá (no dependemos de que Preview/Mitigate devuelva 0). El jugador todavía está
            // eligiendo dados, así que el "anterior real" es directamente LastCombo (Record
            // para este intento aún no corrió), igual que el guard de Preview en DamagePipeline.
            if (AntiRepeatComboModeActive() && IsRepeatOfLastCombo(_lastComboId))
            {
                RenderLabel(
                    Rollgeon.Localization.LocalizedContent.Ui("combat.combo_repeated_zero", "Combo repetido: 0 daño"),
                    0);
                return;
            }

            // Daño pre-mitigación EXACTO: misma función que el golpe real, así el número
            // arrastra ATQ base del PJ, scratchMultiplier de encantamientos y el bono de
            // combo sin re-derivar la fórmula acá (era la causa del desfase reportado).
            // Nota de orden: Resolve lee LastComboScratch de los services de passives/
            // enchants, que se pueblan al procesar el MISMO ComboMatchedPayload — depende de
            // que esos services estén suscriptos antes que esta view (misma dependencia que
            // tenía el viejo ResolveComboBonusDamage).
            int preMitigation = PlayerComboDamage.Resolve(
                _playerGuid, _lastComboBaseDamage, _lastContributingDice, dmgEff.ComboMultiplier);

            // Bono at-played de los items passive del inventario para este combo. No entra
            // en Resolve durante el preview (el LastPlayScratch se limpia al inicio del
            // turno), así que lo previsualizamos aparte para mostrarlo en dorado.
            int itemBonus = 0;
            if (ServiceLocator.TryGetService<IInventoryService>(out var inventory) && inventory != null)
                itemBonus = inventory.GetComboDamageBonusPreview(_lastComboId);

            // Mitigación real (weakness + escudo) por separado para base y total, así el
            // "+ N" dorado refleja la contribución de los objetos ya mitigada.
            int shownBase = Mitigate(preMitigation);
            int shownTotal = itemBonus != 0 ? Mitigate(preMitigation + itemBonus) : shownBase;
            int itemPortion = shownTotal - shownBase;

            string formulaText = itemPortion > 0
                ? $"{comboName}: {shownBase} <color=#{ItemBonusColorHex}>+ {itemPortion}</color>"
                : $"{comboName}: {shownBase}";
            RenderLabel(formulaText, shownTotal);
        }

        // Dorado para el bono aportado por los objetos (rich text de TMP).
        private const string ItemBonusColorHex = "FFC93C";

        /// <summary>
        /// Aplica la mitigación real (weakness + escudo) del enemigo apuntado SIN
        /// side-effects, para que el label == golpe que va a recibir. Sin target devuelve
        /// el daño pre-mitigación.
        /// </summary>
        private int Mitigate(int preMitigation)
        {
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
                return ctx.FinalDamage;
            }
            return preMitigation;
        }

        // El pasivo anti-repetición está en Mode Combo (A/B). Si el servicio no está registrado
        // tratamos la regla como apagada (mismo criterio que DamagePipeline).
        private static bool AntiRepeatComboModeActive()
        {
            return ServiceLocator.TryGetService<IAntiRepeatModeService>(out var svc)
                   && svc != null && svc.Mode == AntiRepeatMode.Combo;
        }

        // ¿El combo actual repite el último ya confirmado? Espejo del guard de Preview en
        // DamagePipeline (compara contra IComboLogService.LastCombo). Combo vacío nunca repite.
        private static bool IsRepeatOfLastCombo(string comboId)
        {
            if (string.IsNullOrEmpty(comboId)) return false;
            if (!ServiceLocator.TryGetService<IComboLogService>(out var log) || log == null) return false;
            return log.LastCombo == comboId;
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
                RenderLabel($"{actionTag} - {Rollgeon.Localization.LocalizedContent.Name(combo.ComboId, combo.DisplayName)} ({effective})", effective);
            else
                RenderLabel($"{actionTag} - seleccioná los dados de tu combo", 0);
            return true;
        }

        private void HideThreshold()
        {
            if (_thresholdLabel != null) _thresholdLabel.gameObject.SetActive(false);
        }

        private void ClearFormula()
        {
            if (_feedback != null) _feedback.Clear();
            else if (_formulaLabel != null) _formulaLabel.text = string.Empty;
        }

        /// <summary>
        /// Fija el board type vigente del value text (color + efectos por tipo). Lo empuja
        /// combate vía <c>CombatHUDView</c>; exploración lo deriva del spec activo. Dedup para
        /// no re-lanzar el tween de color en cada update.
        /// </summary>
        public void SetBoardType(DiceBoardType type)
        {
            if (_boardTypeSet && type == _boardType) return;
            _boardType = type;
            _boardTypeSet = true;
            _feedback?.SetBoardType(type);
        }

        // Renderiza el value text por el controller de feedback (color + tag del tipo con amplitud
        // según el valor del combo) si está cableado, o directo al TMP si no.
        private void RenderLabel(string text, int value)
        {
            if (_feedback != null) _feedback.Show(text, value);
            else if (_formulaLabel != null) _formulaLabel.text = text;
        }
    }
}
