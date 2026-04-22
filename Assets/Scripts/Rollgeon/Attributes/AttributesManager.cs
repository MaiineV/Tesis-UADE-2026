using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes.Modifiers;

namespace Rollgeon.Attributes
{
    /// <summary>
    /// Servicio centralizado de lookup + mutacion de atributos por entidad.
    /// Indexado por <see cref="Guid"/> (TECHNICAL.md §2.3). Clase plana C# —
    /// NO es <c>MonoBehaviour</c> ni <c>ScriptableObject</c>. Lo registra
    /// <c>ServiceBootstrapSO</c> (Foundation#0005) en el <see cref="ServiceLocator"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Thread-safety.</b> Single-threaded / main-thread only.
    /// </para>
    /// <para>
    /// <b>Integracion con events.</b> Al cambiar <c>Value</c> / agregar / quitar
    /// modificadores se disparan los eventos correspondientes del
    /// <see cref="EventManager"/> (<c>OnAttributeChanged</c>, <c>OnModifierAdded</c>,
    /// <c>OnModifierRemoved</c>). Ademas escucha <c>OnModifierRemoved</c> para
    /// barrer el <see cref="Modifier{T}"/> del stack del atributo que lo aloja
    /// (centralizado aca para no multiplicar handlers por N stats x M entidades).
    /// </para>
    /// </remarks>
    public class AttributesManager : IDisposable
    {
        /// <summary>
        /// Toggle de debug: si al hacer un lookup sobre un <see cref="Guid"/> no
        /// registrado se loguea un warning (<c>true</c>) o se lanza una excepcion
        /// (<c>false</c>). Default <c>true</c> para no tumbar runs por bug de
        /// orden de registro.
        /// </summary>
        public static bool LogMissingEntityAsWarning = true;

        private readonly Dictionary<Guid, ModifiableAttributes> _byEntity
            = new Dictionary<Guid, ModifiableAttributes>();

        private readonly EventManager.EventReceiver _onModifierRemovedHandler;

        public AttributesManager()
        {
            _onModifierRemovedHandler = OnModifierRemovedExternal;
            EventManager.Subscribe(EventName.OnModifierRemoved, _onModifierRemovedHandler);
        }

        /// <summary>Desuscribe el handler global. Llamar al cerrar scope run / shutdown.</summary>
        public void Dispose()
        {
            EventManager.UnSubscribe(EventName.OnModifierRemoved, _onModifierRemovedHandler);
            _byEntity.Clear();
        }

        // --- Registro -----------------------------------------------------

        public void Register(Guid entityId, ModifiableAttributes attrs)
        {
            if (entityId == Guid.Empty)
            {
                throw new ArgumentException("entityId must not be Guid.Empty", nameof(entityId));
            }
            if (attrs == null)
            {
                throw new ArgumentNullException(nameof(attrs));
            }
            attrs.EnsureInitialized();
            _byEntity[entityId] = attrs;
        }

        public void Unregister(Guid entityId)
        {
            _byEntity.Remove(entityId);
        }

        public bool IsRegistered(Guid entityId)
        {
            return _byEntity.ContainsKey(entityId);
        }

        /// <summary>
        /// Enumera (Guid, ModifiableAttributes) registrados. Consumido por sistemas que
        /// necesitan iterar todas las entidades de combate (ej. AI de area-effect,
        /// checks transversales). Snapshot — copiar si vas a mutar durante iteración.
        /// </summary>
        public IEnumerable<KeyValuePair<Guid, ModifiableAttributes>> EnumerateEntries()
        {
            return _byEntity;
        }

        // --- Lookups ------------------------------------------------------

        public ModifiableAttributes GetAttributes(Guid entityId)
        {
            if (!_byEntity.TryGetValue(entityId, out var attrs))
            {
                ReportMissing(entityId);
                return null;
            }
            return attrs;
        }

        public TAttribute GetAttribute<TAttribute>(Guid entityId)
            where TAttribute : class, IModifiable
        {
            var attrs = GetAttributes(entityId);
            if (attrs == null || !attrs.HasAttribute<TAttribute>())
            {
                return null;
            }
            return attrs.GetAttribute<TAttribute>();
        }

        // --- Valor --------------------------------------------------------

