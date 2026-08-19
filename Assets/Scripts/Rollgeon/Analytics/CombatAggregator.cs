using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rollgeon.Analytics
{
    /// <summary>
    /// Acumuladores per-combat del <see cref="AnalyticsTrackerService"/>
    /// (Feature#0029). Se resetea en <c>OnCombatStart</c> y se vuelca en el
    /// evento <c>combat_ended</c>. NO se limpia en <c>OnCombatEnd</c>: un
    /// <c>player_death</c> posterior al combate sigue leyendo estos valores.
    /// </summary>
    public sealed class CombatAggregator
    {
        public int TurnCount;
        public int DamageDealt;
        public int DamageTaken;
        public int RerollsUsed;
        public int RollsSpent;
        public double CombatStartTime;

        /// <summary>Fase máxima de boss vista este combate (1-based). 0 = sin boss.</summary>
        public int MaxBossPhase;

        /// <summary>Última energía conocida del player. -1 = sin baseline — el
        /// primer <c>OnPlayerRollsChanged</c> solo establece el punto de partida.</summary>
        public int LastPlayerRolls = -1;

        public readonly Dictionary<string, int> ComboCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public void Reset(double now)
        {
            TurnCount = 0;
            DamageDealt = 0;
            DamageTaken = 0;
            RerollsUsed = 0;
            RollsSpent = 0;
            CombatStartTime = now;
            MaxBossPhase = 0;
            LastPlayerRolls = -1;
            ComboCounts.Clear();
        }

        /// <summary>Solo cuenta decrementos: refills y subas no son gasto.</summary>
        public void TrackRolls(int current)
        {
            if (LastPlayerRolls >= 0 && current < LastPlayerRolls)
            {
                RollsSpent += LastPlayerRolls - current;
            }
            LastPlayerRolls = current;
        }

        /// <summary>
        /// Serializa los combos del combate como <c>"id:count,id:count"</c>
        /// (desc por count, desempate alfabético estable), cortando entradas
        /// enteras para no pasar <paramref name="maxLength"/> — los params
        /// STRING de UGS tienen límite de tamaño.
        /// </summary>
        public string BuildTopCombos(int maxLength)
        {
            if (ComboCounts.Count == 0) return string.Empty;

            var ordered = ComboCounts
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.Ordinal);

            var builder = new StringBuilder();
            foreach (var pair in ordered)
            {
                var token = pair.Key + ":" + pair.Value;
                var lengthWithToken = builder.Length == 0
                    ? token.Length
                    : builder.Length + 1 + token.Length;
                // Se saltea la entry que no cabe (no se corta el loop): una key
                // desproporcionada no debe eclipsar al resto de los combos.
                if (lengthWithToken > maxLength) continue;

                if (builder.Length > 0) builder.Append(',');
                builder.Append(token);
            }

            return builder.ToString();
        }
    }
}
