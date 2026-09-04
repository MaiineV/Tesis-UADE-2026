namespace Rollgeon.UI.Screens
{
    /// <summary>Qué hace la tecla de pausa según lo que esté al top del stack.</summary>
    public enum PauseHotkeyAction
    {
        /// <summary>Abrir el pause menu.</summary>
        Push,

        /// <summary>Cerrar lo que está arriba (pausa u opciones).</summary>
        Pop,

        /// <summary>No hacer nada.</summary>
        Ignore,
    }

    /// <summary>
    /// Decisión pura de <see cref="PauseHotkey"/>, separada para testearla sin
    /// <c>Keyboard.current</c>.
    /// </summary>
    public static class PauseHotkeyRule
    {
        public static PauseHotkeyAction Resolve(IBaseScreen current)
        {
            switch (current)
            {
                // Pause al top → resume. Opciones (abiertas desde la pausa) → cerrarlas y
                // volver a la pausa; sin esta rama se pusheaba OTRA pausa encima.
                case PauseMenuOverlay _:
                case OptionsScreen _:
                    return PauseHotkeyAction.Pop;

                // La encuesta es dueña de Escape: ni pausa encima (PhaseService tiene un
                // solo slot de overlay y el pop de la pausa dejaría la encuesta sin
                // bloqueo), ni skip accidental de un formulario a medio llenar.
                case SurveyOverlay _:
                    return PauseHotkeyAction.Ignore;

                default:
                    return PauseHotkeyAction.Push;
            }
        }
    }
}
