using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Combat;
using Rollgeon.Combat.Pipelines;
using Rollgeon.Combat.Status;
using Rollgeon.Entities.Traits;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Banda negativa de Blood Transfusion — D10 1-3, "Redistribución sanguínea" (Feature#0084).
    /// Suma el HP actual de los enemigos elegibles no-jefe y lo reparte equitativamente, sin
    /// superar sus máximos; con un único elegible no redistribuye — le aplica 1 stack de
    /// Sangrado. Nunca modifica al jugador ni corta la cadena (§ regla "resultado no
    /// autodestructivo").
    /// </summary>
    /// <remarks>
    /// El pool sale de <see cref="CombatantQuery.LiveEnemiesOf"/> filtrado por
    /// <see cref="CombatantQuery.IsEligibleForBlood"/> (no Bloodless) y <c>!IsBoss</c>
    /// (<see cref="IUnitTraitService"/> — sin servicio se asume no-jefe, perfil seguro).
    /// El reparto capea cada entidad a su HP máximo; el sobrante (módulo + overflow de los
    /// capeados) se asigna de a uno, nearest-first respecto del jugador, a quienes sigan
    /// debajo de su máximo, hasta agotarse o hasta que nadie más pueda recibir.
    /// </remarks>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffBloodRedistribute : BaseEffect
    {
        public override string GetEffectName() => "Blood Redistribute";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            var pool = ResolvePool(context.SourceGuid);
            if (pool.Count == 0)
            {
                Debug.Log("[EffBloodRedistribute] Sin enemigos elegibles (no-jefe, no Bloodless) — no-op.");
                return true;
            }

            if (pool.Count == 1)
            {
                if (ServiceLocator.TryGetService<IBleedService>(out var bleed) && bleed != null)
                    bleed.AddStack(pool[0], context.SourceGuid, 1);
                else
                    Debug.LogWarning("[EffBloodRedistribute] IBleedService no registrado — no sangra el único elegible.");
                return true;
            }

            int count = pool.Count;
            var current = new int[count];
            var maxHp = new int[count];
            var target = new int[count];
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                current[i] = CombatantQuery.CurrentHp(pool[i]);
                maxHp[i] = CombatantQuery.MaxHp(pool[i]);
                total += current[i];
            }

            int basePerHead = total / count;
            int spent = 0;
            for (int i = 0; i < count; i++)
            {
                target[i] = Math.Min(basePerHead, maxHp[i]);
                spent += target[i];
            }

            // Sobrante (módulo del reparto + overflow de los capeados): de a uno,
            // nearest-first al jugador, a quienes sigan debajo de su máximo. Si la capacidad
            // conjunta no alcanza, el resto simplemente no se reparte (no hay a quién dárselo).
            int pending = total - spent;
            if (pending > 0)
            {
                var order = NearestFirstIndices(pool, context.SourceGuid);
                bool progressed = true;
                while (pending > 0 && progressed)
                {
                    progressed = false;
                    for (int oi = 0; oi < order.Length && pending > 0; oi++)
                    {
                        int idx = order[oi];
                        if (target[idx] >= maxHp[idx]) continue;
                        target[idx]++;
                        pending--;
                        progressed = true;
                    }
                }
            }

            ApplyDeltas(context.SourceGuid, pool, current, target);
            return true;
        }

        // Pool: enemigos vivos, no Bloodless, no jefe. Sin IUnitTraitService registrado se
        // asume que nadie es jefe (perfil seguro, mismo criterio que CombatantQuery).
        private static List<Guid> ResolvePool(Guid player)
        {
            var result = new List<Guid>();
            var candidates = CombatantQuery.LiveEnemiesOf(player);
            ServiceLocator.TryGetService<IUnitTraitService>(out var traits);

            foreach (var candidate in candidates)
            {
                if (!CombatantQuery.IsEligibleForBlood(candidate)) continue;
                bool isBoss = traits != null && traits.Get(candidate).IsBoss;
                if (isBoss) continue;
                result.Add(candidate);
            }
            return result;
        }

        // Índices de "pool" ordenados por distancia Manhattan ascendente al jugador;
        // empate → orden de Guid (determinismo).
        private static int[] NearestFirstIndices(List<Guid> pool, Guid player)
        {
            int n = pool.Count;
            var indices = new int[n];
            var dist = new int[n];
            bool havePlayerCoord = CombatantQuery.TryGetCoord(player, out var playerCoord);

            for (int i = 0; i < n; i++)
            {
                indices[i] = i;
                dist[i] = havePlayerCoord && CombatantQuery.TryGetCoord(pool[i], out var coord)
                    ? playerCoord.Manhattan(coord)
                    : int.MaxValue;
            }

            Array.Sort(indices, (a, b) =>
            {
                int cmp = dist[a].CompareTo(dist[b]);
                return cmp != 0 ? cmp : pool[a].CompareTo(pool[b]);
            });
            return indices;
        }

        // delta < 0 → daño (Kind ScriptedAbility); delta > 0 → heal; delta == 0 → nada.
        // Nunca corta la cadena: el roll ya se pagó.
        private static void ApplyDeltas(Guid sourceId, List<Guid> pool, int[] current, int[] target)
        {
            ServiceLocator.TryGetService<IDamagePipeline>(out var damagePipeline);
            ServiceLocator.TryGetService<IHealPipeline>(out var healPipeline);

            for (int i = 0; i < pool.Count; i++)
            {
                int delta = target[i] - current[i];
                if (delta == 0) continue;

                if (delta < 0)
                {
                    if (damagePipeline == null)
                    {
                        Debug.LogWarning("[EffBloodRedistribute] IDamagePipeline no registrado — daño de redistribución perdido.");
                        continue;
                    }
                    damagePipeline.Resolve(new DamageContext
                    {
                        SourceId = sourceId,
                        TargetId = pool[i],
                        BaseDamage = -delta,
                        Kind = AttackKind.ScriptedAbility,
                    });
                }
                else
                {
                    if (healPipeline == null)
                    {
                        Debug.LogWarning("[EffBloodRedistribute] IHealPipeline no registrado — cura de redistribución perdida.");
                        continue;
                    }
                    healPipeline.Resolve(new HealContext
                    {
                        SourceId = sourceId,
                        TargetId = pool[i],
                        BaseHeal = delta,
                    });
                }
            }
        }
    }
}
