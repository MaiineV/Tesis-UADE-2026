using System;
using System.Collections.Generic;
using Rollgeon.Combat;
using Rollgeon.Grid;

namespace Rollgeon.Items.Active.Blood
{
    /// <summary>
    /// Resuelve el objetivo automático de Blood Transfusion (Feature#0085) en las bandas
    /// mixta y positiva: el enemigo elegible con mayor HP actual; empate → más cercano al
    /// jugador; empate remanente → orden de <see cref="Guid"/> (determinismo de test, sin
    /// significado de diseño).
    /// </summary>
    public static class BloodTransfusionTargeting
    {
        /// <summary><see cref="Guid.Empty"/> si no hay ningún enemigo elegible (vivo, no Bloodless).</summary>
        public static Guid ResolveDrainTarget(Guid player)
        {
            var candidates = CombatantQuery.LiveEnemiesOf(player);
            if (candidates.Count == 0) return Guid.Empty;

            bool havePlayerCoord = CombatantQuery.TryGetCoord(player, out var playerCoord);

            Guid best = Guid.Empty;
            int bestHp = 0;
            int bestDist = int.MaxValue;

            foreach (var candidate in candidates)
            {
                if (!CombatantQuery.IsEligibleForBlood(candidate)) continue;

                int hp = CombatantQuery.CurrentHp(candidate);
                int dist = int.MaxValue;
                if (havePlayerCoord && CombatantQuery.TryGetCoord(candidate, out var coord))
                    dist = playerCoord.Manhattan(coord);

                if (best == Guid.Empty || IsBetter(hp, dist, candidate, bestHp, bestDist, best))
                {
                    best = candidate;
                    bestHp = hp;
                    bestDist = dist;
                }
            }

            return best;
        }

        // Mayor HP actual gana; empate → menor distancia Manhattan al jugador; empate
        // remanente → orden de Guid (determinismo, sin significado de diseño).
        private static bool IsBetter(int hp, int dist, Guid candidate, int bestHp, int bestDist, Guid best)
        {
            if (hp != bestHp) return hp > bestHp;
            if (dist != bestDist) return dist < bestDist;
            return candidate.CompareTo(best) < 0;
        }
    }
}
