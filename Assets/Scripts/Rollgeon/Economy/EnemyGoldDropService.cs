using System;
using System.Collections.Generic;
using Patterns;
using UnityEngine;

namespace Rollgeon.Economy
{
    /// <summary>
    /// Mapea <c>Guid</c> de enemigo spawneado al oro que dropea al morir.
    /// Suscribe a <see cref="EventName.OnEntityDestroyed"/> y, al disparar,
    /// suma el drop al <see cref="IEconomyService"/> y descarta la entry.
    /// </summary>
    /// <remarks>
    /// MVP del FP: el rolling del rango (Min/MaxGoldDrop del <c>EnemyDataSO</c>)
    /// lo hace el <c>DefaultEnemySpawnResolver</c> al registrar el enemigo y
    /// reporta el resultado via <see cref="RegisterDrop"/>. Si un enemigo no
    /// tiene drop registrado, <c>OnEntityDestroyed</c> es no-op para ese guid.
    /// </remarks>
    public sealed class EnemyGoldDropService : IDisposable
    {
        private readonly IEconomyService _economy;
        private readonly Dictionary<Guid, int> _pendingDrops = new Dictionary<Guid, int>();
        private EventManager.EventReceiver _onEntityDestroyedHandler;
        private bool _disposed;

        public EnemyGoldDropService(IEconomyService economy)
        {
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));

            _onEntityDestroyedHandler = OnEntityDestroyed;
            EventManager.Subscribe(EventName.OnEntityDestroyed, _onEntityDestroyedHandler);
        }

        /// <summary>
        /// Registra el oro que dropea <paramref name="entityId"/> cuando muera.
        /// Si <paramref name="amount"/> &lt;= 0 la entry no se guarda. Sobrescribe
        /// si ya existía una entry para ese guid.
        /// </summary>
        public void RegisterDrop(Guid entityId, int amount)
        {
            if (entityId == Guid.Empty || amount <= 0)
            {
                Debug.Log($"[EnemyGoldDropService] RegisterDrop SKIP guid={entityId} amount={amount} (empty or <=0).");
                return;
            }
            _pendingDrops[entityId] = amount;
            Debug.Log($"[EnemyGoldDropService] RegisterDrop guid={entityId} amount={amount}. Pending={_pendingDrops.Count}");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_onEntityDestroyedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnEntityDestroyed, _onEntityDestroyedHandler);
                _onEntityDestroyedHandler = null;
            }
            _pendingDrops.Clear();
        }

        private void OnEntityDestroyed(params object[] args)
        {
            if (args == null || args.Length < 1)
            {
                Debug.LogWarning("[EnemyGoldDropService] OnEntityDestroyed called with empty args.");
                return;
            }
            if (!(args[0] is Guid targetGuid))
            {
                Debug.LogWarning($"[EnemyGoldDropService] OnEntityDestroyed args[0] is not Guid (got {args[0]?.GetType().Name ?? "null"}).");
                return;
            }

            if (!_pendingDrops.TryGetValue(targetGuid, out var amount))
            {
                Debug.Log($"[EnemyGoldDropService] OnEntityDestroyed guid={targetGuid} — no pending drop registered. Pending={_pendingDrops.Count}");
                return;
            }
            _pendingDrops.Remove(targetGuid);

            Debug.Log($"[EnemyGoldDropService] Awarding {amount} gold for entity {targetGuid}. Economy.CurrentGold before={_economy.CurrentGold}");
            _economy.Add(amount);
            Debug.Log($"[EnemyGoldDropService] Economy.CurrentGold after={_economy.CurrentGold}");
        }
    }
}
