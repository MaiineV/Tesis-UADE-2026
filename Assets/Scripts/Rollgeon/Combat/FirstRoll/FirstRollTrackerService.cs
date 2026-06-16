using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Combat.FirstRoll
{
    /// <summary>
    /// Implementacion runtime del <see cref="IFirstRollTracker"/>. Marca por entidad si
    /// ya resolvio al menos un roll desde el ultimo <c>OnCombatStart</c> y reinicia el set
    /// en cada nuevo combate. TECHNICAL.md §8.2 + §6 (Berserker "Primer golpe ×3").
    /// <para>
    /// <b>Ordenamiento.</b> El tracker se suscribe a <see cref="EventName.OnRollResolved"/>
    /// con prioridad alta (<see cref="DefaultPriority"/> = 200) para que su handler corra
    /// <i>despues</i> de los consumidores que evaluan <c>PCFirstRollOfCombat</c>
    /// (p. ej. el AttackResolver). De esa forma, durante la primera tirada del combate
    /// la PC ve el set vacio y matchea, y recien al cerrar el ciclo del evento se marca
    /// la entidad como consumida.
    /// </para>
    /// </summary>
    public sealed class FirstRollTrackerService : IFirstRollTracker, IPreloadableService, IDisposable
    {
        /// <summary>Prioridad por defecto — alta para subscribirse al final del orden.</summary>
        public const int DefaultPriority = 200;

        private readonly HashSet<Guid> _consumed = new HashSet<Guid>();
        private bool _combatActive;
        private bool _subscribed;

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        // ====================================================================
        // IPreloadableService
        // ====================================================================

        /// <inheritdoc />
        public void Register()
        {
            ServiceLocator.AddService<IFirstRollTracker>(this, ServiceScope.Global);
            SubscribeEvents();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            UnsubscribeEvents();
            _consumed.Clear();
            _combatActive = false;
        }

        // ====================================================================
        // Test hooks
        // ====================================================================

        /// <summary>Hook para tests — fuerza la subscripcion sin pasar por <see cref="Register"/>.</summary>
        public void SubscribeEventsForTests() => SubscribeEvents();

        /// <summary>Hook para tests — desuscribe los listeners.</summary>
        public void UnsubscribeEventsForTests() => UnsubscribeEvents();

        // ====================================================================
        // Event wiring
        // ====================================================================

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            EventManager.Subscribe(EventName.OnCombatStart, OnCombatStartHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.Subscribe(EventName.OnRollResolved, OnRollResolvedHandler);
            _subscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_subscribed) return;
            EventManager.UnSubscribe(EventName.OnCombatStart, OnCombatStartHandler);
            EventManager.UnSubscribe(EventName.OnCombatEnd, OnCombatEndHandler);
            EventManager.UnSubscribe(EventName.OnRollResolved, OnRollResolvedHandler);
            _subscribed = false;
        }

        // ====================================================================
        // Combat lifecycle handlers
        // ====================================================================

        // Schema EventName.OnCombatStart: args = [Guid roomInstanceId]
        private void OnCombatStartHandler(params object[] args)
        {
            _combatActive = true;
            _consumed.Clear();
        }

        // Schema EventName.OnCombatEnd: args = [Guid roomInstanceId, CombatOutcome outcome]
        private void OnCombatEndHandler(params object[] args)
        {
            _combatActive = false;
            _consumed.Clear();
        }

        // Schema EventName.OnRollResolved: args = [Guid sourceGuid, IReadOnlyList<int> finalFaces]
        private void OnRollResolvedHandler(params object[] args)
        {
            if (!_combatActive) return;
            if (args == null || args.Length == 0) return;
            if (args[0] is not Guid sourceGuid || sourceGuid == Guid.Empty) return;
            _consumed.Add(sourceGuid);
        }

        // ====================================================================
        // IFirstRollTracker
        // ====================================================================

        /// <inheritdoc />
        public bool IsFirstRoll(Guid entityGuid)
        {
            if (!_combatActive) return false;
            if (entityGuid == Guid.Empty) return false;
            return !_consumed.Contains(entityGuid);
        }
    }
}
