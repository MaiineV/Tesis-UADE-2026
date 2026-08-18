using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Economy;
using Rollgeon.UI.HUD;
using UnityEngine;

namespace Rollgeon.Combat.Cashier
{
    /// <summary>
    /// Implementación de <see cref="ICashierLedgerService"/>. POCO suscripto a eventos, sin
    /// MonoBehaviour ni bootstrap: se auto-registra vía <see cref="ResolveOrCreate"/> el primer
    /// turno del jefe.
    /// </summary>
    /// <remarks>
    /// Es global y suscripto a <c>TypedEvent&lt;DamageResolvedPayload&gt;</c>, que
    /// <c>ServiceLocator.Clear()</c> no desengancha: un fixture que lo cree debe llamar
    /// <see cref="Dispose"/> en el teardown.
    /// </remarks>
    public sealed class CashierLedgerService : ICashierLedgerService, IDisposable
    {
        private readonly HashSet<Guid> _damaged = new HashSet<Guid>();
        private readonly Dictionary<Guid, ChipEntry> _chips = new Dictionary<Guid, ChipEntry>();

        private Guid _vaultOwner;
        private int _vaultedGold;
        private int _chipValueMultiplier = 1;
        private int _bribeRoundsLeft;
        private int _lastRoundIndex = -1;
        private CashierTierSnapshot? _lastTier;

        private Action<DamageResolvedPayload> _onDamageResolved;
        private EventManager.EventReceiver _onEntityDestroyed;
        private EventManager.EventReceiver _onHazardTriggered;
        private EventManager.EventReceiver _onHazardExpired;
        private EventManager.EventReceiver _onTurnQueueBuilt;
        private EventManager.EventReceiver _onScopeEnded;
        private bool _disposed;

        public CashierLedgerService()
        {
            _onDamageResolved = OnDamageResolved;
            TypedEvent<DamageResolvedPayload>.Subscribe(_onDamageResolved);

            _onEntityDestroyed = OnEntityDestroyedExternal;
            _onHazardTriggered = OnHazardTriggeredExternal;
            _onHazardExpired = OnHazardExpiredExternal;
            _onTurnQueueBuilt = OnTurnQueueBuiltExternal;
            _onScopeEnded = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnEntityDestroyed, _onEntityDestroyed);
            EventManager.Subscribe(EventName.OnHazardTriggered, _onHazardTriggered);
            EventManager.Subscribe(EventName.OnHazardExpired, _onHazardExpired);
            EventManager.Subscribe(EventName.OnTurnQueueBuilt, _onTurnQueueBuilt);
            EventManager.Subscribe(EventName.OnCombatEnd, _onScopeEnded);
            EventManager.Subscribe(EventName.OnRunEnd, _onScopeEnded);
        }

