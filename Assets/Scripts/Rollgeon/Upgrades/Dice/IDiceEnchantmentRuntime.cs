namespace Rollgeon.Upgrades.Dice
{
    /// <summary>
    /// Superficie minimal que los <c>IEnchantmentTrigger</c> usan para leer/escribir
    /// state per-slot y para auto-removerse. El impl concreto vive en Phase 4
    /// (<c>DiceEnchantmentService</c>); este interface vive acá porque los triggers
    /// (Phase 3) lo consumen via <c>ServiceLocator.TryGetService</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Counters per-slot.</b> Triggers stateless (instancias compartidas dentro
    /// de un <c>EnchantmentSO</c>) no pueden guardar estado per-bag-slot. La
    /// solución canónica es: el trigger declara una <c>string key</c> y consulta
    /// counters keyed por <c>(EnchantmentSlotRef, key)</c>.
    /// </para>
    /// <para>
    /// <b>Auto-removal.</b> Triggers tipo "explode if unused" se auto-quitan
    /// llamando <see cref="RemoveEnchantment"/> cuando su condición se cumple.
    /// El service maneja la limpieza del slot y dispara el evento correspondiente.
    /// </para>
    /// </remarks>
    public interface IDiceEnchantmentRuntime
    {
        /// <summary>Valor actual del counter <paramref name="key"/> sobre <paramref name="slot"/>. 0 si nunca seteado.</summary>
        int GetCounter(EnchantmentSlotRef slot, string key);

        /// <summary>
        /// Suma <paramref name="delta"/> al counter (default <c>+1</c>) y devuelve
        /// el valor resultante. Si el counter no existía, lo inicializa en 0 antes
        /// de sumar.
        /// </summary>
        int IncrementCounter(EnchantmentSlotRef slot, string key, int delta = 1);

        /// <summary>Pone el counter en 0. Idempotente.</summary>
        void ResetCounter(EnchantmentSlotRef slot, string key);

        /// <summary>
        /// Remueve el encantamiento del slot — disparado por triggers self-destruct
        /// (ExplodeIfUnusedForTurns). El service limpia el state y dispara el evento
        /// correspondiente.
        /// </summary>
        void RemoveEnchantment(EnchantmentSlotRef slot);
    }
}
