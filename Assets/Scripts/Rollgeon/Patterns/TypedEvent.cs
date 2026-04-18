using System;

namespace Patterns
{
    /// <summary>
    /// Bus tipado complementario a <see cref="EventManager"/>. Especificado en TECHNICAL.md §1.2.1.
    /// <para>
    /// <b>Regla de canal único — no coexistencia.</b> Un evento del juego publica por exactamente
    /// una vía: <see cref="EventManager"/> (legacy, <c>object[]</c>) <b>o</b>
    /// <see cref="TypedEvent{T}"/> (tipado por struct de payload). Nunca ambos. Migrar un evento
    /// significa eliminar su entry del enum <see cref="EventName"/> y reemplazar publishers /
    /// subscribers por la versión tipada. Ver TECHNICAL.md §1.2.1 — párrafo "Regla de canal único".
    /// </para>
    /// <para>
    /// Los payloads son <c>struct</c> (constraint <c>where T : struct</c>) para evitar allocations
    /// por <c>Raise</c> y garantizar inmutabilidad por valor del payload durante la invocación.
    /// </para>
    /// </summary>
    /// <typeparam name="T">Struct de payload. Ej: <see cref="DamageResolvedPayload"/>,
    /// <see cref="HealthChangedPayload"/>, <see cref="ComboMatchedPayload"/>.</typeparam>
    public static class TypedEvent<T> where T : struct
    {
        private static event Action<T> Listeners;

        /// <summary>Suscribe <paramref name="listener"/> al canal tipado <typeparamref name="T"/>.</summary>
        public static void Subscribe(Action<T> listener)
        {
            Listeners += listener;
        }

        /// <summary>Desuscribe <paramref name="listener"/>. No-op si no estaba suscripto.</summary>
        public static void Unsubscribe(Action<T> listener)
        {
            Listeners -= listener;
        }

        /// <summary>Dispara el evento con <paramref name="payload"/>. Null-safe: no lanza si no hay suscriptores.</summary>
        public static void Raise(T payload)
        {
            Listeners?.Invoke(payload);
        }

        /// <summary>
        /// Vacía la lista de suscriptores del canal <typeparamref name="T"/>. Uso típico: teardown
        /// de tests, transiciones de run (<c>ServiceLocator.ClearScope(ServiceScope.Run)</c>) o shutdown.
        /// </summary>
        public static void Clear()
        {
            Listeners = null;
        }
    }
}
