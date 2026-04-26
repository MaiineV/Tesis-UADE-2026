using System;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Phase;

namespace Rollgeon.Exploration
{
    /// <summary>
    /// Drives room-to-room exploration dentro de un piso. Listen a
    /// <see cref="EventName.OnRoomEntered"/> (disparado por el DungeonManager
    /// al cruzar una puerta) y rutea la sala activa al transition phase que
    /// corresponda (combat, shop stub, potion stub).
    /// <para>
    /// TECHNICAL.md §13.6 — ya no tiene <c>AdvanceRoom()</c>. La transición
    /// entre salas es driven por puertas via
    /// <see cref="IDungeonService.EnterRoomByDoor"/>.
    /// </para>
    /// </summary>
    public sealed class ExplorationController : IExplorationController, IDisposable
    {
        private readonly IDungeonService _dungeon;
        private readonly IPhaseService _phase;
        private bool _isExploring;

        public bool IsExploring => _isExploring;

        internal ExplorationController(IDungeonService dungeon, IPhaseService phase)
        {
            _dungeon = dungeon ?? throw new ArgumentNullException(nameof(dungeon));
            _phase = phase ?? throw new ArgumentNullException(nameof(phase));

            EventManager.Subscribe(EventName.OnRoomEntered, OnRoomEntered);
        }

        /// <summary>
        /// Factory: resolves deps from <see cref="ServiceLocator"/>, creates an
        /// instance, and registers it as <see cref="IExplorationController"/> en
        /// <see cref="ServiceScope.Run"/>.
        /// </summary>
        public static ExplorationController CreateAndRegister()
        {
            var dungeon = ServiceLocator.GetService<IDungeonService>();
            var phase = ServiceLocator.GetService<IPhaseService>();
            var controller = new ExplorationController(dungeon, phase);
            ServiceLocator.AddService<IExplorationController>(controller, ServiceScope.Run);
            return controller;
        }

        public void BeginExploration()
        {
            if (_isExploring) return;

            _isExploring = true;
            _phase.ReplacePhase(GamePhase.Exploration);
            EventManager.Trigger(EventName.OnExplorationStarted, Guid.NewGuid());
            ProcessRoom(_dungeon.CurrentRoomInstance);
        }

        public void ResumeAfterCombat()
        {
            _isExploring = true;
            UnityEngine.Debug.Log("[ExplorationController] ResumeAfterCombat — _isExploring=true, cambiando fase a Exploration.");
            _phase.ReplacePhase(GamePhase.Exploration);
            // Con el sistema de puertas, la sala ya quedó Cleared y el player
            // sigue in-place. No advance — se espera un EnterRoomByDoor.
        }

        public void Dispose()
        {
            EventManager.UnSubscribe(EventName.OnRoomEntered, OnRoomEntered);
            _isExploring = false;
        }

        private void OnRoomEntered(params object[] args)
        {
            if (!_isExploring) return;
            ProcessRoom(_dungeon.CurrentRoomInstance);
        }

        private void ProcessRoom(RoomInstance instance)
        {
            if (instance?.Template == null) return;
            var room = instance.Template;

            // Salas ya cleareadas (Start, Shop, Potion, o combat re-visitada)
            // no vuelven a disparar combate al entrar.
            if (instance.State == RoomState.Cleared && room.Type != RoomType.Shop
                && room.Type != RoomType.Potion)
            {
                return;
            }

            switch (room.Type)
            {
                case RoomType.Combat:
                case RoomType.Boss:
                    _isExploring = false;
                    EventManager.Trigger(EventName.OnCombatTriggered,
                        instance.InstanceId, room.RoomId, room.Type);
                    _phase.ReplacePhase(GamePhase.Combat);
                    break;

                case RoomType.Shop:
                    UnityEngine.Debug.Log(
                        $"[ExplorationController] Shop room entered: {room.DisplayName} (stub)");
                    break;

                case RoomType.Potion:
                    UnityEngine.Debug.Log(
                        $"[ExplorationController] Potion room entered: {room.DisplayName} (stub)");
                    break;

                case RoomType.Start:
                    break;
            }
        }
    }
}
