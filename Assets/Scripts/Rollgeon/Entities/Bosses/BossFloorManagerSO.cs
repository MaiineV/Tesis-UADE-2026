using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Bosses
{
    /// <summary>
    /// Datos estaticos del boss del piso 1 — "Gerente de Piso" (Content#0103). Hereda de
    /// <see cref="Rollgeon.Entities.EnemyDataSO"/> y agrega los campos Inspector que
    /// configuran la mecanica distintiva: bloquear un combo del ContractSheet del jugador
    /// cada N turnos, por D turnos, + barra de energia interna que al llenarse sube la
    /// chance de doble dano.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plan §4.1. Todos los valores editables desde el Inspector — DoD #103 exige que
    /// nada de esto sea hardcoded.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(
        menuName = "Rollgeon/Entities/Bosses/Floor Manager",
        fileName = "BossFloorManager")]
    public class BossFloorManagerSO : EnemyDataSO
    {
        // -----------------------------------------------------------------
        // Combo block (§4.1 / §6.2)
        // -----------------------------------------------------------------

        [Title("Boss — Combo Block")]
        [MinValue(1)]
        [Tooltip("Cada cuantos turnos del Boss se dispara un nuevo bloqueo.")]
        public int ComboBlockIntervalTurns = 3;

        [MinValue(1)]
        [Tooltip("Duracion (en turnos del jugador) que dura un bloqueo antes de expirar.")]
        public int ComboBlockDurationTurns = 2;

        // -----------------------------------------------------------------
        // Energy buildup + double damage (§4.4 / §6.3)
        // -----------------------------------------------------------------

        [Title("Boss — Energy Buildup")]
        [MinValue(1)]
        [Tooltip("Energia maxima interna del Boss. Al llenarse, aplica la double-damage chance.")]
        public int BossEnergyMax = 4;

        [MinValue(1)]
        [Tooltip("Energia ganada por turno del Boss.")]
        public int BossEnergyGainPerTurn = 1;

        [Range(0f, 1f)]
        [Tooltip("Probabilidad de doble dano por defecto (cuando la energia NO esta llena).")]
        public float DoubleDamageChanceDefault = 0.0f;

        [Range(0f, 1f)]
        [Tooltip("Probabilidad de doble dano cuando la energia del Boss esta al maximo. Spec #103 default 0.5.")]
        public float DoubleDamageChanceWhenEnergyFull = 0.5f;

        // -----------------------------------------------------------------
        // OnValidate — warning (no error) si duration > interval (overlap entre bloqueos).
        // -----------------------------------------------------------------

        private void OnValidate()
        {
            if (ComboBlockIntervalTurns < 1) ComboBlockIntervalTurns = 1;
            if (ComboBlockDurationTurns < 1) ComboBlockDurationTurns = 1;
            if (BossEnergyMax < 1) BossEnergyMax = 1;
            if (BossEnergyGainPerTurn < 1) BossEnergyGainPerTurn = 1;
            if (DoubleDamageChanceDefault < 0f) DoubleDamageChanceDefault = 0f;
            if (DoubleDamageChanceDefault > 1f) DoubleDamageChanceDefault = 1f;
            if (DoubleDamageChanceWhenEnergyFull < 0f) DoubleDamageChanceWhenEnergyFull = 0f;
            if (DoubleDamageChanceWhenEnergyFull > 1f) DoubleDamageChanceWhenEnergyFull = 1f;

            if (ComboBlockDurationTurns > ComboBlockIntervalTurns)
            {
                Debug.LogWarning(
                    $"[BossFloorManagerSO] '{name}': ComboBlockDurationTurns ({ComboBlockDurationTurns}) > " +
                    $"ComboBlockIntervalTurns ({ComboBlockIntervalTurns}) — bloqueos se solaparan. " +
                    "Warning informativo; OK si el diseno lo pide.");
            }
        }
    }
}
