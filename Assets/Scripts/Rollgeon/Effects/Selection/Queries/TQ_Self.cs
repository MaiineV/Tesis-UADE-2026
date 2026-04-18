using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Selection.Queries
{
    /// <summary>
    /// Query degenerada — el target es siempre el owner. TECHNICAL.md §11.2b.
    /// Caso típico: buffs self, heals auto-aplicados, efectos que disparan pasivas propias.
    /// Prueba el patrón de single-target y la infra de selección sin tocar servicios externos.
    /// </summary>
    [Serializable, HideReferenceObjectPicker]
    public class TQ_Self : BaseTargetQuery
    {
        public override string QueryName => "Self";

        public override List<TargetRef> Evaluate(TargetQueryContext context)
        {
            var result = new List<TargetRef>(1);
            if (context == null) return result;
            result.Add(TargetRef.Entity(context.OwnerGuid));
            return result;
        }
    }
}
