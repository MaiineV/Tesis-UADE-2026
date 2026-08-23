using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.ActionRolls;
using Rollgeon.Combat.Damage;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Dice;
using Rollgeon.Effects.Concretes;
using Rollgeon.Heroes;
using Rollgeon.Items;
using Rollgeon.Player;
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

        [Tooltip("Opcional — el 'N × M' que reemplaza al texto en modo daño-por-combo. Esta " +
                 "view sigue siendo dueña de la detección de modo: en daño-combo delega acá; " +
                 "en escudo/action-roll/degradados lo oculta y renderiza su propio label.")]
        [SerializeField] private Rollgeon.UI.HUD.Breakdown.DamageBreakdownView _breakdownView;

        private Guid _playerGuid;
        private HeroActionBehavior _currentBehavior;
        private string _lastComboDisplayName;
        private string _lastComboId;
        private int _lastComboBaseDamage;
        private IReadOnlyList<ContributingDie> _lastContributingDice;
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

            // Los repintados que cayeron durante la secuencia (guard de arriba) se
            // recuperan acá: cuando el gate baja a 0, la view vuelve a reflejar el
            // estado real (típicamente ClearFormula post-confirm).
            Rollgeon.Feedback.BreakdownUiGate.Changed += HandleBreakdownGateChanged;

            _bound = true;
            ClearFormula();
            HideThreshold();
        }

        private void HandleBreakdownGateChanged()
        {
            if (!Rollgeon.Feedback.BreakdownUiGate.Pending) UpdateFormula();
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
            Rollgeon.Feedback.BreakdownUiGate.Changed -= HandleBreakdownGateChanged;
            _currentBehavior = null;
            _lastComboDisplayName = null;
            _lastComboId = null;
            _lastComboBaseDamage = 0;
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
            _lastContributingDice = payload.ContributingDice;
            UpdateFormula();
        }

        private void UpdateFormula()
        {
            if (_formulaLabel == null) return;

            // Mientras corre la secuencia de breakdown, el DamageBreakdownView es del
            // BreakdownSequenceDirector — repintar acá arrancaría con el Hide() de abajo
            // y pisaría los contadores a mitad de animación. Curar lo gatillaba siempre:
            // al confirmar, ActionRollService cambia de fase y dispara este método en
            // plena secuencia (ataque/escudo zafaban porque nada re-entra durante la
            // suya). El repintado diferido llega por HandleBreakdownGateChanged.
            if (Rollgeon.Feedback.BreakdownUiGate.Pending) return;

            // Default: el N×M solo aplica al modo daño-por-combo — la rama de abajo lo
            // re-muestra; cualquier otra rama (action roll, defensa, degradados) lo apaga.
            if (_breakdownView != null) _breakdownView.Hide();

            // Heal N×M (Spec Heal N×M): Curarse entra por ActionRoll igual que Forzar
            // Puerta, pero TryShowActionRollMode() solo sabe renderizar texto plano con
            // CurrentEffectiveTotal — fórmula legacy que NO coincide con lo que cura
            // EffHeal.ResolveBuildDiceAmount (HealBaseTable × ATQ × Σcaras vía
            // PlayerComboHeal.Resolve). Con combo matcheado, paridad exacta con la rama de
            // escudo de abajo: mismo breakdown N×M, misma base (tabla del sheet) y mismo
            // multiplier (perilla del effect). Sin combo (dado holdeado más alto / build
            // dice recién abierto) cae al modo plano de TryShowActionRollMode, que sigue
            // siendo correcto para ese caso.
            var healEff = _currentBehavior?.FindFirstHealEffect();
            if (healEff != null && healEff.UseBuildDice && !string.IsNullOrEmpty(_lastComboId)
                && _actionRollService != null && _actionRollService.IsActive && _breakdownView != null)
            {
                HideThreshold();
                string healComboName = !string.IsNullOrEmpty(_lastComboDisplayName)
                    ? _lastComboDisplayName : "Combo";
                var preview = ResolveHealPreviewArgs(ResolvePlayerHealBase(_lastComboId), healEff);
                _breakdownView.SetComboName(healComboName);
                _breakdownView.ShowPreview(preview.Base, preview.Multiplier);
                ClearLabelKeepingBreakdown();
                return;
            }

            // Si hay una ActionRoll activa, mostrar threshold + combo seleccionado y SALIR
            // (no se evalúa la fórmula de daño, que no aplica para Heal/ForceDoor).
            if (TryShowActionRollMode()) return;

            // Modo defensa: la tirada activa genera ESCUDO, no daño. Entra por dos
            // caminos: fase >0 de un chain (evento OnChainPhaseStarted) o un behavior
            // standalone sin daño y con escudo (la acción Defense — chain de 1 fase,
            // que nunca emite OnChainPhaseStarted). Se recomputa en vivo con la MISMA
            // fórmula compartida que la aplicación real (anti-drift, igual que la rama
            // de ataque). Misma nota de orden que abajo: Resolve lee LastComboScratch
            // poblado por el mismo ComboMatchedPayload.
            bool standaloneDefense = _currentBehavior != null
                && _currentBehavior.FindFirstDealDamageEffect() == null
                && _currentBehavior.FindFirstAddShieldEffect() != null;
            if (_inDefensePhase || standaloneDefense)
            {
                HideThreshold();
                if (string.IsNullOrEmpty(_lastComboId))
                {
                    RenderLabel("Defensa - arma un combo para generar escudo", 0);
                    return;
                }

                string shieldComboName = !string.IsNullOrEmpty(_lastComboDisplayName)
                    ? _lastComboDisplayName : "Combo";
                var shieldEff = _currentBehavior?.FindFirstAddShieldEffect();

                // Paridad con ataque: el breakdown N×M toma el preview de defensa
                // (N = base de la tabla de escudo del combo, M = perilla de la habilidad);
                // el resto llega volando en la secuencia del confirm. El label viejo queda
                // solo como fallback sin _breakdownView cableado.
                if (_breakdownView != null)
                {
                    _breakdownView.SetComboName(shieldComboName);
                    _breakdownView.ShowPreview(ResolvePlayerShieldBase(_lastComboId),
                        shieldEff?.ComboMultiplier ?? 1f);
                    ClearLabelKeepingBreakdown();
                    return;
                }
                int shieldPreview = PlayerComboShield.Resolve(
                    _playerGuid, ResolvePlayerShieldBase(_lastComboId),
                    _lastContributingDice, shieldEff?.ComboMultiplier ?? 1f, out var shieldBd);

                // Bono at-played de items: entra al escudo real (BeginPlay abre la ventana
                // por fase, también en defensa) — se previsualiza en dorado como en ataque.
                // Con preview 0 (sin entrada en tabla o bloqueado) el efecto no aplica nada,
                // así que el bono tampoco se muestra. Sin Mitigate: el escudo no pasa por
                // el DamagePipeline.
                int shieldItemBonus = 0;
                if (shieldPreview > 0
                    && ServiceLocator.TryGetService<IInventoryService>(out var shieldInv)
                    && shieldInv != null)
                    shieldItemBonus = shieldInv.GetComboDamageBonusPreview(_lastComboId);

                // v3: el bono entra a N y escala por M — mismo redondeo que el golpe real.
                int shieldTotal = shieldItemBonus > 0
                    ? PlayerComboDamage.RoundNxM(shieldBd.N + shieldItemBonus, shieldBd.M)
                    : shieldPreview;
                int shieldItemPortion = shieldTotal - shieldPreview;

                string shieldText = shieldItemPortion > 0
                    ? $"{shieldComboName}: escudo {shieldPreview} <color=#{ItemBonusColorHex}>+ {shieldItemPortion}</color>"
                    : $"{shieldComboName}: escudo {shieldPreview}";
                RenderLabel(shieldText, shieldTotal);
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

            // Modo N×M: el breakdown muestra los contadores iniciales (N = base del combo,
            // M = perilla de la habilidad) y el resto llega volando en la secuencia del
            // confirm. El label viejo queda vacío mientras el breakdown esté a cargo.
            if (_breakdownView != null)
            {
                _breakdownView.SetComboName(comboName);
                _breakdownView.ShowPreview(_lastComboBaseDamage, dmgEff.ComboMultiplier);
                ClearLabelKeepingBreakdown();
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
                _playerGuid, _lastComboBaseDamage, _lastContributingDice, dmgEff.ComboMultiplier,
                PlayerComboFormulaKind.Damage, out var bd);

            // Bono at-played de los items passive del inventario para este combo. No entra
            // en Resolve durante el preview (el LastPlayScratch se limpia al inicio del
            // turno), así que lo previsualizamos aparte para mostrarlo en dorado.
            // Limitación conocida: GetComboDamageBonusPreview solo suma EffAddComboBonus —
            // un item at-played MULTIPLICATIVO sigue sin previsualizarse (follow-up).
            int itemBonus = 0;
            if (ServiceLocator.TryGetService<IInventoryService>(out var inventory) && inventory != null)
                itemBonus = inventory.GetComboDamageBonusPreview(_lastComboId);

            // v3: el bono de item entra a N y escala por M — igual que hará el golpe real
            // cuando el item escriba al play scratch (en v2 se sumaba POST-fórmula).
            int preWithItems = itemBonus != 0 && !bd.Blocked
                ? PlayerComboDamage.RoundNxM(bd.N + itemBonus, bd.M)
                : preMitigation;

            // Mitigación real (weakness + escudo) por separado para base y total, así el
            // "+ N" dorado refleja la contribución de los objetos ya mitigada.
            int shownBase = Mitigate(preMitigation);
            int shownTotal = itemBonus != 0 ? Mitigate(preWithItems) : shownBase;
            int itemPortion = shownTotal - shownBase;

            string formulaText = itemPortion > 0
                ? $"{comboName}: {shownBase} <color=#{ItemBonusColorHex}>+ {itemPortion}</color>"
                : $"{comboName}: {shownBase}";
            RenderLabel(formulaText, shownTotal);
        }

        // Dorado para el bono aportado por los objetos (rich text de TMP).
        private const string ItemBonusColorHex = "FFC93C";

        // Sheet del player como fuente de la tabla de escudo — mismo criterio que
        // EffAddShield.ResolveComboShield, para que preview y aplicación lean la misma base.
        private static int ResolvePlayerShieldBase(string comboId)
        {
            var sheet = ServiceLocator.TryGetService<IPlayerService>(out var player)
                ? player?.CurrentHero?.Sheet
                : null;
            return sheet?.GetShieldBase(comboId) ?? 0;
        }

        // Espejo de ResolvePlayerShieldBase para curación — sheet del player como fuente de
        // la HealBaseTable, mismo criterio que EffHeal.ResolveBuildDiceAmount, para que
        // preview y aplicación real lean la misma base.
        private static int ResolvePlayerHealBase(string comboId)
        {
            var sheet = ServiceLocator.TryGetService<IPlayerService>(out var player)
                ? player?.CurrentHero?.Sheet
                : null;
            return sheet?.GetHealBase(comboId) ?? 0;
        }

        // Combina base de tabla + perilla de habilidad en los args que consume
        // DamageBreakdownView.ShowPreview. Extraído de la rama de heal (que depende de
        // ServiceLocator/MonoBehaviour) para poder testear la combinación sin pasar por
        // Bind/SetBehavior — mismo par (base, multiplier) que arma la rama de escudo inline.
        public static (int Base, float Multiplier) ResolveHealPreviewArgs(
            int healBaseFromSheet, EffHeal healEffect)
            => (healBaseFromSheet, healEffect?.ComboMultiplier ?? 1f);

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

        private bool TryShowActionRollMode()
        {
            if (_actionRollService == null || !_actionRollService.IsActive) return false;
            var spec = _actionRollService.CurrentSpec;

            // Threshold label visible con el puntaje a superar. Acciones sin umbral
            // (Curarse N×M usa Threshold 0) no muestran un "Necesitas >= 0" fantasma.
            if (_thresholdLabel != null)
            {
                bool hasThreshold = spec.Threshold > 0;
                _thresholdLabel.gameObject.SetActive(hasThreshold);
                if (hasThreshold) _thresholdLabel.text = $"Necesitas >= {spec.Threshold}";
            }

            // Formula label: combo actual seleccionado del action roll service.
            var combo = _actionRollService.CurrentCombo;
            int effective = _actionRollService.CurrentEffectiveTotal;
            string actionTag = string.IsNullOrEmpty(spec.ActionLabel) ? "Acción" : spec.ActionLabel;

            if (combo != null)
                RenderLabel($"{actionTag} - {Rollgeon.Localization.LocalizedContent.Name(combo.ComboId, combo.DisplayName)} ({effective})", effective);
            else
                RenderLabel($"{actionTag} - selecciona los dados de tu combo", 0);
            return true;
        }

        private void HideThreshold()
        {
            if (_thresholdLabel != null) _thresholdLabel.gameObject.SetActive(false);
        }

        private void ClearFormula()
        {
            if (_breakdownView != null) _breakdownView.Hide();
            ClearLabelKeepingBreakdown();
        }

        // Limpia SOLO el label de texto (el breakdown, si está mostrado, queda a cargo).
        private void ClearLabelKeepingBreakdown()
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