        /// <summary>
        /// Devuelve el servicio registrado o crea y registra uno nuevo (Global). Lo llaman los
        /// nodos del Cajero — nunca debería crearlo un sistema que sólo quiera leer.
        /// </summary>
        public static ICashierLedgerService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<ICashierLedgerService>(out var existing) && existing != null)
                return existing;

            var created = new CashierLedgerService();
            ServiceLocator.AddService<ICashierLedgerService>(created, ServiceScope.Global);
            return created;
        }

        // ======================================================================
        // ICashierLedgerService
        // ======================================================================

        /// <inheritdoc />
        public int VaultedGold => _vaultedGold;

        /// <inheritdoc />
        public int ChipValueMultiplier => _chipValueMultiplier;

        /// <inheritdoc />
        public int DamageStepDown => _bribeRoundsLeft > 0 ? 1 : 0;

        /// <inheritdoc />
        public int BribeRoundsLeft => _bribeRoundsLeft;

        /// <inheritdoc />
        /// <remarks>
        /// Derivado del índice de ronda absoluto, no de un contador propio: el servicio se crea
        /// perezosamente y se pierde los <c>OnTurnQueueBuilt</c> anteriores. Con la división el
        /// rastrillo queda igual se haya creado en la ronda 0 o en la 4, y sobrevive un restore.
        /// </remarks>
        public int DamageStepUp
        {
            get
            {
                if (RakeRoundsPerStep <= 0 || _lastRoundIndex <= 0) return 0;
                return _lastRoundIndex / RakeRoundsPerStep;
            }
        }

        /// <inheritdoc />
        public int BribeCost { get; set; } = 35;

        /// <inheritdoc />
        public int BribeRounds { get; set; } = 3;

        /// <inheritdoc />
        public int RakeRoundsPerStep { get; set; } = 3;

        /// <inheritdoc />
        public bool ConsumeDamageTaken(Guid entityGuid)
        {
            if (entityGuid == Guid.Empty) return false;
            return _damaged.Remove(entityGuid);
        }

        /// <inheritdoc />
        public int CollectTax(Guid ownerGuid, float percent)
        {
            if (ownerGuid == Guid.Empty || percent <= 0f) return 0;
            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null) return 0;

            // Floor: el arqueo nunca redondea a favor de la casa (40% de 99 = 39, no 40).
            int take = Mathf.FloorToInt(economy.CurrentGold * percent);
            if (take <= 0) return 0;
            if (!economy.Spend(take)) return 0;

            _vaultOwner = ownerGuid;
            _vaultedGold += take;
            return take;
        }

        /// <inheritdoc />
        public void SetChipValueMultiplier(int multiplier)
        {
            _chipValueMultiplier = multiplier < 1 ? 1 : multiplier;
        }

        /// <inheritdoc />
        public bool TryBribe()
        {
            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null) return false;
            if (!economy.Spend(BribeCost)) return false;

            ArmBribeWindow();
            return true;
        }

        /// <summary>
        /// Abre (o reinicia) la ventana de soborno. Paso compartido por las dos formas de sobornar
        /// — pagar el precio (<see cref="TryBribe"/>) y devolverle una ficha
        /// (<see cref="OnHazardTriggeredExternal"/>) — para que no diverjan en duración.
        /// </summary>
        /// <remarks>
        /// La ventana se reinicia, no acumula: <see cref="DamageStepDown"/> está topeado en 1, así
        /// que sobornar dos veces seguidas extiende la duración pero nunca da dos escalones.
        /// </remarks>
        private void ArmBribeWindow()
        {
            _bribeRoundsLeft = BribeRounds < 0 ? 0 : BribeRounds;
        }

        /// <inheritdoc />
        public void RegisterChip(Guid hazardInstanceId, int value, Guid ownerGuid)
        {
            if (hazardInstanceId == Guid.Empty || value <= 0) return;
            _chips[hazardInstanceId] = new ChipEntry { Value = value, Owner = ownerGuid };
        }

        /// <inheritdoc />
        public int GetChipValue(Guid hazardInstanceId)
            => _chips.TryGetValue(hazardInstanceId, out var chip) ? chip.Value : 0;

        /// <inheritdoc />
        public CashierTierSnapshot? LastTier => _lastTier;

        /// <inheritdoc />
        public void ReportTier(int rank, int damage, int gold, int stepUp, int stepDown)
        {
            _lastTier = new CashierTierSnapshot(rank, damage, gold, stepUp, stepDown);
            EventManager.Trigger(EventName.OnCashierTierChanged, rank, damage);
        }

        // ======================================================================
        // Lifecycle
        // ======================================================================

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_onDamageResolved != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_onDamageResolved);
                _onDamageResolved = null;
            }
            Unsubscribe(EventName.OnEntityDestroyed, ref _onEntityDestroyed);
            Unsubscribe(EventName.OnHazardTriggered, ref _onHazardTriggered);
            Unsubscribe(EventName.OnHazardExpired, ref _onHazardExpired);
            Unsubscribe(EventName.OnTurnQueueBuilt, ref _onTurnQueueBuilt);

            if (_onScopeEnded != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onScopeEnded);
                EventManager.UnSubscribe(EventName.OnRunEnd, _onScopeEnded);
                _onScopeEnded = null;
            }
            ResetCombatState();
        }

        private static void Unsubscribe(EventName name, ref EventManager.EventReceiver handler)
        {
            if (handler == null) return;
            EventManager.UnSubscribe(name, handler);
            handler = null;
        }

        /// <summary>
        /// Borra todo el estado de pelea <b>sin</b> devolver la caja: si el combate terminó sin
        /// que el jefe muriera (jugador muerto / run abortada), la banca gana.
        /// </summary>
        private void ResetCombatState()
        {
            _damaged.Clear();
            _chips.Clear();
            _vaultOwner = Guid.Empty;
            _vaultedGold = 0;
            _chipValueMultiplier = 1;
            _bribeRoundsLeft = 0;
            _lastRoundIndex = -1;
            _lastTier = null;
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnDamageResolved(DamageResolvedPayload payload)
        {
            if (payload.TargetGuid == Guid.Empty || payload.FinalDamage <= 0) return;
            _damaged.Add(payload.TargetGuid);
        }

        private void OnEntityDestroyedExternal(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid guid)) return;
            if (guid == Guid.Empty || guid != _vaultOwner) return;

            // "Al vencerlo la caja se abre": la devolución es total y de una vez.
            int amount = _vaultedGold;
            _vaultedGold = 0;
            _vaultOwner = Guid.Empty;
            if (amount <= 0) return;

            PayPlayer(guid, amount);
        }

        private void OnHazardTriggeredExternal(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid instanceId) || !(args[1] is Guid entityGuid)) return;
            if (!_chips.TryGetValue(instanceId, out var chip)) return;

            // El jefe kitea sobre su propia columna: si pisa una ficha, no se la cobra.
            if (entityGuid != Guid.Empty && entityGuid == chip.Owner) return;

            _chips.Remove(instanceId);
            PayPlayer(entityGuid, chip.Value);

            // Levantar una ficha soborna gratis: el oro que paga es justo lo que sube el escalón.
            ArmBribeWindow();
            AnnounceBribe(chip.Owner);
        }

        /// <summary>
        /// Avisa el soborno sobre el jefe y no sobre quien levantó la ficha: lo que cambió es
        /// cuánto pega él.
        /// </summary>
        private void AnnounceBribe(Guid bossGuid)
        {
            if (bossGuid == Guid.Empty) return;

            EventManager.Trigger(
                EventName.OnFloatingNumberRequested,
                bossGuid,
                FloatingNumberType.Status,
                BribeAnnouncement,
                Vector3.zero);
        }

        /// <summary>Texto del aviso de soborno. Literal: todavía no hay tabla de localización de combate.</summary>
        /// <remarks>
        /// Sólo caracteres que están en el atlas de <c>m6x11plus</c>: la pixel font del HUD no tiene
        /// <c>·</c> (U+00B7) ni <c>é</c> (U+00E9), y un glifo que falta sale como cuadradito.
        /// </remarks>
        private const string BribeAnnouncement = "Soborno: -1 escalón";

        private void OnHazardExpiredExternal(params object[] args)
        {
            if (args == null || args.Length < 1 || !(args[0] is Guid instanceId)) return;

            // Ficha no levantada: se descarta. No entra a la caja a propósito — la caja se
            // devuelve al vencer al jefe, y eso convertiría "ignorar las fichas" en oro gratis.
            _chips.Remove(instanceId);
        }

        private void OnTurnQueueBuiltExternal(params object[] args)
        {
            if (args == null || args.Length < 2 || !(args[1] is int roundIndex)) return;
            if (roundIndex == _lastRoundIndex) return;

            _lastRoundIndex = roundIndex;
            if (_bribeRoundsLeft > 0) _bribeRoundsLeft--;
        }

        private void OnScopeEndedExternal(params object[] args) => ResetCombatState();

        /// <summary>Ficha viva: cuánto paga y quién la soltó.</summary>
        private struct ChipEntry
        {
            public int Value;
            public Guid Owner;
        }

        private static void PayPlayer(Guid targetGuid, int amount)
        {
            if (!ServiceLocator.TryGetService<IEconomyService>(out var economy) || economy == null) return;

            economy.Add(amount);
            EventManager.Trigger(
                EventName.OnFloatingNumberRequested,
                targetGuid,
                FloatingNumberType.Gold,
                (float)amount,
                Vector3.zero);
        }
    }
}
