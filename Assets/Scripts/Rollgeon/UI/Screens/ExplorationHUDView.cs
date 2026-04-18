using System;
using Patterns;
using Rollgeon.Player;
using Rollgeon.UI.HUD;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Screen overlay persistente durante <c>GamePhase.Exploration</c>. Coordina las
    /// sub-views (<see cref="HealthBarView"/>, <see cref="EnergyBarView"/>,
    /// <see cref="GoldCounterView"/>, <see cref="ActiveItemsView"/>, <see cref="MinimapView"/>)
    /// propagando el <c>playerGuid</c> resuelto via <see cref="IPlayerService"/>.
    /// </summary>
    /// <remarks>
    /// Plan §4.1 / TECHNICAL.md §17.D. No tiene logica propia de render — solo
    /// orquesta el <c>Bind</c>/<c>Unbind</c> de sus hijos. <b>PausesGameplay = false</b>
    /// (implicito): otras screens se apilan encima y el HUD sigue consumiendo eventos
    /// para que, al quitar el overlay, la UI ya este al dia.
    /// <para>
    /// <b>Resolucion del player:</b> intenta <c>IPlayerService.PlayerGuid</c> via
    /// <see cref="ServiceLocator"/> en <c>OnPushed</c>. Si no hay servicio registrado
    /// o el guid es <see cref="Guid.Empty"/>, degrada con warning (hard rule #6). El
    /// stub de <see cref="IPlayerService"/> de este worktree solo expone
    /// <c>PlayerGuid</c> — los hooks <c>OnPlayerSet</c>/<c>OnPlayerCleared</c> vendran
    /// con F#0008; entretanto, si el HUD se pushea antes del spawn, hay que re-pushear
    /// despues o llamar <see cref="BindAll"/> manualmente.
    /// </para>
    /// </remarks>
    [AddComponentMenu("Rollgeon/UI/Screens/Exploration HUD View")]
    public class ExplorationHUDView : BaseScreen
    {
        private const string LogPrefix = "[ExplorationHUDView] ";

        [Title("Exploration HUD — Sub-views")]
        [InfoBox("Cablear los 5 widgets del HUD. Null = sub-view skipped con warning " +
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

        /// <inheritdoc/>
        public override string ScreenStringId => "ExplorationHUD";

        [ShowInInspector, ReadOnly]
        private Guid _playerGuid;

        [ShowInInspector, ReadOnly]
        private bool _subViewsBound;

        /// <inheritdoc/>
        protected override void OnPushed(IScreenPayload payload)
        {
            ResolvePlayer();
        }

        /// <inheritdoc/>
        protected override void OnPopped()
        {
            UnbindAll();
            _playerGuid = Guid.Empty;
        }

        // OnGainFocus / OnLoseFocus: no-op. El HUD sigue consumiendo eventos aunque
        // otra screen se apile encima — plan §5.3.

        private void ResolvePlayer()
        {
            if (!ServiceLocator.TryGetService<IPlayerService>(out var playerService) || playerService == null)
            {
                // Degradacion graceful (hard rule #6): fallback Guid.Empty + warning.
                Debug.LogWarning(LogPrefix + "IPlayerService no registrado. HUD queda en default. " +
                                 "Cuando F#0008 mergee, el bootstrap debe registrar el servicio real.", this);
                _playerGuid = Guid.Empty;
                BindAll(_playerGuid);
                return;
            }

            _playerGuid = playerService.PlayerGuid;
            if (_playerGuid == Guid.Empty)
            {
                Debug.LogWarning(LogPrefix + "IPlayerService.PlayerGuid = Guid.Empty al momento del push. " +
                                 "HUD queda en default; re-pushear despues del spawn para rebind.", this);
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
            _subViewsBound = false;
        }
    }
}
