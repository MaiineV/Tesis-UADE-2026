using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Selection.Queries
{
    [Serializable, HideReferenceObjectPicker]
    public class TQ_Self : BaseTargetQuery
    {
        public override string QueryName => "Self";

        public override List<TargetRef> Evaluate(TargetQueryContext context)
        {
            var result = new List<TargetRef>(1);
            if (context == null) return result;
            result.Add(TargetRef.At(context.OwnerPosition));
            return result;
        }
    }
}
