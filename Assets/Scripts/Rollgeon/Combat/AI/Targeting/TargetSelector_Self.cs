using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Targeting
{
    /// <summary>
    /// Selector que apunta al owner mismo. Permite que un behavior/effect se aplique sobre
    /// la entidad que lo ejecuta (auto-buff, auto-heal, auto-energy, etc.). Pareja simétrica
    /// de <see cref="TargetSelector_AlwaysPlayer"/>: aquel apunta al jugador, este al owner.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class TargetSelector_Self : BaseEnemyTargetSelector
    {
        public override string SelectorName => "Self";

        public override Guid PickTarget(AIContext ctx, Guid ownerGuid) => ownerGuid;
    }
}
