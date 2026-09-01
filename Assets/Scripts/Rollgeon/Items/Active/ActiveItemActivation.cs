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
