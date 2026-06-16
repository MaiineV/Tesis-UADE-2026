using System;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Exploration;
using Rollgeon.Player;
using Rollgeon.UI;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Listens for <see cref="EventName.OnCombatEnd"/>, pops the CombatHUD screen,
    /// and routes back to exploration (victory/aborted) or fires defeat (loss).
    /// </summary>
    public sealed class CombatReturnService : ICombatReturnService
    {
        // String id de la VictoryScreen (ver VictoryScreen.ScreenStringId). Lo consultamos
        // para no popear la pantalla de victoria si ya quedó al top del stack durante el
        // mismo OnCombatEnd (floor cerrado de inmediato).
        private const string VictoryScreenId = "VictoryScreen";

        private readonly IExplorationController _exploration;
        private readonly IScreenManager _screenManager;
        private readonly IPlayerService _player;

        private EventManager.EventReceiver _onCombatEndHandler;
        private bool _disposed;

        public CombatReturnService(
            IExplorationController exploration,
            IScreenManager screenManager,
            IPlayerService player)
        {
            _exploration = exploration ?? throw new ArgumentNullException(nameof(exploration));
            _screenManager = screenManager ?? throw new ArgumentNullException(nameof(screenManager));
            _player = player ?? throw new ArgumentNullException(nameof(player));

            _onCombatEndHandler = OnCombatEnd;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
        }

        /// <summary>
        /// Factory: resolves dependencies from <see cref="ServiceLocator"/>, creates
        /// an instance, and registers it as <see cref="ICombatReturnService"/> in
        /// <see cref="ServiceScope.Run"/>.
        /// </summary>
        public static CombatReturnService CreateAndRegister()
        {
            var exploration = ServiceLocator.GetService<IExplorationController>();
            var screenManager = ServiceLocator.GetService<IScreenManager>();
            var player = ServiceLocator.GetService<IPlayerService>();

            var service = new CombatReturnService(exploration, screenManager, player);
            ServiceLocator.AddService<ICombatReturnService>(service, ServiceScope.Run);
            return service;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
        }

        private void OnCombatEnd(params object[] args)
        {
            if (args == null || args.Length < 2) return;

            var roomInstanceId = (Guid)args[0];
            var outcome = (CombatOutcome)args[1];

            switch (outcome)
            {
                case CombatOutcome.Victory:
                    HandleVictory(roomInstanceId);
                    break;
                case CombatOutcome.Defeat:
                    HandleDefeat();
                    break;
                default:
                    HandleVictory(roomInstanceId);
                    break;
            }
        }

        private void HandleVictory(Guid roomInstanceId)
        {
            // Si durante este mismo OnCombatEnd ya se pusheó la VictoryScreen (el floor se
            // cerró de inmediato: boss sin rewards para ofrecer, o build sin el canal de
            // Character Rewards), NO la popeamos: PopCurrent la sacaría del top y el jugador
            // no la vería.
            //
            // En el flujo normal post-boss, la victoria se DIFIERE hasta que el player elige
            // una reward en los pedestales, así que acá el top sigue siendo el CombatHUD: lo
            // popeamos y volvemos a exploración para que pueda caminar a los pedestales —
            // igual que en cualquier sala clareada (las puertas quedan abiertas).
            var top = _screenManager.Current;
            if (top != null && top.ScreenStringId == VictoryScreenId) return;

            _screenManager.PopCurrent();
            _exploration.ResumeAfterCombat();
        }

        private void HandleDefeat()
        {
            _screenManager.PopCurrent();
            EventManager.Trigger(EventName.OnPlayerDefeated, _player.RunId);
        }
    }
}
