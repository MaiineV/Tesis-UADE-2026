using System;
using Patterns;
using Rollgeon.Effects;
using Sirenix.Serialization;

namespace Rollgeon.Heroes
{
    [Serializable]
    public class PassiveHook
    {
        public EventName TriggerEvent;

        [OdinSerialize]
        public EffectData Effect = new EffectData();
    }
}
