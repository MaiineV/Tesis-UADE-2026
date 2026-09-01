using System;
using Patterns;
using Rollgeon.Combat.DiceBlock;
using UnityEngine;

namespace Rollgeon.Entities
{
    /// <summary>
    /// La maldición del candado de dados. Activa cuando HAY dados trabados: la fuente es el
    /// mismo servicio que traba, así que el bloque del panel aparece exactamente cuando la
    /// pasiva del jefe empieza a operar (la fase 2 del Croupier, al 70% de vida) y no antes —
    /// sin duplicar el umbral de la fase en la data del curse.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Entities/Dice Block Curse", fileName = "BC_DiceBlock")]
    public class DiceBlockCurseSO : BossCurseSO
    {
        public override bool IsActive(Guid bossGuid)
            => ServiceLocator.TryGetService<IDiceBlockService>(out var dice) && dice != null
               && dice.BlockedIndices != null && dice.BlockedIndices.Count > 0;
    }
}
