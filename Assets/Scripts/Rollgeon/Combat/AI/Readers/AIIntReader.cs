using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Readers
{
    [Serializable, HideReferenceObjectPicker]
    public abstract class AIIntReader
    {
        public abstract int Read(AIContext context);
    }
}