        public TValue GetAttributeValue<TAttribute, TValue>(Guid entityId)
            where TAttribute : class, IModifiable<TValue>
        {
            var attr = GetAttribute<TAttribute>(entityId);
            return attr != null ? attr.Value : default;
        }

        public TValue GetAttributeModifiedValue<TAttribute, TValue>(Guid entityId)
            where TAttribute : class, IModifiable<TValue>
        {
            var attr = GetAttribute<TAttribute>(entityId);
            return attr != null ? attr.ModifiedValue : default;
        }

        public void SetAttributeValue<TAttribute, TValue>(Guid entityId, TValue value)
            where TAttribute : class, IModifiable<TValue>
        {
            var attr = GetAttribute<TAttribute>(entityId);
            if (attr == null)
            {
                return;
            }
            attr.Value = value;
            EventManager.Trigger(EventName.OnAttributeChanged, entityId, typeof(TAttribute));
        }

        public void Modify<TAttribute, TValue>(Guid entityId, Func<TValue, TValue> mutator)
            where TAttribute : class, IModifiable<TValue>
        {
            if (mutator == null)
            {
                throw new ArgumentNullException(nameof(mutator));
            }
            var attr = GetAttribute<TAttribute>(entityId);
            if (attr == null)
            {
                return;
            }
            attr.Value = mutator(attr.Value);
            EventManager.Trigger(EventName.OnAttributeChanged, entityId, typeof(TAttribute));
        }

        // --- Modificadores ------------------------------------------------

        public bool AddModifier<TAttribute, TValue>(Guid entityId, Modifier<TValue> modifier)
            where TAttribute : class, IModifiable<TValue>
        {
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }
            var attr = GetAttribute<TAttribute>(entityId);
            if (attr == null)
            {
                return false;
            }

            bool ok = attr.AddModifier<TValue>(modifier);
            if (ok)
            {
                EventManager.Trigger(
                    EventName.OnModifierAdded,
                    entityId, typeof(TAttribute), modifier.ModifierId);
                EventManager.Trigger(EventName.OnAttributeChanged, entityId, typeof(TAttribute));
            }
            return ok;
        }

        public bool RemoveModifier<TAttribute, TValue>(Guid entityId, Guid modifierId)
            where TAttribute : class, IModifiable<TValue>
        {
            var attr = GetAttribute<TAttribute>(entityId);
            if (attr == null)
            {
                return false;
            }

            if (attr is BaseAttribute<TValue> baseAttr)
            {
                // Localizamos primero y llamamos RemoveAndNotify fuera del iterador
                // para evitar mutar la lista mientras se enumera (el handler
                // global OnModifierRemovedExternal la modifica on-trigger).
                Modifier<TValue> target = null;
                foreach (var m in baseAttr.GetRawModifiers())
                {
                    if (m.ModifierId == modifierId)
                    {
                        target = m;
                        break;
                    }
                }
                if (target == null)
                {
                    return false;
                }
                target.RemoveAndNotify();
                return true;
            }

            // Fallback: implementador custom de IModifiable sin BaseAttribute.
            attr.RemoveModifier(modifierId);
            EventManager.Trigger(EventName.OnModifierRemoved, entityId, modifierId);
            return true;
        }

        /// <summary>
        /// Remueve todos los modifiers de la entidad dada cuyo <see cref="Modifier{T}.SourceId"/>
        /// matchee <paramref name="sourceId"/>, dentro del atributo indicado.
        /// </summary>
        public int RemoveModifierBySource<TAttribute, TValue>(Guid entityId, Guid sourceId)
            where TAttribute : class, IModifiable<TValue>
        {
            if (sourceId == Guid.Empty)
            {
                return 0;
            }

            var attr = GetAttribute<TAttribute>(entityId);
            if (attr == null)
            {
                return 0;
            }

            if (attr is BaseAttribute<TValue> baseAttr)
            {
                var matching = new List<Modifier<TValue>>();
                foreach (var m in baseAttr.GetRawModifiers())
                {
                    if (m.SourceId == sourceId)
                    {
                        matching.Add(m);
                    }
                }
                foreach (var m in matching)
                {
                    m.RemoveAndNotify();
                }
                return matching.Count;
            }

            return 0;
        }

