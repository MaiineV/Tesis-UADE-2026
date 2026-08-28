using UnityEngine;

namespace Rollgeon.UI.HUD.Status
{
    /// <summary>
    /// Un estado del player listo para pintar: qué ícono va y si está activo. Lo produce
    /// un <see cref="IStatusIconProvider"/> y lo consume <see cref="PlayerStatusIconsView"/>.
    /// </summary>
    /// <remarks>
    /// El provider ya resuelve el sprite según activo/inactivo en vez de pasar los dos: la
    /// vista no tiene por qué saber que un estado puede tener arte distinto por estado, y
    /// un estado futuro podría usar el mismo ícono para ambos y distinguirse solo por el
    /// fondo. El fondo (marco) sí lo pone la vista, porque es del sistema y no del estado.
    /// </remarks>
    public readonly struct StatusIconState
    {
        /// <summary>Id estable del estado — la vista lo usa para reusar el mismo slot entre refreshes.</summary>
        public readonly string Id;

        /// <summary>Nombre legible, ya localizado. Encabeza el tooltip.</summary>
        public readonly string DisplayName;

        /// <summary>Descripción ya localizada. Cuerpo del tooltip; puede venir vacía.</summary>
        public readonly string Description;

        public readonly Sprite Icon;
        public readonly bool Active;

        /// <summary>
        /// Turnos que le quedan al estado. <c>null</c> = no tiene duración, y entonces el
        /// ícono no muestra número y el tooltip habla de activada/desactivada. Las pasivas
        /// caen siempre acá; los efectos de estado con timer traen el valor.
        /// </summary>
        public readonly int? RemainingTurns;

        /// <summary>
        /// Cuántas copias hay de esto en juego. <c>null</c> = no es apilable. Va siempre en el
        /// badge del ícono y nunca escrito dentro de la regla: si algún día traba dos dados,
        /// cambia el número del badge y la frase no se toca.
        /// </summary>
        public readonly int? StackCount;

        /// <summary>Ver <see cref="StatusCardStyle"/>.</summary>
        public readonly StatusCardStyle Style;

        /// <summary>
        /// Renglón chico arriba del título — <c>Próximo turno</c>. Dice CUÁNDO pasa lo que la
        /// tarjeta describe; null = la tarjeta no necesita fecha y el renglón no se dibuja.
        /// </summary>
        public readonly string Eyebrow;

        /// <summary>
        /// Lo que pega, pegado al título y <b>nunca</b> dentro de la frase. <c>null</c> = no pega
        /// por sí mismo. Mismo argumento que <see cref="StackCount"/>: rebalancear cambia un
        /// número del dato y no toca una línea de texto en ningún idioma.
        /// </summary>
        public readonly int? Damage;

        // Los últimos van al final y con default para que los providers que ya existen y sus
        // tests compilen sin tocarse.
        public StatusIconState(string id, string displayName, string description,
                               Sprite icon, bool active, int? remainingTurns = null,
                               int? stackCount = null, StatusCardStyle style = StatusCardStyle.Unit,
                               int? damage = null, string eyebrow = null)
        {
            Eyebrow = eyebrow;
            Id = id;
            DisplayName = displayName;
            Description = description;
            Icon = icon;
            Active = active;
            RemainingTurns = remainingTurns;
            StackCount = stackCount;
            Style = style;
            Damage = damage;
        }
    }
}
