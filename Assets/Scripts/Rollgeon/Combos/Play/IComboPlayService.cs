using Rollgeon.Effects;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Combos.Play
{
    /// <summary>
    /// Ventana de "combo jugado": se abre al confirmar una acción con combo matcheado,
    /// ANTES de que ningún efecto consuma <c>ComboResult</c>, y se cierra al terminar la
    /// ejecución. Dentro de la ventana, los suscriptores de
    /// <c>TypedEvent&lt;ComboPlayedPayload&gt;</c> escriben bonos al
    /// <see cref="CurrentPlayScratch"/> y <c>PlayerComboDamage.Resolve</c> los lee como
    /// parte de <c>bono_combo</c>.
    /// </summary>
    /// <remarks>
    /// Canal independiente de los <c>LastComboScratch</c> de pasivas/encantamientos
    /// (canal at-match, visible en preview): el preview crea scratch fresco en cada toggle
    /// de hold y pisaría cualquier bono at-played. Regla anti-doble-conteo: cada fuente de
    /// bono escribe en UN solo canal — at-match XOR at-played.
    /// </remarks>
    public interface IComboPlayService
    {
        /// <summary>
        /// Scratch de la ejecución en curso. NULL fuera de la ventana y en acciones sin
        /// combo — el preview de <c>DamageFormulaView</c> llama <c>Resolve</c> con ventana
        /// cerrada y nunca ve bonos jugados viejos.
        /// </summary>
        EnchantmentScratch CurrentPlayScratch { get; }

        /// <summary><c>true</c> mientras una ejecución de acción está en curso.</summary>
        bool IsPlayWindowOpen { get; }

        /// <summary>Id del combo jugado en la ventana actual. Null sin combo o fuera de ventana.</summary>
        string CurrentComboId { get; }

        /// <summary>
        /// Abre la ventana. Con combo matcheado (id no vacío) crea scratch fresco, emite
        /// <c>ComboPlayedPayload</c> sincrónico y aplica UNA vez los recursos (oro/stats)
        /// que los suscriptores acumularon. Sin combo solo resetea — las acciones sin combo
        /// nunca leen scratch stale. Reentrante: un Begin anidado no re-emite ni resetea.
        /// </summary>
        void BeginPlay(EffectContext effCtx);

        /// <summary>Cierra la ventana (scratch a null). Llamar en try/finally del emisor.</summary>
        void EndPlay();
    }
}
