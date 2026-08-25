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
            _currentOffer = null;
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

        public int ResolveCost()
        {
            if (_config == null) return 0;
            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc)
                || enchSvc?.Bag == null) return _config.BaseCost;

            return _config.ResolveCost(enchSvc.Bag.GetDieCounter(RunCounterIndex, AltarRollKey));
        }

        public EnchantmentOffer? CurrentOffer => _currentOffer;

        public void ClearOffer()
        {
            _currentOffer = null;
        }

        public EnchantmentOfferResult RollOffer(Guid roomInstanceId)
        {
            if (_config == null || _pool == null)
                return EnchantmentOfferResult.Fail("EnchantmentRoomService no configurado (config / pool null).");

            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc)
                || enchSvc == null || !enchSvc.IsReady)
            {
                return EnchantmentOfferResult.Fail("DiceEnchantmentService no está listo.");
            }

            var bag = enchSvc.Bag;
            int cost = _config.ResolveCost(bag.GetDieCounter(RunCounterIndex, AltarRollKey));

            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null)
                return EnchantmentOfferResult.Fail("Economy service no registrado.");
            if (!economy.CanAfford(cost))
                return EnchantmentOfferResult.Fail($"Oro insuficiente ({economy.CurrentGold}/{cost}).");

            // Candidatos: distintos entre sí y pre-validados por coherencia
            // contra AL MENOS un dado del bag (palanca-primero: el dado destino
            // se elige después; la UI marca cuáles son válidos por opción).
            var exclude = new HashSet<EnchantmentSO>();
            var options = new List<EnchantmentSO>(OfferSize);
            int floorDepth = ResolveFloorDepth();
            const int MaxAttempts = 24;
            for (int attempt = 0; attempt < MaxAttempts && options.Count < OfferSize; attempt++)
            {
                var rolled = _pool.Roll(_rng, bag.Dice, floorDepth, exclude);
                if (rolled == null) break;
                // El pool tiene un fallback que ignora el exclude cuando se agota —
                // si devuelve algo ya excluido, no quedan candidatos frescos.
                if (exclude.Contains(rolled)) break;
                exclude.Add(rolled);
                if (!IsValidForAnyDie(enchSvc, bag, rolled)) continue;
                options.Add(rolled);
            }

            if (options.Count == 0)
            {
                return EnchantmentOfferResult.Fail("Sin candidatos válidos para tus dados — no se cobró el roll.");
            }

            if (!economy.Spend(cost))
            {
                return EnchantmentOfferResult.Fail("Economy.Spend rechazó la operación.");
            }

            // El contador global escala el costo del próximo roll: base × mult^n.
            bag.IncrementDieCounter(RunCounterIndex, AltarRollKey);
            IncrementUsageState(roomInstanceId);

            _currentOffer = new EnchantmentOffer(roomInstanceId, options, cost);
            return EnchantmentOfferResult.Ok(_currentOffer.Value);
        }

        public EnchantmentRollResult ConfirmChoice(int optionIndex, int bagIndex)
        {
            if (_currentOffer == null)
                return EnchantmentRollResult.Fail("No hay oferta activa — pagá un roll primero.");

            var offer = _currentOffer.Value;
            if (optionIndex < 0 || optionIndex >= offer.Options.Count)
                return EnchantmentRollResult.Fail($"Opción {optionIndex} fuera de rango.");

            if (!ServiceLocator.TryGetService<IDiceEnchantmentService>(out var enchSvc)
                || enchSvc == null || !enchSvc.IsReady)
            {
                return EnchantmentRollResult.Fail("DiceEnchantmentService no está listo.");
            }

            var chosen = offer.Options[optionIndex];
            var apply = enchSvc.Apply(bagIndex, chosen);
            if (!apply.Success)
            {
                // La oferta se conserva — el jugador puede elegir otro dado/opción.
                return EnchantmentRollResult.Fail("Apply falló: " + apply.ErrorMessage);
            }

            _currentOffer = null;
            return EnchantmentRollResult.Ok(chosen, offer.GoldPaid, apply.ProjectedFaces);
        }

        private static bool IsValidForAnyDie(IDiceEnchantmentService enchSvc, RuntimeDiceBag bag, EnchantmentSO ench)
        {
            for (int i = 0; i < bag.Dice.Count; i++)
            {
                if (enchSvc.ValidateApply(i, ench).Success) return true;
            }
            return false;
        }

        // ====================================================================
        // Offer state
        // ====================================================================

        /// <summary>Opciones reveladas por roll — GDD: 3.</summary>
        public const int OfferSize = 3;

        private const string AltarRollKey = "altar_roll_count";

        /// <summary>
        /// Índice sentinela para el die-counter global de la run — el costo
        /// escala por roll TOTAL (la palanca se tira antes de elegir dado), y el
        /// diccionario de counters del bag acepta cualquier índice como key.
        /// </summary>
        private const int RunCounterIndex = -1;

        private EnchantmentOffer? _currentOffer;

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
            // La mesa ocupa su celda — el jugador no la atraviesa caminando.
            Rollgeon.Dungeon.Components.PropTileBlocker.Attach(go);

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
