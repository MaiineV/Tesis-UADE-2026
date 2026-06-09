using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Entities
{
    /// <summary>
    /// Modo de resolución de un stat para un tier: multiplicador sobre el valor
    /// base (Tier 1) o valor manual exacto. El modo es <b>por-stat</b>, así un mismo
    /// tier puede mezclar (ej. HP en multiplicador y Velocidad en manual). Ticket #158.
    /// </summary>
    public enum StatMode { Multiplier, Manual }

    /// <summary>
    /// Configuración de un único stat dentro de un <see cref="EnemyTier"/>.
    /// </summary>
    [Serializable]
    public struct TierStat
    {
        [Tooltip("Multiplier: ×base. Manual: valor exacto.")]
        public StatMode Mode;

        [Tooltip("Factor sobre el valor base (Tier 1) cuando Mode = Multiplier. 1 = igual al base.")]
        public float Multiplier;

        [Tooltip("Valor exacto cuando Mode = Manual.")]
        public int ManualValue;

        /// <summary>
        /// Resuelve el valor final contra <paramref name="baseValue"/> (el stat de Tier 1).
        /// En modo Multiplier redondea. <b>Salvaguarda:</b> un multiplicador ≤ 0 (incluido el
        /// <c>default(TierStat)</c> con Multiplier=0) se trata como "base" para no producir
        /// stats en 0 por accidente — un 0/negativo nunca es intencional.
        /// </summary>
        public int Resolve(int baseValue)
        {
            if (Mode == StatMode.Manual) return ManualValue;
            if (Multiplier <= 0f) return baseValue;
            return Mathf.RoundToInt(baseValue * Multiplier);
        }

        /// <summary>Stat neutro (×1 = base). Usado como default al crear un tier nuevo.</summary>
        public static TierStat Base => new TierStat { Mode = StatMode.Multiplier, Multiplier = 1f, ManualValue = 0 };
    }

    /// <summary>
    /// Un tier de un enemigo (Tier 2..N — el Tier 1 son los <c>Base*</c> del
    /// <see cref="EnemyDataSO"/>). Define cómo escala cada stat respecto del base.
    /// Los tiers cambian solo stats, nunca la apariencia. Sin límite de tiers.
    /// </summary>
    [Serializable]
    public class EnemyTier
    {
        [Tooltip("Etiqueta editor-only (ej. 'T2'). No es identity.")]
        public string Label;

        public TierStat HP = TierStat.Base;
        public TierStat Attack = TierStat.Base;
        public TierStat Speed = TierStat.Base;
        public TierStat Energy = TierStat.Base;
        public TierStat HealStrength = TierStat.Base;
        public TierStat AttackRange = TierStat.Base;
    }
}
