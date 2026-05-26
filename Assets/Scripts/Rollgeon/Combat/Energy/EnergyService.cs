using System;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Stats;
using Rollgeon.Balance;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine;
using EnergyStat = Rollgeon.Attributes.Stats.Energy;

namespace Rollgeon.Combat.EnergyLib
{
    /// <summary>
    /// Servicio runtime (clase plana, NO <c>MonoBehaviour</c>) que administra la
    /// energia del jugador: inicializacion al empezar la run, gasto por accion,
    /// y regeneracion al finalizar el turno. Plan §4.3.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Lifecycle.</b> <see cref="Register"/> lo invoca <c>ServiceBootstrapSO</c>
    /// durante el bootstrap global (Foundation#0005). Se registra a si mismo en
    /// <c>ServiceLocator</c> bajo <see cref="IEnergyService"/> + suscribe a
    /// <c>OnTurnFinished</c> y <c>OnRunStart</c>. <see cref="Dispose"/> (opcional)
    /// desuscribe los handlers y libera refs.
    /// </para>
    /// <para>
    /// <b>Gate por entidad.</b> En el FP solo el jugador regenera al terminar turno;
    /// enemigos tienen regen propia cuando llegue T99 (Support). Por eso la policy
    /// se aplica unicamente cuando el <see cref="Guid"/> recibido en
    /// <c>OnTurnFinished</c> coincide con el player Guid cacheado via
    /// <see cref="InitializeForEntity"/>. Si nunca se llamo <see cref="InitializeForEntity"/>,
    /// no regeneramos a nadie (seguro por defecto).
    /// </para>
    /// </remarks>
    public sealed class EnergyService : IEnergyService, IPreloadableService, IDisposable
    {
        private RulesetSO _ruleset;
        private AttributesManager _attributes;

        private EventManager.EventReceiver _onTurnFinishedHandler;
        private EventManager.EventReceiver _onRunStartHandler;
        private EventManager.EventReceiver _onCombatStartHandler;

        /// <summary>
        /// Guid del jugador activo, cacheado en <see cref="InitializeForEntity"/>.
        /// <see cref="Guid.Empty"/> mientras no se haya inicializado — el service
        /// no regenera ni dispara <c>OnPlayerEnergyChanged</c> hasta tener un player id.
        /// </summary>
        private Guid _playerId = Guid.Empty;

        /// <summary>Despues de catalogos/settings (Priority default 0–10).</summary>
        public int Priority => 50;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            // Resolve dependencias. Ambas deben estar registradas por catalogos/settings
            // previos en ServiceBootstrapSO.RegisterAll (Priority menor corre antes).
            if (!ServiceLocator.TryGetService<RulesetSO>(out _ruleset) || _ruleset == null)
            {
                Debug.LogError("[EnergyService] RulesetSO no esta registrado en ServiceLocator. " +
                               "Agregarlo a ServiceBootstrapSO.SettingsAssets antes de que EnergyService.Register() corra.");
                return;
            }

            if (!ServiceLocator.TryGetService<AttributesManager>(out _attributes) || _attributes == null)
            {
                Debug.LogError("[EnergyService] AttributesManager no esta registrado en ServiceLocator. " +
                               "Foundation#0003 debe registrarlo antes del bootstrap de servicios.");
                return;
            }

            ServiceLocator.AddService<IEnergyService>(this, ServiceScope.Global);

            _onTurnFinishedHandler = OnTurnFinishedExternal;
            _onRunStartHandler = OnRunStartExternal;
            _onCombatStartHandler = OnCombatStartExternal;
            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
            EventManager.Subscribe(EventName.OnRunStart, _onRunStartHandler);
            EventManager.Subscribe(EventName.OnCombatStart, _onCombatStartHandler);
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
        }

        // ======================================================================
        // Test / dependency injection hook
        // ======================================================================

        /// <summary>
        /// Constructor-like hook para EditMode tests: inyecta <paramref name="ruleset"/>
        /// + <paramref name="attributes"/> sin pasar por <c>ServiceLocator</c>.
        /// Tambien suscribe los handlers (igual que <see cref="Register"/> — minus
        /// el <c>ServiceLocator.AddService</c>, que el test hace si lo necesita).
        /// </summary>
        public void ConfigureForTests(RulesetSO ruleset, AttributesManager attributes)
        {
            _ruleset = ruleset;
            _attributes = attributes;

            _onTurnFinishedHandler = OnTurnFinishedExternal;
            _onRunStartHandler = OnRunStartExternal;
            _onCombatStartHandler = OnCombatStartExternal;
            EventManager.Subscribe(EventName.OnTurnFinished, _onTurnFinishedHandler);
            EventManager.Subscribe(EventName.OnRunStart, _onRunStartHandler);
            EventManager.Subscribe(EventName.OnCombatStart, _onCombatStartHandler);
        }

        // ======================================================================
        // IEnergyService
        // ======================================================================

        /// <summary>
        /// Hidrata la energia del jugador al empezar la run. <b>Publico a proposito</b>
        /// — <c>OnRunStart</c> trae <c>(Guid runId, string rulesetId)</c> pero NO el
        /// Guid del jugador (ver plan R1). El caller que spawnea al player (T98 /
        /// futuro PlayerSpawner) debe invocar este metodo explicitamente tras
        /// registrar al player en el <c>AttributesManager</c>.
        /// </summary>
        public void InitializeForEntity(Guid entityId)
        {
            if (entityId == Guid.Empty) return;
            if (_ruleset == null || _attributes == null) return;
            if (!_attributes.IsRegistered(entityId))
            {
                Debug.LogWarning($"[EnergyService] Entity '{entityId}' no esta registrada en AttributesManager al inicializar energia.");
                return;
            }

            int max = _ruleset.Energy.EnergyMax;
            int start = _ruleset.Energy.EnergyAtRunStart;
            if (start > max) start = max;
            if (start < 0) start = 0;

            var attrs = _attributes.GetAttributes(entityId);
            if (attrs == null) return;

            if (!attrs.HasAttribute<EnergyStat>())
            {
                attrs.SetAttribute<EnergyStat>(new EnergyStat(start));
                EventManager.Trigger(EventName.OnAttributeChanged, entityId, typeof(EnergyStat));
            }
            else
            {
                _attributes.SetAttributeValue<EnergyStat, int>(entityId, start);
            }

            _playerId = entityId;
            TriggerEnergyChanged(entityId, start, max);
        }

