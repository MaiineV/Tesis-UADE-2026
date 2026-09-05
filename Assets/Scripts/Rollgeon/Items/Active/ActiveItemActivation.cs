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
        /// Resolucion completa (Feature#0085): cara, banda, estructura y magnitud. Los
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
    /// Tirada ya hecha y cobrada, todavia sin resolver: el dado salio y la resolucion
    /// (encantamiento, banda, efectos) llega sola cuando la ficha termina de girar. Lo
    /// consume el HUD para animar el giro y asentar la cara cruda.
    /// </summary>
    public readonly struct ActiveItemRoll
    {
        public readonly ItemSO Item;

        /// <summary>Cara cruda, antes del encantamiento (ese corre al resolver).</summary>
        public readonly int RawRoll;

        public ActiveItemRoll(ItemSO item, int rawRoll)
        {
            Item = item;
            RawRoll = rawRoll;
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
    ///   <item>La tirada va inmediatamente despues y <b>se resuelve sola</b>: el servicio
    ///         espera lo que dura el giro del dado en la ficha y corre encantamiento,
    ///         banda y efectos sin ningun input del jugador.</item>
    /// </list>
    /// No hay re-tirada ni ventana de decision (ronda de testers 2026-09-04, GDD §28):
    /// cancelar antes de confirmar es gratis, despues el roll queda pagado y la cara que
    /// salio es la que resuelve. Si el turno o el combate se cierran antes de que el giro
    /// termine, ver <see cref="IsResolving"/>.
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
        /// esperando seleccion como si ya tiro — mirar <see cref="IsSelecting"/>.
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
        /// Confirma la activacion: cobra 1 roll, tira el dado y agenda la resolucion para
        /// cuando la ficha termine de girar. Los efectos todavia no corren en esta llamada
        /// (salvo que el scheduler sea sincronico, como en tests).
        /// </summary>
        /// <param name="selection">
        /// Target ya elegido, o <c>null</c> para los items que activan directo sin paso
        /// de seleccion.
        /// </param>
        /// <returns>
        /// La tirada, o <c>null</c> si la activacion fue rechazada (ver
        /// <see cref="CanActivate"/>) o no se pudo cobrar el roll. En ese caso no se
        /// cobro nada ni se tiro el dado.
        /// </returns>
        ActiveItemRoll? Confirm(TargetSelectionResult selection);

        /// <summary>
        /// <c>true</c> entre la tirada y su resolucion (el dado esta girando en la
        /// ficha). Bloquea otra activacion y el fin de turno. Si el turno del dueño se
        /// cierra igual, la tirada se resuelve en el acto; si el combate se cierra, se
        /// descarta sin efectos (el roll pagado no se devuelve).
        /// </summary>
        bool IsResolving { get; }

        /// <summary>La tirada en curso, o <c>null</c> si no hay ninguna.</summary>
        ActiveItemRoll? Pending { get; }

        /// <summary>
        /// Disparado al tirar el dado (ya cobrado). El HUD lo usa para girar el dado
        /// dentro del slot; la resolucion llega por <see cref="OnResolved"/> cuando el
        /// giro termina.
        /// </summary>
        event Action<ActiveItemRoll> OnRolled;

        /// <summary>
        /// Disparado tras cada activacion resuelta. El HUD lo usa para mostrar la cara
        /// obtenida dentro del slot antes de volver al estado de reposo.
        /// </summary>
        event Action<ActiveItemActivationResult> OnResolved;

        /// <summary>
        /// <c>true</c> mientras un efecto de banda pidio una eleccion post-tirada
        /// (Feature#0085 §A5, Probability Drive cara 4) y todavia no se resolvio.
        /// <see cref="CanActivate"/> devuelve <see cref="ActiveItemBlock.Resolving"/>
        /// en este estado — la activacion ya paso la banda y el efecto espera un tile.
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
