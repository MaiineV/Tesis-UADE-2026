using System;
using Rollgeon.Effects.Selection;

namespace Rollgeon.Items.Active
{
    /// <summary>Resultado de una activacion ya resuelta. Lo consume el HUD y el feedback.</summary>
    public readonly struct ActiveItemActivationResult
    {
        public readonly ItemSO Item;

        /// <summary>Cara obtenida en el dado propio del item.</summary>
        public readonly int Roll;

        /// <summary>Banda en la que cayo <see cref="Roll"/>.</summary>
        public readonly ActiveItemBand Band;

        /// <summary><c>true</c> si el grupo de efectos de la banda corrio sin cortar.</summary>
        public readonly bool EffectsSucceeded;

        public ActiveItemActivationResult(ItemSO item, int roll, ActiveItemBand band, bool effectsSucceeded)
        {
            Item = item;
            Roll = roll;
            Band = band;
            EffectsSucceeded = effectsSucceeded;
        }
    }

    /// <summary>
    /// Ejecuta la activacion del item activo equipado. GDD "Ítems Activos" §22.
    /// </summary>
    /// <remarks>
    /// La secuencia y, sobre todo, <b>cuando se cobra</b>, son parte del diseño:
    /// <list type="number">
    ///   <item>Tocar el boton no cuesta nada y solo abre la seleccion.</item>
    ///   <item>Al confirmar el target se cobra 1 roll. Ese es el punto de no retorno.</item>
    ///   <item>La tirada va inmediatamente despues, sin ventana intermedia.</item>
    ///   <item>La banda decide que grupo de efectos corre.</item>
    /// </list>
    /// Cancelar antes de confirmar es gratis; despues no se puede.
    /// </remarks>
    public interface IActiveItemActivationService
    {
        /// <summary>
        /// Motivo por el que el slot no se puede usar ahora, o
        /// <see cref="ActiveItemBlock.None"/>. Read-only, sin efectos secundarios: lo
        /// llama el HUD en cada refresh para pintar el slot y su mensaje.
        /// </summary>
        ActiveItemBlock CanActivate();

        /// <summary>
        /// Paso 1: el jugador toca la ficha. <b>No cuesta nada.</b> Si el item pide
        /// target abre la seleccion y queda esperando; si activa directo, confirma en el
        /// acto (ahi si se cobra).
        /// </summary>
        /// <returns>
        /// <c>false</c> si la activacion esta bloqueada. <c>true</c> tanto si quedo
        /// esperando seleccion como si ya resolvio — mirar <see cref="IsSelecting"/>.
        /// </returns>
        bool BeginActivation();

        /// <summary>
        /// <c>true</c> mientras se espera que el jugador elija target. En ese estado la
        /// accion todavia no costo nada y se puede cancelar.
        /// </summary>
        bool IsSelecting { get; }

        /// <summary>
        /// Cancela la seleccion en curso sin costo. No-op si no habia ninguna. Despues
        /// de confirmar el target ya no se puede cancelar: el roll esta cobrado.
        /// </summary>
        void CancelActivation();

        /// <summary>
        /// Disparado al abrir la seleccion de target. El HUD lo usa para marcar la ficha
        /// como armada.
        /// </summary>
        event Action OnSelectionStarted;

        /// <summary>
        /// Disparado cuando la seleccion termina sin activar (el jugador cancelo o no
        /// eligio nada). El item no se gasto.
        /// </summary>
        event Action OnSelectionCancelled;

        /// <summary>
        /// Confirma la activacion: cobra 1 roll, tira el dado y resuelve la banda.
        /// </summary>
        /// <param name="selection">
        /// Target ya elegido, o <c>null</c> para los items que activan directo sin paso
        /// de seleccion.
        /// </param>
        /// <returns>
        /// El resultado, o <c>null</c> si la activacion fue rechazada (ver
        /// <see cref="CanActivate"/>) o no se pudo cobrar el roll. En ese caso no se
        /// cobro nada ni se tiro el dado.
        /// </returns>
        ActiveItemActivationResult? Confirm(TargetSelectionResult selection);

        /// <summary>
        /// Disparado tras cada activacion resuelta. El HUD lo usa para mostrar la cara
        /// obtenida dentro del slot antes de volver al estado de reposo.
        /// </summary>
        event Action<ActiveItemActivationResult> OnResolved;
    }
}
