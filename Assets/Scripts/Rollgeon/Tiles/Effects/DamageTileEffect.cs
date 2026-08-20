using Patterns;
using Rollgeon.Combat.Pipelines;
using UnityEngine;

namespace Rollgeon.Tiles.Effects
{
    /// <summary>
    /// Categoría Damage: Pinchos, Fuego, Fuego Temporal. El monto depende del trigger —
    /// OnTurnStart usa <c>TurnStartDamage</c>, cualquier entrada usa <c>EnterDamage</c>.
    /// SourceId = instanceId de la casilla (el kill credit lo traduce
    /// <c>IKillCreditResolver</c>, no este handler).
    /// </summary>
    public sealed class DamageTileEffect : ITileEffectHandler
    {
        public TileEffectCategory Category => TileEffectCategory.Damage;

        public void Apply(in TileEffectContext ctx)
        {
            int damage = ctx.Trigger == TileTrigger.OnTurnStart
                ? ctx.Definition.TurnStartDamage
                : ctx.Definition.EnterDamage;
            if (damage <= 0) return;

            if (!ServiceLocator.TryGetService<IDamagePipeline>(out var pipeline) || pipeline == null)
            {
                Debug.LogWarning("[DamageTileEffect] IDamagePipeline no registrado — la casilla no cobra.");
                return;
            }

            pipeline.Resolve(new DamageContext
            {
                SourceId = ctx.InstanceId,
                TargetId = ctx.TargetGuid,
                BaseDamage = damage,
                Kind = ctx.Definition.DamageKind,
            });
        }
    }
}
