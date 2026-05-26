using System.Collections.Generic;

namespace Patterns
{
    /// <summary>
    /// Event bus legacy: string-keyed vía <see cref="EventName"/> con payload <c>object[]</c>.
    /// Especificado en TECHNICAL.md §1.2.
    /// <para>
    /// Coexiste con <see cref="TypedEvent{T}"/> bajo la <b>regla de canal único</b> — un evento
    /// publica por una vía o por la otra, nunca por ambas.
    /// </para>
    /// </summary>
    public static class EventManager
    {
        /// <summary>
        /// Firma del subscriber. Los publishers pasan sus args vía <c>params object[]</c>; los
        /// subscribers castean posicionalmente según el schema documentado en <see cref="EventName"/>.
        /// </summary>
        public delegate void EventReceiver(params object[] parameter);

        private static readonly Dictionary<EventName, EventReceiver> EventDictionary
            = new Dictionary<EventName, EventReceiver>();

        /// <summary>
        /// Suscribe <paramref name="method"/> al evento <paramref name="eventType"/>.
        /// Si la key no existía, la crea.
        /// </summary>
        public static void Subscribe(EventName eventType, EventReceiver method)
        {
            if (EventDictionary.TryGetValue(eventType, out var existing))
            {
                EventDictionary[eventType] = existing + method;
            }
            else
            {
                EventDictionary[eventType] = method;
            }
        }

        /// <summary>
        /// Desuscribe <paramref name="method"/> del evento <paramref name="eventType"/>.
        /// No lanza si la key nunca fue suscripta.
        /// </summary>
        public static void UnSubscribe(EventName eventType, EventReceiver method)
        {
            if (!EventDictionary.TryGetValue(eventType, out var existing))
            {
                return;
            }

            var updated = existing - method;
            if (updated == null)
            {
                EventDictionary.Remove(eventType);
            }
            else
            {
                EventDictionary[eventType] = updated;
            }
        }

        /// <summary>
        /// Dispara el evento con los parámetros dados. Null-safe: no lanza si no hay suscriptores.
        /// </summary>
        /// <remarks>
        /// Por convención transversal (TECHNICAL.md §1.2 línea 525), cuando el evento referencia
        /// una entidad, <c>parameters[0]</c> debe ser un <see cref="System.Guid"/>
        /// (el <c>InstanceId</c> de la entidad primaria). Esta infra no valida — es contrato del publisher.
        /// </remarks>
        public static void Trigger(EventName eventType, params object[] parameters)
        {
            if (EventDictionary.TryGetValue(eventType, out var receiver))
            {
                receiver?.Invoke(parameters);
            }
        }

        /// <summary>
        /// Vacía el diccionario entero. Uso reservado: shutdown del juego, teardown de tests,
        /// o transiciones de run si el diseñador lo decidiera.
        /// </summary>
        public static void ResetEventDictionary()
        {
            EventDictionary.Clear();
        }
    }
}
