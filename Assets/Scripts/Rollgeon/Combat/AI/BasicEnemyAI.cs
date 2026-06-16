using System;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Combat.Handoff;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Combat.AI
{
    /// <summary>
    /// Fallback enemy AI handler that reads the enemy's Attack stat and deals
    /// damage to the player via <see cref="IDamagePipeline"/>. Usado por
    /// <see cref="TreeDrivenEnemyAI"/> cuando un enemigo no tiene <c>AIRoot</c>
    /// autorado (ver TECHNICAL.md §7.5).
    /// </summary>
    /// <remarks>
    /// Enemies with Attack &lt;= 0 (e.g. Support archetype) skip the attack
    /// phase and immediately complete their turn.
    /// </remarks>
    public sealed class BasicEnemyAI : IEnemyAIHandler
    {
        private readonly AttributesManager _attributes;
        private readonly IPlayerService _playerService;
        private readonly IDamagePipeline _damagePipeline;
        private readonly Action _onTurnComplete;

        public BasicEnemyAI(
            AttributesManager attributes,
            IPlayerService playerService,
            IDamagePipeline damagePipeline,
            Action onTurnComplete)
        {
            _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
            _playerService = playerService ?? throw new ArgumentNullException(nameof(playerService));
            _damagePipeline = damagePipeline ?? throw new ArgumentNullException(nameof(damagePipeline));
            _onTurnComplete = onTurnComplete ?? throw new ArgumentNullException(nameof(onTurnComplete));
        }

        public void HandleEnemyTurn(Guid enemyId)
        {
            // Read attack stat — graceful degradation if enemy not registered
            var attackAttr = _attributes.GetAttribute<Attack>(enemyId);
            if (attackAttr == null)
            {
                Debug.LogWarning(
                    $"[BasicEnemyAI] Enemy '{enemyId}' has no Attack attribute — skipping turn.");
                _onTurnComplete();
                return;
            }

            int attackValue = attackAttr.ModifiedValue;

            // Support enemies (Attack <= 0) skip the attack phase
            if (attackValue <= 0)
            {
                Debug.Log(
                    $"[BasicEnemyAI] Enemy '{enemyId}' has Attack={attackValue} — skipping attack (support).");
                _onTurnComplete();
                return;
            }

            Guid playerGuid = _playerService.PlayerGuid;

            var ctx = new DamageContext
            {
                SourceId = enemyId,
                TargetId = playerGuid,
                BaseDamage = attackValue,
                Kind = AttackKind.BasicAttack,
            };

            var result = _damagePipeline.Resolve(ctx);

            Debug.Log(
                $"[BasicEnemyAI] Enemy {enemyId} attacked player for {result.FinalDamage} damage.");

            _onTurnComplete();
        }
    }
}
