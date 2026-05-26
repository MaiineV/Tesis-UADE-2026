using System;
using System.Collections;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Screen overlay del Combat HUD. Coordina 8 sub-views + spawner de floating
    /// damage siguiendo el patron <c>Bind(guid)</c> / <c>Unbind()</c> idempotente
    /// de T95a.
    /// Plan §3.1 / §4.1 / TECHNICAL.md §17.D.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>PausesGameplay = false</b>. Otras screens se apilan encima y el HUD
    /// sigue consumiendo eventos (<c>OnLoseFocus</c> / <c>OnGainFocus</c> no-op).
    /// </para>
    /// <para>
    /// <b>Dispatch de acciones</b>. Los botones de las sub-views disparan
    /// <see cref="UnityEngine.Events.UnityEvent"/>s que wireamos en <c>Awake</c>
    /// a los delegates publicos <see cref="OnEnergyRerollRequested"/>,
    /// <see cref="OnEndTurnRequested"/>, <see cref="OnBehaviorSelected"/>,
    /// <see cref="OnConfirmRequested"/>. El <c>CombatController</c> los cablea
    /// al hacer push (setup doc §8.7).
    /// </para>
    /// <para>
    /// <b>Safety net</b>: suscribe <c>OnCombatEnd</c> localmente. Si alguien pushea
    /// este HUD fuera del flujo (editor / preview tool), puede auto-popearse al
    /// recibir el evento. Controlado por <see cref="_autoPopOnCombatEnd"/>.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Combat HUD View")]
    public class CombatHUDView : BaseScreen
    {
        private const string LogPrefix = "[CombatHUDView] ";

        [Title("Combat HUD — Sub-views")]
        [InfoBox("Cablear 8 sub-views + spawner. Null = sub-view skipped con warning.")]
        [Required("Arrastrar TurnQueueView.")]
        [SerializeField]
        private TurnQueueView _turnQueue;

        [Required("Arrastrar DiceZoneView.")]
        [SerializeField]
        private DiceZoneView _diceZone;

        [Required("Arrastrar RerollCountView.")]
        [SerializeField]
        private RerollCountView _rerollCount;

        [Required("Arrastrar FloatingDamageSpawner.")]
        [SerializeField]
        private FloatingDamageSpawner _floatingDamage;

        [Required("Arrastrar PlayerActionButtonsView.")]
        [SerializeField]
        private PlayerActionButtonsView _playerActionButtons;

        [Required("Arrastrar HealthBarView.")]
        [SerializeField]
        private HealthBarView _healthBar;

        [Required("Arrastrar EnergyBarView.")]
        [SerializeField]
        private EnergyBarView _energyBar;

        [Required("Arrastrar EndTurnButtonView.")]
        [SerializeField]
        private EndTurnButtonView _endTurnButtonView;

        [Tooltip("Opcional — muestra la formula de dano del behavior seleccionado.")]
        [SerializeField]
        private DamageFormulaView _damageFormula;

        [Tooltip("Opcional — muestra el shield actual del jugador.")]
        [SerializeField]
        private ShieldBarView _shieldBar;

        [Tooltip("Opcional — muestra la fase actual de un EffChain.")]
        [SerializeField]
        private ChainPhaseIndicatorView _chainPhaseIndicator;

        [Tooltip("Opcional — slots de items activos clickables (ej. poción de healing). " +
                 "Si null, no hay UI de items activos en el combate.")]
        [SerializeField]
        private ActiveItemsView _activeItems;

        [Title("Combat HUD — Damage Flash")]
        [SerializeField]
        [Tooltip("CanvasGroup que flashea cuando el player recibe dano (rojo breve).")]
        private CanvasGroup _damageFlashGroup;

        [MinValue(0f)]
        [SerializeField]
        [Tooltip("Duracion del flash en segundos. Default 0.18.")]
        private float _damageFlashSeconds = 0.18f;

        [Range(0f, 1f)]
        [SerializeField]
        [Tooltip("Alpha maximo del flash (1 = opaco).")]
        private float _damageFlashAlpha = 0.5f;

        [Title("Combat HUD — Safety net")]
        [SerializeField]
        [Tooltip("Si true, el HUD se suscribe a OnCombatEnd y se auto-poppea (warning). " +
                 "Sirve para editor / preview tool. El flujo canonico es que el " +
                 "CombatController llame PopOverlay explicito.")]
        private bool _autoPopOnCombatEnd = true;

        // ======================================================================
        // Action delegates (wired by CombatController — setup doc §8.7)
        // ======================================================================

        /// <summary>Delegate que dispara "energy reroll". Seteado por <c>CombatController</c>.</summary>
        public Action OnEnergyRerollRequested;

        /// <summary>Delegate que dispara "end turn". Seteado por <c>CombatController</c>.</summary>
        public Action OnEndTurnRequested;

        /// <summary>Delegate que dispara seleccion de behavior (index 0-3 = fijo, 4+ = contextual).</summary>
        public Action<int> OnBehaviorSelected;

        /// <summary>Delegate que dispara "confirm" (generico, no solo attack).</summary>
        public Action OnConfirmRequested;

        /// <summary>Delegate que dispara "chain pass" (saltear fases restantes del chain).</summary>
        public Action OnChainPassRequested;

        /// <summary>
        /// Delegate que dispara el primer roll de la accion seleccionada. El HUD
        /// decide entre este y <see cref="OnEnergyRerollRequested"/> via
        /// <c>InvokeRollOrReroll</c> segun el estado del budget.
        /// </summary>
        public Action OnRollRequested;

        /// <inheritdoc/>
        public override string ScreenStringId => "CombatHUD";

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _subViewsBound;

        private Action<DamageResolvedPayload> _onDamageResolved;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            if (_rerollCount != null)
                _rerollCount.OnExtraRollPressed.AddListener(InvokeRollOrReroll);

            if (_playerActionButtons != null)
            {
                _playerActionButtons.OnConfirmPressed.AddListener(InvokeConfirmRequested);
                _playerActionButtons.OnBehaviorSelected = InvokeBehaviorSelected;
            }

            if (_endTurnButtonView != null)
                _endTurnButtonView.OnEndTurnPressed.AddListener(InvokeEndTurnRequested);
        }

        private void OnDestroy()
        {
            if (_rerollCount != null)
                _rerollCount.OnExtraRollPressed.RemoveListener(InvokeRollOrReroll);

            if (_playerActionButtons != null)
            {
                _playerActionButtons.OnConfirmPressed.RemoveListener(InvokeConfirmRequested);
                _playerActionButtons.OnBehaviorSelected = null;
            }

            if (_endTurnButtonView != null)
                _endTurnButtonView.OnEndTurnPressed.RemoveListener(InvokeEndTurnRequested);
        }

        /// <inheritdoc/>
        protected override void OnPushed(IScreenPayload payload)
        {
            ResolvePlayer();

            BindAll(_playerGuid);

            // Safety net: feedback de dano + auto-pop opcional.
            _onDamageResolved = HandleDamageResolvedForFlash;
            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);

            if (_autoPopOnCombatEnd)
            {
                EventManager.Subscribe(EventName.OnCombatEnd, HandleCombatEndSafetyNet);
            }
        }

        /// <inheritdoc/>
        protected override void OnPopped()
        {
            UnbindAll();

            if (_onDamageResolved != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);
                _onDamageResolved = null;
            }
            if (_autoPopOnCombatEnd)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, HandleCombatEndSafetyNet);
            }

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }
            _playerGuid = Guid.Empty;
        }

        // ======================================================================
        // API publica (test hooks + re-target)
        // ======================================================================

        /// <summary>
        /// Llama <c>Bind(guid)</c> en cada sub-view presente. Idempotente.
        /// </summary>
        public void BindAll(Guid playerGuid)
        {
            _playerGuid = playerGuid;

            if (_turnQueue != null) _turnQueue.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_turnQueue no cableado.", this);

            if (_rerollCount != null) _rerollCount.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_rerollCount no cableado.", this);

            if (_floatingDamage != null) _floatingDamage.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_floatingDamage no cableado.", this);

            if (_playerActionButtons != null) _playerActionButtons.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_playerActionButtons no cableado.", this);

            if (_healthBar != null) _healthBar.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_healthBar no cableado.", this);

            if (_energyBar != null) _energyBar.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_energyBar no cableado.", this);

            if (_diceZone != null) _diceZone.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_diceZone no cableado.", this);

            if (_endTurnButtonView != null) _endTurnButtonView.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_endTurnButtonView no cableado.", this);

            if (_damageFormula != null) _damageFormula.Bind(playerGuid);
            if (_shieldBar != null) _shieldBar.Bind(playerGuid);
            if (_chainPhaseIndicator != null) _chainPhaseIndicator.Bind(playerGuid);
            if (_activeItems != null) _activeItems.Bind(playerGuid);

            _subViewsBound = true;
        }

        /// <summary>Llama <c>Unbind</c> en cada sub-view. Idempotente.</summary>
        public void UnbindAll()
        {
            if (_turnQueue != null) _turnQueue.Unbind();
            if (_rerollCount != null) _rerollCount.Unbind();
            if (_floatingDamage != null) _floatingDamage.Unbind();
            if (_playerActionButtons != null) _playerActionButtons.Unbind();
            if (_healthBar != null) _healthBar.Unbind();
            if (_energyBar != null) _energyBar.Unbind();
            if (_diceZone != null) _diceZone.Unbind();
            if (_endTurnButtonView != null) _endTurnButtonView.Unbind();
            if (_damageFormula != null) _damageFormula.Unbind();
            if (_shieldBar != null) _shieldBar.Unbind();
            if (_chainPhaseIndicator != null) _chainPhaseIndicator.Unbind();
            if (_activeItems != null) _activeItems.Unbind();
            _subViewsBound = false;
        }

        /// <summary>
        /// Snapshot del array de holds del <see cref="DiceZoneView"/>. Lo consume el
        /// <c>CombatHandoffService</c> para pasarlo como <c>keep[]</c> al reroll.
        /// Devuelve <c>null</c> si el zone view no está cableado.
        /// </summary>
        public bool[] GetCurrentKeep() => _diceZone != null ? _diceZone.GetHeldStates() : null;

        public void SetBehaviorForFormula(HeroActionBehavior behavior)
        {
            Debug.Log($"{LogPrefix}SetBehaviorForFormula — '{behavior?.ActionName ?? "null"}' _damageFormula={(_damageFormula != null ? "set" : "null")}");
            if (_damageFormula != null) _damageFormula.SetBehavior(behavior);
        }

        public void ClearBehaviorForFormula()
        {
            Debug.Log($"{LogPrefix}ClearBehaviorForFormula");
            if (_damageFormula != null) _damageFormula.ClearBehavior();
        }

        // ======================================================================
        // Internals
        // ======================================================================

        private void ResolvePlayer()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService) || playerService == null)
            {
                Debug.LogWarning(LogPrefix + "IPlayerService no registrado. HUD queda en default.", this);
                _playerGuid = Guid.Empty;
                return;
            }
            _playerGuid = playerService.PlayerGuid;
            if (_playerGuid == Guid.Empty)
            {
                Debug.LogWarning(LogPrefix + "IPlayerService.PlayerGuid = Guid.Empty al push. " +
                                 "Re-pushear tras spawn para rebind.", this);
            }
        }

        private void InvokeEnergyRerollRequested()
        {
            if (OnEnergyRerollRequested == null)
            {
                Debug.LogWarning(LogPrefix + "OnEnergyRerollRequested no cableado.", this);
                return;
            }
            OnEnergyRerollRequested.Invoke();
        }

        /// <summary>
        /// Dispatch del boton compartido "Roll / Reroll" en el HUD. Si el budget
        /// esta abierto y todavia no se rolo (FreeRollsRemaining == FreeRollCount,
        /// PaidRollsUsed == 0) es el primer roll; sino es reroll.
        /// </summary>
        private void InvokeRollOrReroll()
        {
            if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget)
                && budget?.Current != null
                && budget.Current.Action != null
                && budget.Current.FreeRollsRemaining == budget.Current.Action.FreeRollCount
                && budget.Current.PaidRollsUsed == 0)
            {
                if (OnRollRequested == null)
                {
                    Debug.LogWarning(LogPrefix + "OnRollRequested no cableado.", this);
                    return;
                }
                OnRollRequested.Invoke();
                return;
            }
            InvokeEnergyRerollRequested();
        }

        private void InvokeEndTurnRequested()
        {
            if (OnEndTurnRequested == null)
            {
                Debug.LogWarning(LogPrefix + "OnEndTurnRequested no cableado.", this);
                return;
            }
            OnEndTurnRequested.Invoke();
        }

        private void InvokeBehaviorSelected(int index)
        {
            if (OnBehaviorSelected == null)
            {
                Debug.LogWarning(LogPrefix + "OnBehaviorSelected no cableado.", this);
                return;
            }
            OnBehaviorSelected.Invoke(index);
        }

        private void InvokeConfirmRequested()
        {
            if (OnConfirmRequested == null)
            {
                Debug.LogWarning(LogPrefix + "OnConfirmRequested no cableado.", this);
                return;
            }
            OnConfirmRequested.Invoke();
        }

        public void InvokeChainPassRequested()
        {
            if (OnChainPassRequested == null)
            {
                Debug.LogWarning(LogPrefix + "OnChainPassRequested no cableado.", this);
                return;
            }
            OnChainPassRequested.Invoke();
        }

        private void HandleDamageResolvedForFlash(DamageResolvedPayload payload)
        {
            if (payload.TargetGuid != _playerGuid) return;
            TriggerDamageFlash();
        }

        private void HandleCombatEndSafetyNet(params object[] args)
        {
            // Safety net: si el HUD estaba pusheado sin CombatController que lo popee,
            // desbindeamos para no dejar handlers colgados. El pop real lo hace el
            // CombatController via ScreenManager; este handler solo protege flujos
            // edge-case (editor / preview).
            if (_subViewsBound)
            {
                UnbindAll();
            }
        }

        /// <summary>Triggera el flash rojo del damage. Publico para tests / tooling.</summary>
        public void TriggerDamageFlash()
        {
            if (_damageFlashGroup == null) return;
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            if (_damageFlashGroup == null) yield break;

            float half = _damageFlashSeconds * 0.5f;
            float elapsed = 0f;

            // fade-in
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                _damageFlashGroup.alpha = Mathf.Lerp(0f, _damageFlashAlpha, t);
                yield return null;
            }

            // fade-out
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / half);
                _damageFlashGroup.alpha = Mathf.Lerp(_damageFlashAlpha, 0f, t);
                yield return null;
            }
            _damageFlashGroup.alpha = 0f;
            _flashCoroutine = null;
        }
    }
}
