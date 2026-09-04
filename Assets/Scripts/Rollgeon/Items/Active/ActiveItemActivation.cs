using System;
using Rollgeon.Dice;
using Rollgeon.Effects.Selection;

namespace Rollgeon.Items.Active
{
    /// <summary>Resultado de una activacion ya resuelta. Lo consume el HUD y el feedback.</summary>
    public readonly struct ActiveItemActivationResult
    {
        public readonly ItemSO Item;

        /// <summary>
        /// Cara final, ya con el ajuste del encantamiento. Es la que decide la banda y la
        /// que muestra la ficha.
        /// </summary>
        public readonly int Roll;

        /// <summary>
        /// Cara cruda del dado, antes del encantamiento. Igual a <see cref="Roll"/> si no
        /// hubo ajuste. El feedback la necesita para poder mostrar que el encantamiento
        /// intervino en vez de que el dado salio distinto.
        /// </summary>
        public readonly int RawRoll;

        /// <summary><c>true</c> si el encantamiento modifico el resultado.</summary>
        public bool WasEnchanted => RawRoll != Roll;

        /// <summary>Banda en la que cayo <see cref="Roll"/>.</summary>
        public readonly ActiveItemBand Band;

        /// <summary><c>true</c> si el grupo de efectos de la banda corrio sin cortar.</summary>
        public readonly bool EffectsSucceeded;

        /// <summary>
        /// Resolucion completa (Feature#0084): cara, banda, estructura y magnitud. Los
        /// campos <see cref="Roll"/>/<see cref="Band"/> se conservan por compatibilidad —
        /// son <c>Resolution.Face</c>/<c>Resolution.Band</c> desagregados.
        /// </summary>
        public readonly ActiveItemRollResolution Resolution;

        public ActiveItemActivationResult(ItemSO item, int roll, ActiveItemBand band,
            bool effectsSucceeded, int rawRoll)
            : this(item, roll, band, effectsSucceeded, rawRoll,
                new ActiveItemRollResolution(roll, rawRoll,
                    item != null ? item.ActiveDie.MaxFace() : 6, band,
                    item != null ? item.ActiveResolution : ActiveItemResolution.Bands, 0))
        {
        }

        public ActiveItemActivationResult(ItemSO item, int roll, ActiveItemBand band,
            bool effectsSucceeded, int rawRoll, ActiveItemRollResolution resolution)
        {
            Item = item;
            Roll = roll;
            Band = band;
            EffectsSucceeded = effectsSucceeded;
            RawRoll = rawRoll;
            Resolution = resolution;
        }
    }

