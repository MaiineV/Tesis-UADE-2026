using Patterns;
using Rollgeon.Combat.Status;
using UnityEngine;

namespace Rollgeon.Tiles.Effects
{
    /// <summary>
    /// Categoría ApplyStatus: Veneno (Envenenado) y Charco Eléctrico (Aturdimiento).
    /// La semántica no-stacking vive en los servicios de estado (refresh en Poison,
    /// max() en Stun) — este handler solo traduce; por eso "cruzar un segundo charco en
    /// el mismo empujón" no aporta nada sin lógica extra acá.
    /// </summary>
    public sealed class ApplyStatusTileEffect : ITileEffectHandler
    {
        public TileEffectCategory Category => TileEffectCategory.ApplyStatus;

        public void Apply(in TileEffectContext ctx)
        {
            var def = ctx.Definition;
            if (def.StatusTurns <= 0) return;

            switch (def.StatusKind)
            {
                case TileStatusKind.Poison:
                    if (ServiceLocator.TryGetService<IPoisonService>(out var poison) && poison != null)
                        poison.ApplyPoison(ctx.TargetGuid, def.StatusTickDamage, def.StatusTurns, ctx.InstanceId);
                    else
                        Debug.LogWarning("[ApplyStatusTileEffect] IPoisonService no registrado — el Veneno no aplica.");
                    return;

                case TileStatusKind.Stun:
                    if (ServiceLocator.TryGetService<IStunService>(out var stun) && stun != null)
                        stun.ApplyStun(ctx.TargetGuid, def.StatusTurns);
                    else
                        Debug.LogWarning("[ApplyStatusTileEffect] IStunService no registrado — el Aturdimiento no aplica.");
                    return;
            }
        }
    }
}
