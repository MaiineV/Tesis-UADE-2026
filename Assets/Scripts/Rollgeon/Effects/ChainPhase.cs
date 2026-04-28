using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Effects
{
    [Serializable, HideReferenceObjectPicker]
    public class ChainPhase
    {
        [PropertyOrder(-1)]
        public string Label = "Phase";

        [OdinSerialize, SerializeReference]
        public EffectData Effects = new EffectData();
    }
}
