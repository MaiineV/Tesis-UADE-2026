using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Rollgeon.Localization
{
    /// <summary>
    /// Notificación de "cambió el idioma" para las vistas que setean su texto por
    /// código con <see cref="LocalizedContent"/>.
    /// <para>
    /// Los labels estáticos usan <c>LocalizeStringEvent</c> y el package los refresca
    /// solo. Los que se setean por código, no: quedan con el texto del idioma anterior
    /// hasta que algo los vuelva a renderizar. Una vista que llama a
    /// <see cref="LocalizedContent"/> una sola vez (al abrirse, al bindear) tiene que
    /// suscribirse acá y re-correr ese mismo render.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Cuelga del evento estático del package y no de <see cref="ILocalizationService"/>
    /// a propósito: así no depende del orden de registro del ServiceLocator, igual que
    /// <see cref="LocalizedContent"/>, que lee <c>LocalizationSettings</c> directo.
    /// </remarks>
    public static class LocalizationRefresh
    {
        private static event Action Changed;
        private static bool _hooked;

        /// <summary>
        /// Registra <paramref name="handler"/> para que corra en cada cambio de idioma.
        /// Idempotente: suscribir dos veces el mismo delegate no lo invoca dos veces.
        /// Siempre emparejar con <see cref="Unsubscribe"/> al cerrarse la vista.
        /// </summary>
        public static void Subscribe(Action handler)
        {
            if (handler == null) return;

            EnsureHooked();
            Changed -= handler;
            Changed += handler;
        }

        /// <summary>Baja a <paramref name="handler"/>. Seguro de llamar sin haber suscrito.</summary>
        public static void Unsubscribe(Action handler)
        {
            if (handler != null) Changed -= handler;
        }

        private static void EnsureHooked()
        {
            if (_hooked) return;

            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
            _hooked = true;
        }

        private static void OnSelectedLocaleChanged(Locale locale)
        {
            // Una vista destruida sin desuscribirse tiraría acá y cortaría la cadena
            // para las demás — cada handler corre aislado.
            var handlers = Changed;
            if (handlers == null) return;

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler).Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// Con "Enter Play Mode sin domain reload" los estáticos sobreviven entre
        /// sesiones de Play y quedarían delegates apuntando a objetos destruidos.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            if (_hooked) LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            Changed = null;
            _hooked = false;
        }
    }
}
