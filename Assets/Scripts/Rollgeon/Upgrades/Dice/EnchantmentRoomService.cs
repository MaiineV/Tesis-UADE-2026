using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Dungeon;
using Rollgeon.Dungeon.Components;
using Rollgeon.Dungeon.State;
using Rollgeon.Economy;
using Rollgeon.Player;
using UnityEngine;

namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Implementación canónica del <see cref="IEnchantmentRoomService"/>. Lazy-init
    /// por room vía <c>OnRoomEntered</c>, instancia el altar prefab en el primer
    /// <c>RewardSpawnPoint</c>, persiste el contador de usos en
    /// <see cref="EnchantmentAltarState"/>. Mismo patrón que <c>ShopManagerService</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scope.</b> Global. El service vive toda la sesión; el estado per-room
    /// vive en <c>RoomInstance.ObjectStates</c> y se serializa con el dungeon.
    /// </para>
    /// <para>
    /// <b>RNG.</b> Una instancia de <see cref="System.Random"/> por service —
    /// las tiradas del pool son no-deterministas a nivel sesión. Tests inyectan
    /// uno seeded via <see cref="ConfigureForTests"/>.
    /// </para>
    /// </remarks>
    public sealed class EnchantmentRoomService : IEnchantmentRoomService, IDisposable
    {
        private const string LogPrefix = "[EnchantmentRoomService] ";
        private const string AltarSpawnPointKey = "enchantment_altar";

        private readonly EnchantmentConfigSO _config;
        private readonly EnchantmentPoolSO _pool;
        private readonly GameObject _altarPrefab;

        private readonly HashSet<Guid> _initialized = new HashSet<Guid>();
        private System.Random _rng;

        private EventManager.EventReceiver _onRoomEnteredHandler;

        public EnchantmentRoomService(EnchantmentConfigSO config, EnchantmentPoolSO pool, GameObject altarPrefab)
        {
            _config = config;
            _pool = pool;
            _altarPrefab = altarPrefab;
            _rng = new System.Random();

            _onRoomEnteredHandler = OnRoomEntered;
            EventManager.Subscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);

            Debug.Log(LogPrefix + $"Service construido. config={(config != null ? "OK" : "NULL")} " +
                                  $"pool={(pool != null ? "OK" : "NULL")} altarPrefab={(altarPrefab != null ? "OK" : "NULL")}");
        }

        public void Dispose()
        {
            if (_onRoomEnteredHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRoomEntered, _onRoomEnteredHandler);
                _onRoomEnteredHandler = null;
            }
            _initialized.Clear();
        }

        // ====================================================================
        // IEnchantmentRoomService
        // ====================================================================

        public bool IsInitialized(Guid roomInstanceId) => _initialized.Contains(roomInstanceId);

        public void NotifyAltarActivated(Guid roomInstanceId, string spawnPointId)
        {
            // Si el bag no está listo (ej. pre-run, post-death), nada que hacer.
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc)
                || enchSvc == null || !enchSvc.IsReady)
            {
                Debug.LogWarning(LogPrefix + "Altar activado pero DiceEnchantmentService no está listo.");
                return;
            }

            int cost = _config != null ? _config.BaseCost : 0;
            Guid playerGuid = ResolvePlayerGuid();
            EventManager.Trigger(EventName.OnEnchantmentAltarActivated, playerGuid, roomInstanceId, cost);
        }

        public int ResolveCost(int bagIndex, int enchSlotIndex)
        {
            if (_config == null) return 0;
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc)
                || enchSvc?.Bag == null) return _config.BaseCost;

            int rerollCount = ReadRerollCount(enchSvc.Bag, bagIndex, enchSlotIndex);
            return _config.ResolveCost(rerollCount);
        }

        public EnchantmentRollResult PerformEnchantment(Guid roomInstanceId, int bagIndex, int enchSlotIndex)
        {
            if (_config == null || _pool == null)
                return EnchantmentRollResult.Fail("EnchantmentRoomService no configurado (config / pool null).");

            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc)
                || enchSvc == null || !enchSvc.IsReady)
            {
                return EnchantmentRollResult.Fail("DiceEnchantmentService no está listo.");
            }

            var bag = enchSvc.Bag;
            if (bagIndex < 0 || bagIndex >= bag.Dice.Count)
                return EnchantmentRollResult.Fail($"Bag index {bagIndex} fuera de rango.");
            if (enchSlotIndex < 0 || enchSlotIndex >= bag.GetEnchantmentSlotCount(bagIndex))
                return EnchantmentRollResult.Fail($"Slot index {enchSlotIndex} fuera de rango.");

            int rerollCount = ReadRerollCount(bag, bagIndex, enchSlotIndex);
            int cost = _config.ResolveCost(rerollCount);

            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null)
                return EnchantmentRollResult.Fail("Economy service no registrado.");
            if (!economy.CanAfford(cost))
                return EnchantmentRollResult.Fail($"Oro insuficiente ({economy.CurrentGold}/{cost}).");

            // Roll con retries — el pool puede devolver entries que la validación bloquee
            // (intersección vacía, redundancia). Excluimos los ya-aplicados de entrada.
            var exclude = new HashSet<EnchantmentSO>();
            foreach (var ench in bag.GetEnchantments(bagIndex))
            {
                if (ench != null) exclude.Add(ench);
            }

            EnchantmentSO rolled = null;
            EnchantmentApplyResult applyValidation = default;
            const int MaxRetries = 8;
            int floorDepth = ResolveFloorDepth();
            for (int attempt = 0; attempt < MaxRetries; attempt++)
            {
                rolled = _pool.Roll(_rng, bag.Dice[bagIndex], floorDepth, exclude);
                if (rolled == null)
                {
                    return EnchantmentRollResult.Fail("Pool vacío o sin candidatos compatibles.");
                }
                applyValidation = enchSvc.ValidateApply(bagIndex, enchSlotIndex, rolled);
                if (applyValidation.Success) break;
                exclude.Add(rolled);
                rolled = null;
            }

            if (rolled == null || !applyValidation.Success)
            {
                return EnchantmentRollResult.Fail(
                    $"No se encontró encantamiento válido tras {MaxRetries} intentos. " +
                    (applyValidation.ErrorMessage ?? "(sin detalle)"));
            }

            if (!economy.Spend(cost))
            {
                return EnchantmentRollResult.Fail("Economy.Spend rechazó la operación.");
            }

            // enchSvc.Apply internamente llama Bag.ClearCountersForSlot — eso
            // también borraría nuestro AltarRerollKey. Guardamos el count antes
            // del Apply y lo restauramos + incrementamos después.
            var slotRef = new EnchantmentSlotRef(bag.Dice[bagIndex], bagIndex, enchSlotIndex);
            int savedRerollCount = bag.GetCounter(slotRef, AltarRerollKey);

            var finalApply = enchSvc.Apply(bagIndex, enchSlotIndex, rolled);
            if (!finalApply.Success)
            {
                // Refund para no dejar el oro perdido — situación de borde improbable.
                economy.Add(cost);
                return EnchantmentRollResult.Fail("Apply falló: " + finalApply.ErrorMessage);
            }

            // Restaurar el counter + sumar uno por este nuevo re-roll. El próximo
            // roll sobre este mismo slot va a costar base × mult^(savedRerollCount+1).
            bag.IncrementCounter(slotRef, AltarRerollKey, delta: savedRerollCount + 1);

            IncrementUsageState(roomInstanceId);
            return EnchantmentRollResult.Ok(rolled, cost, finalApply.ProjectedFaces);
        }

        // ====================================================================
        // Reroll counter helpers
        // ====================================================================

        private const string AltarRerollKey = "altar_reroll_count";

        private static int ReadRerollCount(RuntimeDiceBag bag, int bagIndex, int enchSlotIndex)
        {
            if (bag == null || bagIndex < 0 || bagIndex >= bag.Dice.Count) return 0;
            var slotRef = new EnchantmentSlotRef(bag.Dice[bagIndex], bagIndex, enchSlotIndex);
            return bag.GetCounter(slotRef, AltarRerollKey);
        }

        // ====================================================================
        // OnRoomEntered handler
        // ====================================================================

        private void OnRoomEntered(params object[] args)
        {
            if (args == null || args.Length < 1) return;
            if (!(args[0] is Guid roomId)) return;

            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return;
            if (!dungeon.GetAllRoomInstances().TryGetValue(roomId, out var room)) return;
            if (room.Template == null || room.Template.Type != RoomType.Enchantment) return;

            // Llegamos aca = entramos a una sala de Enchantment confirmada.
            Debug.Log(LogPrefix + $"OnRoomEntered Enchantment '{room.Template.RoomId}' " +
                                  $"(instanceId={roomId})");

            if (_initialized.Contains(roomId))
            {
                Debug.Log(LogPrefix + " └ ya inicializada en esta sesión — skip respawn.");
                return;
            }

            InitializeRoom(room);
        }

        private void InitializeRoom(RoomInstance room)
        {
            if (_altarPrefab == null)
            {
                Debug.LogWarning(LogPrefix + "AltarPrefab no asignado — no se instancia el altar.");
                _initialized.Add(room.InstanceId);
                return;
            }

            var spawnPoint = ResolveAltarSpawnPoint(room);
            if (spawnPoint == null)
            {
                Debug.LogWarning(LogPrefix + $"Room '{room.Template?.RoomId}' no tiene RewardSpawnPoints — no se instancia altar.");
                _initialized.Add(room.InstanceId);
                return;
            }

            Transform parent = room.SpawnedPrefab != null ? room.SpawnedPrefab.transform : null;
            var go = UnityEngine.Object.Instantiate(_altarPrefab, spawnPoint.position, spawnPoint.rotation, parent);
            go.name = "[EnchantmentAltar]";

            var altar = go.GetComponent<EnchantmentAltarInteractable>();
            if (altar == null)
            {
                Debug.LogError(LogPrefix + "AltarPrefab no tiene EnchantmentAltarInteractable — el player no puede interactuar.");
            }
            else
            {
                int cost = _config != null ? _config.BaseCost : 0;
                altar.Configure(room.InstanceId, AltarSpawnPointKey, this, cost);
                Debug.Log(LogPrefix + $"Altar instanciado en {spawnPoint.position} " +
                                      $"(parent={(parent != null ? parent.name : "null")}, " +
                                      $"cost={cost})");
            }

            // Hidratar state si existía (preservar TotalUses entre re-entries / save-load).
            if (!room.ObjectStates.TryGet<EnchantmentAltarState>(AltarSpawnPointKey, out _))
            {
                room.ObjectStates.Set(AltarSpawnPointKey, new EnchantmentAltarState
                {
                    SpawnPointId = AltarSpawnPointKey,
                    TotalUses = 0,
                });
            }

            _initialized.Add(room.InstanceId);
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static Transform ResolveAltarSpawnPoint(RoomInstance room)
        {
            if (room?.SpawnedPrefab == null) return null;
            var layout = room.SpawnedPrefab.GetComponent<RoomLayout>();
            if (layout == null) return null;
            if (layout.RewardSpawnPoints == null || layout.RewardSpawnPoints.Count == 0) return null;
            // El primer RewardSpawnPoint es el canónico del altar — convención compartida con shop.
            return layout.RewardSpawnPoints[0];
        }

        private void IncrementUsageState(Guid roomInstanceId)
        {
            if (!ServiceLocator.TryGetService<IDungeonService>(out var dungeon) || dungeon == null) return;
            if (!dungeon.GetAllRoomInstances().TryGetValue(roomInstanceId, out var room)) return;
            if (!room.ObjectStates.TryGet<EnchantmentAltarState>(AltarSpawnPointKey, out var state))
            {
                state = new EnchantmentAltarState { SpawnPointId = AltarSpawnPointKey };
                room.ObjectStates.Set(AltarSpawnPointKey, state);
            }
            state.TotalUses++;
        }

        private static int ResolveFloorDepth()
        {
            // Placeholder hasta que aterrice multi-floor — Phase 4 del runtime lo hizo igual.
            return 0;
        }

        private static Guid ResolvePlayerGuid()
        {
            return ServiceLocator.TryGetService<IPlayerService>(out var ps) && ps != null
                ? ps.PlayerGuid
                : Guid.Empty;
        }

        // ====================================================================
        // Test hooks
        // ====================================================================

        /// <summary>Hook para tests — inyecta un RNG seeded para reproducibilidad.</summary>
        public void ConfigureForTests(System.Random rng)
        {
            _rng = rng ?? new System.Random();
        }
    }
}
