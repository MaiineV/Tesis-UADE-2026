using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Patterns.Bootstrap;

namespace Rollgeon.Combat.Status
{
    /// <summary>
    /// Implementación POCO de <see cref="IBleedService"/>, espejo estructural de
    /// <see cref="PoisonService"/> con la diferencia central del GDD (Feature#0085): los
    /// stacks se SUMAN en vez de refrescar. Cada stack vive su propia cuenta regresiva.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Tick al inicio del turno del sangrante</b> (<c>OnTurnStarted</c>): mismo momento
    /// que Veneno, un único <see cref="IDamagePipeline.Resolve"/> por turno con
    /// <c>BaseDamage = DamagePerStack × stacks vivos</c> y
    /// <see cref="AttackKind.DamageOverTime"/>. Después del golpe, cada stack decrementa
    /// su remanente; los que llegan a 0 se descartan.
    /// </para>
    /// <para>
    /// <b>SourceId del tick agregado:</b> se usa el del stack más RECIENTE (mismo criterio
    /// que <c>PoisonService</c> — "el último aplicador se lleva el crédito" — porque un
    /// único golpe de pipeline no puede repartirse entre varias fuentes).
    /// </para>
    /// </remarks>
    public sealed class BleedService : IBleedService, IPreloadableService, IDisposable
    {
        /// <summary>Turnos que dura cada stack individual.</summary>
        public const int TurnsPerStack = 3;

        /// <summary>Daño por turno que aporta CADA stack vivo.</summary>
        public const int DamagePerStack = 10;

        private sealed class BleedStack
        {
            public int Remaining;
            public int DamagePerTick;
            public Guid SourceId;
        }

        // Lazy: Odin puede bypassear el ctor al deserializar desde listas polimórficas.
        private Dictionary<Guid, List<BleedStack>> _states;
        private Dictionary<Guid, List<BleedStack>> States
            => _states ??= new Dictionary<Guid, List<BleedStack>>();

        private EventManager.EventReceiver _onTurnStartedHandler;
        private EventManager.EventReceiver _onCombatEndHandler;
        private EventManager.EventReceiver _onRunEndHandler;

        /// <summary>Junto a Poison/Stun (80).</summary>
        public int Priority => 80;

        // ======================================================================
        // IPreloadableService
        // ======================================================================

        public void Register()
        {
            SubscribeHandlers();

            ServiceLocator.AddService<IBleedService>(this, ServiceScope.Global);
            ServiceLocator.AddService<BleedService>(this, ServiceScope.Global);
        }

        /// <summary>Hook para EditMode tests: arma las suscripciones sin ServiceLocator.</summary>
        public void ConfigureForTests() => SubscribeHandlers();

        private void SubscribeHandlers()
        {
            UnsubscribeHandlers();

            _onTurnStartedHandler = OnTurnStartedExternal;
            _onCombatEndHandler = OnScopeEndedExternal;
            _onRunEndHandler = OnScopeEndedExternal;

            EventManager.Subscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
            EventManager.Subscribe(EventName.OnCombatEnd, _onCombatEndHandler);
            EventManager.Subscribe(EventName.OnRunEnd, _onRunEndHandler);
        }

        private void UnsubscribeHandlers()
        {
            if (_onTurnStartedHandler != null)
            {
                EventManager.UnSubscribe(EventName.OnTurnStarted, _onTurnStartedHandler);
                _onTurnStartedHandler = null;
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
            States.Clear();
        }

        // ======================================================================
        // IBleedService
        // ======================================================================

        /// <inheritdoc />
        public void AddStack(Guid entity, Guid source, int stacks = 1)
        {
            if (entity == Guid.Empty) return;
            if (stacks <= 0) return;

            var list = States.TryGetValue(entity, out var existing) ? existing : (States[entity] = new List<BleedStack>());
            for (int i = 0; i < stacks; i++)
            {
                list.Add(new BleedStack
                {
                    Remaining = TurnsPerStack,
                    DamagePerTick = DamagePerStack,
                    SourceId = source,
                });
            }

            EventManager.Trigger(EventName.OnBleedApplied, entity, list.Count);
        }

        /// <inheritdoc />
        public bool IsBleeding(Guid entity)
        {
            if (entity == Guid.Empty) return false;
            return States.TryGetValue(entity, out var list) && list.Count > 0;
        }

        /// <inheritdoc />
        public int GetStacks(Guid entity)
        {
            if (entity == Guid.Empty) return 0;
            return States.TryGetValue(entity, out var list) ? list.Count : 0;
        }

        /// <inheritdoc />
        public int GetMaxRemainingTurns(Guid entity)
        {
            if (entity == Guid.Empty) return 0;
            if (!States.TryGetValue(entity, out var list) || list.Count == 0) return 0;

            int max = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Remaining > max) max = list[i].Remaining;
            return max;
        }

        /// <inheritdoc />
        public void Clear(Guid entity)
        {
            if (entity == Guid.Empty) return;
            if (!States.Remove(entity)) return;
            EventManager.Trigger(EventName.OnBleedExpired, entity);
        }

        /// <inheritdoc />
        public void ClearAll() => States.Clear();

        // ======================================================================
        // Event handlers
        // ======================================================================

        private void OnTurnStartedExternal(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid entity)) return;
            if (!States.TryGetValue(entity, out var list) || list.Count == 0) return;

            int totalDamage = 0;
            Guid latestSource = Guid.Empty;
            for (int i = 0; i < list.Count; i++)
            {
                totalDamage += list[i].DamagePerTick;
                latestSource = list[i].SourceId; // último de la lista = más reciente (se appendea al final).
            }

            if (totalDamage > 0
                && ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) && pipeline != null)
            {
                pipeline.Resolve(new DamageContext
                {
                    SourceId = latestSource,
                    TargetId = entity,
                    BaseDamage = totalDamage,
                    Kind = AttackKind.DamageOverTime,
                });
            }

            // Decrementar y descartar vencidos DESPUÉS del golpe: el daño de este turno
            // cuenta con todos los stacks que estaban vivos al empezarlo.
            for (int i = list.Count - 1; i >= 0; i--)
            {
                list[i].Remaining--;
                if (list[i].Remaining <= 0) list.RemoveAt(i);
            }

            EventManager.Trigger(EventName.OnBleedTicked, entity, totalDamage, list.Count);

            if (list.Count > 0) return;
            States.Remove(entity);
            EventManager.Trigger(EventName.OnBleedExpired, entity);
        }

        private void OnScopeEndedExternal(params object[] args) => ClearAll();
    }
}
