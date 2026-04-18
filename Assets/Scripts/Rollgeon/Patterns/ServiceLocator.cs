using System;
using System.Collections.Generic;
using System.Linq;

namespace Patterns
{
    /// <summary>
    /// Registro estático <c>Type → instance</c> con scopes <see cref="ServiceScope.Global"/> /
    /// <see cref="ServiceScope.Run"/>. Especificado en TECHNICAL.md §1.1.
    /// <para>
    /// Esta clase es la API pura — el bootstrap concreto (scene + <c>ServiceBootstrapSO</c>) vive
    /// en Foundation#0005_CatalogsAndBootstrap. Downstream sólo se acopla a estas firmas.
    /// </para>
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, (object instance, ServiceScope scope)> Services
            = new Dictionary<Type, (object instance, ServiceScope scope)>();

        /// <summary>
        /// Registra (o sobrescribe, upsert) la instancia del servicio <typeparamref name="T"/>
        /// en el scope indicado.
        /// </summary>
        /// <typeparam name="T">Tipo / interface bajo el que se expone el servicio.</typeparam>
        /// <param name="instance">Instancia concreta que implementa / deriva de <typeparamref name="T"/>.</param>
        /// <param name="scope">Scope de vida del servicio. Por defecto <see cref="ServiceScope.Global"/>.</param>
        public static void AddService<T>(object instance, ServiceScope scope = ServiceScope.Global)
        {
            Services[typeof(T)] = (instance, scope);
        }

        /// <summary>
        /// Devuelve el servicio registrado bajo <typeparamref name="T"/>. Asume presencia:
        /// lanza <see cref="KeyNotFoundException"/> si no existe (consistente con §1.1 —
        /// es responsabilidad del bootstrap haber registrado los servicios requeridos).
        /// </summary>
        public static T GetService<T>()
        {
            return (T)Services[typeof(T)].instance;
        }

        /// <summary>
        /// Versión defensiva de <see cref="GetService{T}"/>. Devuelve <c>false</c> y
        /// <c>default(T)</c> si el servicio no está registrado.
        /// </summary>
        public static bool TryGetService<T>(out T service)
        {
            if (Services.TryGetValue(typeof(T), out var entry))
            {
                service = (T)entry.instance;
                return true;
            }

            service = default;
            return false;
        }

        /// <summary>
        /// Borra la entry asociada a <typeparamref name="T"/>. No lanza si la key no existía.
        /// </summary>
        public static void RemoveService<T>()
        {
            Services.Remove(typeof(T));
        }

        /// <summary>
        /// <c>true</c> si hay un servicio registrado bajo <typeparamref name="T"/>.
        /// </summary>
        public static bool HasService<T>()
        {
            return Services.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Borra únicamente las entries cuyo <see cref="ServiceScope"/> sea <paramref name="scope"/>.
        /// Uso típico: <c>ClearScope(ServiceScope.Run)</c> al terminar una run para liberar managers
        /// de run sin tocar la infra <see cref="ServiceScope.Global"/>.
        /// </summary>
        public static void ClearScope(ServiceScope scope)
        {
            var keys = Services
                .Where(kvp => kvp.Value.scope == scope)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keys)
            {
                Services.Remove(key);
            }
        }

        /// <summary>
        /// Vacía el diccionario completo. Uso reservado: shutdown del juego o teardown de tests.
        /// </summary>
        public static void Clear()
        {
            Services.Clear();
        }
    }
}
