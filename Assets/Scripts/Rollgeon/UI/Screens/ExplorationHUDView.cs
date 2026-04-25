using System;
using Patterns;
using Rollgeon.Player;
using Rollgeon.Run;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Screen overlay persistente durante <c>GamePhase.Exploration</c>. Coordina las
    /// sub-views (<see cref="HealthBarView"/>, <see cref="EnergyBarView"/>,
    /// <see cref="GoldCounterView"/>, <see cref="ActiveItemsView"/>, <see cref="MinimapView"/>,
    /// <see cref="RoomNavigationView"/>) propagando el <c>playerGuid</c> resuelto via
    /// <see cref="IPlayerService"/>.
    /// </summary>
    /// <remarks>
    /// Plan §4.1 / TECHNICAL.md §17.D. No tiene logica propia de render — solo
    /// orquesta el <c>Bind</c>/<c>Unbind</c> de sus hijos. <b>PausesGameplay = false</b>
    /// (implicito): otras screens se apilan encima y el HUD sigue consumiendo eventos
    /// para que, al quitar el overlay, la UI ya este al dia.
    /// <para>
    /// <b>Resolucion del player:</b> intenta <c>IPlayerService.PlayerGuid</c> via
    /// <see cref="ServiceLocator"/> en <c>OnPushed</c>. Si no hay servicio registrado
    /// o el guid es <see cref="Guid.Empty"/>, degrada con warning (hard rule #6) y
    /// espera rebind. El HUD se auto-rebindea suscribiendose a
    /// <c>EventName.OnRunStart</c>: cuando <c>GameplayBootstrapper</c> pushea el HUD
    /// antes de <c>RunBootstrapper.StartRun</c>, el evento dispara despues de que
    /// <c>RunController</c> registra <c>IDungeonService</c>/<c>IExplorationController</c>
    /// y con <c>IPlayerService.PlayerGuid</c> ya set, y el handler local corre
    /// <see cref="BindAll"/> con todo listo.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Exploration HUD View")]
    public class ExplorationHUDView : BaseScreen
    {
        private const string LogPrefix = "[ExplorationHUDView] ";

        [Title("Exploration HUD — Sub-views")]
        [InfoBox("Cablear los 6 widgets del HUD. Null = sub-view skipped con warning " +
                 "(no crashea, pero esa parte del HUD no actualiza).")]
        [Required("Arrastrar el HealthBarView del widget.")]
        [SerializeField]
        private HealthBarView _healthBar;

        [Required("Arrastrar el EnergyBarView del widget.")]
        [SerializeField]
        private EnergyBarView _energyBar;

        [Required("Arrastrar el GoldCounterView del widget.")]
        [SerializeField]
        private GoldCounterView _goldCounter;

        [Required("Arrastrar el ActiveItemsView del widget.")]
        [SerializeField]
        private ActiveItemsView _activeItems;

        [Required("Arrastrar el MinimapView del widget.")]
        [SerializeField]
        private MinimapView _minimap;

        [Required("Arrastrar el RoomNavigationView del widget.")]
        [SerializeField]
        private RoomNavigationView _roomNavigation;

        [Tooltip("Botones de accion para exploracion (Movement, Heal, etc.). Null = sin acciones.")]
        [SerializeField]
        private ExplorationActionButtonsView _explorationActions;

        /// <inheritdoc/>
        public override string ScreenStringId => "ExplorationHUD";

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _subViewsBound;

        private EventManager.EventReceiver _onRunStartHandler;

        /// <inheritdoc/>
        protected override void OnPushed(IScreenPayload payload)
        {
            SubscribeRebindEvents();
            ResolvePlayer();
        }

        /// <inheritdoc/>
        protected override void OnPopped()
        {
            UnsubscribeRebindEvents();
            UnbindAll();
            _playerGuid = Guid.Empty;
        }

        private void SubscribeRebindEvents()
        {
            if (_onRunStartHandler != null) return;

            _onRunStartHandler = _ => ResolvePlayer();
            EventManager.Subscribe(EventName.OnRunStart, _onRunStartHandler);
        }

        private void UnsubscribeRebindEvents()
        {
            if (_onRunStartHandler == null) return;

            EventManager.UnSubscribe(EventName.OnRunStart, _onRunStartHandler);
            _onRunStartHandler = null;
        }

        // OnGainFocus / OnLoseFocus: no-op. El HUD sigue consumiendo eventos aunque
        // otra screen se apile encima — plan §5.3.

        private void ResolvePlayer()
        {
            // Pre-run: el HUD se pushea desde GameplayBootstrapper antes de StartRun.
            // No hay IRunContextService ni servicios de run todavia — silenciar y esperar
            // al handler de OnRunStart que re-ejecuta ResolvePlayer con todo listo.
            if (!ServiceLocator.TryGetService<IRunContextService>(out _))
            {
                _playerGuid = Guid.Empty;
                return;
            }

            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService) || playerService == null)
            {
                Debug.LogWarning(LogPrefix + "IPlayerService no registrado dentro de la run. " +
                                 "Bootstrap debe registrar el servicio antes de StartRun.", this);
                _playerGuid = Guid.Empty;
                BindAll(_playerGuid);
                return;
            }

            _playerGuid = playerService.PlayerGuid;
            if (_playerGuid == Guid.Empty)
            {
                Debug.LogWarning(LogPrefix + "IPlayerService.PlayerGuid = Guid.Empty con run activa. " +
                                 "SetPlayer no corrio antes del bind.", this);
            }
            BindAll(_playerGuid);
        }

        /// <summary>
        /// Llama <c>Bind(guid)</c> en cada sub-view presente. Idempotente — cada
        /// sub-view hace un <c>Unbind</c> interno si ya estaba bound.
        /// </summary>
        public void BindAll(Guid playerGuid)
        {
            _playerGuid = playerGuid;

            if (_healthBar != null) _healthBar.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_healthBar no esta cableado en el Inspector.", this);

            if (_energyBar != null) _energyBar.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_energyBar no esta cableado en el Inspector.", this);

            if (_goldCounter != null) _goldCounter.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_goldCounter no esta cableado en el Inspector.", this);

            if (_activeItems != null) _activeItems.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_activeItems no esta cableado en el Inspector.", this);

            if (_minimap != null) _minimap.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_minimap no esta cableado en el Inspector.", this);

            if (_roomNavigation != null) _roomNavigation.Bind(playerGuid);
            else Debug.LogWarning(LogPrefix + "_roomNavigation no esta cableado en el Inspector.", this);

            if (_explorationActions != null) _explorationActions.Bind(playerGuid);

            _subViewsBound = true;
        }

        /// <summary>Llama <c>Unbind</c> en cada sub-view presente. Idempotente.</summary>
        public void UnbindAll()
        {
            if (_healthBar != null) _healthBar.Unbind();
            if (_energyBar != null) _energyBar.Unbind();
            if (_goldCounter != null) _goldCounter.Unbind();
            if (_activeItems != null) _activeItems.Unbind();
            if (_minimap != null) _minimap.Unbind();
            if (_roomNavigation != null) _roomNavigation.Unbind();
            if (_explorationActions != null) _explorationActions.Unbind();
            _subViewsBound = false;
        }
    }
}
