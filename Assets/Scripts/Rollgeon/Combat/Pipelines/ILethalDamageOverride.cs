using System;

namespace Rollgeon.Combat.Pipelines
{
    /// <summary>
    /// Override de letalidad del <see cref="DamagePipeline"/>: cuando un golpe
    /// dejaría al target en 0 HP y este servicio devuelve <c>true</c>, el pipeline
    /// clampea a 1 HP y el golpe deja de ser letal (<c>WasLethal=false</c>, así
    /// <c>CombatDeathWatcher</c> nunca dispara la derrota).
    /// <para>
    /// Registrado en <c>ServiceScope.Run</c> SOLO cuando aplica (ej. tutorial —
    /// <c>TutorialInvulnerabilityService</c>); ausente del locator = letalidad normal.
    /// A diferencia del GodMode del dev console (que re-pinea HP <i>después</i> del
    /// golpe), esto previene el flag letal en la fuente.
    /// </para>
    /// </summary>
    public interface ILethalDamageOverride
    {
        bool ShouldPreventLethal(Guid targetId);

        /// <summary>
        /// HP con los que queda el target salvado. Default: la constante histórica del
        /// pipeline (tutorial). "Sello del Segundo Aliento" (GDD) devuelve 1.
        /// </summary>
        int GetRemainingHp(Guid targetId) => DamagePipeline.LethalOverrideRemainingHp;

        /// <summary>
        /// El pipeline YA aplicó la salvada (HP escrito). Punto para consumir cargas
        /// one-shot (Segundo Aliento se remueve del inventario acá). Default no-op.
        /// </summary>
        void NotifyLethalPrevented(Guid targetId) { }
    }
}