    /// <summary>
    /// Tirada pendiente de decision: el dado ya salio pero el jugador todavia no acepto.
    /// Lo consume el HUD para mostrar la cara y ofrecer el reroll.
    /// </summary>
    public readonly struct ActiveItemPendingRoll
    {
        public readonly ItemSO Item;

        /// <summary>Cara cruda vigente, antes del encantamiento (ese corre al aceptar).</summary>
        public readonly int RawRoll;

        /// <summary>Cuantos rerolls ya se pagaron en esta activacion. 0 = primera tirada.</summary>
        public readonly int RerollCount;

        public ActiveItemPendingRoll(ItemSO item, int rawRoll, int rerollCount)
        {
            Item = item;
            RawRoll = rawRoll;
            RerollCount = rerollCount;
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
    ///   <item>La tirada va inmediatamente despues y queda <b>pendiente de decision</b>:
    ///         el jugador puede re-tirar el dado pagando 1 roll por tirada (las veces que
    ///         el pool aguante) o aceptar el resultado, igual que en ataque/defensa.</item>
    ///   <item>Al aceptar corren el encantamiento, la banda y sus efectos.</item>
    /// </list>
    /// Cancelar antes de confirmar es gratis. Despues no: cada tirada pagada se queda
    /// pagada (nunca hay reembolso), y la unica salida de la ventana de decision es
    /// aceptar la cara vigente. El target elegido queda fijo durante toda la ventana.
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
        /// Confirma la activacion: cobra 1 roll, tira el dado y deja la tirada
        /// <b>pendiente de decision</b> (ver <see cref="AcceptRoll"/> y
        /// <see cref="RequestReroll"/>). Los efectos todavia no corren.
        /// </summary>
        /// <param name="selection">
        /// Target ya elegido, o <c>null</c> para los items que activan directo sin paso
        /// de seleccion.
        /// </param>
        /// <returns>
        /// La tirada pendiente, o <c>null</c> si la activacion fue rechazada (ver
        /// <see cref="CanActivate"/>) o no se pudo cobrar el roll. En ese caso no se
        /// cobro nada ni se tiro el dado.
        /// </returns>
        ActiveItemPendingRoll? Confirm(TargetSelectionResult selection);

        /// <summary><c>true</c> mientras hay una tirada esperando aceptar o re-tirar.</summary>
        bool IsAwaitingDecision { get; }

        /// <summary>La tirada pendiente de decision, o <c>null</c> si no hay ninguna.</summary>
        ActiveItemPendingRoll? Pending { get; }

        /// <summary>
        /// <c>true</c> si hay tirada pendiente y el pool alcanza para pagar otro roll.
        /// Read-only, sin efectos secundarios: el HUD lo consulta para gatear el boton.
        /// </summary>
        bool CanRequestReroll { get; }

        /// <summary>
        /// Re-tira el dado pagando 1 roll, como el reroll de ataque/defensa. La tirada
        /// nueva reemplaza a la anterior y vuelve a quedar pendiente — se puede repetir
        /// mientras el pool aguante.
        /// </summary>
        /// <returns>
        /// <c>false</c> si no habia tirada pendiente o no se pudo cobrar. En ese caso la
        /// cara vigente no cambia y la unica salida sigue siendo <see cref="AcceptRoll"/>.
        /// </returns>
        bool RequestReroll();

        /// <summary>
        /// Acepta la cara vigente y resuelve la activacion: encantamiento, banda y
        /// efectos, en ese orden (§14). No cuesta nada.
        /// </summary>
        /// <returns>El resultado, o <c>null</c> si no habia tirada pendiente.</returns>
        ActiveItemActivationResult? AcceptRoll();

        /// <summary>
        /// Disparado en cada tirada que queda pendiente de decision (la primera y cada
        /// reroll). El HUD lo usa para girar el dado dentro del slot.
        /// </summary>
        event Action<ActiveItemPendingRoll> OnRollPending;

        /// <summary>
        /// Disparado tras cada activacion resuelta. El HUD lo usa para mostrar la cara
        /// obtenida dentro del slot antes de volver al estado de reposo.
        /// </summary>
        event Action<ActiveItemActivationResult> OnResolved;

        /// <summary>
        /// <c>true</c> mientras un efecto de banda pidio una eleccion post-tirada
        /// (Feature#0084 §A5, Probability Drive cara 4) y todavia no se resolvio.
        /// <see cref="CanActivate"/> devuelve <see cref="ActiveItemBlock.AwaitingDecision"/>
        /// en este estado — es la misma "ventana abierta" que la del roll pendiente, solo
        /// que ya paso la banda y el efecto espera un tile.
        /// </summary>
        bool IsAwaitingChoice { get; }

        /// <summary>
        /// Disparado cuando se abre la seleccion de la eleccion post-tirada (despues de
        /// <see cref="OnResolved"/> de la misma activacion). El HUD lo usa para bloquear
        /// End Turn/Confirm mientras dura.
        /// </summary>
        event Action OnChoicePending;

        /// <summary>
        /// Disparado cuando la eleccion post-tirada termina, elegida o abandonada.
        /// </summary>
        event Action OnChoiceResolved;
    }
}
