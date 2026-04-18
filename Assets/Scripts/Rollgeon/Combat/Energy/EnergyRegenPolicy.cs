namespace Rollgeon.Combat.Energy
{
    /// <summary>
    /// Funcion pura que encapsula la regla de regeneracion de energia al
    /// finalizar turno. Aislada para testeo sin Unity y para que T100b/T100d,
    /// T100T, y tweaks de balance no reinventen la formula.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Regla GDD #100.</b> <i>"Al terminar el turno, el jugador regenera
    /// 2 de energia base + la energia no utilizada del turno (clampeado a
    /// EnergyMax)"</i>. En la practica, la "energia no utilizada" coincide con
    /// el <c>current</c> al momento del end-of-turn. Por eso la formula
    /// simplifica a <c>min(max, current + regenBase)</c>.
    /// </para>
    /// </remarks>
    public static class EnergyRegenPolicy
    {
        /// <summary>
        /// Calcula el nuevo valor de energia tras la regen al terminar el turno.
        /// </summary>
        /// <param name="currentAfterActions">Energia no utilizada al cerrar el turno.</param>
        /// <param name="max">Cap de energia (<c>EnergyConfig.EnergyMax</c>).</param>
        /// <param name="regenBase">Regen base (<c>EnergyConfig.EnergyRegenBase</c>).</param>
        public static int ComputeNewCurrent(int currentAfterActions, int max, int regenBase)
        {
            if (currentAfterActions < 0) currentAfterActions = 0;
            if (regenBase < 0) regenBase = 0;
            if (max < 0) max = 0;

            int raw = currentAfterActions + regenBase;
            if (raw > max) raw = max;
            return raw;
        }
    }
}
