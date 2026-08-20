using UnityEngine;

namespace Rollgeon.Combat.AI.Pathing
{
    /// <summary>
    /// Tabla de tuning del pathing IA (GDD Casillas Especiales — Fórmula de Pathing).
    /// Todo lo que el diseño puede querer balancear vive acá; el planner cae a estos mismos
    /// defaults si el asset no está registrado (tests, escenas sin bootstrap).
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Combat/AI Path Tuning", fileName = "AIPathTuning")]
    public class AIPathTuningSO : ScriptableObject
    {
        [Header("Personalidades — MinSurvivalHP (% de HP máx) y Caution")]
        [Range(0f, 1f)] public float NormalMinSurvivalPct = 0.20f;
        [Min(0f)] public float NormalCaution = 1.0f;

        [Tooltip("El GDD define Caution 1.5 pero no MinSurvival para Support/Cobarde — 20% propuesto.")]
        [Range(0f, 1f)] public float SupportMinSurvivalPct = 0.20f;
        [Min(0f)] public float SupportCaution = 1.5f;

        [Range(0f, 1f)] public float AggressiveMinSurvivalPct = 0.10f;
        [Min(0f)] public float AggressiveCaution = 0.65f;

        [Range(0f, 1f)] public float KamikazeMinSurvivalPct = 0f;
        [Min(0f)] public float KamikazeCaution = 0.25f;

        [Header("Scoring")]
        [Tooltip("Peso del error de banda |dist−desired| contra el DestinationScore. Con 3, un " +
                 "beneficio de 3 (SafeZone) justifica 1 tile de desvío de banda.")]
        [Min(1)] public int BandWeight = 3;

        [Header("DestinationScore — beneficios")]
        [Range(0f, 1f)] public float HealMaxHpPct = 0.6f;
        [Min(0)] public int HealDetourMaxTiles = 2;
        [Min(1)] public int HealBenefitScale = 4;
        [Min(0)] public int FortressBenefit = 2;
        [Tooltip("Impulso está INERTE (sin tirada de movimiento real): el planner lo ignora " +
                 "aunque este valor esté cargado. Queda listo para cuando se active.")]
        [Min(0)] public int ImpulseBenefit = 1;
        [Min(0)] public int SafeZoneBenefit = 3;

        [Header("TacticalGain — pisar peligro a propósito")]
        [Min(0)] public int GainAttackFromTile = 4;
        [Min(0)] public int GainOnlyBandReacher = 3;
        [Min(0)] public int GainCutsDistance = 2;
        [Min(0)] public int ContextBonusCap = 2;
        [Min(0)] public int TacticalGainCap = 8;
        [Min(0)] public int PenaltyStayDamage = 2;
        [Min(0)] public int PenaltyTelegraph = 2;
        [Min(0)] public int PenaltyLowHpAfter = 1;
    }
}
