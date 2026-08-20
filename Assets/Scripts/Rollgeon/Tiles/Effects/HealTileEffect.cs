using Patterns;
using Rollgeon.Combat.Pipelines;
using UnityEngine;

namespace Rollgeon.Tiles.Effects
{
    /// <summary>
    /// Categoría Heal: Curación. Solo dispara por OnEndTurn (pasar de largo no cura —
    /// el gate lo hace la máscara de triggers de la definición, no este handler).
    /// </summary>
    public sealed class HealTileEffect : ITileEffectHandler
    {
        public TileEffectCategory Category => TileEffectCategory.Heal;

        public void Apply(in TileEffectContext ctx)
        {
            int heal = ctx.Definition.HealAmount;
            if (heal <= 0) return;

            if (!ServiceLocator.TryGetService<IHealPipeline>(out var pipeline) || pipeline == null)
            {
                Debug.LogWarning("[HealTileEffect] IHealPipeline no registrado — la casilla no cura.");
                return;
            }

            pipeline.Resolve(new HealContext
            {
                SourceId = ctx.InstanceId,
                TargetId = ctx.TargetGuid,
                BaseHeal = heal,
                SourceTag = "tile.heal",
            });
        }
    }
}
