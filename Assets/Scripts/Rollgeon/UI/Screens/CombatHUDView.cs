using System;
using System.Collections;
using Patterns;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Screen overlay del Combat HUD. Coordina 6 sub-views + spawner de floating
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
    /// <b>Dispatch de acciones</b>. Los botones del <see cref="PlayerActionButtonsView"/>
    /// disparan <see cref="UnityEngine.Events.UnityEvent"/>s que wireamos en
    /// <c>Awake</c> a los delegates publicos <see cref="OnRollDiceRequested"/>,
    /// <see cref="OnConfirmAttackRequested"/>, <see cref="OnEnergyRerollRequested"/>,
    /// <see cref="OnEndTurnRequested"/>. El <c>CombatController</c> los cablea
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
        [InfoBox("Cablear 6 sub-views + spawner. Null = sub-view skipped con warning.")]
        [Required("Arrastrar TurnQueueView.")]
        [SerializeField]
        private TurnQueueView _turnQueue;

        [Required("Arrastrar ComboIndicatorView.")]
        [SerializeField]
        private ComboIndicatorView _comboIndicator;

        [Required("Arrastrar EnemyPanelView.")]
        [SerializeField]
        private EnemyPanelView _enemyPanel;

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

        /// <summary>Delegate que dispara "roll dice" en la FSM. Seteado por <c>CombatController</c>.</summary>
        public Action OnRollDiceRequested;

        /// <summary>Delegate que dispara "confirm attack" en la FSM. Seteado por <c>CombatController</c>.</summary>
        public Action OnConfirmAttackRequested;

        /// <inheritdoc/>
        public override string ScreenStringId => "CombatHUD";

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private Guid _enemyTarget;

        [ShowInInspector, ReadOnly]
        private bool _subViewsBound;

        private Action<DamageResolvedPayload> _onDamageResolved;
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            if (_rerollCount != null)
                _rerollCount.OnExtraRollPressed.AddListener(InvokeEnergyRerollRequested);

            if (_playerActionButtons != null)
            {
                _playerActionButtons.OnRollDicePressed.AddListener(InvokeRollDiceRequested);
                _playerActionButtons.OnRerollPressed.AddListener(InvokeEnergyRerollRequested);
                _playerActionButtons.OnConfirmAttackPressed.AddListener(InvokeConfirmAttackRequested);
                _playerActionButtons.OnEndTurnPressed.AddListener(InvokeEndTurnRequested);
            }
        }

        private void OnDestroy()
        {
            if (_rerollCount != null)
                _rerollCount.OnExtraRollPressed.RemoveListener(InvokeEnergyRerollRequested);

            if (_playerActionButtons != null)
            {
                _playerActionButtons.OnRollDicePressed.RemoveListener(InvokeRollDiceRequested);
                _playerActionButtons.OnRerollPressed.RemoveListener(InvokeEnergyRerollRequested);
                _playerActionButtons.OnConfirmAttackPressed.RemoveListener(InvokeConfirmAttackRequested);
                _playerActionButtons.OnEndTurnPressed.RemoveListener(InvokeEndTurnRequested);
            }
        }

        /// <inheritdoc/>
        protected override void OnPushed(IScreenPayload payload)
        {
            ResolvePlayer();

            _enemyTarget = Guid.Empty;
            if (payload is CombatHUDPayload p)
            {
                _enemyTarget = p.EnemyTargetGuid;
                if (!string.IsNullOrEmpty(p.EncounterDisplayName) && _enemyPanel != null)
                {
                    _enemyPanel.SetNameText(p.EncounterDisplayName);
                }
            }

            BindAll(_playerGuid);

            // Aplicar target despues del bind (EnemyPanelView.SetTarget requiere que Bind
            // haya corrido para que el filtro por targetGuid funcione).
            if (_enemyPanel != null) _enemyPanel.SetTarget(_enemyTarget);

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
            _enemyTarget = Guid.Empty;
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

            if (_comboIndicator != null) _comboIndicator.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_comboIndicator no cableado.", this);

            if (_enemyPanel != null) _enemyPanel.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_enemyPanel no cableado.", this);

            if (_rerollCount != null) _rerollCount.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_rerollCount no cableado.", this);

            if (_floatingDamage != null) _floatingDamage.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_floatingDamage no cableado.", this);

            if (_playerActionButtons != null) _playerActionButtons.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_playerActionButtons no cableado.", this);

            // DiceZoneView no tiene Bind — no-op (plan §3.6).

            _subViewsBound = true;
        }

        /// <summary>Llama <c>Unbind</c> en cada sub-view. Idempotente.</summary>
        public void UnbindAll()
        {
            if (_turnQueue != null) _turnQueue.Unbind();
            if (_comboIndicator != null) _comboIndicator.Unbind();
            if (_enemyPanel != null) _enemyPanel.Unbind();
            if (_rerollCount != null) _rerollCount.Unbind();
            if (_floatingDamage != null) _floatingDamage.Unbind();
            if (_playerActionButtons != null) _playerActionButtons.Unbind();
            _subViewsBound = false;
        }

        /// <summary>Re-target del enemy panel. Invocado por el <c>CombatController</c>.</summary>
        public void SetEnemyTarget(Guid enemyGuid)
        {
            _enemyTarget = enemyGuid;
            if (_enemyPanel != null) _enemyPanel.SetTarget(enemyGuid);
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

        private void InvokeEndTurnRequested()
        {
            if (OnEndTurnRequested == null)
            {
                Debug.LogWarning(LogPrefix + "OnEndTurnRequested no cableado.", this);
                return;
            }
            OnEndTurnRequested.Invoke();
        }

        private void InvokeRollDiceRequested()
        {
            if (OnRollDiceRequested == null)
            {
                Debug.LogWarning(LogPrefix + "OnRollDiceRequested no cableado.", this);
                return;
            }
            OnRollDiceRequested.Invoke();
        }

        private void InvokeConfirmAttackRequested()
        {
            if (OnConfirmAttackRequested == null)
            {
                Debug.LogWarning(LogPrefix + "OnConfirmAttackRequested no cableado.", this);
                return;
            }
            OnConfirmAttackRequested.Invoke();
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
