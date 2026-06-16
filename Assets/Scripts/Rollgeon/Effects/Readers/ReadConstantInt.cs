using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Readers
{
    [Serializable, HideReferenceObjectPicker]
    public sealed class ReadConstantInt : EffectIntReader
    {
        [HideLabel]
        public int Value;

        public override int Read(EffectContext context) => Value;
    }
}
