using System;
using Sirenix.OdinInspector;

namespace Rollgeon.Effects.Readers
{
    [Serializable, HideReferenceObjectPicker]
    public abstract class EffectIntReader
    {
        public abstract int Read(EffectContext context);
    }
}