        public bool SpendEnergy(Guid entityId, int cost)
        {
            if (entityId == Guid.Empty) return false;
            if (_attributes == null || _ruleset == null) return false;
            if (cost < 0) return false; // No es un heal — bug del caller.
            if (!_attributes.IsRegistered(entityId)) return false;

            var attrs = _attributes.GetAttributes(entityId);
            if (attrs == null || !attrs.HasAttribute<EnergyStat>()) return false;

            int current = _attributes.GetAttributeValue<EnergyStat, int>(entityId);
            if (cost > current) return false;

            int newVal = current - cost;
            _attributes.SetAttributeValue<EnergyStat, int>(entityId, newVal);
            TriggerEnergyChanged(entityId, newVal, _ruleset.Energy.EnergyMax);
            return true;
        }

        public void RegenerateAtTurnEnd(Guid entityId)
        {
            if (entityId == Guid.Empty) return;
            if (_attributes == null || _ruleset == null) return;
            if (!_attributes.IsRegistered(entityId)) return;

            var attrs = _attributes.GetAttributes(entityId);
            if (attrs == null || !attrs.HasAttribute<EnergyStat>()) return;

            int current = _attributes.GetAttributeValue<EnergyStat, int>(entityId);
            int max = _ruleset.Energy.EnergyMax;
            int regenBase = _ruleset.Energy.EnergyRegenBase;
            int newVal = EnergyRegenPolicy.ComputeNewCurrent(current, max, regenBase);

            if (newVal == current) return; // no-op: no disparamos evento redundante.

            _attributes.SetAttributeValue<EnergyStat, int>(entityId, newVal);
            TriggerEnergyChanged(entityId, newVal, max);
        }

        public int GetCurrent(Guid entityId)
        {
            if (_attributes == null) return 0;
            if (!_attributes.IsRegistered(entityId)) return 0;
            var attrs = _attributes.GetAttributes(entityId);
            if (attrs == null || !attrs.HasAttribute<EnergyStat>()) return 0;
            return _attributes.GetAttributeValue<EnergyStat, int>(entityId);
        }

        // [FOLLOWUP] EnergyMaxBonus stat para items que suban el cap (plan R8).
        public int GetMax(Guid entityId)
        {
            return _ruleset != null ? _ruleset.Energy.EnergyMax : 0;
        }

        // ======================================================================
        // Event handlers
        // ======================================================================

        /// <summary>
        /// <c>OnRunStart</c> schema: <c>[Guid runId, string rulesetId]</c> — NO trae
        /// player Guid. Este handler queda como diagnostico/no-op; la hidratacion
        /// real la hace el caller externo invocando <see cref="InitializeForEntity"/>
        /// tras spawnear al jugador (plan R1).
        /// </summary>
        private void OnRunStartExternal(params object[] args)
        {
            // Reset del player cacheado: una nueva run siempre empieza sin player
            // hasta que el spawner llame InitializeForEntity explicitamente.
            _playerId = Guid.Empty;
        }

        /// <summary>
        /// <c>OnTurnFinished</c> schema: <c>[Guid entityGuid]</c>. Gateamos por
        /// <see cref="_playerId"/> cacheado — solo el jugador regenera en el FP.
        /// </summary>
        private void OnTurnFinishedExternal(params object[] args)
        {
            if (args == null || args.Length == 0) return;
            if (!(args[0] is Guid entityId)) return;
            if (_playerId == Guid.Empty || entityId != _playerId) return;
            RegenerateAtTurnEnd(entityId);
        }

        /// <summary>
        /// <c>OnCombatStart</c> schema: <c>[Guid roomInstanceId]</c>. No trae player
        /// guid — usamos el <see cref="_playerId"/> cacheado por <c>InitializeForEntity</c>.
        /// <para>
        /// <b>Por qué.</b> Sin esto, el player entra a un combate con la energía que
        /// le quedó del combate anterior. Si terminó al 0, el primer turno queda sin
        /// acciones disponibles (solo end turn). Política JRPG: cada combate arranca
        /// con energía al máximo.
        /// </para>
        /// </summary>
        private void OnCombatStartExternal(params object[] args)
        {
            if (_playerId == Guid.Empty) return;
            if (_attributes == null || _ruleset == null) return;
            if (!_attributes.IsRegistered(_playerId)) return;

            var attrs = _attributes.GetAttributes(_playerId);
            if (attrs == null || !attrs.HasAttribute<EnergyStat>()) return;

            int max = _ruleset.Energy.EnergyMax;
            _attributes.SetAttributeValue<EnergyStat, int>(_playerId, max);
            TriggerEnergyChanged(_playerId, max, max);
        }

        // ======================================================================
        // Helpers
        // ======================================================================

        private void TriggerEnergyChanged(Guid entityId, int current, int max)
        {
            EventManager.Trigger(EventName.OnEnergyChanged, entityId, current, max);
            if (_playerId != Guid.Empty && entityId == _playerId)
            {
                EventManager.Trigger(EventName.OnPlayerEnergyChanged, entityId, current, max);
            }
        }
    }
}
