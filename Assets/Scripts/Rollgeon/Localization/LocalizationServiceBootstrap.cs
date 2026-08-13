using System;
using Patterns;
using Rollgeon.Patterns.Bootstrap;
using UnityEngine.Localization.Settings;

namespace Rollgeon.Localization
{
    /// <summary>
    /// Engancha la localización al bootstrap global. Va en la lista
    /// <c>ExtraServices</c> de <c>ServiceBootstrap.asset</c> (mismo patrón que
    /// <c>SteamServiceBootstrap</c>).
    /// <para>
    /// <see cref="Priority"/> muy bajo: el locale guardado debe quedar aplicado
    /// antes de que cualquier otra pantalla o servicio lea texto. La selección de
    /// idioma real (PlayerPref guardado → idioma del sistema → fallback ES) la
    /// resuelven los <c>StartupLocaleSelectors</c> configurados en
    /// <c>LocalizationSettings</c> al inicializar.
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class LocalizationServiceBootstrap : IPreloadableService, IDisposable
    {
        /// <summary>Antes que el resto: el idioma debe estar resuelto para el primer texto.</summary>
        public const int DefaultPriority = -100;

        [NonSerialized] private LocalizationService _service;

        /// <inheritdoc />
        public int Priority => DefaultPriority;

        /// <inheritdoc />
        public void Register()
        {
            _service = new LocalizationService();
            ServiceLocator.AddService<ILocalizationService>(_service, ServiceScope.Global);

            // Tocar SelectedLocale dispara la inicialización de Localization, que corre
            // los startup selectors y aplica el idioma (guardado/sistema/fallback).
            var _ = LocalizationSettings.SelectedLocale;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _service?.Dispose();
        }
    }
}
