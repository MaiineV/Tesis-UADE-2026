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
        // String id de la CombatHUD (ver CombatHUDView.ScreenStringId). El pop de OnCombatEnd
        // debe sacar EXACTAMENTE la CombatHUD y nunca la screen de abajo (ExplorationHUD, que
        // no se re-pushea: reaparece solo porque PopCurrent la re-activa). Un OnCombatEnd
        // duplicado o tardío — p.ej. la victoria diferida de la secuencia de muerte que
        // sobrevive al cierre y llega ya en otro combate — encontraría otra screen al top
        // (ExplorationHUD, o la VictoryScreen del floor cerrado). Sin este guard, PopCurrent
        // sacaría esa screen y el juego queda colgado con todo apagado.
        private const string CombatHudScreenId = "CombatHUD";

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
            // Solo popeamos+resumimos si el CombatHUD es el top actual (ver TryPopCombatHud).
            // En el flujo normal post-boss la victoria se DIFIERE hasta que el player elige una
            // reward en los pedestales, así que acá el top sigue siendo el CombatHUD: lo
            // popeamos y volvemos a exploración para que pueda caminar a los pedestales — igual
            // que en cualquier sala clareada. Si el floor se cerró de inmediato (VictoryScreen
            // ya al top) o el HUD ya se popeó por un OnCombatEnd previo, no tocamos el stack.
            if (!TryPopCombatHud()) return;
            _exploration.ResumeAfterCombat();
        }

        private void HandleDefeat()
        {
            if (!TryPopCombatHud()) return;
            EventManager.Trigger(EventName.OnPlayerDefeated, _player.RunId);
        }

        // Popea la CombatHUD solo si es el top actual del stack. Devuelve true si popeó.
        // Blinda contra un OnCombatEnd duplicado/tardío que, de otro modo, sacaría del stack
        // la ExplorationHUD (o la VictoryScreen) dejando el juego colgado.
        private bool TryPopCombatHud()
        {
            var top = _screenManager.Current;
            if (top == null || top.ScreenStringId != CombatHudScreenId) return false;

            _screenManager.PopCurrent();
            return true;
        }
    }
}
