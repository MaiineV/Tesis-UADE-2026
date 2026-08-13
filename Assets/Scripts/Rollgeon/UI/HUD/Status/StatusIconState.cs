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

        public StatusIconState(string id, string displayName, string description,
                               Sprite icon, bool active, int? remainingTurns = null)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            Icon = icon;
            Active = active;
            RemainingTurns = remainingTurns;
        }
    }
}
