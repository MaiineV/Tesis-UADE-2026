using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Actions;
using Rollgeon.Combos;
using Rollgeon.Dice;
using Rollgeon.Dungeon;
using Rollgeon.Entities.Behaviors;
using Rollgeon.Heroes;
using Rollgeon.Player;
using Rollgeon.UI;
using Rollgeon.UI.Screens;
using UnityEngine;

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
        /// <summary>
        /// Path en <c>Resources/</c> al asset de fallback que se carga cuando
        /// <see cref="IPlayerService.DiceBag"/> es <c>null</c>. Asset autoreado por el
        /// usuario (Round3-Checklist) — 5 × D6 para el Guerrero hasta que la pantalla
        /// de build (Fase 2) construya bags reales.
        /// </summary>
        public const string FallbackBagResourcePath = "Dice/AD_Warrior_StartingBag";

        private readonly IDungeonService _dungeon;
        private readonly IPlayerService _player;
        private readonly IEnemySpawnResolver _resolver;
        private readonly IEnemyAIHandler _aiHandler;
        private readonly IScreenManager _screenManager;
        private readonly ICombatStarter _combatStarter;
        private readonly IPlayerCombatActions _playerActions;

        private EventManager.EventReceiver _onCombatTriggeredHandler;
        private bool _disposed;

        private int[] _lastFaces;
        private HeroActionBehavior _selectedBehavior;

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
                var instance = _dungeon.CurrentRoomInstance;
                var room = instance?.Template;
                if (room == null)
                {
                    UnityEngine.Debug.LogWarning(
                        "[CombatHandoffService] CurrentRoom is null — aborting handoff.");
                    return;
                }

                var rng = new System.Random(roomInstanceId.GetHashCode());
                var spawned = _resolver.Resolve(instance, rng);

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
                        RoomInstanceId = roomInstanceId,
                        EncounterDisplayName = room.DisplayName
                    });

                // Cablea delegates del HUD contra IPlayerCombatActions + reroll budget
                // (setup doc UI#0095b §8.7). Sin esto, los clicks del HUD loggean
                // "no cableado" y combat se stucka.
                WireCombatHUDDelegates(firstEnemyId);

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

        private void WireCombatHUDDelegates(Guid firstEnemyId)
        {
            if (_screenManager.Current is not CombatHUDView hud) return;

            var playerGuid = _player.PlayerGuid;
            _lastFaces = null;
            _selectedBehavior = null;

            hud.OnEndTurnRequested = () =>
            {
                if (_selectedBehavior != null) return;

                if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
                    budget.EndBudget();

                var resolved = _lastFaces ?? Array.Empty<int>();
                EventManager.Trigger(EventName.OnRollResolved, playerGuid, (IReadOnlyList<int>)resolved);

                _lastFaces = null;
                _playerActions.EndPlayerTurn();
            };

            hud.OnBehaviorSelected = (int index) =>
            {
                if (_selectedBehavior != null) return;

                var hero = ResolveHero();
                if (hero == null) return;

                HeroActionBehavior behavior;
                if (index < 4)
                    behavior = hero.Actions?.GetByIndex(index);
                else
                    behavior = index - 4 < hero.ContextualBehaviors.Count
                        ? hero.ContextualBehaviors[index - 4]
                        : null;

                if (behavior == null)
                {
                    Debug.LogWarning($"[CombatHandoffService] Behavior index {index} not found.");
                    return;
                }

                if (ServiceLocator.TryGetService<TurnManager>(out var tm) && tm != null)
                {
                    if (!tm.CanExecute(behavior, playerGuid, out var reason))
                    {
                        Debug.Log($"[CombatHandoffService] Cannot execute '{behavior.ActionName}': {reason}");
                        return;
                    }
                }

                _selectedBehavior = behavior;

                if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
                {
                    var wrapper = BuildBudgetAction(behavior);
                    if (wrapper != null)
                        budget.StartBudget(wrapper);
                }

                var bag = ResolvePlayerBag();
                var roller = ResolveRoller();
                if (bag == null || roller == null)
                {
                    Debug.LogError("[CombatHandoffService] No se pudo resolver bag/roller — Roll abortado.");
                    _selectedBehavior = null;
                    return;
                }

                _lastFaces = roller.RollAll(bag);
                EventManager.Trigger(EventName.OnDiceRolled, playerGuid, (IReadOnlyList<int>)_lastFaces);
            };

            hud.OnConfirmRequested = () =>
            {
                if (_selectedBehavior == null) return;

                var hero = ResolveHero();
                BaseComboSO combo = null;
                ComboDetectionResult? comboResult = null;

                if (hero != null && _lastFaces != null)
                {
                    combo = hero.Sheet?.MatchBest(_lastFaces);
                    if (combo != null)
                        comboResult = combo.Detect(_lastFaces);
                }

                var behaviorCtx = new HeroBehaviorContext
                {
                    DiceResult = _lastFaces,
                    MatchedComboResult = comboResult,
                    TargetGuid = firstEnemyId,
                };

                if (ServiceLocator.TryGetService<TurnManager>(out var tm) && tm != null)
                    tm.TryExecute(_selectedBehavior, playerGuid, behaviorCtx);

                if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
                    budget.EndBudget();

                var resolved = _lastFaces ?? Array.Empty<int>();
                EventManager.Trigger(EventName.OnRollResolved, playerGuid, (IReadOnlyList<int>)resolved);

                _lastFaces = null;
                _selectedBehavior = null;
            };

            hud.OnEnergyRerollRequested = () =>
            {
                if (ServiceLocator.TryGetService<IRerollBudgetService>(out var budget) && budget != null)
                    budget.TryExtraRoll(playerGuid);

                var bag = ResolvePlayerBag();
                var roller = ResolveRoller();
                if (bag == null || roller == null)
                {
                    Debug.LogError("[CombatHandoffService] No se pudo resolver bag/roller — Reroll abortado.");
                    return;
                }

                var keep = hud.GetCurrentKeep();
                _lastFaces = roller.Reroll(bag, _lastFaces, keep);
                EventManager.Trigger(EventName.OnDiceRolled, playerGuid, (IReadOnlyList<int>)_lastFaces);
            };
        }

        // Bag del jugador con fallback al asset Resources (Fase 1 — hasta que el
        // BuildSelectionScreen de Fase 2 popule el bag desde un pool por clase).
        private DiceBagSO ResolvePlayerBag()
        {
            var bag = _player?.DiceBag;
            if (bag != null) return bag;

            var fallback = Resources.Load<DiceBagSO>(FallbackBagResourcePath);
            if (fallback == null)
            {
                Debug.LogError($"[CombatHandoffService] IPlayerService.DiceBag null y fallback " +
                               $"'{FallbackBagResourcePath}' no encontrado en Resources/. " +
                               $"Crear el asset (Create → Rollgeon → Dice Bag) o popular DiceBag " +
                               $"vía Fase 2.");
                return null;
            }
            Debug.LogWarning($"[CombatHandoffService] Usando bag fallback '{fallback.name}' — " +
                             $"Fase 2 debería popular IPlayerService.DiceBag desde el build screen.");
            return fallback;
        }

        private ClassHeroSO ResolveHero()
        {
            var hero = _player?.CurrentHero;
            if (hero == null)
            {
                Debug.LogWarning("[CombatHandoffService] IPlayerService.CurrentHero is null — " +
                                 "cannot resolve hero behaviors.");
            }
            return hero;
        }

        private ActionDefinitionSO BuildBudgetAction(HeroActionBehavior behavior)
        {
            var wrapper = UnityEngine.ScriptableObject.CreateInstance<ActionDefinitionSO>();
            wrapper.ActionId = behavior.ActionName;
            wrapper.EnergyCost = behavior.EnergyCost;

            if (behavior.AllowsReroll)
            {
                wrapper.FreeRollCount = behavior.FreeRollCount;
                wrapper.AllowsEnergyReroll = behavior.AllowsEnergyReroll;
            }
            else
            {
                wrapper.FreeRollCount = 1;
                wrapper.AllowsEnergyReroll = false;
            }
            return wrapper;
        }

        private IDiceRoller ResolveRoller()
        {
            if (ServiceLocator.TryGetService<IDiceRoller>(out var roller) && roller != null)
                return roller;
            Debug.LogError("[CombatHandoffService] IDiceRoller no registrado. " +
                           "Agregar DiceRollerBootstrap a ServiceBootstrapSO.ExtraServices.");
            return null;
        }
    }
}
