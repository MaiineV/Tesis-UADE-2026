using System;
using Patterns;
using Rollgeon.Balance;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;

namespace Rollgeon.Combat.Rolls
{
    /// <summary>
    /// Servicio runtime (clase plana, NO <c>MonoBehaviour</c>) que administra el
    /// Pool de Rolls del jugador (GDD "Turn System", Feature#0050). Reemplaza a
    /// <c>EnergyService</c> en su mismo slot de Priority (50).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Estado plano.</b> A diferencia de la energía, el pool NO vive como
    /// atributo en <c>AttributesManager</c>: es estado interno del service. Es
    /// exclusivo del jugador y exclusivo de combate, así que no hay razón para
    /// pagar el costo del sistema de atributos (ni el riesgo de que un effect
    /// genérico lo mutee por fuera del path canónico).
    /// </para>
    /// <para>
    /// <b>Lifecycle.</b> <c>OnCombatStart</c> → pool = <c>RollsAtCombatStart</c>;
    /// <c>OnTurnFinished</c> (gated al player) → pool += grant por turno, clamp a
    /// cap; <c>OnCombatEnd</c> → pool = 0 (no existe fuera de combate);
    /// <c>OnRunStart</c> → reset total (player id, pool y bonus por turno).
    /// </para>
    /// </remarks>
    public sealed class RollPoolService : IRollPoolService, IPreloadableService, IDisposable
    {
        private RulesetSO _ruleset;

        private EventManager.EventReceiver _onTurnFinishedHandler;
        private EventManager.EventReceiver _onRunStartHandler;
        private EventManager.EventReceiver _onCombatStartHandler;
        private EventManager.EventReceiver _onCombatEndHandler;

        /// <summary>
        /// Guid del jugador activo, cacheado en <see cref="InitializeForEntity"/>.
        /// <see cref="Guid.Empty"/> mientras no se haya inicializado — el service
        /// no otorga ni dispara <c>OnPlayerRollsChanged</c> hasta tener un player id.
        /// </summary>
        private Guid _playerId = Guid.Empty;

        private int _current;
        private bool _inCombat;
        private int _rollPoolBonus;

        /// <summary>Mismo slot que tenía EnergyService: antes de TurnManager (60).</summary>
        public int Priority => 50;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            if (!ServiceLocator.TryGetService<RulesetSO>(out _ruleset) || _ruleset == null)
            {
                Debug.LogError("[RollPoolService] RulesetSO no esta registrado en ServiceLocator. " +
                               "Agregarlo a ServiceBootstrapSO.SettingsAssets antes de que RollPoolService.Register() corra.");
                return;
            }

            ServiceLocator.AddService<IRollPoolService>(this, ServiceScope.Global);
            SubscribeHandlers();
        }

