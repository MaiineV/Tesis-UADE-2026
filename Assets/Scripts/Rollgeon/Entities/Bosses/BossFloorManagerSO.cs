using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities.Bosses
{
    [CreateAssetMenu(
        menuName = "Rollgeon/Entities/Bosses/Floor Manager",
        fileName = "BossFloorManager")]
    public class BossFloorManagerSO : EnemyDataSO
    {
        [Title("Boss — Combo Block")]
        [MinValue(1)]
        [Tooltip("Cada cuantos turnos del Boss se dispara un nuevo bloqueo.")]
        public int ComboBlockIntervalTurns = 3;

        [MinValue(1)]
        [Tooltip("Duracion (en turnos del jugador) que dura un bloqueo antes de expirar.")]
        public int ComboBlockDurationTurns = 2;

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
