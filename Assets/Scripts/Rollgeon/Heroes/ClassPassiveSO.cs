using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Heroes
{
    [CreateAssetMenu(menuName = "Rollgeon/Heroes/Class Passive", fileName = "ClassPassive")]
    public class ClassPassiveSO : SerializedScriptableObject
    {
        [Title("Identity")]
        [Tooltip("Id unico de la pasiva (ej. 'passive.warrior.rage').")]
        public string PassiveId;

        [Tooltip("Nombre legible para UI.")]
        public string DisplayName;

        [TextArea]
        [Tooltip("Descripcion para tooltip / codex.")]
        public string Description;

        [Title("Hooks")]
        [InfoBox("Cada hook vincula un evento del bus legacy a un EffectData. " +
                 "Los handlers se filtran por InstanceId al bindear.")]
        [OdinSerialize]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        public List<PassiveHook> Hooks = new List<PassiveHook>();
    }
}
