using System;
using System.Collections.Generic;
using Rollgeon.Attributes.Modifiers;

namespace Rollgeon.Attributes
{
    /// <summary>
    /// Base abstracta de todos los stats de runtime. Implementa el 80% comun:
    /// - almacenamiento del raw value,
    /// - stack de modificadores tipados,
    /// - validacion de tipo en los accessors genericos,
    /// - aplicacion de <see cref="ModifierDirection.Intrinsic"/> en orden de insercion,
    /// - callbacks vinculados via <see cref="LinkAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Subclass contract.</b> Un stat concreto (ej: <c>Health : BaseAttribute&lt;int&gt;</c>)
    /// solo necesita implementar <see cref="CreateDuplicate"/> si quiere clonar
    /// estado extra propio. Por defecto se clona el <see cref="_rawValue"/>;
    /// los modificadores NO se clonan (hero "fresco" arranca sin buffs).
    /// </para>
    /// <para>
    /// <b>Direction.</b> <see cref="GetModifiedValue{T}"/> solo recorre modifiers con
    /// <see cref="ModifierDirection.Intrinsic"/>. Los direccionales
    /// (<see cref="ModifierDirection.Outgoing"/> / <see cref="ModifierDirection.Incoming"/>)
    /// los evalua la pipeline de dano/heal (§12, §17.M), no este accessor.
    /// </para>
    /// </remarks>
    /// <typeparam name="TValue">Tipo primitivo del valor.</typeparam>
    [Serializable]
    public abstract class BaseAttribute<TValue> : IModifiable<TValue>
    {
        protected TValue _rawValue;
        protected readonly List<Modifier<TValue>> _modifiers = new List<Modifier<TValue>>();
        private event Action<Guid> _linkedCallbacks;

        protected BaseAttribute() { }

        protected BaseAttribute(TValue initial)
        {
            _rawValue = initial;
        }

        // --- IAttribute<TValue> -------------------------------------------
        public TValue Value
        {
            get => _rawValue;
            set => _rawValue = value;
        }

        // --- IModifiable<TValue> ------------------------------------------
        public TValue ModifiedValue => ComputeModifiedValue();

        // --- IAttribute ---------------------------------------------------
        public T GetValue<T>()
        {
            AssertTypeMatches<T>();
            return (T)(object)_rawValue;
        }

        public void SetValue<T>(T value)
        {
            AssertTypeMatches<T>();
            _rawValue = (TValue)(object)value;
        }

        public Type GetValueType() => typeof(TValue);

        public virtual string GetAttributeName() => GetType().Name;

        public IAttribute Duplicate()
        {
            var clone = CreateDuplicate();
            // Modificadores NO se clonan (TECHNICAL.md §2.2 / plan §10 item 4).
            return clone;
        }

        // --- IModifiable --------------------------------------------------
        public T GetModifiedValue<T>()
        {
            AssertTypeMatches<T>();
            return (T)(object)ComputeModifiedValue();
        }

        public virtual void SubscribeModifier()
        {
            // Hook reservado para extension — los consumidores avanzados pueden
            // override para recalcular derivados al cambiar el stack.
        }

        public bool AddModifier<T>(IModifier<T> modifier)
        {
            if (typeof(T) != typeof(TValue))
            {
                return false;
            }

            if (modifier is Modifier<TValue> concrete)
            {
                _modifiers.Add(concrete);
                _linkedCallbacks?.Invoke(concrete.ModifierId);
                return true;
            }

            // Wrapper interface-only implementors no caen aca (el container
            // produce Modifier<T>), pero contemplamos el caso de hacer una
            // copia para preservar la invariante "todos los mods son
            // Modifier<TValue>" que usa el accessor modificado.
            return false;
        }

        public void RemoveModifier(Guid modifierId)
        {
            int idx = _modifiers.FindIndex(m => m.ModifierId == modifierId);
            if (idx < 0)
            {
                return;
            }

            var removed = _modifiers[idx];
            _modifiers.RemoveAt(idx);
            _linkedCallbacks?.Invoke(removed.ModifierId);
        }

        public void LinkAttribute(Action<Guid> callback)
        {
            if (callback == null)
            {
                return;
            }
            _linkedCallbacks += callback;
        }

        // --- Internals (usado por AttributesManager via helpers) ----------

        /// <summary>Enumera todos los modifiers crudos en orden de insercion.</summary>
        public IReadOnlyList<Modifier<TValue>> GetRawModifiers() => _modifiers;

        /// <summary>
        /// Quita de la lista el mod cuyo id coincida sin disparar callbacks.
        /// Util para el barrido del <see cref="AttributesManager"/> al escuchar
        /// <c>OnModifierRemoved</c> (el dispatch del callback ya ocurrio).
        /// </summary>
        public bool RemoveModifierSilent(Guid modifierId)
        {
            int idx = _modifiers.FindIndex(m => m.ModifierId == modifierId);
            if (idx < 0)
            {
                return false;
            }
            _modifiers.RemoveAt(idx);
            return true;
        }

        // --- Abstract / hooks ---------------------------------------------

        /// <summary>
        /// Crea la instancia concreta de la misma subclase con el <see cref="_rawValue"/>
        /// clonado. Los modificadores NO se clonan. Override si el stat concreto
        /// guarda flags / estado extra que tambien deba duplicarse.
        /// </summary>
        protected abstract BaseAttribute<TValue> CreateDuplicate();

        // --- Helpers ------------------------------------------------------

        private TValue ComputeModifiedValue()
        {
            TValue current = _rawValue;
            foreach (var mod in _modifiers)
            {
                if (mod.Direction == ModifierDirection.Intrinsic)
                {
                    current = mod.ApplyModifier(current);
                }
            }
            return current;
        }

        private static void AssertTypeMatches<T>()
        {
            if (typeof(T) != typeof(TValue))
            {
                throw new InvalidCastException(
                    $"[BaseAttribute<{typeof(TValue).Name}>] Expected {typeof(TValue).Name}, got {typeof(T).Name}. " +
                    "Check the generic type argument.");
            }
        }
    }
}
