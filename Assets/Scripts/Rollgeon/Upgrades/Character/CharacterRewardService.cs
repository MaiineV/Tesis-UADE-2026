using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Effects;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Upgrades.Character
{
    /// <summary>
    /// Service Global del Canal Personaje. Combina dos responsabilidades:
    /// </summary>
    /// <list type="bullet">
    /// <item><description><b>Application.</b> <see cref="Apply"/> crea
    /// <c>Modifier&lt;int&gt;</c> Run-scoped + Intrinsic + Add y lo agrega al
    /// stat target del player.</description></item>
    /// <item><description><b>Room flow.</b> Listener de <c>OnRoomCleared</c> /
    /// <c>OnRoomEntered</c> que spawnea N pedestales (default 3) en una Boss
    /// room post-clear, persiste qué rewards rolleó, y despawnea los hermanos
    /// al claim.</description></item>
    /// </list>
    /// <remarks>
    /// <para>
    /// <b>Persistence.</b> Cada pedestal tiene su <see cref="CharacterRewardState"/>
    /// en <c>RoomInstance.ObjectStates</c> con <c>ReservedRewardId</c> +
    /// <c>Claimed</c>. Re-entrada a una sala con claim previo no respawnea nada.
    /// </para>
    /// </remarks>
    public sealed class CharacterRewardService : ICharacterRewardService, IDisposable
    {
        private const string LogPrefix = "[CharacterRewardService] ";
        private const int DefaultSlotsPerBoss = 3;
        private const string SpawnPointPrefix = "char_reward_";

        private readonly CharacterRewardPoolSO _pool;
        private readonly GameObject _pedestalPrefab;
        private readonly int _slotsPerBoss;
        private readonly Vector3 _visualOffset;
        private readonly List<CharacterRewardSO> _claimedRewards = new List<CharacterRewardSO>();
        private readonly HashSet<Guid> _spawnedInRoom = new HashSet<Guid>();
        private System.Random _rng;

        private EventManager.EventReceiver _onRoomClearedHandler;
        private EventManager.EventReceiver _onRoomEnteredHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        public IReadOnlyList<CharacterRewardSO> ClaimedRewards => _claimedRewards;

        public CharacterRewardService(
            CharacterRewardPoolSO pool,
            GameObject pedestalPrefab,
            int slotsPerBoss = DefaultSlotsPerBoss,
            Vector3? visualOffset = null)
        {
            _pool = pool;
            _pedestalPrefab = pedestalPrefab;
            _slotsPerBoss = Mathf.Max(1, slotsPerBoss);
            _visualOffset = visualOffset ?? new Vector3(0f, 1.5f, 0f);
            _rng = new System.Random();

            _onRoomClearedHandler = OnRoomClearedHandler;
            _onRoomEnteredHandler = OnRoomEnteredHandler;
            _onRunEndHandler = OnRunEndHandler;
            EventManager.Subscribe(EventName.OnRoomCleared, _onRoomClearedHandler);
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
        }

        public void Dispose()
        {
            if (_onRoomClearedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRoomCleared, _onRoomClearedHandler);
                _onRoomClearedHandler = null;
            }
            if (_onRoomEnteredHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
                _onRoomEnteredHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }
            _spawnedInRoom.Clear();
            _claimedRewards.Clear();
        }

        private void OnRunEndHandler(params object[] args)
        {
            // Limpiamos state in-memory entre runs — los modifiers Run-lifetime ya se
            // limpian solos al RunEnd (vía Modifier&lt;T&gt;.OnLoad subscription).
            _spawnedInRoom.Clear();
            _claimedRewards.Clear();
        }

        // ====================================================================
        // ICharacterRewardService — public API
        // ====================================================================

        public bool Apply(CharacterRewardSO reward)
        {
            if (reward == null) return false;

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null)
            {
                Debug.LogWarning(LogPrefix + "AttributesManager no registrado — reward no aplicado.");
                return false;
            }
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps == null
                || ps.PlayerGuid == Guid.Empty)
            {
                Debug.LogWarning(LogPrefix + "PlayerService no listo — reward no aplicado.");
                return false;
            }

            int amount = ReadAmount(reward, ps.PlayerGuid);
            if (amount == 0)
            {
                Debug.LogWarning(LogPrefix + $"Reward '{reward.UpgradeId}' resolvió amount=0 — se omite.");
                return false;
            }

            var modifier = new Modifier<int>(
                amount: amount,
                op: ModifierOperation.Add,
                duration: 0, // unused para lifetime Run
                carrierId: ps.PlayerGuid,
                sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Run,
                tickEvent: EventName.OnTurnFinished // unused para lifetime Run
            );

            bool ok = ApplyModifierToStat(reward.TargetStat, attrs, ps.PlayerGuid, modifier);
            if (!ok)
            {
                Debug.LogWarning(LogPrefix + $"AddModifier falló para stat {reward.TargetStat} — reward no aplicado.");
                return false;
            }

            _claimedRewards.Add(reward);
            EventManager.Trigger(EventName.OnItemObtained, ps.PlayerGuid, reward.UpgradeId);
            return true;
        }

        public void NotifyPedestalClaimed(Guid roomInstanceId, string spawnPointId)
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return;
            if (!dungeon.GetAllRoomInstances().TryGetValue(roomInstanceId, out var room)) return;

            // Resolver el reward del slot reclamado.
            if (!room.ObjectStates.TryGet<CharacterRewardState>(spawnPointId, out var claimedState))
            {
                Debug.LogWarning(LogPrefix + $"NotifyPedestalClaimed: state no encontrado para slot '{spawnPointId}'.");
                return;
            }
            if (claimedState.Claimed)
            {
                // Idempotente — alguien ya hizo claim, ignoramos.
                return;
            }

            var reward = ResolveRewardById(claimedState.ReservedRewardId);
            if (reward == null)
            {
                Debug.LogWarning(LogPrefix + $"Reward id '{claimedState.ReservedRewardId}' no encontrado en el pool.");
                return;
            }

            // BUG-017: marcar Claimed ANTES de Apply para cerrar el TOCTOU contra los
            // pedestales hermanos. Sin esto, si el frame procesa Update() de varios
            // pedestales en sucesión (3 pedestales reciben el mismo wasPressedThisFrame
            // de F), todos pasaban la guard de Claimed=false antes de que el primero
            // llegue a MarkAllSlotsClaimedAndDespawn. Si Apply falla, rollback.
            claimedState.Claimed = true;

            if (!Apply(reward))
            {
                claimedState.Claimed = false;
                return;
            }

            // Marcar TODOS los slots de la room como claimed + destruir pedestales hermanos.
            MarkAllSlotsClaimedAndDespawn(room);

            // Reward elegida tras el boss → floor completo. Disparamos OnFloorCleared para
            // que reaccione la VictoryScreen. En esta versión es el fin de la run; a futuro
            // será el handoff al próximo floor. floorIndex=0 placeholder hasta multi-floor.
            EventManager.Trigger(EventName.OnFloorCleared, roomInstanceId, 0);
        }

        // ====================================================================
        // Event handlers — room flow
        // ====================================================================

        private void OnRoomClearedHandler(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid roomId)) return;
            EnsurePedestals(roomId);
        }

        private void OnRoomEnteredHandler(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid roomId)) return;
            EnsurePedestals(roomId);
        }

        /// <summary>
        /// Punto unificado: si la room es Boss cleareada y tiene rewards no
        /// claimed (o ninguno rolled todavía), spawnea / hydrata pedestales.
        /// Idempotente por <see cref="_spawnedInRoom"/>.
        /// </summary>
        private void EnsurePedestals(Guid roomId)
        {
            if (_spawnedInRoom.Contains(roomId)) return;
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return;
            if (!dungeon.GetAllRoomInstances().TryGetValue(roomId, out var room)) return;
            if (room.Template == null || room.Template.Type != RoomType.Boss) return;
            if (room.State != RoomState.Cleared) return;

            // Si algún slot ya está claimed, no spawneamos nada (run irrevocable).
            if (HasAnyClaimedState(room))
            {
                _spawnedInRoom.Add(roomId);
                return;
            }

            int spawned = InitializeOrHydrate(room);
            _spawnedInRoom.Add(roomId);

            // Boss clareada pero sin rewards para ofrecer (pool vacío, sin RewardSpawnPoints,
            // o prefab faltante): no hay nada que elegir, así que cerramos el floor de una.
            // Sin esto el player quedaría atascado — el DungeonManager nos delega la victoria
            // cuando este canal está activo.
            if (spawned == 0)
            {
                EventManager.Trigger(EventName.OnFloorCleared, roomId, 0);
            }
        }

        private int InitializeOrHydrate(RoomInstance room)
        {
            var spawnPoints = ResolveRewardSpawnPoints(room);
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning(LogPrefix + $"Boss room '{room.Template?.RoomId}' sin RewardSpawnPoints — no spawn.");
                return 0;
            }

            int slotCount = Mathf.Min(spawnPoints.Count, _slotsPerBoss);

            // Hydrate path: si existen states previos para los keys, usarlos.
            // Sino: rolear nuevos rewards distintos.
            var rolledRewards = new List<CharacterRewardSO>(slotCount);
            var exclude = new HashSet<CharacterRewardSO>();

            int spawned = 0;
            int floorDepth = 0; // placeholder hasta multi-floor wiring
            for (int i = 0; i < slotCount; i++)
            {
                string key = SpawnPointKey(i);
                CharacterRewardSO reward;
                if (room.ObjectStates.TryGet<CharacterRewardState>(key, out var existing))
                {
                    reward = ResolveRewardById(existing.ReservedRewardId);
                    if (reward == null)
                    {
                        Debug.LogWarning(LogPrefix + $"State referenciaba reward '{existing.ReservedRewardId}' " +
                                                     "no encontrado en el pool — slot se omite.");
                        continue;
                    }
                }
                else
                {
                    if (_pool == null)
                    {
                        Debug.LogWarning(LogPrefix + "CharacterRewardPoolSO no asignado — no se spawnea.");
                        return spawned;
                    }
                    reward = _pool.Roll(_rng, floorDepth, exclude);
                    if (reward == null) continue;
                    room.ObjectStates.Set(key, new CharacterRewardState
                    {
                        SpawnPointId = key,
                        ReservedRewardId = reward.UpgradeId,
                        Claimed = false,
                    });
                }
                exclude.Add(reward);
                rolledRewards.Add(reward);
                if (SpawnPedestal(room, key, spawnPoints[i], reward)) spawned++;
            }
            return spawned;
        }

        /// <summary>Instancia un pedestal interactuable. Devuelve <c>true</c> si quedó un
        /// pedestal con el que el player puede interactuar; <c>false</c> si faltó prefab,
        /// spawn point o el componente requerido.</summary>
        private bool SpawnPedestal(RoomInstance room, string spawnPointKey, Transform spawnPoint, CharacterRewardSO reward)
        {
            if (_pedestalPrefab == null)
            {
                Debug.LogWarning(LogPrefix + "PedestalPrefab no asignado — no se instancia visual.");
                return false;
            }
            if (spawnPoint == null) return false;

            Transform parent = room.SpawnedPrefab != null ? room.SpawnedPrefab.transform : null;
            var go = UnityEngine.Object.Instantiate(_pedestalPrefab, spawnPoint.position, spawnPoint.rotation, parent);
            go.name = $"[CharacterRewardPedestal] {reward.DisplayName ?? reward.UpgradeId}";

            var pedestal = go.GetComponent<CharacterRewardPedestalInteractable>();
            if (pedestal == null)
            {
                Debug.LogError(LogPrefix + "PedestalPrefab no tiene CharacterRewardPedestalInteractable.");
                return false;
            }
            pedestal.Configure(room.InstanceId, spawnPointKey, this, reward);

            SpawnRewardVisualOnTop(go.transform, reward);
            return true;
        }

        /// <summary>
        /// Instancia el <see cref="CharacterRewardSO.WorldPrefab"/> como hijo del
        /// pedestal — mismo patrón que <c>ShopManagerService.SpawnItemVisualOnTop</c>.
        /// Sin esto, el pedestal queda solo y no se ve qué reward es.
        /// </summary>
        private void SpawnRewardVisualOnTop(Transform pedestalRoot, CharacterRewardSO reward)
        {
            if (pedestalRoot == null || reward?.WorldPrefab == null) return;

            var visual = UnityEngine.Object.Instantiate(reward.WorldPrefab, pedestalRoot);
            visual.transform.localPosition = _visualOffset;
            visual.transform.localRotation = Quaternion.identity;
            visual.name = $"[CharacterRewardVisual] {reward.DisplayName ?? reward.UpgradeId}";
        }

        private void MarkAllSlotsClaimedAndDespawn(RoomInstance room)
        {
            // Marcar todos los CharacterRewardState como claimed + destruir pedestales encontrados como hijos.
            for (int i = 0; i < _slotsPerBoss; i++)
            {
                string key = SpawnPointKey(i);
                if (room.ObjectStates.TryGet<CharacterRewardState>(key, out var st))
                {
                    st.Claimed = true;
                    st.Consumed = true;
                }
            }

            if (room.SpawnedPrefab != null)
            {
                var pedestals = room.SpawnedPrefab.GetComponentsInChildren<CharacterRewardPedestalInteractable>(includeInactive: true);
                foreach (var p in pedestals)
                {
                    if (p != null) UnityEngine.Object.Destroy(p.gameObject);
                }
            }
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static bool ApplyModifierToStat(
            CharacterRewardTargetStat target,
            AttributesManager attrs,
            Guid playerGuid,
            Modifier<int> modifier)
        {
            switch (target)
            {
                case CharacterRewardTargetStat.Health:
                    return attrs.AddModifier<Health, int>(playerGuid, modifier);
                case CharacterRewardTargetStat.Energy:
                    return attrs.AddModifier<Energy, int>(playerGuid, modifier);
                case CharacterRewardTargetStat.Speed:
                    return attrs.AddModifier<Speed, int>(playerGuid, modifier);
                case CharacterRewardTargetStat.Attack:
                    return attrs.AddModifier<Attack, int>(playerGuid, modifier);
                default:
                    Debug.LogWarning(LogPrefix + $"Target stat {target} no soportado — agregar al switch.");
                    return false;
            }
        }

        private static int ReadAmount(CharacterRewardSO reward, Guid playerGuid)
        {
            if (reward?.Amount == null) return 0;
            var ctx = new EffectContext { SourceGuid = playerGuid };
            return reward.Amount.Read(ctx);
        }

        private CharacterRewardSO ResolveRewardById(string upgradeId)
        {
            if (_pool == null || string.IsNullOrEmpty(upgradeId)) return null;
            foreach (var entry in _pool.Entries)
            {
                if (entry?.Reward == null) continue;
                if (entry.Reward.UpgradeId == upgradeId) return entry.Reward;
            }
            return null;
        }

        private bool HasAnyClaimedState(RoomInstance room)
        {
            for (int i = 0; i < _slotsPerBoss; i++)
            {
                string key = SpawnPointKey(i);
                if (room.ObjectStates.TryGet<CharacterRewardState>(key, out var st) && st.Claimed)
                {
                    return true;
                }
            }
            return false;
        }

        private static List<Transform> ResolveRewardSpawnPoints(RoomInstance room)
        {
            var list = new List<Transform>();
            if (room?.SpawnedPrefab == null) return list;
            var layout = room.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null || layout.RewardSpawnPoints == null) return list;
            foreach (var t in layout.RewardSpawnPoints)
            {
                if (t != null) list.Add(t);
            }
            return list;
        }

        private static string SpawnPointKey(int index) => SpawnPointPrefix + index;

        // ====================================================================
        // Test hooks
        // ====================================================================

        /// <summary>Inyecta un RNG seeded para reproducibilidad en EditMode tests.</summary>
        public void ConfigureForTests(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }
    }
}
