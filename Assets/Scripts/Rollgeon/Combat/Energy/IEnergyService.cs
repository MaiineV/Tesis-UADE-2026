using System;

namespace Rollgeon.Combat.EnergyLib
{
    /// <summary>
    /// API publica del servicio de energia. Consumida por:
    /// <list type="bullet">
    ///   <item>T100b (TurnManager) — cobra energia via <see cref="SpendEnergy"/>.</item>
    ///   <item>T100d (CombatTurnFSM) — fin de turno dispara <c>OnTurnFinished</c>
    ///         que este servicio escucha internamente; tambien puede invocar
    ///         <see cref="RegenerateAtTurnEnd"/> manualmente si lo necesita.</item>
    ///   <item>T95a/T95b (HUD) — lee <see cref="GetCurrent"/> / <see cref="GetMax"/>
    ///         y se suscribe a <c>EventName.OnEnergyChanged</c> / <c>OnPlayerEnergyChanged</c>.</item>
    /// </list>
    /// </summary>
    public interface IEnergyService
    {
        /// <summary>
        /// Hidrata la energia de la entidad al valor <c>EnergyAtRunStart</c>
        /// (clampeado a <c>EnergyMax</c>). Si la entidad no tiene el stat
        /// <c>Energy</c> aun, lo crea. Dispara <c>OnEnergyChanged</c>.
        /// </summary>
        void InitializeForEntity(Guid entityId);

        /// <summary>
        /// Intenta cobrar <paramref name="cost"/> de energia.
        /// </summary>
        /// <returns>
        /// <c>true</c> si habia suficiente energia y se cobro; <c>false</c>
        /// si <c>cost &gt; current</c> (sin mutar nada). Dispara
        /// <c>OnEnergyChanged</c> solo en caso de exito.
        /// </returns>
        /// <remarks>
        /// Path canonico para restar energia. Los callers NO deben usar
        /// <c>AttributesManager.Modify&lt;Energy,int&gt;</c> directo — ese path
        /// solo dispara <c>OnAttributeChanged</c> y se pierde el payload
        /// <c>(current, max)</c> que el HUD necesita.
        /// </remarks>
        bool SpendEnergy(Guid entityId, int cost);

        /// <summary>
        /// Aplica la regla de regen al cierre de turno: <c>current = min(max, current + regenBase)</c>.
        /// Dispara <c>OnEnergyChanged</c> si el valor cambio.
        /// </summary>
        void RegenerateAtTurnEnd(Guid entityId);

        /// <summary>Lee la energia actual (raw, sin mods intrinsicos).</summary>
        int GetCurrent(Guid entityId);

        /// <summary>
        /// Lee la energia maxima. En el FP = <c>EnergyConfig.EnergyMax</c>;
        /// items que suban el cap son followup (plan R8).
        /// </summary>
        int GetMax(Guid entityId);
    }
}
