using System.Text;
using Rollgeon.Localization;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Arma el texto del tooltip de un estado: nombre, descripción y el pie que dice si
    /// está activo (pasivas) o cuánto le queda (efectos con duración).
    /// </summary>
    /// <remarks>
    /// Función pura y separada de la vista para poder testear el formato sin escena. La
    /// localización se resuelve acá y no en el provider porque el pie es del SISTEMA
    /// (activada / turnos restantes), no del estado — el provider solo trae nombre y
    /// descripción, que sí son suyos.
    /// </remarks>
    public static class StatusTooltipText
    {
        public static string Build(in StatusIconState state)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(state.DisplayName))
                sb.Append("<b>").Append(state.DisplayName).Append("</b>");

            if (!string.IsNullOrEmpty(state.Description))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(state.Description);
            }

            string footer = ResolveFooter(state);
            if (!string.IsNullOrEmpty(footer))
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.Append(footer);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Badge del ícono/tarjeta: la cantidad si el estado es apilable, la duración si no.
        /// El stack nunca se escribe en la regla — así un estado que hoy traba un dado y
        /// mañana traba dos cambia el número acá y la frase queda intacta.
        /// </summary>
        public static string ResolveBadge(in StatusIconState state)
        {
            if (state.StackCount.HasValue)
                return state.StackCount.Value == 0 ? string.Empty : state.StackCount.Value.ToString();

            return ResolveDurationBadge(state);
        }

        /// <summary>Texto que va bajo el ícono ("1 Turno" / "3 Turnos"). Vacío sin duración.</summary>
        public static string ResolveDurationBadge(in StatusIconState state)
        {
            if (!state.RemainingTurns.HasValue) return string.Empty;

            int turns = state.RemainingTurns.Value;
            if (turns <= 1)
                return LocalizedContent.Ui(StatusTextKeys.BadgeOneTurn, "1 Turno");

            return string.Format(
                LocalizedContent.Ui(StatusTextKeys.BadgeTurns, "{0} Turnos"), turns);
        }

        private static string ResolveFooter(in StatusIconState state)
        {
            if (!state.RemainingTurns.HasValue)
            {
                // Sin duración = pasiva: lo único que aporta el pie es si está rindiendo.
                return state.Active
                    ? LocalizedContent.Ui(StatusTextKeys.Active, "Activada")
                    : LocalizedContent.Ui(StatusTextKeys.Inactive, "Desactivada");
            }

            int turns = state.RemainingTurns.Value;
            if (turns <= 1)
                return LocalizedContent.Ui(StatusTextKeys.DurationLastTurn, "Último turno");

            return string.Format(
                LocalizedContent.Ui(StatusTextKeys.Duration, "Dura {0} turnos"), turns);
        }
    }
}
