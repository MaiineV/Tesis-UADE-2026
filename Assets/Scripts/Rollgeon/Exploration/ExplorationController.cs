using System;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Phase;
using UnityEngine;

namespace Rollgeon.Exploration
{
    /// <summary>
    /// Drives room-to-room exploration within a dungeon floor.
    /// Listens to <see cref="EventName.OnRoomEntered"/> and routes each room
    /// to the appropriate phase transition (combat, shop stub, potion stub).
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
        /// Factory: resolves deps from <see cref="ServiceLocator"/>, creates an instance,
        /// and registers it as <see cref="IExplorationController"/> in
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
            ProcessRoom(_dungeon.CurrentRoom);
        }

        public bool AdvanceRoom()
        {
            if (!_isExploring) return false;

            bool advanced = _dungeon.NextRoom();
            if (!advanced)
                _isExploring = false;

            return advanced;
        }

        public void ResumeAfterCombat()
        {
            _isExploring = true;
            _phase.ReplacePhase(GamePhase.Exploration);
            AdvanceRoom();
        }

        public void Dispose()
        {
            EventManager.UnSubscribe(EventName.OnRoomEntered, OnRoomEntered);
            _isExploring = false;
        }

        private void OnRoomEntered(params object[] args)
        {
            if (!_isExploring) return;
            ProcessRoom(_dungeon.CurrentRoom);
        }

        private void ProcessRoom(RoomSO room)
        {
            if (room == null) return;

            switch (room.Type)
            {
                case RoomType.Combat:
                case RoomType.Boss:
                    _isExploring = false;
                    EventManager.Trigger(EventName.OnCombatTriggered,
                        Guid.NewGuid(), room.RoomId, room.Type);
                    _phase.ReplacePhase(GamePhase.Combat);
                    break;

                case RoomType.Shop:
                    Debug.Log($"[ExplorationController] Shop room entered: {room.DisplayName} (stub)");
                    break;

                case RoomType.Potion:
                    Debug.Log($"[ExplorationController] Potion room entered: {room.DisplayName} (stub)");
                    break;

                case RoomType.Start:
                    break;
            }
        }
    }
}
