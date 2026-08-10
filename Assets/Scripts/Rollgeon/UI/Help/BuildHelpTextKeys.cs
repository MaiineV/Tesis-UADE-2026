using System.Collections.Generic;

namespace Rollgeon.UI.Help
{
    /// <summary>
    /// Keys de la String Table <c>UI</c> para la guía de armado de bolsa. Mismo criterio
    /// que <c>TutorialTextKeys</c> y <c>UiTextKeys</c>: el texto vive en la tabla y el
    /// flow lo resuelve con <c>LocalizedContent.Ui(key, fallbackEspañol)</c>.
    /// </summary>
    /// <remarks>
    /// La copy es DESCRIPTIVA, no imperativa: los pasos corren con el dim capturando el
    /// click, así que el jugador no puede ejecutar la acción mientras la lee. Decirle
    /// "clickeá un dado ahora" y comerle el click sería mentirle.
    /// </remarks>
    public static class BuildHelpTextKeys
    {
        /// <summary>Paso 1 — el pool de dados de la clase.</summary>
        public const string Pool = "build.help.pool";

        /// <summary>Paso 2 — la tira de la bolsa: orden automático y click para quitar.</summary>
        public const string Strip = "build.help.strip";

        /// <summary>Paso 3 — el botón de limpiar.</summary>
        public const string Clear = "build.help.clear";

        /// <summary>Paso 4 — confirmar y arrancar la run.</summary>
        public const string Confirm = "build.help.confirm";

        /// <summary>Todas las keys de esta clase, para validación en tests.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            Pool, Strip, Clear, Confirm,
        };
    }
}
