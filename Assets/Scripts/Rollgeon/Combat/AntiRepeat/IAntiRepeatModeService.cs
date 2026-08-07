namespace Rollgeon.Combat.AntiRepeat
{
    /// <summary>
    /// Estado vivo del pasivo anti-repetición (A/B global del jugador). Sembrado desde
    /// <see cref="AntiRepeatConfigSO"/> al bootstrap y flipeado en runtime por el comando de
    /// consola <c>passive</c>. Lo leen <c>DamagePipeline</c> (Combo → 0 daño en repetido),
    /// <c>DamageFormulaView</c> (advertencia en UI) y el handler de bloqueo de dados (Dice).
    /// </summary>
    public interface IAntiRepeatModeService
    {
        /// <summary>Modo activo actual.</summary>
        AntiRepeatMode Mode { get; }

        /// <summary>
        /// Cambia el modo activo (no persiste ni modifica el <see cref="AntiRepeatConfigSO"/>).
        /// Dispara <c>EventName.OnAntiRepeatModeChanged</c> si el valor cambió.
        /// </summary>
        void SetMode(AntiRepeatMode mode);
    }
}
