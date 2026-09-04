using System.Collections.Generic;

namespace Rollgeon.Combat.Healing
{
    /// <summary>
    /// Reglas que alteran la curación del jugador mientras un item las sostenga
    /// (patrón <c>IComboRuleService</c>: fuentes por ItemId, la regla vale mientras haya al
    /// menos una). Reglas: bloqueo de curas de items PASIVOS, y multiplicador de la curación
    /// de la poción (acción Curarse) — Ayuno: ×0.5.
    /// </summary>
    public interface IHealingRuleService
    {
        /// <summary>True si alguna fuente bloquea la curación proveniente de items pasivos.</summary>
        bool PassiveItemHealingBlocked { get; }

        void AddPassiveHealingBlock(string sourceId);

        void RemovePassiveHealingBlock(string sourceId);

        /// <summary>
        /// Producto de los factores registrados sobre la curación de la poción. 1 sin fuentes.
        /// Entra a M en <c>PlayerComboDamage.Resolve</c> con kind Heal — la fórmula que usa la
        /// acción Curarse (con combo y en el fallback sin combo).
        /// </summary>
        float PotionHealMultiplier { get; }

        /// <summary>
        /// Factores por fuente (ItemId → factor), para que la fórmula journalee cada uno con su
        /// icono en el desglose. Vacío sin fuentes.
        /// </summary>
        IReadOnlyDictionary<string, float> PotionHealMultiplierSources { get; }

        /// <summary>Registra (o reemplaza) el factor de <paramref name="sourceId"/>. Factores
        /// no positivos se ignoran.</summary>
        void AddPotionHealMultiplier(string sourceId, float factor);

        void RemovePotionHealMultiplier(string sourceId);
    }
}
