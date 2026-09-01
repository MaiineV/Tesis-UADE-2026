using System;
using System.Collections.Generic;
using Patterns;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Rollgeon.Attributes.Stats;
using Rollgeon.Player;
using Rollgeon.Upgrades.Character;

namespace Rollgeon.Upgrades
{
    /// <summary>
    /// Aplicador compartido de <see cref="StatGrant"/> al jugador. Construye un
    /// <c>Modifier&lt;int&gt;</c> Run / Intrinsic / Add y lo agrega al stat correspondiente.
    /// Canal único usado por el <c>CharacterRewardService</c> (rewards) y por las pasivas/ítems
    /// de tienda (<see cref="UpgradeSO.StatGrants"/>). Reusa el mismo lifecycle que los rewards
    /// de personaje, así que los modifiers se limpian solos en <c>OnRunEnd</c>.
    /// </summary>
    public static class PlayerStatGrants
    {
        /// <summary>
        /// Aplica <paramref name="amount"/> al stat <paramref name="stat"/> del jugador.
        /// Devuelve <c>true</c> si se agregó el modifier; <c>false</c> si el monto es 0, el player
        /// no está listo, o el stat no está registrado (ej. el jugador no tiene ese stat).
        /// </summary>
        public static bool Apply(CharacterRewardTargetStat stat, int amount)
        {
            if (amount == 0) return false;

            // RollRegen no es un atributo: el bonus vive en el RollPoolService y lo
            // persiste RollPoolSaveable (Feature#0050).
            if (stat == CharacterRewardTargetStat.RollRegen)
                return ApplyRollRegen(amount);

            if (!ServiceLocator.TryGetService<AttributesManager>(out var attrs) || attrs == null) return false;
            if (!ServiceLocator.TryGetService<IPlayerService>(out var ps) || ps == null || ps.PlayerGuid == Guid.Empty)
                return false;

            var modifier = new Modifier<int>(
                amount: amount,
                op: ModifierOperation.Add,
                duration: 0, // unused para lifetime Run
                carrierId: ps.PlayerGuid,
                sourceId: Guid.Empty,
                dir: ModifierDirection.Intrinsic,
                lifetime: ModifierLifetime.Run,
                tickEvent: EventName.OnTurnFinished);

            if (!ApplyToStat(stat, attrs, ps.PlayerGuid, modifier)) return false;

            // BUG-022 (decisión de diseño): subir el máximo también otorga la misma
            // cantidad del recurso actual — el pickup se siente al instante y el HUD
            // refresca solo (HealResolvedPayload). El modifier del máximo ya está
            // aplicado, así que el clamp usa el cap nuevo.
            if (amount > 0 && stat == CharacterRewardTargetStat.Health)
                HealCurrentHp(attrs, ps.PlayerGuid, amount);

            return true;
        }

        private static bool ApplyRollRegen(int amount)
        {
            if (!ServiceLocator.TryGetService<Rollgeon.Combat.Rolls.IRollPoolService>(out var rolls)
                || rolls == null)
                return false;
            rolls.AddRollPoolBonus(amount);
            return true;
        }

        private static void HealCurrentHp(AttributesManager attrs, Guid playerGuid, int amount)
        {
            if (ServiceLocator.TryGetService<Rollgeon.Combat.Pipelines.IHealPipeline>(out var heal)
                && heal != null)
            {
                heal.Resolve(new Rollgeon.Combat.Pipelines.HealContext
                {
                    SourceId = playerGuid,
                    TargetId = playerGuid,
                    BaseHeal = amount,
                });
                return;
            }

            // Fallback sin pipeline (tests / bootstrap parcial): clamp manual al max nuevo.
            int maxHp = PlayerMaxHp.Resolve(playerGuid);
            attrs.Modify<Health, int>(playerGuid,
                current => maxHp > 0 ? Math.Min(maxHp, current + amount) : current + amount);
        }

        /// <summary>Aplica una lista de grants. Devuelve cuántos se aplicaron efectivamente.</summary>
        public static int Apply(IEnumerable<StatGrant> grants)
        {
            if (grants == null) return 0;
            int applied = 0;
            foreach (var g in grants)
                if (g != null && Apply(g.Stat, g.Amount)) applied++;
            return applied;
        }

        /// <summary>Rutea el modifier al stat concreto. Mismo switch que usaba el CharacterRewardService.</summary>
        public static bool ApplyToStat(
            CharacterRewardTargetStat target, AttributesManager attrs, Guid playerGuid, Modifier<int> modifier)
        {
            switch (target)
            {
                // BUG-022: Health rutea al stat de MÁXIMO. RollRegen no pasa por acá
                // (no es atributo — ver ApplyRollRegen).
                case CharacterRewardTargetStat.Health: return attrs.AddModifier<MaxHealth, int>(playerGuid, modifier);
                case CharacterRewardTargetStat.Speed:  return attrs.AddModifier<Speed, int>(playerGuid, modifier);
                case CharacterRewardTargetStat.Attack: return attrs.AddModifier<Attack, int>(playerGuid, modifier);
                case CharacterRewardTargetStat.MoveRange: return attrs.AddModifier<MoveRange, int>(playerGuid, modifier);
                default: return false;
            }
        }
    }
}
