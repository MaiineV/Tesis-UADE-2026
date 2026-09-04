using System;
using Patterns;
using Rollgeon.Items.Active;
using Rollgeon.Items.Active.Blood;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Único grupo (Gradient) de Blood D6 (Feature#0085): arma la carga del próximo combo de
    /// Ataque con <see cref="IBloodD6Service.Arm"/>, usando la cara resuelta del item como
    /// magnitud. Sin <see cref="ActiveItemRollTriggerContext"/> o sin servicio: warning + no-op.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffBloodD6Charge : BaseEffect
    {
        public override string GetEffectName() => "Blood D6 Charge";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            if (!ActiveItemRollTriggerContext.TryGet(context, out var rc))
            {
                Debug.LogWarning("[EffBloodD6Charge] Sin ActiveItemRollTriggerContext — no-op.");
                return true;
            }

            if (!ServiceLocator.TryGetService<IBloodD6Service>(out var service) || service == null)
            {
                Debug.LogWarning("[EffBloodD6Charge] IBloodD6Service no registrado — Blood D6 no arma. " +
                                 "Agregá BloodD6ServiceBootstrap a ExtraServices.");
                return true;
            }

            service.Arm(context.SourceGuid, rc.Magnitude);
            return true;
        }
    }
}
