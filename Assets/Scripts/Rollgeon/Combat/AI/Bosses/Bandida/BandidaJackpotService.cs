using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Grid;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Combat.AI.Bosses.Bandida
{
    /// <summary>
    /// Implementación POCO de <see cref="IBandidaJackpotService"/>. Calca el lifecycle de
    /// <c>StunService</c>: registro global vía <see cref="IPreloadableService"/>, estado
    /// combat-scoped y limpieza por <c>OnCombatEnd</c> / <c>OnRunEnd</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nace lazy.</b> No hay bootstrap en escena para este jefe (el setup de engine lo hace el
    /// usuario a mano): el primer nodo del árbol que corre lo crea vía
    /// <see cref="ResolveOrCreate"/>, igual que <c>ThreatTelegraphOverlay</c>. Si más adelante se
    /// agrega un bootstrap, <see cref="ResolveOrCreate"/> encuentra el existente y no duplica.
    /// </para>
    /// <para>
    /// <b>Asimetría deliberada del ciclo.</b> La cuenta que llega a 0 y dispara se rearma en el
    /// acto (el árbol llama <see cref="ResetCountdown"/> en el mismo turno que marca el jackpot);
    /// la ronda muerta solo existe cuando el jugador rompe un rodillo. La pausa es el premio de
    /// cancelar — tanquear el jackpot no la recibe.
    /// </para>
    /// </remarks>
    public sealed class BandidaJackpotService : IBandidaJackpotService, IPreloadableService, IDisposable
    {
        private readonly List<ReelSlot> _slots = new List<ReelSlot>();

        private Guid _bossGuid;
        private int _countdown;
        private bool _isCounting;
        private int _respawnDelayTurns = 2;
        private bool _respawnDelayInitialized;
        private int _lockedReelHp;

        private Action<DamageResolvedPayload> _damageHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>Junto al resto de servicios de combate (ver <c>StunService.Priority</c>).</summary>
        public int Priority => 80;

        public Guid BossGuid => _bossGuid;
        public int Countdown => _countdown;
        public bool IsCounting => _isCounting;
        public int RespawnDelayTurns => _respawnDelayTurns;
        public int LockedReelHp => _lockedReelHp;
        public IReadOnlyList<ReelSlot> Slots => _slots;

        // ======================================================================
        // Lifecycle
        // ======================================================================

        /// <summary>
        /// Devuelve el servicio registrado, o crea y registra uno. Único camino que usan los nodos
        /// del árbol — así el jefe funciona sin bootstrap en escena.
        /// </summary>
        public static IBandidaJackpotService ResolveOrCreate()
        {
            if (ServiceLocator.TryGetService<IBandidaJackpotService>(out var existing) && existing != null)
                return existing;

            var created = new BandidaJackpotService();
            created.Register();
            return created;
        }

        public void Register()
        {
            SubscribeHandlers();

            ServiceLocator.AddService<IBandidaJackpotService>(this, ServiceScope.Global);
            ServiceLocator.AddService<BandidaJackpotService>(this, ServiceScope.Global);
        }

        /// <summary>Hook para EditMode tests: suscribe handlers sin pasar por el ServiceLocator.</summary>
        public void ConfigureForTests() => SubscribeHandlers();

        private void SubscribeHandlers()
        {
            // Idempotente: si Register y ConfigureForTests corren sobre la misma instancia, no
            // queremos dos suscripciones al canal de daño (cancelaría dos veces y sumaría ruido).
            UnsubscribeHandlers();

            _damageHandler = OnDamageResolved;
            TypedEvent<DamageResolvedPayload>.Subscribe(_damageHandler);

            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
        }

        private void UnsubscribeHandlers()
        {
            if (_damageHandler != null)
            {
                TypedEvent<DamageResolvedPayload>.Unsubscribe(_damageHandler);
                _damageHandler = null;
            }
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
            if (_onRunEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunEnd, _onRunEndHandler);
                _onRunEndHandler = null;
            }
        }

        public void Dispose()
        {
            UnsubscribeHandlers();
            ResetAll();
        }

        // ======================================================================
        // IBandidaJackpotService
        // ======================================================================

        public void BindBoss(Guid bossGuid)
        {
            if (bossGuid == Guid.Empty) return;
            if (_bossGuid == bossGuid) return;

            // Pelea nueva: el servicio es Global y las ranuras/guids del combate anterior ya no
            // existen. Sin este reset el jefe nuevo creería tener su fila armada.
            ResetAll();
            _bossGuid = bossGuid;
        }

        public void InitRespawnDelay(int turns)
        {
            if (_respawnDelayInitialized) return;
            _respawnDelayInitialized = true;
            _respawnDelayTurns = turns < 0 ? 0 : turns;
        }

        public void SetRespawnDelay(int turns)
        {
            _respawnDelayInitialized = true;
            _respawnDelayTurns = turns < 0 ? 0 : turns;
        }

        public void SetSlots(IReadOnlyList<GridCoord> coords)
        {
            if (coords == null || coords.Count == 0) return;
            if (_slots.Count > 0) return; // La fila se arma una sola vez: las ranuras son fijas.

            int middle = coords.Count / 2;
            for (int i = 0; i < coords.Count; i++)
            {
                _slots.Add(new ReelSlot
                {
                    Side = i < middle ? ReelSide.Left : (i == middle ? ReelSide.Middle : ReelSide.Right),
                    Coord = coords[i],
                    ReelGuid = Guid.Empty,
                    TurnsUntilRespawn = 0,
                    Locked = false,
                });
            }
        }

        public void AttachReel(int index, Guid reelGuid)
        {
            if (index < 0 || index >= _slots.Count) return;
            _slots[index].ReelGuid = reelGuid;
            _slots[index].TurnsUntilRespawn = 0;
        }

        public void DetachReel(int index)
        {
            if (index < 0 || index >= _slots.Count) return;
            _slots[index].ReelGuid = Guid.Empty;
            _slots[index].TurnsUntilRespawn = _respawnDelayTurns;
        }

        public int Tick()
        {
            if (!_isCounting) return _countdown;

            if (_countdown > 0) _countdown--;
            Publish();
            return _countdown;
        }

        public void ResetCountdown(int value)
        {
            _countdown = value < 0 ? 0 : value;
            _isCounting = true;
            Publish();
        }

        public bool CancelFromReelDamage(Guid reelGuid)
        {
            if (reelGuid == Guid.Empty) return false;

            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.ReelGuid != reelGuid) continue;

                // El rodillo trabado (HOLD) no cancela: en Fase 2 quedan los dos de la punta como
                // únicos blancos válidos, y pegarle al del medio es tempo perdido.
                if (slot.Locked) return false;
                if (!_isCounting) return false;

                _isCounting = false;
                Publish();
                return true;
            }

            return false;
        }

        public void LockSlot(ReelSide side, int lockedHp)
        {
            _lockedReelHp = lockedHp < 1 ? 1 : lockedHp;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Side == side) _slots[i].Locked = true;
            }
        }

        public void ResetAll()
        {
            _slots.Clear();
            _bossGuid = Guid.Empty;
            _countdown = 0;
            _isCounting = false;
            _respawnDelayInitialized = false;
            _respawnDelayTurns = 2;
            _lockedReelHp = 0;
        }

        // ======================================================================
        // Hooks
        // ======================================================================

        // El hook de cancelación. Filtra por daño efectivo: un golpe absorbido entero por escudo
        // no rompe nada, así que tampoco desarma la bomba.
        private void OnDamageResolved(DamageResolvedPayload payload)
        {
            if (payload.FinalDamage <= 0) return;
            CancelFromReelDamage(payload.TargetGuid);
        }

        private void OnScopeEndedExternal(params object[] args) => ResetAll();

        private void Publish() =>
            TypedEvent<JackpotCountdownPayload>.Raise(
                new JackpotCountdownPayload(_bossGuid, _countdown, _isCounting));
    }
}
