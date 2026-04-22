using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Combat.Handoff;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.Movement;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Handler de turno enemigo que evalúa el <c>AIRoot</c> registrado para cada enemigo.
    /// TECHNICAL.md §7.5. Si no hay árbol registrado, cae a <see cref="BasicEnemyAI"/>.
    /// </summary>
    /// <remarks>
    /// Se suscribe a <see cref="EventName.OnTurnQueueBuilt"/> para mantener un contador
    /// de rondas consumido por <see cref="Conditions.AICond_RoundNumber"/>. Al llamar
    /// <see cref="HandleEnemyTurn"/> construye un <see cref="AIContext"/> fresco.
    /// </remarks>
    public sealed class TreeDrivenEnemyAI : IEnemyAIHandler, IDisposable
    {
        private readonly IEnemyAIRegistry _registry;
        private readonly BasicEnemyAI _fallback;
        private readonly Action _onTurnComplete;
        private readonly AttributesManager _attributes;
        private readonly IPlayerService _playerService;
        private readonly IDamagePipeline _damagePipeline;

        private int _roundIndex;
        private bool _subscribed;

        public TreeDrivenEnemyAI(
            IEnemyAIRegistry registry,
            AttributesManager attributes,
            IPlayerService playerService,
            IDamagePipeline damagePipeline,
            BasicEnemyAI fallback,
            Action onTurnComplete)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _damagePipeline = damagePipeline ?? throw new ArgumentNullException(nameof(damagePipeline));
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            _onTurnComplete = onTurnComplete ?? throw new ArgumentNullException(nameof(onTurnComplete));

            EventManager.Subscribe(EventName.OnTurnQueueBuilt, OnTurnQueueBuilt);
            _subscribed = true;
        }

        public int CurrentRoundIndex => _roundIndex;

        public void Dispose()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnTurnQueueBuilt, OnTurnQueueBuilt);
            _subscribed = false;
        }

        public void HandleEnemyTurn(Guid enemyId)
        {
            if (!_registry.TryGet(enemyId, out var root, out var maxHp) || root == null)
            {
                _fallback.HandleEnemyTurn(enemyId);
                return;
            }

            var ctx = BuildContext(enemyId, maxHp);
            try
            {
                root.Tick(ctx);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TreeDrivenEnemyAI] Exception ticking AIRoot for {enemyId}: {ex}");
            }
            finally
            {
                _onTurnComplete();
            }
        }

        private AIContext BuildContext(Guid enemyId, int maxHp)
        {
            ServiceLocator.TryGetService<IGridManager>(out var grid);
            ServiceLocator.TryGetService<IMovementService>(out var movement);

            return new AIContext
            {
                SelfGuid = enemyId,
                PlayerGuid = _playerService.PlayerGuid,
                SelfMaxHp = maxHp,
                Attributes = _attributes,
                DamagePipeline = _damagePipeline,
                Grid = grid,
                Movement = movement,
                PlayerService = _playerService,
                RoundIndex = _roundIndex,
                Rng = null,
            };
        }

        private void OnTurnQueueBuilt(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (args[1] is int idx) _roundIndex = idx + 1; // 1-based for condition UX
        }
    }
}
