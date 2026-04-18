using System;
using System.Collections.Generic;
using Sirenix.Serialization;

namespace Rollgeon.Attributes
{
    /// <summary>
    /// Contenedor tipado por <see cref="Type"/>. Invariante: una entidad no
    /// puede tener dos instancias del mismo stat (TECHNICAL.md §2.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Odin.</b> El diccionario <see cref="_attributes"/> usa claves <see cref="Type"/>
    /// + valores polimorficos <see cref="IModifiable"/> — Unity <c>SerializeField</c>
    /// no soporta ese shape. Se usa <see cref="OdinSerializeAttribute"/> para
    /// que Odin maneje la serializacion.
    /// </para>
    /// <para>
    /// <b>Validacion de tipo.</b> Los accessors <see cref="GetAttributeValue{T, V}"/> /
    /// <see cref="SetAttributeValue{T, V}"/> lanzan <see cref="KeyNotFoundException"/>
    /// si el stat no existe y <see cref="InvalidCastException"/> si el parametro
    /// <c>V</c> no coincide con el tipo del stat.
    /// </para>
    /// </remarks>
    [Serializable]
    public class ModifiableAttributes
    {
        [OdinSerialize]
        private Dictionary<Type, IModifiable> _attributes = new Dictionary<Type, IModifiable>();

        /// <summary>
        /// Inicializa el diccionario si quedo <c>null</c> por deserializacion
        /// o construccion default. Idempotente.
        /// </summary>
        public void EnsureInitialized()
        {
            if (_attributes == null)
            {
                _attributes = new Dictionary<Type, IModifiable>();
            }
        }

        // --- Lookup -------------------------------------------------------

        public bool HasAttribute<T>() where T : class, IModifiable
        {
            EnsureInitialized();
            return _attributes.ContainsKey(typeof(T));
        }

        public T GetAttribute<T>() where T : class, IModifiable
        {
            EnsureInitialized();
            if (!_attributes.TryGetValue(typeof(T), out var attr))
            {
                throw new KeyNotFoundException(
                    $"[ModifiableAttributes] Attribute '{typeof(T).Name}' is not registered on this container.");
            }
            return (T)attr;
        }

        public void SetAttribute<T>(IModifiable attribute) where T : class, IModifiable
        {
            EnsureInitialized();
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }
            _attributes[typeof(T)] = attribute;
        }

        /// <summary>
        /// Registra un atributo usando su tipo runtime como clave. Conveniente
        /// cuando el caller no tiene el <c>typeof(T)</c> concreto a mano.
        /// </summary>
        public void SetAttribute(IModifiable attribute)
        {
            EnsureInitialized();
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }
            _attributes[attribute.GetType()] = attribute;
        }

        public void RemoveAttribute<T>() where T : class, IModifiable
        {
            EnsureInitialized();
            _attributes.Remove(typeof(T));
        }

        // --- Valor --------------------------------------------------------

        public V GetAttributeValue<T, V>() where T : class, IModifiable
        {
            return GetAttribute<T>().GetValue<V>();
        }

        public void SetAttributeValue<T, V>(V value) where T : class, IModifiable
        {
            GetAttribute<T>().SetValue<V>(value);
        }

        public V GetAttributeModifiedValue<T, V>() where T : class, IModifiable
        {
            return GetAttribute<T>().GetModifiedValue<V>();
        }

        // --- Enumeracion --------------------------------------------------

        public List<IModifiable> GetAllAttributes()
        {
            EnsureInitialized();
            return new List<IModifiable>(_attributes.Values);
        }

        /// <summary>
        /// Enumera pares <c>(Type, IModifiable)</c> sin copiar. Usado por
        /// <see cref="AttributesManager"/> para limpiar modifiers por id
        /// cuando no sabe en que atributo viven.
        /// </summary>
        public IEnumerable<KeyValuePair<Type, IModifiable>> EnumerateEntries()
        {
            EnsureInitialized();
            return _attributes;
        }

        // --- Clone --------------------------------------------------------

        /// <summary>
        /// Deep clone del contenedor: cada atributo se duplica via
        /// <see cref="IAttribute.Duplicate"/> (TECHNICAL.md §2.2). Usado al
        /// iniciar run para no mutar la plantilla del <c>ClassHeroSO</c>.
        /// </summary>
        public ModifiableAttributes DuplicateAttributes()
        {
            EnsureInitialized();
            var clone = new ModifiableAttributes();
            clone.EnsureInitialized();

            foreach (var kvp in _attributes)
            {
                var duplicated = kvp.Value.Duplicate();
                if (duplicated is IModifiable modifiable)
                {
                    clone._attributes[kvp.Key] = modifiable;
                }
            }

            return clone;
        }
    }
}
