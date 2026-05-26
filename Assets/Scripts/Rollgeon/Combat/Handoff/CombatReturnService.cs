using System;
using Patterns;
using Rollgeon.Combat.FSM;
using Rollgeon.Dungeon;
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
            // Si la sala es Boss, el DungeonManager ya disparó OnFloorCleared antes
            // que este handler corra (mismo bus de eventos), y la VictoryScreen ya
            // se pusheó al top del stack. Si hiciéramos PopCurrent + ResumeAfterCombat,
            // popearíamos la VictoryScreen recién pusheada y volveríamos a Exploration —
            // el jugador no vería la pantalla de victoria. Para Boss, salimos sin tocar.
            if (ServiceLocator.TryGetService<IDungeonService>(out var dungeon)
                && dungeon != null
                && dungeon.GetAllRoomInstances().TryGetValue(roomInstanceId, out var instance)
                && instance?.Template != null
                && instance.Template.Type == RoomType.Boss)
            {
                return;
            }

            _screenManager.PopCurrent();
            // OnRoomCleared lo dispara el DungeonManager cuando recibe
            // OnCombatEnd(Victory) — acá solo cerramos el overlay y volvemos
            // a la fase de exploración. El player queda en la sala con las
            // puertas abiertas hasta que decida cruzar una.
            _exploration.ResumeAfterCombat();
        }

        private void HandleDefeat()
        {
            _screenManager.PopCurrent();
            EventManager.Trigger(EventName.OnPlayerDefeated, _player.RunId);
        }
    }
}
