using System;
using Patterns;
using Rollgeon.Combat.ContractMod;
using UnityEngine;

namespace Rollgeon.Entities
{
    /// <summary>
    /// La maldición del Repeat Ban de la Generala. Activa cuando el contrato tiene un combo
    /// prohibido: la fuente es el mismo servicio que tacha la fila, así que el bloque aparece
    /// recién cuando hay una mano vetada de verdad — el turno 1, antes de anotar nada, no hay
    /// castigo que anunciar.
    /// </summary>
    [CreateAssetMenu(menuName = "Rollgeon/Entities/Repeat Ban Curse", fileName = "BC_RepeatBan")]
    public class RepeatBanCurseSO : BossCurseSO
    {
        public override bool IsActive(Guid bossGuid)
            => ServiceLocator.TryGetService<IContractModifierService>(out var mods) && mods != null
               && mods.HasAnyModifier;
    }
}
