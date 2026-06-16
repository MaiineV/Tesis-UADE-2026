using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Combat.AI.Readers
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class AIConstantInt : AIIntReader
    {
        [HideLabel]
        public int Value;

        public override int Read(AIContext context) => Value;
    }
}
