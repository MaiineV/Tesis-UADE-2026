namespace Rollgeon.Attributes.Modifiers
{
    /// <summary>
    /// Politica de vida del modificador. Determina a que evento se suscribe
    /// <see cref="Modifier{T}.OnLoad"/> para auto-removerse. Especificado en
    /// TECHNICAL.md §3.1.
    /// </summary>
    public enum ModifierLifetime
    {
        /// <summary>
        /// <see cref="Modifier{T}.Duration"/> cuenta ticks de <see cref="Modifier{T}.TickEvent"/>.
        /// Se remueve al llegar a 0. Uso: buffs de N turnos.
        /// </summary>
        Turns,

        /// <summary>
        /// No tickea. Vive hasta que un EffRemoveModifier explicito lo quite.
        /// Uso: stat boosts comprados en tienda, pasivas siempre-activas.
        /// </summary>
        Permanent,

        /// <summary>
        /// Se remueve con <c>EventName.OnRunEnd</c>. Uso: run buffs, boss debuffs,
        /// strike combos, cualquier cosa que dure la corrida.
        /// </summary>
        Run,

        /// <summary>
        /// Se remueve con <c>EventName.OnCombatEnd</c>. Uso: "durante este combate
        /// los combos de fuego hacen +20%".
        /// </summary>
        Encounter,
    }
}
