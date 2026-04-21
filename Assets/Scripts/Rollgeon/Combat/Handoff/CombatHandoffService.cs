using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Dice;
using Rollgeon.Dungeon;
using Rollgeon.Player;
using Rollgeon.UI;
using Rollgeon.UI.Screens;

namespace Rollgeon.Combat.Handoff
{
    /// <summary>
    /// Listens for <see cref="EventName.OnCombatTriggered"/>, resolves enemy
    /// spawns, pushes the CombatHUD screen, and kicks off the combat FSM.
    /// </summary>
    /// <remarks>
    /// Spawn count heuristic: 1 enemy for <see cref="RoomType.Boss"/>,
    /// 2 for <see cref="RoomType.Combat"/>. The resolver handles the actual
    /// weighted roll from the room's <see cref="EnemyPoolSO"/>.
    /// </remarks>
    public sealed class CombatHandoffService : ICombatHandoffService
    {
        private readonly IDungeonService _dungeon;
        private readonly IPlayerService _player;
        private readonly IEnemySpawnResolver _resolver;
        private readonly IEnemyAIHandler _aiHandler;
        private readonly IScreenManager _screenManager;
        private readonly ICombatStarter _combatStarter;
        private readonly IPlayerCombatActions _playerActions;

        private EventManager.EventReceiver _onCombatTriggeredHandler;
        private bool _disposed;

        public bool IsHandoffInProgress { get; private set; }

        public CombatHandoffService(
            IDungeonService dungeon,
            IPlayerService player,
            IEnemySpawnResolver resolver,
            IEnemyAIHandler aiHandler,
            IScreenManager screenManager,
            ICombatStarter combatStarter,
            IPlayerCombatActions playerActions)
        {
            _dungeon = dungeon ?? throw new ArgumentNullException(nameof(dungeon));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _aiHandler = aiHandler ?? throw new ArgumentNullException(nameof(aiHandler));
            _screenManager = screenManager ?? throw new ArgumentNullException(nameof(screenManager));
            _combatStarter = combatStarter ?? throw new ArgumentNullException(nameof(combatStarter));
            _playerActions = playerActions ?? throw new ArgumentNullException(nameof(playerActions));

            _onCombatTriggeredHandler = OnCombatTriggered;
            EventManager.Subscribe(EventName.OnCombatTriggered, _onCombatTriggeredHandler);
        }

        /// <summary>
        /// Factory: resolves dependencies from <see cref="ServiceLocator"/>, creates
        /// an instance, and registers it as <see cref="ICombatHandoffService"/> in
        /// <see cref="ServiceScope.Run"/>.
        /// </summary>
        public static CombatHandoffService CreateAndRegister()
        {
            var dungeon = ServiceLocator.GetService<IDungeonService>();
            var player = ServiceLocator.GetService<IPlayerService>();
            var resolver = ServiceLocator.GetService<IEnemySpawnResolver>();
            var aiHandler = ServiceLocator.GetService<IEnemyAIHandler>();
            var screenManager = ServiceLocator.GetService<IScreenManager>();
            var combatStarter = ServiceLocator.GetService<ICombatStarter>();
            var playerActions = ServiceLocator.GetService<IPlayerCombatActions>();

            var service = new CombatHandoffService(
                dungeon, player, resolver, aiHandler, screenManager, combatStarter, playerActions);

            ServiceLocator.AddService<ICombatHandoffService>(service, ServiceScope.Run);
            return service;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_onCombatTriggeredHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatTriggered, _onCombatTriggeredHandler);
                _onCombatTriggeredHandler = null;
            }
        }

        private void OnCombatTriggered(params object[] args)
        {
            // args: [Guid roomInstanceId, string roomId, RoomType roomType]
            if (args == null || args.Length < 3) return;

            var roomInstanceId = (Guid)args[0];
            var roomType = (RoomType)args[2];

            if (IsHandoffInProgress) return;
            IsHandoffInProgress = true;

            try
            {
                var room = _dungeon.CurrentRoom;
                if (room == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[CombatHandoffService] CurrentRoom is null — aborting handoff.");
                    return;
                }

                int spawnCount = roomType == RoomType.Boss ? 1 : 2;
                var rng = new System.Random(roomInstanceId.GetHashCode());

                var spawned = _resolver.Resolve(room, spawnCount, rng);

                // Build participant list: player + enemies.
                var participants = new List<Guid> { _player.PlayerGuid };
                Guid firstEnemyId = Guid.Empty;

                foreach (var (id, _) in spawned)
                {
                    participants.Add(id);
                    if (firstEnemyId == Guid.Empty)
                        firstEnemyId = id;
                }

                // Push combat HUD screen.
                _screenManager.PushByStringId("CombatHUD",
                    new CombatHUDPayload
                    {
                        EnemyTargetGuid = firstEnemyId,
                        RoomInstanceId = roomInstanceId,
                        EncounterDisplayName = room.DisplayName
                    });

                // Cablea delegates del HUD contra IPlayerCombatActions + reroll budget
                // (setup doc UI#0095b §8.7). Sin esto, los clicks del HUD loggean
                // "no cableado" y combat se stucka.
                WireCombatHUDDelegates();

                // Start the combat FSM.
                _combatStarter.StartCombat(
                    _player.PlayerGuid,
                    participants,
                    roomInstanceId,
                    _aiHandler.HandleEnemyTurn);
            }
            finally
            {
                IsHandoffInProgress = false;
            }
        }

        private void WireCombatHUDDelegates()
        {
            // Silent skip si el screen manager no esta produciendo la HUD view real
            // (tests con stubs de ScreenManager). En produccion, Current == HUD recien
            // pushada. Si estuvieramos en prod y el cast fallara, los clicks del HUD
            // loggearian "no cableado" por si mismos — no duplicamos el warning aca.
            if (_screenManager.Current is not CombatHUDView hud) return;

            hud.OnAttackRequested = _playerActions.SendPlayerAction;
            hud.OnEndTurnRequested = _playerActions.EndPlayerTurn;

            // Roll/Confirm son pasos intra-turno: por ahora los tratamos como
            // "accion del player termino" (mismo input FSM). Si mas adelante se
            // agregan inputs dedicados, desdoblar aca.
            hud.OnRollDiceRequested = _playerActions.SendPlayerAction;
            hud.OnConfirmAttackRequested = _playerActions.SendPlayerAction;

            // Reroll: el HUD dispara gasto de presupuesto de rerolls. Resolvemos
            // lazy porque IRerollBudgetService puede no estar registrado en tests.
            var playerGuid = _player.PlayerGuid;
            hud.OnEnergyRerollRequested = () =>
            {
                if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
                {
                    budget.TryExtraRoll(playerGuid);
                }
            };
        }
    }
}
