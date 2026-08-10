using System.Collections.Generic;
using Rollgeon.Entities;
using Rollgeon.PreConditions;
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

        [Title("HUD")]
        [Tooltip("Ícono mientras la pasiva está surtiendo efecto.")]
        public Sprite ActiveIcon;

        [Tooltip("Ícono mientras la pasiva está latente. Vacío = se usa el activo y solo " +
                 "cambia el marco.")]
        public Sprite InactiveIcon;

        [Tooltip("Pasivas que rinden siempre (sin condición de disparo). Saltea la " +
                 "evaluación y el ícono queda activo todo el tiempo.")]
        public bool AlwaysActive;

        [InfoBox("Condiciones que definen si la pasiva se está aplicando AHORA, solo para el " +
                 "ícono del HUD. Se evalúan con AND y no pueden tener efectos secundarios.\n\n" +
                 "Preferí una condición que mire el RESULTADO (ej. PCHasModifier sobre el " +
                 "stat que la pasiva toca, filtrando por SourceId) antes que re-declarar el " +
                 "umbral: duplicar el número deja el ícono y el efecto desincronizados en " +
                 "cuanto alguien retunea uno solo.")]
        [OdinSerialize, SerializeReference]
        [ListDrawerSettings(ShowFoldout = false, DraggableItems = true)]
        public List<BasePreCondition> ActiveConditions = new List<BasePreCondition>();

        /// <summary>
        /// Si la pasiva está surtiendo efecto ahora para <paramref name="ownerGuid"/>.
        /// </summary>
        /// <remarks>
        /// Sin condiciones autoradas devuelve <c>false</c> a propósito: una pasiva que no
        /// declaró cómo se la ve prendida no puede decir que lo está. Para las de efecto
        /// permanente el autor pone <see cref="AlwaysActive"/>, que es explícito.
        /// </remarks>
        public bool IsActiveFor(System.Guid ownerGuid)
        {
            if (AlwaysActive) return true;
            if (ActiveConditions == null || ActiveConditions.Count == 0) return false;

            var context = new PreConditionContext
            {
                OwnerGuid = ownerGuid,
                Entity = new Entity { Guid = ownerGuid },
            };
            return BasePreCondition.EvaluateAll(ActiveConditions, context);
        }
    }
}
