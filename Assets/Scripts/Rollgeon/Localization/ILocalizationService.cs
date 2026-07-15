using System;
using UnityEngine.Localization;

namespace Rollgeon.Localization
{
    /// <summary>
    /// Servicio global de idioma (Feature localización ES/EN). Registrado en el
    /// bootstrap y resuelto por la UI (ej. <see cref="LanguageSelector"/>) para
    /// cambiar de idioma en runtime. Envuelve <c>LocalizationSettings</c> para que
    /// el resto del juego no dependa directo del package.
    /// </summary>
    public interface ILocalizationService
    {
        /// <summary>Locale activo, o <c>null</c> si Localization aún no inicializó.</summary>
        Locale Current { get; }

        /// <summary>Código del locale activo (ej. "es"), o <c>null</c>.</summary>
        string CurrentCode { get; }

        /// <summary>
        /// Cambia el idioma activo por código ("es" / "en"). No-op con warning si el
        /// código no está entre los locales disponibles del proyecto. La elección la
        /// persiste el <c>PlayerPrefLocaleSelector</c> configurado en LocalizationSettings.
        /// </summary>
        void SetLanguage(string localeCode);

        /// <summary>Se dispara cada vez que cambia el locale seleccionado.</summary>
        event Action LanguageChanged;
    }
}
