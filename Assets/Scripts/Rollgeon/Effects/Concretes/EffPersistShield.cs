using System;
using Patterns;
using Rollgeon.Combat;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Rollgeon.Effects.Concretes
{
    /// <summary>
    /// Banda par de Coin Shield (Feature#0085): el jugador salta su próximo reset de escudo
    /// por inicio de turno vía <see cref="IShieldPersistenceService.PersistThroughNextReset"/>.
    /// Sin servicio registrado: warning + no-op (nunca corta la cadena).
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public sealed class EffPersistShield : BaseEffect
    {
        public override string GetEffectName() => "Persist Shield";

        public override bool ApplyEffect(EffectContext context)
        {
            if (context == null) return false;

            if (!ServiceLocator.TryGetService<IShieldPersistenceService>(out var service) || service == null)
            {
                Debug.LogWarning("[EffPersistShield] IShieldPersistenceService no registrado — no-op.");
                return true;
            }

            service.PersistThroughNextReset(context.SourceGuid);
            return true;
        }
    }
}
