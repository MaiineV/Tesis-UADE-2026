namespace Rollgeon.Combat.ContractMod
{
    /// <summary>
    /// Capa de modificadores temporales sobre el Contrato del jugador (Sistemas prerequisito
    /// Bosses §4). NO toca los valores base del Contrato (los <c>BaseComboSO</c> son SOs de
    /// catálogo inmutables) — es un overlay runtime que se consulta al resolver daño y al
    /// renderear la UI del Contrato. Lo usa el Boss 3 (Director General) para sus Reglas Variables.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Tipos de modificador (DoD §4).</b>
    /// <list type="bullet">
    ///   <item><description><see cref="MultiplyCombo"/> — R01 (×2) / R02 (×0.5).</description></item>
    ///   <item><description><see cref="ForbidCombo"/> — R03 (daño 0 si se ejecuta).</description></item>
    ///   <item><description><see cref="SetComboToNeighbor"/> — R04/R05 (valor del combo inmediatamente
    ///   superior/inferior <b>por daño base</b>).</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>N simultáneos.</b> Soporta varios modificadores activos a la vez (sobre el mismo combo se
    /// acumulan: los multiplicadores se multiplican, "prohibido" es sticky). <see cref="ClearAll"/>
    /// descarta todo y devuelve el Contrato a sus valores base.
    /// </para>
    /// <para>
    /// <b>Lifecycle.</b> Run-scoped vía limpieza en <c>OnCombatEnd</c> / <c>OnRunEnd</c>.
    /// </para>
    /// </remarks>
    public interface IContractModifierService
    {
        /// <summary>
        /// Devuelve el daño base efectivo de <paramref name="comboId"/> aplicando los
        /// modificadores activos sobre <paramref name="baseDamage"/> (el valor del Contrato base).
        /// Sin modificadores ⇒ devuelve <paramref name="baseDamage"/> tal cual.
        /// </summary>
        int GetEffectiveBaseDamage(string comboId, int baseDamage);

        /// <summary><c>true</c> si <paramref name="comboId"/> está prohibido (R03 ⇒ daño 0).</summary>
        bool IsForbidden(string comboId);

        /// <summary><c>true</c> si hay al menos un modificador activo.</summary>
        bool HasAnyModifier { get; }

        /// <summary>R01/R02 — multiplica el daño base del combo por <paramref name="factor"/>.</summary>
        void MultiplyCombo(string comboId, float factor);

        /// <summary>R03 — prohíbe el combo: daño 0 si se ejecuta.</summary>
        void ForbidCombo(string comboId);

        /// <summary>
        /// R04/R05 — fija el valor del combo al del vecino por daño base: <paramref name="direction"/>
        /// = +1 ⇒ combo inmediatamente superior, -1 ⇒ inmediatamente inferior. El vecino se calcula
        /// sobre valores BASE del Contrato. No-op si no hay vecino en esa dirección.
        /// </summary>
        void SetComboToNeighbor(string comboId, int direction);

        /// <summary>Descarta todos los modificadores. El Contrato vuelve a sus valores base.</summary>
        void ClearAll();
    }
}
