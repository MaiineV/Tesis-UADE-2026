using System;
using Patterns;

namespace Rollgeon.Attributes.Modifiers
{
    /// <summary>
    /// Modificador tipado con lifecycle propio: se auto-suscribe a eventos
    /// segun <see cref="Lifetime"/> para tickear / removerse. Especificado en
    /// TECHNICAL.md §3.1 (actualizado 2026-04-18 con CarrierId + SourceId).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Serializacion.</b> Las suscripciones al <see cref="EventManager"/> no
    /// se persisten. <see cref="_resolvedOp"/> esta marcado <c>[NonSerialized]</c>.
    /// Al restaurar desde save, el <c>ISaveable</c> que contiene el modifier
    /// debe invocar <see cref="OnLoad"/> para re-hookear (TECHNICAL.md §3.5).
    /// </para>
    /// <para>
    /// <b>Thread-safety.</b> Single-threaded / main-thread only (TECHNICAL.md §0).
    /// </para>
    /// </remarks>
    /// <typeparam name="T">Tipo del amount (<c>int</c>, <c>float</c>, <c>bool</c>).</typeparam>
    [Serializable]
    public class Modifier<T> : IModifier<T>
    {
        // --- Fields (publicos para serializacion; todos los setters path-through) -
        public T Amount;
        public ModifierOperation Operation;
        public int Duration;
        public Guid ModifierId;

        /// <summary>
        /// Entidad que CARGA el modificador (<c>Entity.InstanceId</c>). Usado
        /// para tick-gating (el turno de esta entidad decrementa <see cref="Duration"/>)
        /// y como primer argumento del evento <c>OnModifierRemoved</c>.
        /// </summary>
        public Guid CarrierId;

        /// <summary>
        /// Entidad o efecto que ORIGINO el modificador. <see cref="Guid.Empty"/>
        /// si no aplica. Consumido por
        /// <see cref="AttributesManager.RemoveAllModifiersBySource"/> y equivalentes.
        /// </summary>
        public Guid SourceId;

        public ModifierDirection Direction;
        public ModifierLifetime Lifetime;
        public EventName TickEvent;

        [NonSerialized]
        private Func<T, T, T> _resolvedOp;

        // --- IModifier (metadata) -----------------------------------------
        Guid IModifier.ModifierId => ModifierId;
        Guid IModifier.CarrierId => CarrierId;
        Guid IModifier.SourceId => SourceId;
        ModifierDirection IModifier.Direction => Direction;
        ModifierLifetime IModifier.Lifetime => Lifetime;
        ModifierOperation IModifier.Operation => Operation;
        EventName IModifier.TickEvent => TickEvent;

        // --- IModifier<T> --------------------------------------------------
        T IModifier<T>.Amount => Amount;

        /// <summary>Ctor sin-args para deserializer (Unity/Odin). No invoca <see cref="OnLoad"/>.</summary>
        public Modifier() { }

        /// <summary>
        /// Ctor completo — asigna campos, genera <see cref="ModifierId"/> e invoca
        /// <see cref="OnLoad"/> para subscribirse segun <see cref="Lifetime"/>.
        /// </summary>
        public Modifier(T amount, ModifierOperation op, int duration,
                        Guid carrierId, Guid sourceId,
                        ModifierDirection dir, ModifierLifetime lifetime, EventName tickEvent)
        {
            Amount = amount;
            Operation = op;
            Duration = duration;
            ModifierId = Guid.NewGuid();
            CarrierId = carrierId;
            SourceId = sourceId;
            Direction = dir;
            Lifetime = lifetime;
            TickEvent = tickEvent;
            OnLoad();
        }

        /// <summary>
        /// Subscribe al evento que corresponda segun <see cref="Lifetime"/>. Idempotente:
        /// UnSubscribe + Subscribe garantiza una sola suscripcion — esta propiedad
        /// es critica para la rehidratacion desde save (TECHNICAL.md §3.5).
        /// </summary>
        public void OnLoad()
        {
            _resolvedOp = OperationResolver.Resolve<T>(Operation);

            switch (Lifetime)
            {
                case ModifierLifetime.Turns:
                    EventManager.UnSubscribe(TickEvent, OnTickTriggered);
                    EventManager.Subscribe(TickEvent, OnTickTriggered);
                    break;
                case ModifierLifetime.Run:
                    EventManager.UnSubscribe(EventName.OnRunEnd, OnScopeEnded);
                    EventManager.Subscribe(EventName.OnRunEnd, OnScopeEnded);
                    break;
                case ModifierLifetime.Encounter:
                    EventManager.UnSubscribe(EventName.OnCombatEnd, OnScopeEnded);
                    EventManager.Subscribe(EventName.OnCombatEnd, OnScopeEnded);
                    break;
                case ModifierLifetime.Permanent:
                    // No subscription — vive hasta EffRemoveModifier explicito.
                    break;
            }
        }

        /// <summary>Aplica la operacion. Lazy-resuelve <see cref="_resolvedOp"/> si deserializado.</summary>
        public T ApplyModifier(T value)
        {
            if (_resolvedOp == null)
            {
                _resolvedOp = OperationResolver.Resolve<T>(Operation);
            }
            return _resolvedOp(value, Amount);
        }

        /// <summary>Desuscribe del evento de scope/tick. Idempotente.</summary>
        public void OnRemove()
        {
            switch (Lifetime)
            {
                case ModifierLifetime.Turns:
                    EventManager.UnSubscribe(TickEvent, OnTickTriggered);
                    break;
                case ModifierLifetime.Run:
                    EventManager.UnSubscribe(EventName.OnRunEnd, OnScopeEnded);
                    break;
                case ModifierLifetime.Encounter:
                    EventManager.UnSubscribe(EventName.OnCombatEnd, OnScopeEnded);
                    break;
            }
        }

        /// <summary>
        /// Remueve el mod y dispara <c>OnModifierRemoved(CarrierId, ModifierId)</c>.
        /// El <see cref="AttributesManager"/> escucha este evento para limpiar el
        /// <see cref="Modifier{T}"/> del stack del atributo concreto.
        /// </summary>
        public void RemoveAndNotify()
        {
            OnRemove();
            EventManager.Trigger(EventName.OnModifierRemoved, CarrierId, ModifierId);
        }

        // --- Handlers ------------------------------------------------------

        /// <summary>
        /// Handler para <see cref="TickEvent"/>. Compara <see cref="CarrierId"/>
        /// con <c>args[0]</c> (Guid de la entidad que dispara el tick) y decrementa
        /// <see cref="Duration"/>; si llega a 0, invoca <see cref="RemoveAndNotify"/>.
        /// </summary>
        private void OnTickTriggered(params object[] args)
        {
            if (args == null || args.Length == 0 || !(args[0] is Guid triggerGuid))
            {
                return;
            }

            if (CarrierId != triggerGuid)
            {
                return;
            }

            Duration--;
            if (Duration <= 0)
            {
                RemoveAndNotify();
            }
        }

        /// <summary>Handler para <c>OnRunEnd</c> / <c>OnCombatEnd</c> segun <see cref="Lifetime"/>.</summary>
        private void OnScopeEnded(params object[] args)
        {
            RemoveAndNotify();
        }
    }
}