        /// <summary>
        /// Barrido global: remueve todos los modifiers cuyo <see cref="Modifier{T}.SourceId"/>
        /// matchee <paramref name="sourceId"/>, en TODAS las entidades registradas
        /// y TODOS sus atributos. <b>No-op si <paramref name="sourceId"/> es
        /// <see cref="Guid.Empty"/></b> (regla de seguridad para evitar borrado
        /// masivo accidental de mods anonimos).
        /// </summary>
        public int RemoveAllModifiersBySource(Guid sourceId)
        {
            if (sourceId == Guid.Empty)
            {
                return 0;
            }

            int removed = 0;
            // Copiamos a lista para no mutar mientras iteramos: los RemoveAndNotify
            // disparan eventos que borran del stack del atributo inmediatamente.
            var entitySnapshot = new List<KeyValuePair<Guid, ModifiableAttributes>>(_byEntity);
            foreach (var entityKvp in entitySnapshot)
            {
                foreach (var attrKvp in entityKvp.Value.EnumerateEntries())
                {
                    removed += RemoveMatchingFromAttribute(attrKvp.Value, sourceId);
                }
            }
            return removed;
        }

        // --- Internals ----------------------------------------------------

        /// <summary>
        /// Handler suscripto a <c>OnModifierRemoved</c>. Cuando un mod dispara
        /// <see cref="Modifier{T}.RemoveAndNotify"/>, el <see cref="BaseAttribute{TValue}"/>
        /// que lo aloja NO se entera directamente — este handler lo busca por
        /// id y lo saca del stack.
        /// </summary>
        private void OnModifierRemovedExternal(params object[] args)
        {
            if (args == null || args.Length < 2) return;
            if (!(args[0] is Guid carrierId)) return;
            if (!(args[1] is Guid modifierId)) return;

            if (!_byEntity.TryGetValue(carrierId, out var attrs))
            {
                return;
            }

            foreach (var kvp in attrs.EnumerateEntries())
            {
                if (TryRemoveFromAttributeSilent(kvp.Value, modifierId))
                {
                    EventManager.Trigger(EventName.OnAttributeChanged, carrierId, kvp.Key);
                    return;
                }
            }
        }

        private static bool TryRemoveFromAttributeSilent(IModifiable attr, Guid modifierId)
        {
            Type valueType = attr.GetValueType();
            if (valueType == typeof(int)) return RemoveSilentTyped<int>(attr, modifierId);
            if (valueType == typeof(float)) return RemoveSilentTyped<float>(attr, modifierId);
            if (valueType == typeof(bool)) return RemoveSilentTyped<bool>(attr, modifierId);
            // Fallback generico: confiar en la API publica (dispara callbacks).
            attr.RemoveModifier(modifierId);
            return true;
        }

        private static bool RemoveSilentTyped<TVal>(IModifiable attr, Guid modifierId)
        {
            if (attr is BaseAttribute<TVal> baseAttr)
            {
                return baseAttr.RemoveModifierSilent(modifierId);
            }
            attr.RemoveModifier(modifierId);
            return true;
        }

        private static int RemoveMatchingFromAttribute(IModifiable attr, Guid sourceId)
        {
            Type valueType = attr.GetValueType();
            if (valueType == typeof(int)) return RemoveMatchingTyped<int>(attr, sourceId);
            if (valueType == typeof(float)) return RemoveMatchingTyped<float>(attr, sourceId);
            if (valueType == typeof(bool)) return RemoveMatchingTyped<bool>(attr, sourceId);
            return 0;
        }

        private static int RemoveMatchingTyped<TVal>(IModifiable attr, Guid sourceId)
        {
            if (!(attr is BaseAttribute<TVal> baseAttr))
            {
                return 0;
            }
            var matching = new List<Modifier<TVal>>();
            foreach (var m in baseAttr.GetRawModifiers())
            {
                if (m.SourceId == sourceId)
                {
                    matching.Add(m);
                }
            }
            foreach (var m in matching)
            {
                m.RemoveAndNotify();
            }
            return matching.Count;
        }

        private static void ReportMissing(Guid entityId)
        {
            string msg = $"[AttributesManager] Entity '{entityId}' is not registered.";
            if (LogMissingEntityAsWarning)
            {
                UnityEngine.Debug.LogWarning(msg);
            }
            else
            {
                throw new KeyNotFoundException(msg);
            }
        }
    }
}
