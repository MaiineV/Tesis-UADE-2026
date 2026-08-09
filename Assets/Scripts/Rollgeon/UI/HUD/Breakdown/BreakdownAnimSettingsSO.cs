using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.UI.HUD.Breakdown
{
    /// <summary>
    /// Perillas de la secuencia de breakdown (patrón <c>DiceUiAnimationSettingsSO</c>):
    /// tiempos cortos y secos estilo Balatro, tuneables por diseño sin recompilar.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/UI/Breakdown Anim Settings",
        fileName = "BreakdownAnimSettings")]
    public class BreakdownAnimSettingsSO : ScriptableObject
    {
        [Title("Vuelos")]
        [MinValue(0.05f), Tooltip("Duración del vuelo de un valor (player base / cara de dado).")]
        public float FlightSeconds = 0.32f;

        [Tooltip("Curvatura del vuelo (px perpendiculares al segmento).")]
        public float FlightArc = 60f;

        [MinValue(0.05f), Tooltip("Duración del vuelo de un proc de dado / global (curva más pronunciada).")]
        public float ProcFlightSeconds = 0.38f;

        public float ProcFlightArc = 110f;

        [MinValue(0f), Tooltip("Pausa entre pasos consecutivos.")]
        public float StepGapSeconds = 0.08f;

        [Title("Cascade (globales)")]
        [MinValue(0f), Tooltip("Caída de las entradas al retirarse la de abajo.")]
        public float CascadeFallSeconds = 0.15f;

        [Title("Choque final")]
        [MinValue(0.05f), Tooltip("Viaje de N y M hacia el punto de choque (acelerando).")]
        public float ClashTravelSeconds = 0.22f;

        [MinValue(0f), Tooltip("Hold del total tras el choque, antes de liberar el golpe.")]
        public float ClashHoldSeconds = 0.4f;

        [Title("Mitigación (post-choque)")]
        [MinValue(0.05f), Tooltip("Vuelo del '-X' de mitigación hacia el total.")]
        public float MitigationSeconds = 0.3f;

        [Title("Skip / seguridad")]
        [MinValue(1f), Tooltip("Multiplicador de velocidad del primer click de skip.")]
        public float SkipSpeedMultiplier = 3f;

        [MinValue(1f), Tooltip("Presupuesto duro de la secuencia completa — vencido, se " +
                               "fuerza el estado final y se libera el gate.")]
        public float MaxSequenceSeconds = 8f;
    }
}