        public void Dispose()
        {
            if (_onTurnFinishedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
                _onTurnFinishedHandler = null;
            }
            if (_onRunStartHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnRunStart, _onRunStartHandler);
                _onRunStartHandler = null;
            }
            if (_onCombatStartHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatStart, _onCombatStartHandler);
                _onCombatStartHandler = null;
            }
            if (_onCombatEndHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnCombatEnd, _onCombatEndHandler);
                _onCombatEndHandler = null;
            }
        }

        // ======================================================================
        // Test / dependency injection hook
        // ======================================================================

        /// <summary>
        /// Constructor-like hook para EditMode tests: inyecta <paramref name="ruleset"/>
        /// sin pasar por <c>ServiceLocator</c> y suscribe los handlers (igual que
        /// <see cref="Register"/> — minus el <c>AddService</c>, que el test hace si
        /// lo necesita).
        /// </summary>
        public void ConfigureForTests(RulesetSO ruleset)
        {
            _ruleset = ruleset;
            SubscribeHandlers();
        }

        private void SubscribeHandlers()
        {
            _onTurnFinishedHandler = OnTurnFinishedExternal;
            _onRunStartHandler = OnRunStartExternal;
            _onCombatStartHandler = OnCombatStartExternal;
            _onCombatEndHandler = OnCombatEndExternal;
            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
            EventManager.Subscribe(EventName.OnRunStart, _onRunStartHandler);
            EventManager.Subscribe(EventName.OnCombatStart, _onCombatStartHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
        }

        // ======================================================================
        // IRollPoolService
        // ======================================================================

        public bool IsCombatActive => _inCombat;

        public void InitializeForEntity(Guid entityId)
        {
            if (entityId == Guid.Empty) return;
            _playerId = entityId;
            // El pool no existe fuera de combate: queda en 0 hasta OnCombatStart.
            _current = 0;
            _inCombat = false;
            TriggerRollsChanged();
        }

        public bool TrySpendRolls(Guid entityId, int count)
        {
            if (!IsPlayer(entityId)) return false;
            if (count < 0) return false; // No es un grant — bug del caller.
            if (count == 0) return true;
            if (count > _current) return false;

            _current -= count;
            TriggerRollsChanged();
            return true;
        }

        public int Drain(Guid entityId, int amount)
        {
            if (!IsPlayer(entityId)) return 0;
            if (amount <= 0) return 0;

            int drained = Math.Min(amount, _current);
            if (drained == 0) return 0;

            _current -= drained;
            TriggerRollsChanged();
            return drained;
        }

        public void AddRolls(Guid entityId, int amount)
        {
            if (!IsPlayer(entityId)) return;
            if (amount <= 0) return;

            int newVal = Math.Min(GetMax(entityId), _current + amount);
            if (newVal == _current) return;

            _current = newVal;
            TriggerRollsChanged();
        }

        public int GetCurrent(Guid entityId)
        {
            return IsPlayer(entityId) ? _current : 0;
        }

        public int GetMax(Guid entityId)
        {
            if (_ruleset == null) return 0;
            int cap = _ruleset.RollPool.RollPoolCap;
            // BUG-85: el bonus de los boss rewards sube el TECHO del pool (solo del
            // player) — antes solo alimentaba el grant por turno y era invisible.
            return IsPlayer(entityId) ? cap + _rollPoolBonus : cap;
        }

        public int GetRollsPerTurn(Guid entityId)
        {
            // BUG-85: el bonus ya no infla el grant por turno — su efecto es máximo
            // más alto + más rolls al arrancar el combate (visible al instante).
            int baseGrant = _ruleset != null ? _ruleset.RollPool.RollsPerTurn : 0;
            return baseGrant > 0 ? baseGrant : 0;
        }

        public void AddRollPoolBonus(int amount)
        {
            _rollPoolBonus += amount;
            // El claim ocurre en exploración: refrescar ya el HUD con el máximo
            // nuevo (y que el próximo combate arranque mostrando el cambio).
            TriggerRollsChanged();
        }

        /// <summary>
        /// Bonus de pool acumulado (rewards "Rolls +1"). Expuesto para el snapshot
        /// de run (<c>RollPoolSaveable</c>) — no está en la interfaz porque solo la
        /// persistencia lo necesita.
        /// </summary>
        public int RollPoolBonus => _rollPoolBonus;

        /// <summary>Restaura el bonus desde un save de run (pisa, no suma).</summary>
        public void RestoreRollPoolBonus(int value)
        {
            _rollPoolBonus = value;
        }

        public void RestoreCurrent(Guid entityId, int value)
        {
            if (!IsPlayer(entityId)) return;

            int clamped = Math.Clamp(value, 0, GetMax(entityId));
            _current = clamped;
            TriggerRollsChanged();
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        /// <summary>
        /// <c>OnRunStart</c> schema: <c>[Guid runId, string rulesetId]</c> — NO trae
        /// player Guid. Reset total: una run nueva arranca sin player cacheado, sin
        /// pool y sin bonus de rewards.
        /// </summary>
        private void OnRunStartExternal(params object[] args)
        {
            _playerId = Guid.Empty;
            _current = 0;
            _inCombat = false;
            _rollPoolBonus = 0;
        }

        /// <summary>
        /// <c>OnCombatStart</c> schema: <c>[Guid roomInstanceId]</c>. El pool arranca
        /// en <c>RollsAtCombatStart</c> — un resume mid-combat lo sobrescribe después
        /// vía <see cref="RestoreCurrent"/> (mismo contrato que tenía EnergyService).
        /// </summary>
        private void OnCombatStartExternal(params object[] args)
        {
            if (_playerId == Guid.Empty || _ruleset == null) return;

            _inCombat = true;
            // BUG-85: el bonus de los rewards también suma al arranque, así el efecto
            // se ve en el primer turno de cada combate (clampeado al máximo nuevo).
            _current = Math.Min(_ruleset.RollPool.RollsAtCombatStart + _rollPoolBonus, GetMax(_playerId));
            TriggerRollsChanged();
        }

        /// <summary>
        /// <c>OnTurnFinished</c> schema: <c>[Guid entityGuid]</c>. Solo el turno del
        /// jugador, y solo dentro de combate, otorga el grant — así el pool jamás
        /// acumula en exploración.
        /// </summary>
        private void OnTurnFinishedExternal(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if (!(args[0] is Guid entityId)) return;
            if (!_inCombat || !IsPlayer(entityId)) return;

            // ANTES del grant/clamp: los suscriptores (items "de la fortuna") leen el
            // pool todavía sin regalos — GetCurrent == leftover durante este dispatch.
            EventManager.Trigger(EventName.OnPlayerTurnRollsLeftover, entityId, _current);

            int newVal = Math.Min(GetMax(entityId), _current + GetRollsPerTurn(entityId));
            if (newVal == _current) return; // no-op: no disparamos evento redundante.

            _current = newVal;
            TriggerRollsChanged();
        }

        /// <summary>El pool es exclusivo de combate: al salir, se vacía.</summary>
        private void OnCombatEndExternal(params object[] args)
        {
            if (!_inCombat) return;

            _inCombat = false;
            _current = 0;
            TriggerRollsChanged();
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private bool IsPlayer(Guid entityId)
        {
            return _playerId != Guid.Empty && entityId == _playerId;
        }

        private void TriggerRollsChanged()
        {
            if (_playerId == Guid.Empty) return;
            EventManager.Trigger(EventName.OnPlayerRollsChanged, _playerId, _current, GetMax(_playerId));
        }
    }
}
